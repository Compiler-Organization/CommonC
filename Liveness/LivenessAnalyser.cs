using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.Liveness
{
    /*
    The idea for this will be an analyser that goes through every read / write to a variable / what would be memory, then insert a free at the last read/write to the memory location.
    * Variables that returns with a functions needs to be preserved.
    * Need to take into account variables can be assigned conditionally. E.g "if var == true, bigVar = ...".

    */
    public class LivenessAnalyser
    {
        StatementList Statements { get; set; }

        public LivenessAnalyser(StatementList statements)
        {
            Statements = statements;
        }


    }
}
