using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class IndexExpression : Expression
    {
        /// <summary>
        /// The array being indexed
        /// </summary>
        public Expression Expression { get; set; } = new Expression();

        /// <summary>
        /// The index being accessed
        /// </summary>
        public Expression Index { get; set; } = new Expression();
    }
}
