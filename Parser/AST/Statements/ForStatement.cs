using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class ForStatement : Statement
    {
        public RangeExpression Range { get; set; } = new RangeExpression();

        public VariableDeclarationStatement Variable { get; set; } = new VariableDeclarationStatement();

        public ClosureStatement Body { get; set; } = new ClosureStatement();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("for ");
            Builder.Append(Range.PrettyPrint());
            Builder.Append(", ");
            Builder.Append(Variable.Name);
            Builder.Append(Environment.NewLine);

            Builder.Append(Body.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }
}
