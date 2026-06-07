using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Statements
{
    public abstract class Statement
    {
        public TypeAnnotation TypeAnnotation { get; set; } = new TypeAnnotation();

        public ulong Line { get; set; }
        public string FileName { get; set; } = "unspecified";
        public abstract string PrettyPrint(int indentLevel = 0);

        /// <summary>
        /// Converts the statement to a string
        /// </summary>
        /// <returns></returns>
        public string ToString(bool newLine)
        {
            return PrettyPrint(0) + (newLine ? Environment.NewLine : "");
        }

        protected string GetIndent(int level) => new string(' ', level * 4);
    }
}
