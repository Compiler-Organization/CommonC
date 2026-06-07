using CommonC.Semantic.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public abstract class Expression
    {
        public TypeAnnotation TypeAnnotation { get; set; } = new TypeAnnotation();

        public ulong Line { get; set; }

        public string FileName { get; set; } = "unspecified";

        public abstract string PrettyPrint(int indentLevel = 0);

        /// <summary>
        /// Converts the statement to a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return PrettyPrint(0);
        }

        protected string GetIndent(int level) => new string(' ', level * 4);
    }
}
