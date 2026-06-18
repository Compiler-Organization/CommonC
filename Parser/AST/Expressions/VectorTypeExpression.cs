using CommonC.Error;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class VectorTypeExpression : Expression
    {
        public TypeExpression? Type { get; set; } = null;

        public NumberExpression? Size { get; set; } = null;

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if(Size == null)
            {
                Size = new NumberExpression();
                if (Type == null)
                {
                    Type = new TypeExpression();
                }
                throw ErrorHandler.CreateError("Size of vector cannot be null when prettyprinting", this);
            }
            if (Type == null)
            {
                Type = new TypeExpression();
                throw ErrorHandler.CreateError("Type of vector cannot be null when prettyprinting", this);
            }

            stringBuilder.Append("vector");
            stringBuilder.Append('<');
            stringBuilder.Append(Size.PrettyPrint());
            stringBuilder.Append(" x ");
            stringBuilder.Append(Type.PrettyPrint());
            stringBuilder.Append('>');

            return stringBuilder.ToString();
        }
    }
}
