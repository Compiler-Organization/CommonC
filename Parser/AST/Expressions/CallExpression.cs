using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class CallExpression : Expression
    {
        public Expression Expression { get; set; } = null!;

        public ExpressionList Arguments { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            if (Expression != null)
            {
                Builder.Append(Expression.ToString());
            }
            Builder.Append("(");
            if (Arguments != null)
            {
                Builder.Append(Arguments.ToString());
            }
            Builder.Append(")");

            return Builder.ToString();
        }
    }
}
