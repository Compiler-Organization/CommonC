using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ExpressionList : List<Expression>
    {
        public ExpressionList() { }

        public ExpressionList(params List<Expression> expressions)
        {
            this.AddRange(expressions);
        }

        public bool IsLast(Expression e) => this.Count > 0 && this.Last() == e;

        public bool MatchTypes(ExpressionList other, bool ignorePointerTypes)
        {
            if (this.Count != other.Count)
                throw new Exception($"The count of this ({this.Count}) does not exactly match other ({other.Count})");

            bool matches = true;

            for(int i = 0; i < this.Count; i++)
            {
                if (!this[i].TypeAnnotation.Match(other[i].TypeAnnotation, ignorePointerTypes))
                {
                    matches = false;
                    break;
                }
            }

            return matches;
        }

        public override string ToString()
            => string.Join(", ", this.Select(e => e.ToString()));

        /// <summary>
        /// Converts a list of expressions to a string
        /// </summary>
        /// <returns></returns>
        public string PrettyPrint(int indentedLevel = 0)
            => string.Join(", ", this.Select(p => p.PrettyPrint(indentedLevel)));
    }
}
