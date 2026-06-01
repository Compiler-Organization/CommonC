using CommonC.Parser.AST;
using CommonC.Parser.AST.Statements;
using LLVMSharp.Interop;
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

        public int ArrayDepth { get; set; }

        public bool IsVariable { get; set; } = false;

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

        public LLVMTypeRef ToLLVMType(bool destructArray = false)
        {
            LLVMTypeRef baseType = this switch
            {
                { IsReservedType: true } => ReservedType switch
                {
                    ReservedTypes.I8 or ReservedTypes.U8 or ReservedTypes.Char => LLVMTypeRef.Int8,
                    ReservedTypes.I16 or ReservedTypes.U16 => LLVMTypeRef.Int16,
                    ReservedTypes.I32 or ReservedTypes.U32 => LLVMTypeRef.Int32,
                    ReservedTypes.I64 or ReservedTypes.U64 => LLVMTypeRef.Int64,
                    ReservedTypes.I128 or ReservedTypes.U128 => LLVMTypeRef.CreateInt(128),
                    ReservedTypes.F32 => LLVMTypeRef.Float,
                    ReservedTypes.F64 => LLVMTypeRef.Double,
                    ReservedTypes.Bool => LLVMTypeRef.Int1,
                    ReservedTypes.String => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                    ReservedTypes.Fn => LLVMTypeRef.Void,
                    _ => throw new InvalidOperationException($"Unsupported reserved type: {ReservedType}")
                },
                { IsStruct: true } => Struct.LLVMStructType,
                _ => throw new InvalidOperationException($"Type annotation does not have a valid LLVM type: {ToString()}")
            };

            return !destructArray && IsArray ? LLVMTypeRef.CreatePointer(baseType, 0) : baseType;
        }

        /// <summary>
        /// Creates a deep copy of the type annotation.
        /// Useful for when you want to modify a type annotation without affecting the original
        /// </summary>
        /// <returns></returns>
        public TypeAnnotation Copy()
        {
            return new TypeAnnotation
            {
                IsReservedType = IsReservedType,
                ReservedType = ReservedType,
                IsStruct = IsStruct,
                Struct = Struct,
                IsArray = IsArray,
                ArrayDepth = ArrayDepth,
                IsVariable = IsVariable
            };
        }

        /// <summary>
        /// Converts the annotation to a readable string for debugging purposes
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{(IsReservedType ? ReservedType.ToString() : IsStruct ? Struct.Name : "Unknown")}{(IsArray ? string.Concat(Enumerable.Repeat("[]", ArrayDepth)) : "")}";
        }
    }
}
