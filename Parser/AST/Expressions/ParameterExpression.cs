using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ParameterExpression : Expression
    {
        public Expression Type { get; set; } = null!;

        public Expression? Value { get; set; } = null;

        public string Name { get; set; } = "";

        public override string PrettyPrint(int indentLevel = 0)
        {
            return "";
        }
    }

    public class ParameterExpressionList : List<ParameterExpression>
    {
        public bool IsVararg { get; set; }

        public bool MatchTypes(ExpressionList expressions, bool ignorePointerTypes)
        {
            if (this.Count != expressions.Count)
                throw new Exception($"The count of this ({this.Count}) does not exactly match expressions ({expressions.Count})");

            bool matches = true;

            for (int i = 0; i < this.Count; i++)
            {
                if (!this[i].TypeAnnotation.Match(expressions[i].TypeAnnotation, ignorePointerTypes))
                {
                    matches = false;
                    break;
                }
            }

            return matches;
        }

        public bool MatchTypes(ParameterExpressionList parameters, bool ignorePointerTypes)
            => MatchTypes(new ExpressionList([.. parameters]), ignorePointerTypes);

        public string PrettyPrint(int indentedLevel = 0)
            => string.Join("", this.Select(p => p.PrettyPrint(indentedLevel)));
    }
}
