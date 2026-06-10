using CommonC.Lexer.Objects;
using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class AssignmentStatement : Statement
    {
        public Expression Variable { get; set; } = null!;

        public Expression Expression { get; set; } = null!;

        public AssignmentOperator Operator { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append(Variable.PrettyPrint());

            switch (Operator)
            {
                case AssignmentOperator.Equals:
                    Builder.Append(" = ");
                    break;
                case AssignmentOperator.CompoundAdd:
                    Builder.Append(" += ");
                    break;
                case AssignmentOperator.CompoundSubtract:
                    Builder.Append(" -= ");
                    break;
                case AssignmentOperator.CompoundMultiply:
                    Builder.Append(" *= ");
                    break;
                case AssignmentOperator.CompoundDivide:
                    Builder.Append(" /= ");
                    break;
                case AssignmentOperator.CompoundModulo:
                    Builder.Append(" %= ");
                    break;
                case AssignmentOperator.CompoundXor:
                    Builder.Append(" ^= ");
                    break;
                case AssignmentOperator.CompoundExp:
                    Builder.Append(" **= ");
                    break;
                case AssignmentOperator.CompoundLeftShift:
                    Builder.Append(" <<= ");
                    break;
                case AssignmentOperator.CompoundRightShift:
                    Builder.Append(" >>= ");
                    break;
                case AssignmentOperator.CompoundBitwiseAnd:
                    Builder.Append(" &= ");
                    break;
                case AssignmentOperator.CompoundBitwiseOr:
                    Builder.Append(" |= ");
                    break;
            }
            Builder.Append(Expression.PrettyPrint());
            Builder.Append(";");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }

    public enum AssignmentOperator
    {
        Equals = LexKinds.Equals,
        CompoundAdd = LexKinds.CompoundAdd,
        CompoundSubtract = LexKinds.CompoundSub,
        CompoundMultiply = LexKinds.CompoundMul,
        CompoundDivide = LexKinds.CompoundDiv,
        CompoundModulo = LexKinds.CompoundMod,
        CompoundXor = LexKinds.CompoundXor,
        CompoundExp = LexKinds.CompoundExp,
        CompoundLeftShift = LexKinds.CompoundLeftShift,
        CompoundRightShift = LexKinds.CompoundRightShift,
        CompoundBitwiseAnd = LexKinds.CompoundBitwiseAnd,
        CompoundBitwiseOr = LexKinds.CompoundBitwiseOr,
    }
}
