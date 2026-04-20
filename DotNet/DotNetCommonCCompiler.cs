using AsmResolver.PE.File;
using CommonC.DotNet.CodeGen;
using CommonC.Lexer;
using CommonC.Lexer.Objects;
using CommonC.Parser;
using CommonC.Parser.AST.Statements;
using CommonC.Printer;
using CommonC.Semantic;
using Microsoft.NET.HostModel.AppHost;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CommonC.DotNet
{
    public class DotNetCommonCCompiler
    {
        DotNetCommonCCompilerSettings Settings { get; set; }

        public DotNetCommonCCompiler(DotNetCommonCCompilerSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        /// Imports use-files and compiles the MainFilePath
        /// </summary>
        /// <returns>PEFile</returns>
        /// <exception cref="FileNotFoundException">MainFilePath file was not found.</exception>
        public PEFile Compile()
        {
            if(File.Exists(Settings.MainFilePath))
            {
                StatementList statements = ParseText(File.ReadAllText(Settings.MainFilePath));
                statements = ImportUseFiles(statements);

                PrettyPrinter prettyPrinter = new PrettyPrinter(statements, PrettyPrinterSettings.Beautify);
                Console.WriteLine(prettyPrinter.Print());

                SemanticAnalyzer semanticAnalyzer = new SemanticAnalyzer();
                semanticAnalyzer.Analyze(statements);

                DotNetCodeGen dotNetCodeGen = new DotNetCodeGen(Settings.DotNetCodeGenSettings);

                return dotNetCodeGen.GeneratePEFile(statements);
            }

            throw new FileNotFoundException($"Main file {Settings.MainFilePath} does not exist");
        }

        /// <summary>
        /// Creates an app host for newer .NET versions. Also creates a runtime config for specified .NET version.
        /// </summary>
        public void CreateAppHost()
        {
            CreateRuntimeConfig();

            HostWriter.CreateAppHost(
                appHostSourceFilePath: "bin\\apphost.exe",
                appHostDestinationFilePath: $"{Settings.DotNetCodeGenSettings.Name}.exe",
                appBinaryFilePath: $"{Settings.DotNetCodeGenSettings.Name}.dll",
                assemblyToCopyResorcesFrom: $"{Settings.DotNetCodeGenSettings.Name}.dll",
                windowsGraphicalUserInterface: false
            );
        }

        void CreateRuntimeConfig()
        {
            var config = new
            {
                runtimeOptions = new
                {
                    tfm = $"net{Settings.DotNetCodeGenSettings.DotNetRuntimeInfo.Version.Major}.{Settings.DotNetCodeGenSettings.DotNetRuntimeInfo.Version.Minor}",
                    framework = new
                    {
                        name = "Microsoft.NETCore.App",
                        version = $"{Settings.DotNetCodeGenSettings.DotNetRuntimeInfo.Version.Major}.{Settings.DotNetCodeGenSettings.DotNetRuntimeInfo.Version.Minor}.{Settings.DotNetCodeGenSettings.DotNetRuntimeInfo.Version.Build}"
                    }
                }
            };
            string jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText($"{Settings.DotNetCodeGenSettings.Name}.runtimeconfig.json", jsonString);
        }

        StatementList ImportUseFiles(StatementList statements)
        {
            List<UseStatement> useStatements = statements.OfType<UseStatement>().ToList();

            for(int i = 0; i < useStatements.Count; i++)
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
