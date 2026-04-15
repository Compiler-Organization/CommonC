using CommonC.CodeGen.DotNet;
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
        public required string FilePath { get; set; }

        public DotNetCodeGenSettings DotNetCodeGenSettings { get; set; } = new DotNetCodeGenSettings
        {
            Name = "app",
            Version = new Version(1, 0, 0, 0)
        };
    }
}
