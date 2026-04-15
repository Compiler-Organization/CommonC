using CommonC.CodeGen.DotNet;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using System;
using System.Collections.Generic;
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

        public void Compile()
        {
            if(File.Exists(Settings.FilePath))
            {
                StatementList statements = ParseText(File.ReadAllText(Settings.FilePath));
                statements = ImportUseFiles(statements);

                PrettyPrinter prettyPrinter = new PrettyPrinter(statements, PrettyPrinterSettings.Beautify);
                Console.WriteLine(prettyPrinter.Print());

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer();
                semanticAnalyzer.Analyze(statements);

                DotNetCodeGen dotNetCodeGen = new DotNetCodeGen(Settings.DotNetCodeGenSettings);

                dotNetCodeGen.Generate(statements).Write("test.exe");
            }
            else
            {
                throw new FileNotFoundException($"Main file {Settings.FilePath} does not exist");
            }
        }

        StatementList ImportUseFiles(StatementList statements)
        {
            List<UseStatement> useStatements = statements.OfType<UseStatement>().ToList();

            Console.WriteLine("useStatements --- " + useStatements.Count);

            for(int i = 0; i < useStatements.Count; i++)
            {
                string filePath = Settings.WorkingDirectory + "\\" + useStatements[i].Identifier.Name + ".coc";
                if (File.Exists(filePath))
                {
                    Console.WriteLine("useStatements --- reading file " + filePath);
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
