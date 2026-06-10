using CommonC.Lexer.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class LogicalExpression : Expression
    {
        public Expression Left { get; set; } = null!;

        public LogicalOperator Operator { get; set; }

        public Expression Right { get; set; } = null!;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Left.PrettyPrint(indentLevel));
            switch (Operator)
            {
                case LogicalOperator.And:
                    Builder.Append(" and ");
                    break;
                case LogicalOperator.Or:
                    Builder.Append(" or ");
                    break;
            }
            Builder.Append(Right.PrettyPrint(indentLevel));

            return Builder.ToString();
        }
    }

    public enum LogicalOperator
    {
        And = LexKinds.LogicalAnd,
        Or = LexKinds.LogicalOr,
    }
}
