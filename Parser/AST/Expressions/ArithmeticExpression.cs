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

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(Left.ToString());

            switch (Operator)
            {
                case ArithmeticOperator.Addition:
                    Builder.Append(" + ");
                    break;
                case ArithmeticOperator.Subtraction:
                    Builder.Append(" - ");
                    break;
                case ArithmeticOperator.Multiplication:
                    Builder.Append(" * ");
                    break;
                case ArithmeticOperator.Division:
                    Builder.Append(" / ");
                    break;
                case ArithmeticOperator.Modulo:
                    Builder.Append(" % ");
                    break;
                case ArithmeticOperator.Exponentiation:
                    Builder.Append(" ^ ");
                    break;
                case ArithmeticOperator.LeftShift:
                    Builder.Append(" << ");
                    break;
                case ArithmeticOperator.RightShift:
                    Builder.Append(" >> ");
                    break;
            }

            Builder.Append(Right.ToString());

            return Builder.ToString();
        }
    }

    public enum ArithmeticOperator
    {
        Addition = LexKinds.Addition,
        Subtraction = LexKinds.Subtraction,
        Multiplication = LexKinds.Multiplication,
        Division = LexKinds.Division,
        Modulo = LexKinds.Modulus,
        LeftShift = LexKinds.LeftShift,
        RightShift = LexKinds.RightShift,
        Xor = LexKinds.Xor,
        Exponentiation = LexKinds.Exponentiation,
    }
}
