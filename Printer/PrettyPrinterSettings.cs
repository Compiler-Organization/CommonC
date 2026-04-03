using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Printer
{
    public class PrettyPrinterSettings
    {
        public required string Indentation { get; set; }
        public required string NewLine { get; set; }

        public static PrettyPrinterSettings Beautify = new PrettyPrinterSettings
        {
            Indentation = "\t",
            NewLine = Environment.NewLine
        };

        public static PrettyPrinterSettings Minify = new PrettyPrinterSettings
        {
            Indentation = "",
            NewLine = " "
        };
    }
}
