using AsmResolver.DotNet;
using CommonC.DotNet;
using CommonC.DotNet.CodeGen;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using CommonC.Targets.CommonIR.CodeGen;
using CommonC.Targets.LLVM.CodeGen;
using CommonIR;
using GeneralTK.Extensions.Console;
using GeneralTK.Extensions.Logging;
using LLVMSharp.Interop;
using System.Buffers;
using System.Diagnostics;
using System.CommandLine;
using CommonC.Error;

namespace CommonC.App
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            args = ["compile", "C:\\Users\\_King_\\source\\repos\\CommonC\\CommonC.App\\bin\\Debug\\net10.0\\win-x64\\Samples\\test.coc", "-o=C:\\Users\\_King_\\source\\repos\\CommonC\\CommonC.App\\bin\\Debug\\net10.0\\win-x64\\Samples\\tester.exe", "-t=webassembly-1-0-mvp"];

            Option<string> targetOption = new("--target", "-t")
            {
                Description = "The compiler target architecture",
                DefaultValueFactory = _ => "i686-pc-windows-msvc"
            };

            Option<FileInfo> outputOption = new("--output", "-o")
            {
                Description = "The path the compiler should output the file to"
            };

            Option<int> optimizationOption = new("--optimization", "-opt")
            {
                Description = "The optimization mode the compiler should use (0 = None, 1 = Basic, 2 = Moderate, 3 = Aggressive)"
            };

            Argument<FileInfo> inputFileArgument = new("source-file")
            {
                Description = "The .coc source file to compile."
            };
            inputFileArgument.AcceptExistingOnly();

            RootCommand rootCommand = new("Common C compiler");

            Command compileCommand = new("compile", "Compiles a source file to a specified architecture")
            {
                inputFileArgument
            };
            compileCommand.Options.Add(targetOption);
            compileCommand.Options.Add(outputOption);
            compileCommand.Options.Add(optimizationOption);
            rootCommand.Subcommands.Add(compileCommand);

            compileCommand.SetAction(parseResult =>
            {
                FileInfo inputFile = parseResult.GetValue(inputFileArgument)!;
                FileInfo? output = parseResult.GetValue(outputOption);
                string target = parseResult.GetValue(targetOption)!;
                int optimization = parseResult.GetValue(optimizationOption);

                if (output == null)
                {
                    throw ErrorHandler.CreateError("Output path is invalid. Provide an output path using --output or -o.");
                }

                CommonCCompilerSettings settings = new()
                {
                    MainFilePath = inputFile.FullName,
                    WorkingDirectory = inputFile.DirectoryName ?? throw ErrorHandler.CreateError("Working directory path is invalid."),
                    Target = target,
                    Name = inputFile.Name,
                    EntryPoint = "main",
                    Version = new Version(1, 0, 0, 0)
                };

                CommonCCompiler compiler = new(settings);
                compiler.Compile(out string statusMessage);

                Console.WriteLine(statusMessage);
            });

            return rootCommand.Parse(args).Invoke();
        }


        //static void CreateLLVM()
        //{
        //    string appName = "test";

        //    LLVMCommonCCompilerSettings settings = new LLVMCommonCCompilerSettings
        //    {
        //        MainFilePath = Environment.CurrentDirectory + "\\Samples\\test.coc",
        //        WorkingDirectory = Environment.CurrentDirectory + "\\Samples",
        //        // TargetTripe = "x86_64-pc-windows-msvc",
        //        Target = "i686-pc-windows-msvc",
        //        Name = appName,
        //        EntryPoint = "main",
        //        Version = new Version(1, 0, 0, 0)
        //    };

        //    settings.AddLibrary("gdi32");
        //    settings.AddLibrary("user32");
        //    settings.AddLibrary("advapi32");
        //    settings.AddLibrary("SDL3", Environment.CurrentDirectory + "\\lib");

        //    LLVMCommonCCompiler compiler = new LLVMCommonCCompiler(settings);
        //    LLVMModuleRef module = compiler.Compile(out string statusMessage);


        //    string moduleIR = string.Join(Environment.NewLine,
        //    module.ToString().Split('\n')
        //        .Select((line, index) => $"{index}: {line}"));


        //    Console.WriteLine($"LLVM IR\n=========\n{moduleIR}");

        //    if(string.IsNullOrEmpty(statusMessage))
        //    {
        //        File.WriteAllText($"{appName}.ll", module.ToString());
        //        StartApp($"{Environment.CurrentDirectory}\\{appName}");
        //    }
        //    else
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.WriteLine($"Module failed verification!\n{statusMessage}");
        //        Console.ForegroundColor = ConsoleColor.Gray;
        //    }
        //}

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
