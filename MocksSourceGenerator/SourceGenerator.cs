﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MocksSourceGenerator
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        private const string PostFix = "Mock";

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                {
                    return;
                }

                var mockSources = new List<string>();
                var generatedTypes = new Dictionary<string,string>();

                foreach (var candidate in receiver.Candidates)
                {
                    var semanticModel = context.Compilation.GetSemanticModel(candidate.ObjectCreationExpressionSyntax.SyntaxTree);
                    var targetTypeSymbol = semanticModel.GetSymbolInfo(candidate.TargetType.Type).Symbol as INamedTypeSymbol;
                    if (targetTypeSymbol == null)
                    {
                        continue;
                    }

                    var namespaceName = targetTypeSymbol.ContainingNamespace
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", string.Empty);
                    var fullMockTypeName = $"{namespaceName}.{candidate.TypeName}";
                    var isSameAssembly = string.Equals(
                        semanticModel.Compilation.Assembly.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        targetTypeSymbol.ContainingAssembly.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                    if (generatedTypes.ContainsKey(fullMockTypeName))
                    {
                        // A mock with this name already exists
                        if (generatedTypes[fullMockTypeName] != targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        {
                            // The target type is not the same; so the generated mock will likely not work; a different name should be used for the mock
                            context.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "SI0107",
                                    "Duplicate usage of a Mock type was detected for different target types",
                                    $"The type '{fullMockTypeName}' cannot be used for mocking '{targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}', as it was already used to mock '{generatedTypes[fullMockTypeName]}'",
                                    "MocksSourceGenerator",
                                    DiagnosticSeverity.Error,
                                    isEnabledByDefault: true),
                                Location.None));
                        }

                        continue;
                    }

                    var targetTypeName = targetTypeSymbol.ToDisplayString(NullableFlowState.None, SymbolDisplayFormat.FullyQualifiedFormat);

                    var targetSymbolMembersSources = GetMemberSources(targetTypeSymbol, candidate, isSameAssembly);
                    var targetSymbolPropertiesSources = GetPropertiesSources(targetTypeSymbol, isSameAssembly);

                    string mockSource = $@"
namespace {namespaceName}
{{
    using System;
    using MocksSourceGenerator;
    using {targetTypeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)};

    {GetAccessibility(targetTypeSymbol, isSameAssembly)} partial class {candidate.TypeName} : {targetTypeName}
    {{
        /// <summary>
        /// Set this to true, if you want members that don't have a mock implementation
        /// to return a default value instead of throwing an exception.
        /// </summary>
        public bool ReturnDefaultIfNotMocked {{ get; set; }}

        private System.Collections.Generic.List<HistoryEntry> historyEntries = new System.Collections.Generic.List<HistoryEntry>();
        public System.Collections.ObjectModel.ReadOnlyCollection<HistoryEntry> HistoryEntries
        {{
            get
            {{
                return historyEntries.AsReadOnly();
            }}
        }}
{string.Join("\r\n", targetSymbolPropertiesSources)}
{string.Join("\r\n", targetSymbolMembersSources)}
    }}
}}";

                    mockSources.Add(mockSource);
                    generatedTypes.Add(fullMockTypeName, targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }


                var source = $@"
#nullable enable

namespace MocksSourceGenerator
{{
    using System;

    public class HistoryEntry
    {{
        public string MethodName {{ get; private set; }}
        public System.Collections.ObjectModel.ReadOnlyCollection<string> ArgumentValuesToString {{ get; private set; }}
        public HistoryEntry (string methodName, string[] argumentValuesToString) {{
            this.MethodName = methodName;
            this.ArgumentValuesToString = Array.AsReadOnly<string>(argumentValuesToString);
        }}
        public override string ToString()
        {{
            return $""{{MethodName}}({{string.Join("", "", ArgumentValuesToString)}})"";
        }}
    }}
}}

