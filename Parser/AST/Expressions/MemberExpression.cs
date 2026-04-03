using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class MemberExpression : Expression
    {
        public Expression Parent { get; set; } = null!;

        public Expression Member { get; set; } = null!;
    }
}
