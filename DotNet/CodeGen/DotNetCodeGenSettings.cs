using AsmResolver.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.DotNet.CodeGen
{
    public class DotNetCodeGenSettings
    {
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
        /// The target .NET runtime
        /// </summary>
        public required DotNetRuntimeInfo DotNetRuntimeInfo { get; set; }
    }
}
