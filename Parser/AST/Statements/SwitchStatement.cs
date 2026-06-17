using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class SwitchStatement : Statement
    {
        public Expression Expression { get; set; } = null!;

        public List<SwitchCase> Cases { get; set; } = new List<SwitchCase>();

        public SwitchCase? DefaultCase { get; set; } = null;

        public override string PrettyPrint(int indentLevel = 0)
        {
            string indent = new string(' ', indentLevel * 4);
            string bodyIndent = new string(' ', (indentLevel + 1) * 4);
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"{indent}switch {Expression.PrettyPrint()}");
            stringBuilder.AppendLine($"{indent}{{");

            foreach (SwitchCase @case in Cases)
            {
                stringBuilder.Append(@case.PrettyPrint(indentLevel + 1));
            }

            if (DefaultCase != null)
            {
                stringBuilder.AppendLine($"{bodyIndent}_:");
                stringBuilder.Append(DefaultCase.PrettyPrint(indentLevel + 2));
            }

            stringBuilder.AppendLine($"{indent}}}");
            return stringBuilder.ToString();
        }
    }

    public class SwitchCase : Expression
    {
        public Expression Expression { get; set; } = null!;

        public ClosureStatement Body { get; set; } = new();

        public override string PrettyPrint(int indentLevel = 0)
        {
            string indent = new string(' ', indentLevel * 4);
            StringBuilder stringBuilder = new StringBuilder();

            if(Expression == null)
            {
                stringBuilder.AppendLine($"{indent}_: ");
            }
            else
            {
                stringBuilder.Append($"{indent}{Expression.PrettyPrint()}: ");
            }

            if(Body.Statements.Count > 1)
            {
                stringBuilder.Append(Body.PrettyPrint(indentLevel + 1));
            }
            else if(Body.Statements.Count > 0)
            {
                stringBuilder.Append(Body.Statements.First().PrettyPrint());
            }

            return stringBuilder.ToString();
        }
    }
}
