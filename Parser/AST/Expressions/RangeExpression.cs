using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class RangeExpression : Expression
    {
        public Expression Start { get; set; } = null!;

        public Expression End { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Start.PrettyPrint(indentLevel));
            Builder.Append("..");
            Builder.Append(End.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }
}
