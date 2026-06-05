using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class ArrayExpression : Expression
    {
        public ExpressionList Expressions { get; set; } = new ExpressionList();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append("{");
            if (Expressions != null)
            {
                Builder.Append(Expressions.ToString());
            }
            Builder.Append("}");

            return Builder.ToString();
        }
    }
}
