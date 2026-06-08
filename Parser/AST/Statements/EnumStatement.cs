using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public class EnumStatement : Statement
    {
        public string Name { get; set; }

        public Expression? Type { get; set; }

        public List<EnumVariant> Variants = new List<EnumVariant>();

        public override string PrettyPrint(int indentLevel = 0)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append(GetIndent(indentLevel));
            Builder.Append("enum ");
            Builder.Append(Name);
            if(Type != null)
            {
                Builder.Append(" : ");
                Builder.Append(Type.ToString());
            }
            Builder.Append(" {");
            Builder.Append(Environment.NewLine);
            foreach (EnumVariant variant in Variants)
            {
                Builder.Append(GetIndent(indentLevel + 1));

                Builder.Append(variant.Name);
                if (variant.Expression != null)
                {
                    Builder.Append(": ");
                    Builder.Append(variant.Expression.PrettyPrint(indentLevel + 1));
                }
                if (Variants.IndexOf(variant) != Variants.Count - 1)
                {
                    Builder.Append(",");
                }
                Builder.Append(Environment.NewLine);
            }
            Builder.Append(GetIndent(indentLevel));
            Builder.Append("}");
            Builder.Append(Environment.NewLine);

            return Builder.ToString();
        }
    }

    public class EnumVariant
    {
        public string Name { get; set; } = "";

        public NumberExpression? Expression { get; set; }
    }
}
