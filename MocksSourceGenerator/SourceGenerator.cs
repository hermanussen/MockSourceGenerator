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

                    var targetSymbolMembersSources = GetMemberSources(targetTypeSymbol, candidate);
                    var targetSymbolPropertiesSources = GetPropertiesSources(targetTypeSymbol);

#pragma warning disable CA1308 // Normalize strings to uppercase
                    string mockSource = $@"
namespace {namespaceName}
{{
    {Enum.GetName(typeof(Accessibility), targetTypeSymbol.DeclaredAccessibility)?.ToLowerInvariant()} class {candidate.TypeName} : {targetTypeName}
    {{
{string.Join("\r\n", targetSymbolPropertiesSources)}
{string.Join("\r\n", targetSymbolMembersSources)}
    }}
}}";
#pragma warning restore CA1308 // Normalize strings to uppercase

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

        private static IEnumerable<string> GetPropertiesSources(ITypeSymbol targetTypeSymbol)
        {
            List<ITypeSymbol> allTypes = new List<ITypeSymbol>();
            AddBaseTypesAndThis(allTypes, targetTypeSymbol);
            return allTypes
                .SelectMany(t => t.GetMembers().OfType<IPropertySymbol>().Select(m => new { Type = t, Member = m }))
                .Select(m =>
                {
                    return $@"
    /// <summary>
    /// Implemented for type {GetFullyQualifiedTypeName(m.Type)}
    /// </summary>
    public {GetFullyQualifiedTypeName(m.Member.Type)} {m.Member.Name} {{ get; set; }}";
                });
        }

        private static IEnumerable<string> GetMemberSources(ITypeSymbol targetTypeSymbol, Candidate candidate)
        {
            List<ITypeSymbol> allTypes = new List<ITypeSymbol>();
            AddBaseTypesAndThis(allTypes, targetTypeSymbol);
            return allTypes
                .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>().Select(m => new { Type = t, Member = m }))
                .Where(m => !m.Member.IsStatic
                    && !m.Member.IsImplicitlyDeclared
                    && !m.Member.Name.StartsWith("get_", StringComparison.InvariantCulture)
                    && !m.Member.Name.StartsWith("set_", StringComparison.InvariantCulture))
                .Select(m =>
                {
                    var methodParameters = string.Join(", ",
                        m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)} {p.Name}"));
                    var methodParameterNames = string.Join(", ", m.Member.Parameters.Select(p => $"{p.Name}"));
                    var methodParameterTypes =
                        string.Join(",", m.Member.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)}"));

                    if (SymbolEqualityComparer.Default.Equals(m.Type, targetTypeSymbol)
                        && m.Member.MethodKind == MethodKind.Constructor)
                    {
                        return $@"
        public {candidate.TypeName}({methodParameters}) : base({methodParameterNames})
        {{
        }}";
                    }

                    var funcTypeParameters =
                        $"{methodParameterTypes},{(m.Member.ReturnsVoid ? string.Empty : GetFullyQualifiedTypeName(m.Member.ReturnType))}"
                            .Trim(',');
                    funcTypeParameters = m.Member.Parameters.Any() || !m.Member.ReturnsVoid ? $"<{funcTypeParameters}>" : string.Empty;

                    var funcTypeName = m.Member.ReturnsVoid ? "Action" : "Func";
                    var overrideStr = targetTypeSymbol.TypeKind == TypeKind.Class && (m.Member.IsAbstract || m.Member.IsVirtual)
                        ? "override "
                        : string.Empty;

#pragma warning disable CA1308 // Normalize strings to uppercase
                    return $@"
        /// <summary>
        /// Implemented for type {GetFullyQualifiedTypeName(m.Type)}
        /// </summary>
        public {funcTypeName}{funcTypeParameters} Mock{m.Member.Name} {{ get; set; }}
        {Enum.GetName(typeof(Accessibility), m.Member.DeclaredAccessibility)?.ToLowerInvariant()} {overrideStr}{(m.Member.ReturnsVoid ? "void" : GetFullyQualifiedTypeName(m.Member.ReturnType))} {m.Member.Name}({methodParameters})
        {{
            if (Mock{m.Member.Name} == null)
            {{
                throw new NotImplementedException(""Method 'Mock{m.Member.Name}' was called, but no mock implementation was provided"");
            }}

            {(m.Member.ReturnsVoid ? string.Empty : "return ")}Mock{m.Member.Name}({methodParameterNames});
        }}";
                });
#pragma warning restore CA1308 // Normalize strings to uppercase
        }

        private static string GetFullyQualifiedTypeName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
