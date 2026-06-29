using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Liveness;
using CommonC.LLVM.CodeGen;
using CommonC.Optimizer;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CommonC.LLVM
{
    public class LLVMCommonCCompiler
    {
        LLVMCommonCCompilerSettings Settings { get; set; }

        public LLVMCommonCCompiler(LLVMCommonCCompilerSettings settings)
        {
            Settings = settings;
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
            return executionEngine.RunFunction(module.GetNamedFunction(Settings.LLVMCodeGenSettings.EntryPoint), args);
        }

        /// <summary>
        /// Builds a LLVM module
        /// </summary>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public LLVMModuleRef BuildLLVMModule()
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

                // LivenessAnalyser livenessAnalyser = new LivenessAnalyser(closure);
                // livenessAnalyser.Analyse();


                //LLVMCodeGen2 lLVMCodeGen = new LLVMCodeGen2(Settings.LLVMCodeGenSettings);
                //return lLVMCodeGen.CreateModule(closure);

                LLVMCodeGen lLVMCodeGen = new LLVMCodeGen(Settings.LLVMCodeGenSettings, closure);
                return lLVMCodeGen.GenerateLLVMModule();
            }

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        /// <summary>
        /// Compiles the application to a .exe
        /// </summary>
        /// <returns></returns>
        public LLVMModuleRef Compile(out string statusMessage)
        {
            LLVMModuleRef module = BuildLLVMModule();
            module.Target = Settings.TargetTripe;

            if(!module.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out string message))
            {
                statusMessage = message;
            }
            else
            {
                File.WriteAllText($"{Settings.LLVMCodeGenSettings.Name}.ll", module.ToString());

                ProcessStartInfo clang = new ProcessStartInfo()
                {
                    FileName = @".\\Llvm\\bin\\clang.exe",
                    Arguments = $"\"{Environment.CurrentDirectory}\\{Settings.LLVMCodeGenSettings.Name}.ll\" {Settings.Libraries.CreateArguments()} --target=\"{Settings.TargetTripe}\" -O3 -o \"{Environment.CurrentDirectory}\\{Settings.LLVMCodeGenSettings.Name}.exe\"",
                };

                Process.Start(clang).WaitForExit();
                statusMessage = message;
            }

            return module;
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
