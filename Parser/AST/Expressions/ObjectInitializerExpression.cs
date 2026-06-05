using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ObjectInitializerExpression : Expression
    {
        public Expression Expression = null!;

        public List<AssignmentStatement> Fields = new List<AssignmentStatement>();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Expression.PrettyPrint(indentLevel));
            Builder.Append(" {");
            Builder.Append(Environment.NewLine);
            foreach (AssignmentStatement assignmentStatement in Fields)
            {
                Builder.Append(GetIndent(indentLevel + 1));
                Builder.Append(assignmentStatement.Variable.PrettyPrint(indentLevel + 1));
                Builder.Append(": ");
                Builder.Append(assignmentStatement.Expression.PrettyPrint(indentLevel + 1));

                if (Fields.IndexOf(assignmentStatement) != Fields.Count - 1)
                {
                    Builder.Append("," + Environment.NewLine);
                }
            }
            Builder.Append(Environment.NewLine);
            Builder.Append(GetIndent(indentLevel));
            Builder.Append("}");

            return Builder.ToString();
        }
    }
}
