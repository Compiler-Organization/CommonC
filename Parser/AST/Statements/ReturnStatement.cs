using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class ReturnStatement : Statement
    {
        public Expression? Expression { get; set; } = null;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("return");
            if (Expression != null)
            {
                Builder.Append(" ");
                Builder.Append(Expression.PrettyPrint(indentLevel));
            }
            Builder.Append(";");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }
}