{string.Join("\r\n", mockSources)}
#nullable disable";

                context.AddSource("GeneratedMocks.gen.cs", source);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static IEnumerable<string> GetPropertiesSources(ITypeSymbol targetTypeSymbol, bool isSameAssembly)
        {
            List<ITypeSymbol> allTypes = new List<ITypeSymbol>();
            AddBaseTypesAndThis(allTypes, targetTypeSymbol);
            return allTypes
                .SelectMany(t => t.GetMembers().OfType<IPropertySymbol>().Select(m => new { Type = t, Member = m }))
                .Where(t => t.Type.TypeKind != TypeKind.Class || (t.Type.TypeKind != TypeKind.Class || t.Member.IsAbstract || t.Member.IsVirtual))
                .Select(m =>
                {
                    var overrideStr = targetTypeSymbol.TypeKind == TypeKind.Class && (m.Member.IsAbstract || m.Member.IsVirtual)
                        ? "override "
                        : string.Empty;

                    string mainAccessibility = GetAccessibility(m.Member, isSameAssembly);

                    string getAccessibility = GetPropertyAccessorAccessibility(mainAccessibility, m.Member.GetMethod, isSameAssembly);
                    string setAccessibility = GetPropertyAccessorAccessibility(mainAccessibility, m.Member.SetMethod, isSameAssembly);
                    string getter = m.Member.GetMethod != null && m.Member.GetMethod.DeclaredAccessibility != Accessibility.Private ? $"{getAccessibility} get {{ return Mock{m.Member.Name} ?? default({GetFullyQualifiedTypeName(m.Member.Type)}); }}" : string.Empty;
                    string setter = m.Member.SetMethod != null && m.Member.SetMethod.DeclaredAccessibility != Accessibility.Private ? $"{setAccessibility} set {{ Mock{m.Member.Name} = value; }}" : string.Empty;

                    return $@"
    /// <summary>
    /// Implemented for type {GetFullyQualifiedTypeName(m.Type)}
    /// </summary>
    public {GetFullyQualifiedTypeName(m.Member.Type)}? Mock{m.Member.Name} {{ get; set; }}
    {mainAccessibility} {overrideStr}{GetFullyQualifiedTypeName(m.Member.Type)} {m.Member.Name} {{ {getter} {setter} }}";
                });
        }

        private static IEnumerable<string> GetMemberSources(ITypeSymbol targetTypeSymbol, Candidate candidate, bool isSameAssembly)
        {
            List<string> memberSources = new();
            List<string> addedNames = new();

            List<ITypeSymbol> allTypes = new List<ITypeSymbol>();
            AddBaseTypesAndThis(allTypes, targetTypeSymbol);

            var memberGroups = allTypes
                .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>().Select(m => new
                    {
                        Type = t,
                        Member = m,
                        NameWithParamTypes = $"{m.Name}{string.Join(string.Empty, m.Parameters.Select(p => p.Type.Name))}"
                    }))
                .Where(m => !m.Member.IsStatic
                    && !m.Member.IsImplicitlyDeclared
                    && !m.Member.Name.StartsWith("get_", StringComparison.InvariantCulture)
                    && !m.Member.Name.StartsWith("set_", StringComparison.InvariantCulture))
                .GroupBy(m => m.Member.Name);

            foreach(var group in memberGroups)
            {
                var groupOrdered = group.OrderByDescending(i =>
                    {
                        List<ITypeSymbol> allTypes = new List<ITypeSymbol>();
                        AddBaseTypesAndThis(allTypes, i.Type);
                        return allTypes.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Distinct().Count();
                    });
                var firstInGroup = groupOrdered.First();
                if (firstInGroup.Type.TypeKind == TypeKind.Class
                    && !(firstInGroup.Member.IsVirtual || firstInGroup.Member.IsAbstract)
                    && !firstInGroup.Member.Name.StartsWith(".ctor", StringComparison.InvariantCulture))
                {
                    // If the lowest implementation cannot be overridden, then skip
                    continue;
                }

                foreach (var m in groupOrdered)
                {
                    var methodParameters = string.Join(", ",
                        m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)} {p.Name}"));
                    var methodParameterNames = string.Join(", ", m.Member.Parameters.Select(p => $"{p.Name}"));
                    var methodParameterTypes =
                        string.Join(",", m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)}"));
                    var methodParameterValues = m.Member.Parameters.Any() ? $"new [] {{ {string.Join(", ", m.Member.Parameters.Select(p => $"$\"{{{p.Name}}}\""))} }}" : "new string[0]";

                    if (SymbolEqualityComparer.Default.Equals(m.Type, targetTypeSymbol)
                        && m.Member.MethodKind == MethodKind.Constructor)
                    {
                        memberSources.Add($@"
        public {candidate.TypeName}({methodParameters}) : base({methodParameterNames})
        {{
        }}");
                        continue;
                    }

                    var name = group.Select(g => g.NameWithParamTypes).Distinct().Count() > 1 ? m.NameWithParamTypes : m.Member.Name;
                    if(addedNames.Contains(name))
                    {
                        continue;
                    }
                    else
                    {
                        addedNames.Add(name);
                    }

                    var returnType = (INamedTypeSymbol) m.Member.ReturnType;
                    if(m.Member.IsAsync && returnType.Name.Equals(nameof(System.Threading.Tasks.Task)))
                    {
                        returnType = (INamedTypeSymbol) returnType.TypeArguments.First();
                    }

                    var funcTypeParameters =
                        $"{methodParameterTypes},{(m.Member.ReturnsVoid ? string.Empty : GetFullyQualifiedTypeName(returnType))}"
                            .Trim(',');
                    funcTypeParameters = m.Member.Parameters.Any() || !m.Member.ReturnsVoid ? $"<{funcTypeParameters}>" : string.Empty;

                    var funcTypeName = m.Member.ReturnsVoid ? "Action" : "Func";
                    var overrideStr = targetTypeSymbol.TypeKind == TypeKind.Class && (m.Member.IsVirtual || (m.Member.IsAbstract && m.Type.IsAbstract))
                        ? "override "
                        : string.Empty;

                    var asyncStr = m.Member.IsAsync
                        ? "async "
                        : string.Empty;

                    var methodCallStr = $"Mock{name}({methodParameterNames})";

                    var returnStr = $"{(m.Member.ReturnsVoid ? string.Empty : "return ")}{methodCallStr};";

                    memberSources.Add($@"
        /// <summary>
        /// Implemented for type {GetFullyQualifiedTypeName(m.Type)} ({m.Member.DeclaredAccessibility}, same assembly: {isSameAssembly})
        /// </summary>
        public {funcTypeName}{funcTypeParameters}? Mock{name} {{ get; set; }}
        {GetAccessibility(m.Member, isSameAssembly)} {overrideStr}{asyncStr}{(m.Member.ReturnsVoid ? "void" : GetFullyQualifiedTypeName(m.Member.ReturnType))} {m.Member.Name}({methodParameters})
        {{
            historyEntries.Add(new HistoryEntry(""{m.Member.Name}"", {methodParameterValues}));

            if (Mock{name} == null)
            {{
                if (ReturnDefaultIfNotMocked)
                {{
                    {(m.Member.ReturnsVoid ? "return;" : $"return default({GetFullyQualifiedTypeName(returnType)});")}
                }}
                else
                {{
                    throw new NotImplementedException(""Method 'Mock{name}' was called, but no mock implementation was provided"");
                }}
            }}

            {returnStr}
        }}");
                }
            }

            return memberSources;
        }

        private static string GetFullyQualifiedTypeName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string GetAccessibility(ISymbol symbol, bool isSameAssembly)
        {
            switch(symbol.DeclaredAccessibility)
            {
                case Accessibility.NotApplicable or Accessibility.Public: return "public";
                case Accessibility.Private: return "private";
                case Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal: return isSameAssembly ? "protected internal" : "protected";
                case Accessibility.Protected: return "protected";
                case Accessibility.Internal: return isSameAssembly ? "internal" : "public";
                default: return string.Empty;
            }
        }

        private static string GetPropertyAccessorAccessibility(string mainAccessibility, IMethodSymbol? method, bool isSameAssembly)
        {
            if (method != null)
            {
                string accessibility = GetAccessibility(method, isSameAssembly);
                if (accessibility == mainAccessibility)
                {
                    return string.Empty;
                }

                return accessibility;
            }

            return string.Empty;
        }

        private static void AddBaseTypesAndThis(IList<ITypeSymbol> result, ITypeSymbol? type)
        {
            if (type != null && !result.Contains(type) && type.SpecialType == SpecialType.None)
            {
                result.Add(type);
                AddBaseTypesAndThis(result, type.BaseType);
                foreach(var typeInterface in type.Interfaces)
                {
                    AddBaseTypesAndThis(result, typeInterface);
                }
            }
        }

        /// <summary>
        /// Created on demand before each generation pass
        /// </summary>
        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<Candidate> Candidates { get; } = new List<Candidate>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
                {
                    var typeName = objectCreationExpressionSyntax.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault()
                        ?.ChildTokens().FirstOrDefault().Text;
                    if ((typeName?.EndsWith(PostFix, StringComparison.InvariantCulture) ?? false)
                        && objectCreationExpressionSyntax.Parent != null
                        && objectCreationExpressionSyntax.Parent is CastExpressionSyntax castExpressionSyntax)
                    {
                        Candidates.Add(new Candidate(typeName, objectCreationExpressionSyntax, castExpressionSyntax));
                    }
                }
            }
        }
    }
}
