using AsmResolver.DotNet;
using CommonC.DotNet;
using CommonC.DotNet.CodeGen;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.LLVM;
using CommonC.LLVM.CodeGen;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using GeneralTK.Extensions.Console;
using GeneralTK.Extensions.Logging;
using LLVMSharp.Interop;
using System.Buffers;
using System.Diagnostics;

namespace CommonC.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();

            // CreateDotNet();
             CreateLLVM();
            // RunLLVM();

            // Console.WriteLine($"LLVM IR\n=========\n{CreateLLVMModule()}");
        }

        static void RunLLVM()
        {
            string appName = "test";

            LLVMCommonCCompilerSettings settings = new LLVMCommonCCompilerSettings
            {
                MainFilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
                WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
                LLVMCodeGenSettings = new LLVMCodeGenSettings
                {
                    Name = appName,
                    EntryPoint = "main",
                    Version = new Version(1, 0, 0, 0)
                }
            };

            LLVMCommonCCompiler compiler = new LLVMCommonCCompiler(settings);

            ConsoleColor.Green.WriteLine("Compiling module...");
            LLVMModuleRef module = compiler.BuildLLVMModule();
            Console.WriteLine($"LLVM IR\n=========\n{module}");

            ConsoleColor.Green.WriteLine("Running module...");

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            LLVMGenericValueRef p = compiler.RunModule(module, new LLVMGenericValueRef[0]);
            stopwatch.Stop();

            ConsoleColor.Green.WriteLine($"\nExecution completed in;");
            ConsoleColor.Green.WriteLine($"Seconds; {(stopwatch.ElapsedMilliseconds) / (double)1000}s");
            ConsoleColor.Green.WriteLine($"Milliseconds; {stopwatch.ElapsedMilliseconds}ms");
        }

        static LLVMModuleRef CreateLLVMModule()
        {
            string appName = "test";

            LLVMCommonCCompilerSettings settings = new LLVMCommonCCompilerSettings
            {
                MainFilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
                WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
                LLVMCodeGenSettings = new LLVMCodeGenSettings
                {
                    Name = appName,
                    EntryPoint = "main",
                    Version = new Version(1, 0, 0, 0)
                }
            };

            LLVMCommonCCompiler compiler = new LLVMCommonCCompiler(settings);
            return compiler.BuildLLVMModule();
        }

        static void CreateLLVM()
        {
            string appName = "test";

            LLVMCommonCCompilerSettings settings = new LLVMCommonCCompilerSettings
            {
                MainFilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
                WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
                TargetTripe = "i686-pc-windows-msvc19.50.35730",
                LLVMCodeGenSettings = new LLVMCodeGenSettings
                {
                    Name = appName,
                    EntryPoint = "main",
                    Version = new Version(1, 0, 0, 0)
                }
            };
            
            LLVMCommonCCompiler compiler = new LLVMCommonCCompiler(settings);
            LLVMModuleRef module = compiler.Compile(out bool success);


            string moduleIR = string.Join(Environment.NewLine,
            module.ToString().Split('\n')
                .Select((line, index) => $"{index}: {line}"));


            Console.WriteLine($"LLVM IR\n=========\n{moduleIR}");

            if(success)
            {
                File.WriteAllText($"{appName}.ll", module.ToString());
                StartApp($"{Environment.CurrentDirectory}\\{appName}");
            }
        }

        static void CreateDotNet()
        {
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
            AsmResolver.PE.File.PEFile peFile = compiler.Compile();

            peFile.Write($"{appName}.dll");
            compiler.CreateAppHost();

            StartApp(appName);
        }

        static void StartApp(string appName)
        {
            ConsoleColor.Green.WriteLine("Starting application...");

            string exePath = $"{appName}.exe";
            if (!File.Exists(exePath))
            {
                ConsoleColor.Red.WriteLine($"File {exePath} does not exist!");
                return;
            }

            using (var process = new Process())
            {
                process.StartInfo.FileName = exePath;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError = false;
                process.StartInfo.RedirectStandardInput = false;

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                process.Start();
                process.WaitForExit();

                stopwatch.Stop();

                ConsoleColor.Green.WriteLine($"\nExecution completed in;");
                ConsoleColor.Green.WriteLine($"Seconds; {(stopwatch.ElapsedMilliseconds) / (double)1000}s");
                ConsoleColor.Green.WriteLine($"Milliseconds; {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
