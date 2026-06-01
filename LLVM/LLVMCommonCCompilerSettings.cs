using CommonC.DotNet.CodeGen;
using CommonC.LLVM.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.LLVM
{
    public class LLVMCommonCCompilerSettings
    {

        /// <summary>
        /// The working directory when importing "use" files.
        /// </summary>
        public required string WorkingDirectory { get; set; }

        /// <summary>
        /// The main, working file path for the compilation process.
        /// </summary>
        public required string MainFilePath { get; set; }

        public LLVMCodeGenSettings LLVMCodeGenSettings { get; set; } = new LLVMCodeGenSettings
        {
            Name = "app",
            EntryPoint = "main",
            Version = new Version(1, 0, 0, 0),
        };
    }
}
