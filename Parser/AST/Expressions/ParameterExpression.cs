using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ParameterExpression : Expression
    {
        public Expression Type { get; set; } = null!;

        public Expression? Value { get; set; } = null;

        public string Name { get; set; } = "";

        public override string PrettyPrint(int indentLevel = 0)
        {
            return $"{this.Type.PrettyPrint(indentLevel)} {this.Name}{(this.Value == null ? "" : $" = {this.Value.PrettyPrint(indentLevel)}")}";
        }
    }

    public class ParameterExpressionList : List<ParameterExpression>
    {
        public bool IsVararg { get; set; }

        public bool MatchTypes(ExpressionList expressions, bool ignorePointerTypes)
        {
            if (!IsVararg && this.Count != expressions.Count)
            {
                return false;
            }

            if (IsVararg && expressions.Count < this.Count)
            {
                return false;
            }

            for (int i = 0; i < this.Count; i++)
            {
                if (!this[i].TypeAnnotation.Match(expressions[i].TypeAnnotation, ignorePointerTypes))
                {
                    return false;
                }
            }

            return true;
        }

        public bool MatchTypes(ParameterExpressionList parameters, bool ignorePointerTypes)
        {
            if (this.IsVararg != parameters.IsVararg || this.Count != parameters.Count)
            {
                return false;
            }

            for (int i = 0; i < this.Count; i++)
            {
                if (!this[i].TypeAnnotation.Match(parameters[i].TypeAnnotation, ignorePointerTypes))
                {
                    return false;
                }
            }

            return true;
        }

        public string PrettyPrint(int indentedLevel = 0)
            => string.Join(", ", this.Select(p => p.PrettyPrint(indentedLevel)));
    }
}
