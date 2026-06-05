using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class IndexExpression : Expression
    {
        /// <summary>
        /// The array being indexed
        /// </summary>
        public Expression Expression { get; set; } = null!;

        /// <summary>
        /// The index being accessed
        /// </summary>
        public Expression? Index { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Expression.ToString());
            Builder.Append("[");
            if (Index != null)
            {
                Builder.Append(Index.ToString());
            }
            Builder.Append("]");

            return Builder.ToString();
        }
    }
}
