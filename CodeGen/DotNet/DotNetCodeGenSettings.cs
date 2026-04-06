using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.CodeGen.DotNet
{
    public class DotNetCodeGenSettings
    {
        /// <summary>
        /// Name of the assembly to be generated, without the .dll extension (E.g: "MyAssembly")
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Version of the assembly
        /// </summary>
        public required Version Version { get; set; }
    }
}
