using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class RelationalExpression : Expression
    {
        public RelationalOperators Operator { get; set; }

        public Expression Left { get; set; } = null!;

        public Expression Right { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Left.PrettyPrint(indentLevel));
            switch (Operator)
            {
                case RelationalOperators.Equal:
                    Builder.Append(" == ");
                    break;
                case RelationalOperators.NotEqual:
                    Builder.Append(" != ");
                    break;
                case RelationalOperators.GreaterThan:
                    Builder.Append(" > ");
                    break;
                case RelationalOperators.LessThan:
                    Builder.Append(" < ");
                    break;
                case RelationalOperators.GreaterThanOrEqual:
                    Builder.Append(" >= ");
                    break;
                case RelationalOperators.LessThanOrEqual:
                    Builder.Append(" <= ");
                    break;
            }
            Builder.Append(Right.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
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
