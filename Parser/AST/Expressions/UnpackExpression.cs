using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    /// <summary>
    /// <para>Unpacks an array as seperate values.</para>
    /// <para>Right hand side is the amount from zero or a range.</para>
    /// <para>Example: print({ "Hello ", "there ", "world!" }->0..2) // Hello there world!</para>
    /// </summary>
    public class UnpackExpression : Expression
    {
        public Expression Left { get; set; } = null!;

        public Expression Right { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Left.PrettyPrint(indentLevel));
            Builder.Append("->");
            Builder.Append(Right.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }
}
