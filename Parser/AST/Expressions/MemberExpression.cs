using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class MemberExpression : Expression
    {
        public Expression Parent { get; set; } = null!;

        public Expression Member { get; set; } = null!;

        public ExpressionList Flatten()
        {
            ExpressionList expressions = new ExpressionList();

            if(Parent is MemberExpression parentMember)
            {
                expressions.AddRange(parentMember.Flatten());
            }
            else
            {
                expressions.Add(Parent);
            }

            if(Member is MemberExpression memberMember) 
            { 
                expressions.AddRange(memberMember.Flatten());
            }
            else
            {
                expressions.Add(Member);
            }

            return expressions;
        }
    }
}
