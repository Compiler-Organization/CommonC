using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class CharacterExpression : Expression
    {
        public char Value { get; set; }
    }
}
