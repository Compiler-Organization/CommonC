using CommonC.CodeGen.DotNet;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using GeneralTK.Extensions.Console;
using GeneralTK.Extensions.Logging;
using System.Diagnostics;

namespace CommonC.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();

            CommonCCompilerSettings settings = new CommonCCompilerSettings
            {
                FilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
                WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
                DotNetCodeGenSettings = new DotNetCodeGenSettings
                {
                    Name = "test",
                }
            };

            CommonCCompiler compiler = new CommonCCompiler(settings);
            compiler.Compile();

            ConsoleColor.Green.WriteLine("Starting application...");

            var process = new Process();

            process.StartInfo.FileName = "test.exe";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            ConsoleColor.White.Write(output);
            if(!string.IsNullOrEmpty(error))
            {
                ConsoleColor.Red.Write(error);
            }

            ConsoleColor.Green.Write("Execution completed!");
        }

    }
}
