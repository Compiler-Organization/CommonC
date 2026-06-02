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
    }

    public enum LogicalOperator
    {
        And = LexKinds.And,
        Or = LexKinds.Or,
    }
}
