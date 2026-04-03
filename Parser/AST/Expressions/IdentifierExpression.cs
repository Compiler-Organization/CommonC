using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class IdentifierExpression : Expression
    {
        public string Name { get; set; } = "";
    }
}
