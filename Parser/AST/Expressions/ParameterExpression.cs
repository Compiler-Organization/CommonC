using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ParameterExpression : Expression
    {
        public Expression Type { get; set; } = new Expression();

        public Expression? Value { get; set; } = null;

        public string Name { get; set; } = "";
    }
}
