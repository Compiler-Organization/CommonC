using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class LengthExpression : Expression
    {
        public Expression Expression { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append("#");
            Builder.Append(Expression.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }
}
