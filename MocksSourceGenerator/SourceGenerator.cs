using System;
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
                var usings = new List<string>();
                var generatedTypes = new Dictionary<string,string>();

                foreach (var candidate in receiver.Candidates)
                {
                    var semanticModel = context.Compilation.GetSemanticModel(candidate.ObjectCreationExpressionSyntax.SyntaxTree);
                    var targetTypeSymbol = semanticModel.GetSymbolInfo(candidate.TargetType.Type).Symbol?.OriginalDefinition as ITypeSymbol;
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

                    usings.Add($"using {targetTypeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)};");

                    var targetSymbolMembersSources = GetMemberSources(targetTypeSymbol, candidate, isSameAssembly);
                    var targetSymbolPropertiesSources = GetPropertiesSources(targetTypeSymbol, isSameAssembly);

                    string mockSource = $@"
namespace {namespaceName}
{{
    {GetAccessibility(targetTypeSymbol, isSameAssembly)} partial class {candidate.TypeName} : {targetTypeName}
    {{
{string.Join("\r\n", targetSymbolPropertiesSources)}
{string.Join("\r\n", targetSymbolMembersSources)}
    }}
}}";

                    mockSources.Add(mockSource);
                    generatedTypes.Add(fullMockTypeName, targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }


                var source = $@"using System;
{string.Join("\r\n", usings.Distinct())}
{string.Join("\r\n", mockSources)}";

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

                    return $@"
    /// <summary>
    /// Implemented for type {GetFullyQualifiedTypeName(m.Type)}
    /// </summary>
    {GetAccessibility(m.Member, isSameAssembly)} {overrideStr}{GetFullyQualifiedTypeName(m.Member.Type)} {m.Member.Name} {{ get; set; }}";
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
                    && !m.Member.Name.StartsWith("set_", StringComparison.InvariantCulture)
                    && (m.Type.TypeKind != TypeKind.Class || m.Member.IsAbstract || m.Member.IsVirtual || m.Member.MethodKind == MethodKind.Constructor))
                .GroupBy(m => m.Member.Name);

            foreach(var group in memberGroups)
            {
                foreach (var m in group)
                {
                    var methodParameters = string.Join(", ",
                        m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)} {p.Name}"));
                    var methodParameterNames = string.Join(", ", m.Member.Parameters.Select(p => $"{p.Name}"));
                    var methodParameterTypes =
                        string.Join(",", m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)}"));

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

                    var funcTypeParameters =
                        $"{methodParameterTypes},{(m.Member.ReturnsVoid ? string.Empty : GetFullyQualifiedTypeName(m.Member.ReturnType))}"
                            .Trim(',');
                    funcTypeParameters = m.Member.Parameters.Any() || !m.Member.ReturnsVoid ? $"<{funcTypeParameters}>" : string.Empty;

                    var funcTypeName = m.Member.ReturnsVoid ? "Action" : "Func";
                    var overrideStr = targetTypeSymbol.TypeKind == TypeKind.Class && (m.Member.IsAbstract || m.Member.IsVirtual)
                        ? "override "
                        : string.Empty;

                    memberSources.Add($@"
        /// <summary>
        /// Implemented for type {GetFullyQualifiedTypeName(m.Type)} ({m.Member.DeclaredAccessibility}, same assembly: {isSameAssembly})
        /// </summary>
        public {funcTypeName}{funcTypeParameters} Mock{name} {{ get; set; }}
        {GetAccessibility(m.Member, isSameAssembly)} {overrideStr}{(m.Member.ReturnsVoid ? "void" : GetFullyQualifiedTypeName(m.Member.ReturnType))} {m.Member.Name}({methodParameters})
        {{
            if (Mock{name} == null)
            {{
                throw new NotImplementedException(""Method 'Mock{name}' was called, but no mock implementation was provided"");
            }}

            {(m.Member.ReturnsVoid ? string.Empty : "return ")}Mock{name}({methodParameterNames});
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

        private static void AddBaseTypesAndThis(IList<ITypeSymbol> result, ITypeSymbol type)
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
