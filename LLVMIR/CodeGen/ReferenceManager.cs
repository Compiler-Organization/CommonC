using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.LLVMIR.CodeGen
{
    internal class ReferenceManager
    {
        LLVMBuilderRef Builder { get; set; }

        public ReferenceManager(LLVMBuilderRef builder)
        {
            Builder = builder;
        }

        Dictionary<int, List<LLVMValueRef>> ScopeMallocs = new Dictionary<int, List<LLVMValueRef>>();

        int Scope = 0;

        public void EnterScope()
        {
            Scope++;
        }

        public void ExitScope()
        {
            FreeAll();
            Scope--;
        }

        public void AddMalloc(LLVMValueRef malloc)
        {
            if (!ScopeMallocs.ContainsKey(Scope))
            {
                ScopeMallocs[Scope] = new List<LLVMValueRef>();
            }
            ScopeMallocs[Scope].Add(malloc);
        }

        public void FreeAll()
        {
            if(ScopeMallocs.ContainsKey(Scope)) 
            {
                foreach (var malloc in ScopeMallocs[Scope])
                {
                    Builder.BuildFree(malloc);
                }
                ScopeMallocs.Remove(Scope);
            }
        }
    }
}
