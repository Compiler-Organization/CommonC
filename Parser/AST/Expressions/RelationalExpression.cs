using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class RelationalExpression : Expression
    {
        public RelationalOperators Operator { get; set; }

        public Expression Left { get; set; } = new Expression();

        public Expression Right { get; set; } = new Expression();
    }

    public enum RelationalOperators
    {
        /// <summary>
        /// ==
        /// </summary>
        Equal,

        /// <summary>
        /// ~=
        /// </summary>
        NotEqual,

        /// <summary>
        /// >
        /// </summary>
        GreaterThan,

        /// <summary>
        /// >=
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// <
        /// </summary>
        LessThan,

        /// <summary>
        /// <=
        /// </summary>
        LessThanOrEqual
    }
}
