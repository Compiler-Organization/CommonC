using AsmResolver.DotNet;
using CommonC.DotNet.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.DotNet
{
    public class DotNetCommonCCompilerSettings
    {
        /// <summary>
        /// The working directory when importing "use" files.
        /// </summary>
        public required string WorkingDirectory { get; set; }

        /// <summary>
        /// The main, working file path for the compilation process.
        /// </summary>
        public required string MainFilePath { get; set; }

        public DotNetCodeGenSettings DotNetCodeGenSettings { get; set; } = new DotNetCodeGenSettings
        {
            Name = "app",
            Version = new Version(1, 0, 0, 0),
            DotNetRuntimeInfo = DotNetRuntimeInfo.NetFramework(4, 0)
        };
    }
}
