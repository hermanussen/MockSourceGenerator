using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MocksSourceGenerator
{
    [Generator]
    public class MocksSourceGenerator : ISourceGenerator
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
                if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
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
                    var targetSymbolPropertiesSources = GetPropertiesSources(targetTypeSymbol, candidate);

                    string mockSource = $@"
namespace {namespaceName}
{{
    {Enum.GetName(typeof(Accessibility), targetTypeSymbol.DeclaredAccessibility)?.ToLowerInvariant()} class {candidate.TypeName} : {targetTypeName}
    {{{string.Join("\r\n", targetSymbolPropertiesSources)}
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

        private static IEnumerable<string> GetPropertiesSources(ITypeSymbol targetTypeSymbol, Candidate candidate)
        {
            return targetTypeSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Select(m =>
                {
                    return $@"
    public {GetFullyQualifiedTypeName(m.Type)} {m.Name} {{ get; set; }}";
                });
        }

        private static IEnumerable<string> GetMemberSources(ITypeSymbol targetTypeSymbol, Candidate candidate)
        {
            return targetTypeSymbol
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.IsStatic && !m.IsImplicitlyDeclared && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                .Select(m =>
                {
                    var methodParameters = string.Join(", ",
                        m.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)} {p.Name}"));
                    var methodParameterNames = string.Join(", ", m.Parameters.Select(p => $"{p.Name}"));
                    var methodParameterTypes =
                        string.Join(",", m.Parameters.Select(p => $"{GetFullyQualifiedTypeName(p.Type)}"));

                    if (m.MethodKind == MethodKind.Constructor)
                    {
                        return $@"
        public {candidate.TypeName}({methodParameters}) : base({methodParameterNames})
        {{
        }}";
                    }

                    var funcTypeParameters =
                        $"{methodParameterTypes},{(m.ReturnsVoid ? string.Empty : GetFullyQualifiedTypeName(m.ReturnType))}"
                            .Trim(',');
                    funcTypeParameters = m.Parameters.Any() || !m.ReturnsVoid ? $"<{funcTypeParameters}>" : string.Empty;

                    var funcTypeName = m.ReturnsVoid ? "Action" : "Func";
                    var overrideStr = targetTypeSymbol.TypeKind == TypeKind.Class && (m.IsAbstract || m.IsVirtual)
                        ? "override "
                        : string.Empty;

                    return $@"
        public {funcTypeName}{funcTypeParameters} Mock{m.Name} {{ private get; set; }}
        {Enum.GetName(typeof(Accessibility), m.DeclaredAccessibility)?.ToLowerInvariant()} {overrideStr}{(m.ReturnsVoid ? "void" : GetFullyQualifiedTypeName(m.ReturnType))} {m.Name}({methodParameters})
        {{
            if (Mock{m.Name} == null)
            {{
                throw new NotImplementedException(""Method 'Mock{m.Name}' was called, but no mock implementation was provided"");
            }}

            {(m.ReturnsVoid ? string.Empty : "return ")}Mock{m.Name}({methodParameterNames});
        }}";
                });
        }

        private static string GetFullyQualifiedTypeName(ITypeSymbol typeSymbol)
        {
            Action<string> ac = (a) => {};
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
                    if ((typeName?.EndsWith(PostFix) ?? false)
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
