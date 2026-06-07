using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Error
{
    public class ErrorHandler
    {
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
        {
            string prefix = $"Line {statement.Line}: ";
            string prettyCode = statement.PrettyPrint(0);
            string[] codeLines = prettyCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var outputLines = new List<string>();
            var caretLines = new List<string>();

            for (int i = 0; i < codeLines.Length; i++)
            {
                string currentLine = codeLines[i];
                string linePrefix = (i == 0) ? prefix : new string(' ', prefix.Length);

                outputLines.Add($"{linePrefix}{currentLine}");

                int leadingSpaces = currentLine.TakeWhile(char.IsWhiteSpace).Count();
                int totalPadding = linePrefix.Length + leadingSpaces;
                string padding = new string(' ', totalPadding);

                int codeWidth = currentLine.Length - leadingSpaces;
                int caretCount = Math.Max(1, codeWidth);
                string carets = new string('^', caretCount);

                caretLines.Add($"{padding}{carets}");
            }

            string fullCodeOutput = string.Join(Environment.NewLine, outputLines);
            string fullCaretOutput = string.Join(Environment.NewLine, caretLines);

            string finalCaretLinePadding = new string(' ', caretLines[^1].TakeWhile(c => c == ' ').Count());
            string messageOutput = $"{finalCaretLinePadding}{statement.FileName}: {message}";

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(fullCodeOutput);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(fullCaretOutput);
            Console.WriteLine(messageOutput);

            Console.ForegroundColor = ConsoleColor.Gray;

            return new Exception($"{fullCodeOutput}\n{fullCaretOutput}\n{messageOutput}");
        }

        public static Exception CreateError(string message, Expression expression)
        {
            string prefix = $"Line {expression.Line}: ";
            string prettyCode = expression.PrettyPrint(0);
            string[] codeLines = prettyCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var outputLines = new List<string>();
            var caretLines = new List<string>();

            for (int i = 0; i < codeLines.Length; i++)
            {
                string currentLine = codeLines[i];
                string linePrefix = (i == 0) ? prefix : new string(' ', prefix.Length);

                outputLines.Add($"{linePrefix}{currentLine}");

                int leadingSpaces = currentLine.TakeWhile(char.IsWhiteSpace).Count();
                int totalPadding = linePrefix.Length + leadingSpaces;
                string padding = new string(' ', totalPadding);

                int codeWidth = currentLine.Length - leadingSpaces;
                int caretCount = Math.Max(1, codeWidth);
                string carets = new string('^', caretCount);

                caretLines.Add($"{padding}{carets}");
            }

            string fullCodeOutput = string.Join(Environment.NewLine, outputLines);
            string fullCaretOutput = string.Join(Environment.NewLine, caretLines);

            string finalCaretLinePadding = new string(' ', caretLines[^1].TakeWhile(c => c == ' ').Count());
            string messageOutput = $"{finalCaretLinePadding}{expression.FileName}: {message}";

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(fullCodeOutput);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(fullCaretOutput);
            Console.WriteLine(messageOutput);

            Console.ForegroundColor = ConsoleColor.Gray;

            return new Exception($"{fullCodeOutput}\n{fullCaretOutput}\n{messageOutput}");
        }


    }
}
