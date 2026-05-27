using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Liveness;
using CommonC.LLVMIR.CodeGen;
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

namespace CommonC.LLVMIR
{
    public class LLVMIRCommonCCompiler
    {
        LLVMIRCommonCCompilerSettings Settings { get; set; }

        public LLVMIRCommonCCompiler(LLVMIRCommonCCompilerSettings settings)
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
            return executionEngine.RunFunction(module.GetNamedFunction(Settings.LLVMIRCodeGenSettings.EntryPoint), args);
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

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer(closure);
                semanticAnalyzer.Analyze();

                LivenessAnalyser livenessAnalyser = new LivenessAnalyser(closure);
                livenessAnalyser.Analyse();

                PrettyPrinter prettyPrinter = new PrettyPrinter(closure.Statements, PrettyPrinterSettings.Beautify);
                Console.WriteLine(prettyPrinter.Print());

                LLVMIRCodeGen lLVMIRCodeGen = new LLVMIRCodeGen(Settings.LLVMIRCodeGenSettings, closure);
                return lLVMIRCodeGen.GenerateLLVMModule();
            }

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        /// <summary>
        /// Compiles the application to a .exe
        /// </summary>
        /// <returns></returns>
        public LLVMModuleRef Compile()
        {
            LLVMModuleRef module = BuildLLVMModule();

            try
            {
                module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Module failed verification!\n{ex.Message}");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            File.WriteAllText($"{Settings.LLVMIRCodeGenSettings.Name}.ll", module.ToString());

            ProcessStartInfo clang = new ProcessStartInfo()
            {
                FileName = @".\\Llvm\\bin\\clang.exe",
                Arguments = $"\"{Environment.CurrentDirectory}\\{Settings.LLVMIRCodeGenSettings.Name}.ll\" -L{Environment.CurrentDirectory}\\libs -llegacy_stdio_definitions -O3 -o \"{Environment.CurrentDirectory}\\{Settings.LLVMIRCodeGenSettings.Name}.exe\"",
            };
            Process.Start(clang).WaitForExit();

            return module;
        }

        ClosureStatement ImportUseFiles(ClosureStatement closure)
        {
            List<UseStatement> useStatements = closure.Statements.OfType<UseStatement>().ToList();

            for (int i = 0; i < useStatements.Count; i++)
            {
                string filePath = Settings.WorkingDirectory + "\\" + useStatements[i].Identifier.Name + ".coc";
                if (File.Exists(filePath))
                {
                    closure.Statements.InsertRange(0, ParseText(File.ReadAllText(filePath)).Statements);
                }
                else
                {
                    throw new FileNotFoundException($"File {filePath} does not exist.");
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
