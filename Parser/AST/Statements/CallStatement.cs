using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class CallStatement : Statement
    {
        public Expression Expression { get; set; } = null!;

        public ExpressionList Arguments { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));

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
            Builder.Append(";");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }
}
