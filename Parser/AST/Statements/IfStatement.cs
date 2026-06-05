using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class IfStatement : Statement
    {
        public Expression Condition { get; set; } = null!;

        public ClosureStatement Body { get; set; } = new ClosureStatement();

        public List<IfStatement> ElseIfs { get; set; } = new List<IfStatement>();

        public ClosureStatement Else { get; set; } = new ClosureStatement();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("if");
            Builder.Append(" ");
            Builder.Append(Condition.PrettyPrint());
            Builder.Append(Environment.NewLine);
            Builder.Append(Body.PrettyPrint(indentLevel));

            if (ElseIfs.Count > 0)
            {
                foreach (IfStatement elseIfStatement in ElseIfs)
                {
                    Builder.Append(GetIndent(indentLevel));
                    Builder.Append("elseif");
                    Builder.Append(" (");
                    Builder.Append(elseIfStatement.Condition.PrettyPrint());
                    Builder.Append(")");
                    Builder.Append(Environment.NewLine);
                    Builder.Append(elseIfStatement.Body.PrettyPrint(indentLevel));
                }
            }

            if (Else.Statements != null && Else.Statements.Count > 0)
            {
                Builder.Append(GetIndent(indentLevel));
                Builder.Append("else");
                Builder.Append(Environment.NewLine);
                Builder.Append(Else.PrettyPrint(indentLevel));
            }

            return Builder.ToString();
        }
    }
}
