using CommonC.DotNet.CodeGen;
using CommonC.Targets.LLVM.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC
{
    public class CommonCCompilerSettings
    {

        /// <summary>
        /// The working directory when importing "use" files.
        /// </summary>
        public required string WorkingDirectory { get; set; }

        /// <summary>
        /// The main, working file path for the compilation process.
        /// </summary>
        public required string MainFilePath { get; set; }

        /// <summary>
        /// The compiler backend target
        /// </summary>
        public string Target { get; set; } = "";

        public Libraries Libraries = new Libraries();

        public void AddLibrary(Library library) => Libraries.Add(library);

        public void AddLibrary(string name, string? rootPath = null) => Libraries.Add(new Library { LibraryName = name, Root = rootPath });

        /// <summary>
        /// Name of the assembly to be generated, without the .dll extension (E.g: "MyAssembly")
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Version of the assembly.
        /// </summary>
        public Version Version { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// Determines the entry point of the application.
        /// </summary>
        public string EntryPoint { get; set; } = "main";

        /// <summary>
        /// What optimization mode the compiler should use.
        /// <para>0 = None</para>
        /// <para>1 = Basic</para>
        /// <para>2 = Moderate</para>
        /// <para>3 = Aggressive</para>
        /// <para></para>
        /// </summary>
        public int OptimizationMode { get; set; } = 0;
    }

    public class Library
    {
        /// <summary>
        /// The root path of the library
        /// E.g $"{Environment.CurrentDirectory}\\libs"
        /// </summary>
        public string? Root { get; set; }

        /// <summary>
        /// 'legacy_stdio_definitions', for example, without the .lib extension.
        /// </summary>
        public required string LibraryName { get; set; }
    }

    public class Libraries : List<Library>
    {
        public string CreateArguments() => string.Join(" ", this.Select(lib => (lib.Root == null ? "" : $"-L{lib.Root} ") + $"-l{lib.LibraryName}"));
    }
}
