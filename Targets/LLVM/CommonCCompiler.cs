using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Liveness;
using CommonC.Optimizer;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using CommonC.Targets.CommonIR.CodeGen;
using CommonC.Targets.LLVM.CodeGen;
using CommonIR;
using CommonIR.Generators;
using CommonIR.Passes.Optimization;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CommonC
{
    public class CommonCCompiler
    {
        CommonCCompilerSettings Settings { get; set; }

        public CommonCCompiler(CommonCCompilerSettings settings)
        {
            Settings = settings;
        }

        public bool Compile(out string statusMessage)
        {
            if (File.Exists(Settings.MainFilePath))
            {
                Console.WriteLine("Parsing main file..");
                ClosureStatement closure = ParseText(File.ReadAllText(Settings.MainFilePath), Path.GetFileName(Settings.MainFilePath));
                Console.WriteLine("Importing files..");
                closure = ImportUseFiles(closure);

                Console.WriteLine("Statements " + closure.Statements.PrettyPrint(0));

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer(closure);
                semanticAnalyzer.Analyze();

                switch (Settings.Target)
                {
                    case "webassembly-1-0-mvp":
                        {
                            return CompileCommonIR(closure, out statusMessage);
                        }

                    default:
                        {
                            return CompileLLVM(closure, out statusMessage);
                        }
                }
            }

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        bool CompileCommonIR(ClosureStatement closure, out string statusMessage)
        {
            CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
            {
                Target = CommonIRTargets.WebAssembly_1_0_MVP,
                OptimizingMode = (OptimizingMode)Settings.OptimizationMode,
            };
            CommonIRCodeGen codeGen = new CommonIRCodeGen(closure, codeGenSettings, this.Settings);

            List<SourceFile> sourceFiles = codeGen.GenerateSourceFiles();

            foreach (SourceFile sourceFile in sourceFiles)
            {
                string filename = $"{sourceFile.Name}{sourceFile.Extension}";
                sourceFile.WriteToDisk();
            }

            statusMessage = "Success";
            return true;
        }

        /// <summary>
        /// Compiles the application to a .exe
        /// </summary>
        /// <returns></returns>
        bool CompileLLVM(ClosureStatement closure, out string statusMessage)
        {
            LLVMCodeGen lLVMCodeGen = new LLVMCodeGen(Settings, closure);
            LLVMModuleRef module = lLVMCodeGen.GenerateLLVMModule();

            module.Target = Settings.Target;

            if (!module.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out string message))
            {
                statusMessage = message;
            }
            else
            {
                File.WriteAllText($"{Settings.Name}.ll", module.ToString());

                ProcessStartInfo clang = new ProcessStartInfo()
                {
                    FileName = @".\\Llvm\\bin\\clang.exe",
                    Arguments = $"\"{Environment.CurrentDirectory}\\{Settings.Name}.ll\" {Settings.Libraries.CreateArguments()} --target=\"{Settings.Target}\" -O{Settings.OptimizationMode} -o \"{Environment.CurrentDirectory}\\{Settings.Name}.exe\"",
                };

                Process.Start(clang).WaitForExit();
                statusMessage = message;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Runs the given LLVM module with the provided arguments into the entry point function, which is specified in the code gen settings.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public LLVMGenericValueRef RunModule(LLVMModuleRef module, LLVMGenericValueRef[] args)
        {
            LLVMExecutionEngineRef executionEngine = module.CreateExecutionEngine();
            return executionEngine.RunFunction(module.GetNamedFunction(Settings.EntryPoint), args);
        }

        // Thread-safe path comparison for cross-platform robustness
        private static readonly StringComparer PathComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public ClosureStatement ImportUseFiles(ClosureStatement rootClosure)
        {
            if (rootClosure == null)
                throw new ArgumentNullException(nameof(rootClosure));

            var processedFiles = new HashSet<string>(PathComparer);
            var activeStack = new HashSet<string>(PathComparer);

            var flattenedStatements = new StatementList();

            ResolveClosure(rootClosure, processedFiles, activeStack, flattenedStatements);

            return new ClosureStatement(flattenedStatements);
        }

        private void ResolveClosure(
            ClosureStatement closure,
            HashSet<string> processedFiles,
            HashSet<string> activeStack,
            StatementList resultList)
        {
            foreach (var statement in closure.Statements)
            {
                if (statement is UseStatement useStmt)
                {
                    ResolveUseStatement(useStmt, processedFiles, activeStack, resultList);
                }
                else
                {
                    resultList.Add(statement);
                }
            }
        }

        private void ResolveUseStatement(
            UseStatement useStmt,
            HashSet<string> processedFiles,
            HashSet<string> activeStack,
            StatementList resultList)
        {
            if (useStmt?.Identifier?.Name == null)
            {
                throw new InvalidDataException("MalFormed AST: Use statement missing a valid identifier.");
            }

            string fileName = $"{useStmt.Identifier.Name}.coc";
            string fullPath = Path.GetFullPath(Path.Combine(Settings.WorkingDirectory, fileName));

            if (activeStack.Contains(fullPath))
            {
                throw new InvalidOperationException($"Circular dependency detected: {string.Join(" -> ", activeStack)} -> {fullPath}");
            }

            if (processedFiles.Contains(fullPath))
            {
                return;
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Import target file not found: '{fullPath}'", fullPath);
            }

            activeStack.Add(fullPath);
            processedFiles.Add(fullPath);

            try
            {
                string sourceCode = File.ReadAllText(fullPath);

                ClosureStatement importedAST = ParseText(sourceCode, fileName);
                Console.WriteLine($"{fileName} parsed!");

                if (importedAST?.Statements != null)
                {
                    ResolveClosure(importedAST, processedFiles, activeStack, resultList);
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
            {
                throw new FormatException($"Failed parsing imported file '{fullPath}'. See inner exception.", ex);
            }
            finally
            {
                activeStack.Remove(fullPath);
            }
        }

        private ClosureStatement ParseText(string code, string fileName)
        {
            Console.WriteLine($"Lexing {fileName}..");
            var lexicalAnalyser = new LexicalAnalyser(code);
            var lexTokens = lexicalAnalyser.Analyze();
            Console.WriteLine($"Parsing {fileName}..");
            var parser = new SyntaxParser(lexTokens, fileName);

            return parser.ParseLexTokenList();
        }
    }
}
