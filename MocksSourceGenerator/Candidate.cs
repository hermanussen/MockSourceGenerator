using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace MocksSourceGenerator
{
    public record Candidate(
                string TypeName,
                ObjectCreationExpressionSyntax ObjectCreationExpressionSyntax,
                CastExpressionSyntax TargetType
            )
    {
        public string TypeName { get; } = TypeName;
        public ObjectCreationExpressionSyntax ObjectCreationExpressionSyntax { get; } = ObjectCreationExpressionSyntax;
        public CastExpressionSyntax TargetType { get; } = TargetType;
    }
}
