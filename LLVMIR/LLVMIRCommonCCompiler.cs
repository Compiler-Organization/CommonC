using CommonC.Lexer;
using CommonC.Lexer.Objects;
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

        public LLVMModuleRef Compile()
        {
            if (File.Exists(Settings.MainFilePath))
            {
                StatementList statements = ParseText(File.ReadAllText(Settings.MainFilePath));
                statements = ImportUseFiles(statements);

                PrettyPrinter prettyPrinter = new PrettyPrinter(statements, PrettyPrinterSettings.Beautify);
                Console.WriteLine(prettyPrinter.Print());

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer();
                semanticAnalyzer.Analyze(statements);

                LLVMIRCodeGen lLVMIRCodeGen = new LLVMIRCodeGen(Settings.LLVMIRCodeGenSettings, statements);
                LLVMModuleRef module = lLVMIRCodeGen.GenerateLLVMModule();

                try
                {
                    module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Module failed verification! {ex.Message}");
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

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        StatementList ImportUseFiles(StatementList statements)
        {
            List<UseStatement> useStatements = statements.OfType<UseStatement>().ToList();

            for (int i = 0; i < useStatements.Count; i++)
            {
                string filePath = Settings.WorkingDirectory + "\\" + useStatements[i].Identifier.Name + ".coc";
                if (File.Exists(filePath))
                {
                    statements.InsertRange(0, ParseText(File.ReadAllText(filePath)));
                }
                else
                {
                    throw new FileNotFoundException($"File {filePath} does not exist.");
                }
            }

            statements.RemoveAll(s => s is UseStatement);
            return statements;
        }

        StatementList ParseText(string code)
        {
            LexicalAnalyser lexicalAnalyser = new LexicalAnalyser(code);
            LexTokenList lexTokens = lexicalAnalyser.Analyze();

            SyntaxParser parser = new SyntaxParser(lexTokens);

            return parser.ParseLexTokenList();
        }
    }
}
