using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class WhileStatement : Statement
    {
        public Expression Expression { get; set; } = null!;

        public ClosureStatement Body { get; set; } = new ClosureStatement();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("while ");
            Builder.Append(Expression.PrettyPrint(indentLevel));
            Builder.Append(Environment.NewLine);

            Builder.Append(Body.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }
}
