using CommonC.Lexer.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ArithmeticExpression : Expression
    {
        public Expression Left { get; set; } = null!;

        public ArithmeticOperator Operator { get; set; }

        public Expression Right { get; set; } = null!;
    }

    public enum ArithmeticOperator
    {
        Addition = LexKinds.Addition,
        Subtraction = LexKinds.Subtraction,
        Multiplication = LexKinds.Multiplication,
        Division = LexKinds.Division,
        Modulus = LexKinds.Modulus,
        Exponential = LexKinds.Exponential,
        LeftShift = LexKinds.LeftShift,
        RightShift = LexKinds.RightShift,
    }
}
