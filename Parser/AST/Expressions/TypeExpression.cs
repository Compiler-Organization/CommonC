using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    /// <summary>
    /// Type expression for reserved types (E.g: string, str, int, i32, etc.)
    /// </summary>
    public class TypeExpression : Expression
    {
        public ReservedTypes Type { get; set; }

        public override string PrettyPrint(int indentLevel = 0)
        {
            switch (Type)
            {
                case ReservedTypes.I8:
                    return "i8";
                    
                case ReservedTypes.U8:
                    return "u8";
                    

                case ReservedTypes.I16:
                    return "i16";
                    
                case ReservedTypes.U16:
                    return "u16";
                    

                case ReservedTypes.I32:
                    return "i32";
                    
                case ReservedTypes.U32:
                    return "u32";
                    

                case ReservedTypes.I64:
                    return "i64";
                    
                case ReservedTypes.U64:
                    return "u64";
                    

                case ReservedTypes.I128:
                    return "i128";
                    
                case ReservedTypes.U128:
                    return "u128";
                    

                case ReservedTypes.F32:
                    return "f32";
                    

                case ReservedTypes.F64:
                    return "f64";
                    

                case ReservedTypes.Ptr:
                    return "ptr";
                    

                case ReservedTypes.String:
                    return "str";
                    
                case ReservedTypes.Char:
                    return "char";
                    
                case ReservedTypes.Fn:
                    return "fn";
                    
                case ReservedTypes.Bool:
                    return "bool";

                case ReservedTypes.None:
                    return "None";

                default:
                    throw new Exception($"Reserved type {Type.GetType().Name} does not exist.");
            }
        }
    }
}
