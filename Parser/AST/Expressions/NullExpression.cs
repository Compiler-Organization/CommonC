using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Parser.AST.Expressions
{
    public class NullExpression : Expression
    {
        public override string PrettyPrint(int indentLevel = 0)
        {
            return "null";
        }
    }
}
