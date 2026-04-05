using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class RangeExpression : Expression
    {
        public Expression Start { get; set; } = new Expression();

        public Expression End { get; set; } = new Expression();
    }
}
