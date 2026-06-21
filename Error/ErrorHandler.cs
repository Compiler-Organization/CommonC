using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Error
{
    public class ErrorHandler
    {
        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Warning]: {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static Exception CreateError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.Gray;

            return new Exception($"{message}");
        }

        public static Exception CreateError(string message, object errorObject)
        {
            if(errorObject is Statement statement)
                return CreateError(message, statement);
            else if(errorObject is Expression expression)
                return CreateError(message, expression);

            throw new NotSupportedException($"Object of type {errorObject.GetType().FullName} cannot be used to generate an error");
        }

        public static Exception CreateError(string message, Statement statement)
            => CreateError(message, statement.PrettyPrint(), statement.FileName, statement.Line);

        public static Exception CreateError(string message, Expression expression)
            => CreateError(message, expression.PrettyPrint(), expression.FileName, expression.Line);

        static Exception CreateError(string message, string code, string fileName, ulong lineNumber)
        {
            string prefix = $"Line {lineNumber}: ";
            string[] codeLines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int longestLineLength = 0;
            foreach (string line in codeLines)
            {
                if (line.Length > longestLineLength)
                    longestLineLength = line.Length;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(prefix);
            stringBuilder.Append(code);
            stringBuilder.AppendLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(stringBuilder.ToString());

            StringBuilder errorMessage = new StringBuilder();
            errorMessage.Append(new String(' ', prefix.Length));
            errorMessage.Append(new String('^', longestLineLength));
            errorMessage.AppendLine();

            errorMessage.Append(new String(' ', prefix.Length));
            errorMessage.Append(fileName);
            errorMessage.Append(": ");
            errorMessage.Append(message);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage.ToString());

            Console.ForegroundColor = ConsoleColor.Gray;

            return new Exception(stringBuilder.ToString() + errorMessage.ToString());
        }
    }
}
