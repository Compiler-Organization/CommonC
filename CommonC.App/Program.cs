using AsmResolver.DotNet;
using CommonC.DotNet;
using CommonC.DotNet.CodeGen;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using GeneralTK.Extensions.Console;
using GeneralTK.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;

namespace CommonC.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();

            string appName = "godspeaks";

            DotNetCommonCCompilerSettings settings = new DotNetCommonCCompilerSettings
            {
                MainFilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
                WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
                DotNetCodeGenSettings = new DotNetCodeGenSettings
                {
                    Name = appName,
                    Version = new Version(1, 0, 0, 0),
                    DotNetRuntimeInfo = DotNetRuntimeInfo.NetCoreApp(10, 0, 0)
                }
            };

            DotNetCommonCCompiler compiler = new DotNetCommonCCompiler(settings);
            compiler.Compile().Write($"{appName}.dll");
            compiler.CreateAppHost();

            ConsoleColor.Green.WriteLine("Starting application...");

            var process = new Process();

            process.StartInfo.FileName = $"{appName}.exe";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            Stopwatch stopwatch = new Stopwatch();
            process.Start();
            stopwatch.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            stopwatch.Stop();

            ConsoleColor.White.Write(output);
            if (!string.IsNullOrEmpty(error))
            {
                ConsoleColor.Red.Write(error);
            }

            ConsoleColor.Green.WriteLine($"\nExecution completed in;");
            ConsoleColor.Green.WriteLine($"Seconds; {(stopwatch.ElapsedMilliseconds) / (double)1000}s");
            ConsoleColor.Green.WriteLine($"Milliseconds; {stopwatch.ElapsedMilliseconds}ms");

            process.Kill();
        }

    }
}
