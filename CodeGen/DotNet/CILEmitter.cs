using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonC.CodeGen.DotNet
{
    public class CILEmitter
    {
        public void EmitLdc_I4(int value, CilMethodBody body)
        {
            switch (value)
            {
                case -1: body.Instructions.Add(CilOpCodes.Ldc_I4_M1); return;
                case 0: body.Instructions.Add(CilOpCodes.Ldc_I4_0); return;
                case 1: body.Instructions.Add(CilOpCodes.Ldc_I4_1); return;
                case 2: body.Instructions.Add(CilOpCodes.Ldc_I4_2); return;
                case 3: body.Instructions.Add(CilOpCodes.Ldc_I4_3); return;
                case 4: body.Instructions.Add(CilOpCodes.Ldc_I4_4); return;
                case 5: body.Instructions.Add(CilOpCodes.Ldc_I4_5); return;
                case 6: body.Instructions.Add(CilOpCodes.Ldc_I4_6); return;
                case 7: body.Instructions.Add(CilOpCodes.Ldc_I4_7); return;
                case 8: body.Instructions.Add(CilOpCodes.Ldc_I4_8); return;
            }

            
            if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldc_I4_S, (sbyte)value);
                return;
            }

            if (value >= int.MinValue && value <= int.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldc_I4, (int)value);
                return;
            }


        }
        
        public void EmitLdc_R8(double value, CilMethodBody body)
        {
            if (value >= double.MinValue && value <= double.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldc_R8, value);
                return;
            }
        }

        public void EmitLdc_I8(double value, CilMethodBody body)
        {
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldc_I8, value);
                return;
            }
        }

        public void EmitLdarg(int index, CilMethodBody body)
        {
            switch (index)
            {
                case 0:
                    body.Instructions.Add(CilOpCodes.Ldarg_0);
                    return;
                case 1:
                    body.Instructions.Add(CilOpCodes.Ldarg_1);
                    return;
                case 2:
                    body.Instructions.Add(CilOpCodes.Ldarg_2);
                    return;
                case 3:
                    body.Instructions.Add(CilOpCodes.Ldarg_3);
                    return;
            }

            if (index >= 0 && index <= byte.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldarg_S, index);
                return;
            }

            if (index >= 0 && index <= ushort.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldarg, index);
                return;
            }

            throw new Exception($"Argument at {index} is too big, max amount of arguments is {ushort.MaxValue}");
        }

        public void EmitStelem(ITypeDescriptor type, CilMethodBody body)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.SByte":
                case "System.Byte":
                    body.Instructions.Add(CilOpCodes.Stelem_I1);
                    return;
                case "System.Int16":
                case "System.UInt16":
                    body.Instructions.Add(CilOpCodes.Stelem_I2);
                    return;
                case "System.Int32":
                case "System.UInt32":
                    body.Instructions.Add(CilOpCodes.Stelem_I4);
                    return;
                case "System.Int64":
                case "System.UInt64":
                    body.Instructions.Add(CilOpCodes.Stelem_I8);
                    return;
                case "System.Single":
                    body.Instructions.Add(CilOpCodes.Stelem_R4);
                    return;
                case "System.Double":
                    body.Instructions.Add(CilOpCodes.Stelem_R8);
                    return;
                case "System.IntPtr":
                case "System.UIntPtr":
                    body.Instructions.Add(CilOpCodes.Stelem_I);
                    return;
                default:
                    body.Instructions.Add(CilOpCodes.Stelem_Ref);
                    return;
            }
        }

        public void EmitLdelem(TypeReference type, CilMethodBody body)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.SByte":
                case "System.Byte":
                    body.Instructions.Add(CilOpCodes.Ldelem_I1);
                    return;
                case "System.Int16":
                case "System.UInt16":
                    body.Instructions.Add(CilOpCodes.Ldelem_I2);
                    return;
                case "System.Int32":
                case "System.UInt32":
                    body.Instructions.Add(CilOpCodes.Ldelem_I4);
                    return;
                case "System.Int64":
                case "System.UInt64":
                    body.Instructions.Add(CilOpCodes.Ldelem_I8);
                    return;
                case "System.Single":
                    body.Instructions.Add(CilOpCodes.Ldelem_R4);
                    return;
                case "System.Double":
                    body.Instructions.Add(CilOpCodes.Ldelem_R8);
                    return;
                case "System.IntPtr":
                case "System.UIntPtr":
                    body.Instructions.Add(CilOpCodes.Ldelem_I);
                    return;
                default:
                    body.Instructions.Add(CilOpCodes.Ldelem_Ref);
                    return;
            }
        }
    }
}
