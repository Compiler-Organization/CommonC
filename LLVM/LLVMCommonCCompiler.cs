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
                ClosureStatement closure = ParseText(File.ReadAllText(Settings.MainFilePath));
                closure = ImportUseFiles(closure);

                PrettyPrinter prettyPrinter = new PrettyPrinter(closure.Statements, PrettyPrinterSettings.Beautify);
                Console.WriteLine(prettyPrinter.Print());

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer(closure);
                semanticAnalyzer.Analyze();

                // LivenessAnalyser livenessAnalyser = new LivenessAnalyser(closure);
                // livenessAnalyser.Analyse();


                LLVMCodeGen lLVMCodeGen = new LLVMCodeGen(Settings.LLVMCodeGenSettings, closure);
                return lLVMCodeGen.GenerateLLVMModule();
            }

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        /// <summary>
        /// Compiles the application to a .exe
        /// </summary>
        /// <returns></returns>
        public LLVMModuleRef Compile(out bool success)
        {
            LLVMModuleRef module = BuildLLVMModule();
            module.Target = Settings.TargetTripe;

            if(!module.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out string message))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Module failed verification!\n{message}");
                Console.ForegroundColor = ConsoleColor.Gray;
                success = false;
            }
            else
            {
                File.WriteAllText($"{Settings.LLVMCodeGenSettings.Name}.ll", module.ToString());

                ProcessStartInfo clang = new ProcessStartInfo()
                {
                    FileName = @".\\Llvm\\bin\\clang.exe",
                    Arguments = $"\"{Environment.CurrentDirectory}\\{Settings.LLVMCodeGenSettings.Name}.ll\" -O3 -o \"{Environment.CurrentDirectory}\\{Settings.LLVMCodeGenSettings.Name}.exe\"",
                };
                Process.Start(clang).WaitForExit();
                success = true;
            }

            return module;
        }

        ClosureStatement ImportUseFiles(ClosureStatement closure)
        {
            HashSet<string> importedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                List<UseStatement> pendingUses = closure.Statements.OfType<UseStatement>().ToList();

                if (pendingUses.Count == 0)
                {
                    break;
                }

                foreach (UseStatement useStmt in pendingUses)
                {
                    string fileName = useStmt.Identifier.Name;
                    string filePath = Path.Combine(Settings.WorkingDirectory, $"{fileName}.coc");

                    if (importedFiles.Contains(filePath))
                    {
                        closure.Statements.Remove(useStmt);
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        string fileContent = File.ReadAllText(filePath);
                        ClosureStatement importedAST = ParseText(fileContent);

                        importedFiles.Add(filePath);
                        closure.Statements.Remove(useStmt);
                        closure.Statements.InsertRange(0, importedAST.Statements);
                    }
                    else
                    {
                        throw new FileNotFoundException($"File {filePath} does not exist.");
                    }
                }
            }

            closure.Statements.RemoveAll(s => s is UseStatement);
            return closure;
        }


        ClosureStatement ParseText(string code)
        {
            LexicalAnalyser lexicalAnalyser = new LexicalAnalyser(code);
            LexTokenList lexTokens = lexicalAnalyser.Analyze();

            SyntaxParser parser = new SyntaxParser(lexTokens);

            return parser.ParseLexTokenList();
        }
    }
}
