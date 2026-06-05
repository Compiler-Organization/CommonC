using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ParenthesizedExpression : Expression
    {
        public Expression Expression { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new();

            Builder.Append("(");
            Builder.Append(this.Expression.PrettyPrint(indentLevel));
            Builder.Append(")");

            return Builder.ToString();
        }
    }
}
