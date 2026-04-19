using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST
{
    public enum ReservedTypes
    {
        I8,
        U8,

        I16,
        U16,

        I32,
        U32,

        I64,
        U64,

        I128,
        U128,

        F32,
        F64,

        String,
        Char,
        Bool,
        Fn,
    }
}
