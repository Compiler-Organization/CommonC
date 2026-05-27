using CommonC.Parser.AST;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Semantic.Objects
{
    public class TypeAnnotation
    {
        public bool IsReservedType { get; set; }

        public ReservedTypes ReservedType { get; set; }

        public bool IsStruct { get; set; }

        public StructStatement Struct { get; set; }

        public bool IsArray { get; set; }
    }
}
