using CommonC.Parser.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic.Objects
{
    public class TypeAnnotation
    {
        public bool IsReservedType { get; set; }

        public ReservedTypes ReservedType { get; set; }

        public string UserTypeName { get; set; } = "";
    }
}
