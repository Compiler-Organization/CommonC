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
        EqualTo,

        /// <summary>
        /// ~=
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// >
        /// </summary>
        BiggerThan,

        /// <summary>
        /// >=
        /// </summary>
        BiggerOrEqual,

        /// <summary>
        /// <
        /// </summary>
        SmallerThan,

        /// <summary>
        /// <=
        /// </summary>
        SmallerOrEqual
    }
}
