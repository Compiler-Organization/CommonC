using CommonC.DotNet.CodeGen;
using CommonC.LLVMIR.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.LLVMIR
{
    public class LLVMIRCommonCCompilerSettings
    {

        /// <summary>
        /// The working directory when importing "use" files.
        /// </summary>
        public required string WorkingDirectory { get; set; }

        /// <summary>
        /// The main, working file path for the compilation process.
        /// </summary>
        public required string MainFilePath { get; set; }

        public LLVMIRCodeGenSettings LLVMIRCodeGenSettings { get; set; } = new LLVMIRCodeGenSettings
        {
            Name = "app",
            EntryPoint = "main",
            Version = new Version(1, 0, 0, 0),
        };
    }
}
