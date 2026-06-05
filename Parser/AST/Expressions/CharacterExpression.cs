using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class CharacterExpression : Expression
    {
        public char Value { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            return $"'{Value}'";
        }
    }
}
