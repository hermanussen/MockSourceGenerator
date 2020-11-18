using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MocksSourceGenerator
{
    public class Candidate
    {
        public string TypeName { get; }
        public ObjectCreationExpressionSyntax ObjectCreationExpressionSyntax { get; }

        public CastExpressionSyntax TargetType { get; }

        public Candidate(
            string typeName,
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax,
            CastExpressionSyntax targetType)
        {
            this.TypeName = typeName;
            this.ObjectCreationExpressionSyntax = objectCreationExpressionSyntax;
            this.TargetType = targetType;
        }
    }
}
