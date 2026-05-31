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

        public bool Match(TypeAnnotation other)
        {
            if (other == null)
                return false;

            if (IsReservedType != other.IsReservedType)
                return false;

            if (IsReservedType && ReservedType != other.ReservedType)
                return false;

            if (IsStruct != other.IsStruct)
                return false;

            if (IsStruct && Struct.Name != other.Struct.Name)
                return false;

            if (IsArray != other.IsArray)
                return false;

            return true;
        }
    }
}
