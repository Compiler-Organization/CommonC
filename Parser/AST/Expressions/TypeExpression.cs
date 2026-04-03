using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    /// <summary>
    /// Type expression for reserved types (E.g: string, str, int, i32, etc.)
    /// </summary>
    public class TypeExpression : Expression
    {
        public ReservedTypes Type { get; set; }
    }
}
