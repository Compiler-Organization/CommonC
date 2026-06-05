using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Error
{
    public class ErrorHandler
    {
        public static void Throw(string message, Statement statement)
        {
            throw new Exception();
        }

        public static void Throw(string message, Expression expression)
        {

        }
    }
}
