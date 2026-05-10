using CommonC.Lexer.Objects;
using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class AssignmentStatement : Statement
    {
        public Expression Variable { get; set; } = new Expression();

        public Expression Expression { get; set; } = new Expression();

        public AssignmentOperator Operator { get; set; }
    }

    public enum AssignmentOperator
    {
        Equals = LexKinds.Equals,
        CompoundAdd = LexKinds.CompoundAdd,
        CompoundSubtract = LexKinds.CompoundSub,
        CompoundMultiply = LexKinds.CompoundMul,
        CompoundDivide = LexKinds.CompoundDiv,
        CompoundModulo = LexKinds.CompoundMod
    }
}
