using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    /// <summary>
    /// <para>Unpacks an array as seperate values.</para>
    /// <para>Right hand side is the amount from zero or a range.</para>
    /// <para>Example: log({ "Hello ", "there ", "world!" }->0..2) // Hello there world!</para>
    /// </summary>
    public class UnpackExpression : Expression
    {
        public Expression Left { get; set; } = new Expression();

        public Expression Right { get; set; } = new Expression();
    }
}
