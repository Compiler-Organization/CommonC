using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
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


        public bool IsClass { get; set; }
        public ClassStatement Class { get; set; }


        public bool IsEnum { get; set; }
        public EnumStatement Enum { get; set; }


        public bool IsArray { get; set; }
        public int ArrayDepth { get; set; }

        public bool IsVector { get; set; }
        public VectorTypeExpression VectorType { get; set; }


        public bool IsVariable { get; set; } = false;

        public bool Match(TypeAnnotation typeAnnotation, bool ignorePointerTypes)
        {
            if (typeAnnotation == null)
                return false;

            if(!ignorePointerTypes)
            {
                if (IsStruct != typeAnnotation.IsStruct)
                    return false;

                if (IsStruct && Struct.Name != typeAnnotation.Struct.Name)
                    return false;

                if (IsClass != typeAnnotation.IsClass)
                    return false;

                if (IsClass && Class.Name != typeAnnotation.Class.Name)
                    return false;

                if (IsArray != typeAnnotation.IsArray)
                    return false;
            }

            if (IsReservedType != typeAnnotation.IsReservedType)
                return false;

            if (IsReservedType && ReservedType != typeAnnotation.ReservedType)
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
                    ReservedTypes.Ptr => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                    _ => throw new InvalidOperationException($"Unsupported reserved type: {ReservedType}")
                },
                //{ IsStruct: true } => Struct.LLVMStructType,
                //{ IsClass: true } => Class.LLVMStructType,
                { IsStruct: true } => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                { IsClass: true } => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
                { IsVector: true } => LLVMTypeRef.CreateVector(VectorType.Type.TypeAnnotation.ToLLVMType(), (uint)VectorType.Size.ToUlong()),
                _ => throw new InvalidOperationException($"Type annotation does not have a valid LLVM type: {ToString()}")
            };

            if (IsArray)
            {
                int targetDepth = destructArray ? ArrayDepth - 1 : ArrayDepth;

                LLVMTypeRef pointerType = baseType;
                for (int i = 0; i < targetDepth; i++)
                {
                    pointerType = LLVMTypeRef.CreatePointer(pointerType, 0);
                }
                return pointerType;
            }

            return baseType;
        }

        public bool IsPointerType() => 
            IsStruct 
            || IsArray 
            || IsClass
            || ReservedType == ReservedTypes.String
            || ReservedType == ReservedTypes.Ptr;

        public bool IsSigned() =>
            IsReservedType ? ReservedType switch
            {
                ReservedTypes.I8 => true,
                ReservedTypes.I16 => true,
                ReservedTypes.I32 => true,
                ReservedTypes.I64 => true,
                ReservedTypes.I128 => true,
                _ => false
            } : false;

        /// <summary>
        /// Creates a shallow copy of the type annotation.
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
                IsClass = IsClass,
                Class = Class,
                IsVector = IsVector,
                VectorType = VectorType,
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
            return $"{(IsReservedType ? ReservedType.ToString() : IsStruct ? Struct.Name : IsClass ? Class.Name : IsEnum ? Enum.Name : IsVector ? VectorType.PrettyPrint() : "<Unknown!>")}{(IsArray ? string.Concat(Enumerable.Repeat("[]", ArrayDepth)) : "")}";
        }
    }
}
