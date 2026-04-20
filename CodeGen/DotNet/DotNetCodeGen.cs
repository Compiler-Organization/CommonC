using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE;
using AsmResolver.PE.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.File;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

/*
TODO:
    CodeGen:
    * Change so branching doesnt create a nop instruction
    * Support arrays without values
    * Use ArrayPool instead of standard arrays.
    
    * Fix parser precedence so it handles n % i == 0 as (n % i) == 0 properly.
 */

namespace CommonC.CodeGen.DotNet
{
    public class DotNetCodeGen
    {
        DotNetCodeGenSettings Settings { get; set; }

        ModuleDefinition Module { get; set; } = null!;

        RuntimeContext Runtime { get; set; } = null!;

        CILEmitter Emitter { get; set; }

        public DotNetCodeGen(DotNetCodeGenSettings settings)
        {
            Settings = settings;
            Emitter = new CILEmitter();
        }

        ITypeDefOrRef ValueTypeReference { get; set; }

        CorLibTypeSignature ResolveReservedCorLibType(ReservedTypes type)
        {
            switch (type)
            {
                case ReservedTypes.String:
                    return Module.CorLibTypeFactory.String;
                case ReservedTypes.I32:
                    return Module.CorLibTypeFactory.Int32;
                case ReservedTypes.F64:
                    return Module.CorLibTypeFactory.Double;
                case ReservedTypes.I64:
                    return Module.CorLibTypeFactory.Int64;
                case ReservedTypes.Bool:
                    return Module.CorLibTypeFactory.Boolean;
                case ReservedTypes.Fn:
                    return Module.CorLibTypeFactory.Void;

                default:
                    throw new Exception($"Type '{type}' does not exist and could not be resolved.");
            }
        }

        TypeSignature ResolveCorLibType(Expression expression)
        {
            if (expression is TypeExpression typeExpression)
            {
                return ResolveReservedCorLibType(typeExpression.Type);
            }

            if(expression is IdentifierExpression identifierExpression)
            {
                List<TypeDefinition> types = Module.TopLevelTypes.First().NestedTypes.Where(t => t.Name == identifierExpression.Name).ToList();
                if (types.Any())
                {
                    return types.First().ToTypeSignature(types.First().Attributes.HasFlag(TypeAttributes.BeforeFieldInit));
                }
                else
                {
                    throw new Exception($"Unknown type expression '{identifierExpression.Name}' could not be resolved.");
                }
            }

            throw new Exception($"Unknown type expression '{expression}' could not be resolved.");
        }

        TypeSignature ResolveTypeSignature(Expression typeExpression, Expression valueExpression, List<VariableDeclarationStatement> locals)
        {
            bool isValueType = Module.TopLevelTypes.First().NestedTypes.Any(t => typeExpression is IdentifierExpression id && t.Name == id.Name && t.Attributes.HasFlag(TypeAttributes.BeforeFieldInit));

            ITypeDefOrRef? resolvedType = ResolveType(typeExpression, locals);

            if(valueExpression is ArrayInitializerExpression arrayInitializerExpression)
            {
                return resolvedType.ToTypeSignature(isValueType).MakeSzArrayType();
            }

            return resolvedType.ToTypeSignature(isValueType);
        }

        ITypeDefOrRef ResolveType(Expression expression, List<VariableDeclarationStatement> locals, TypeDefinition? parentType = null)
        {
            if(parentType == null)
            {
                parentType = Module.TopLevelTypes.First();
            }

            if (expression is TypeExpression typeExpression)
            {
                switch (typeExpression.Type)
                {
                    case ReservedTypes.String:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "String");

                    // Byte
                    case ReservedTypes.I8:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "SByte");
                    case ReservedTypes.U8:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Byte");

                    // Short
                    case ReservedTypes.I16:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int16");
                    case ReservedTypes.U16:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "UInt16");

                    // integer
                    case ReservedTypes.I32:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int32");
                    case ReservedTypes.U32:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "UInt32");

                    // long
                    case ReservedTypes.I64:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int64");
                    case ReservedTypes.U64:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "UInt64");

                    case ReservedTypes.I128:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int128");
                    case ReservedTypes.U128:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "UInt128");

                    // float
                    case ReservedTypes.F32:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Single");

                    // double
                    case ReservedTypes.F64:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Double");

                    case ReservedTypes.Char:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Char");

                    case ReservedTypes.Bool:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Boolean");
                    default:
                        throw new Exception($"Type '{typeExpression.Type}' does not exist and could not be resolved.");
                }
            }

            if (expression is StringExpression stringExpression)
            {
                return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "String");
            }

            if (expression is NumberExpression numberExpression)
            {
                return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int32");
            }

            if (expression is BooleanExpression boolExpression)
            {
                return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Boolean");
            }

            if (expression is IdentifierExpression identifierExpression)
            {
                // TODO: 
                if(identifierExpression.Name == "rand") 
                {
                    return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int32");
                }

                List<VariableDeclarationStatement> existingLocals = locals.Where(l => l.Name == identifierExpression.Name).ToList();
                if (existingLocals.Any())
                {
                    return ResolveType(existingLocals.FirstOrDefault()!.Type, locals);
                }

                List<TypeDefinition> types = parentType.NestedTypes.Where(t => t.Name == identifierExpression.Name).ToList();
                if (types.Any())
                {
                    return types.First();
                }

                List<FieldDefinition> fields = parentType.Fields.Where(f => f.Name == identifierExpression.Name).ToList();
                if (fields.Any())
                {
                    return fields.First().Signature!.FieldType.ToTypeDefOrRef();
                }

                List<MethodDefinition> methods = parentType.Methods.Where(f => f.Name == identifierExpression.Name).ToList();
                if (methods.Any())
                {
                    return methods.First().Signature!.ReturnType.ToTypeDefOrRef();
                }

                throw new Exception($"Exception occured when resolving type: Local, type or field '{identifierExpression.Name}' does not exist in the current scope.");
            }

            if(expression is IndexExpression indexExpression)
            {
                return ResolveType(indexExpression.Expression, locals);
            }

            if(expression is ArrayExpression arrayExpression)
            {
                if(arrayExpression.Expressions == null || arrayExpression.Expressions.Count <= 0)
                {
                    throw new Exception("Cannot resolve type of empty arrays");
                }

                return ResolveType(arrayExpression.Expressions.FirstOrDefault()!, locals);
            }
            if(expression is MemberExpression memberExpression)
            {
                if(memberExpression.Member is MemberExpression)
                {
                    throw new NotSupportedException("Cannot resolve nested member expressions");
                }

                ResolveType(memberExpression.Parent, locals).Resolve(Runtime, out TypeDefinition parentTypeDef);

                return ResolveType(memberExpression.Member, locals, parentTypeDef);
            }
            if(expression is CallExpression callExpression)
            {
                return ResolveType(callExpression.Expression, locals);
            }
            if(expression is ArithmeticExpression arithmeticExpression)
            {
                return ResolveType(arithmeticExpression.Left, locals);
            }

            throw new Exception($"Type {expression.GetType().FullName} cannot be resolved.");
        }

        public ModuleDefinition GenerateModule(StatementList statements)
        {
            Module = new ModuleDefinition(Settings.Name, DotNetRuntimeInfo.NetFramework(4, 0));

            AssemblyDefinition assembly = new AssemblyDefinition(Settings.Name, Settings.Version);
            assembly.Modules.Add(Module);

            Runtime = new RuntimeContext(DotNetRuntimeInfo.NetFramework(4, 0));
            Runtime.AddAssembly(assembly);

            ValueTypeReference = Module.CorLibTypeFactory.CorLibScope
                .CreateTypeReference("System", "ValueType");

            Console.WriteLine(string.Join(", ", statements));

            // Prepare constructor
            MethodDefinition constructor = Module.TopLevelTypes.First().GetOrCreateStaticConstructor();

            if (constructor.CilMethodBody!.Instructions.First().OpCode == CilOpCodes.Ret)
            {
                constructor.CilMethodBody.Instructions.RemoveAt(0);
            }

            GenerateRandFunction();

            GenerateStructStatements(statements);

            GenerateFieldReferences(statements);
            GenerateFieldDeclarations(statements);

            GenerateFunctionReferences(statements);
            GenerateFunctionDeclarations(statements);

            // Close constructor
            constructor.CilMethodBody.Instructions.Add(CilOpCodes.Ret);

            foreach (var item in Module.AssemblyReferences)
            {
                Console.WriteLine(item);
            }

            foreach (TypeDefinition type in Module.TopLevelTypes.First().NestedTypes)
            {
                Console.WriteLine("Type " + type.FullName);
                foreach (FieldDefinition field in type.Fields)
                {
                    Console.WriteLine("\t" + field.FullName);
                }
                Console.WriteLine();
            }

            foreach (MethodDefinition method in Module.TopLevelTypes.First().Methods)
            {
                if (method.CilMethodBody == null)
                {
                    continue;
                }

                Console.WriteLine(method.FullName);

                foreach (CilLocalVariable local in method.CilMethodBody.LocalVariables)
                {
                    Console.WriteLine("\t" + local.Index + ": " + local.VariableType.FullName);
                }

                foreach (CilInstruction instruction in method.CilMethodBody.Instructions)
                {
                    Console.WriteLine(instruction);
                }
                Console.WriteLine();
            }

            return Module;
        }

        public PEFile GeneratePEFile(StatementList statements)
        {
            ModuleDefinition module = GenerateModule(statements);

            ManagedPEImageBuilder builder = new ManagedPEImageBuilder();

            return module.ToPEImage(builder)
                .ToPEFile(new ManagedPEFileBuilder());
        }

        void GenerateFieldReferences(StatementList statements)
        {
            foreach (VariableDeclarationStatement variableDeclarationStatement in statements.OfType<VariableDeclarationStatement>())
            {
                FieldDefinition fieldDefinition = new FieldDefinition(variableDeclarationStatement.Name, FieldAttributes.Public | FieldAttributes.Static, new FieldSignature(ResolveTypeSignature(variableDeclarationStatement.Type, variableDeclarationStatement.Expression, new List<VariableDeclarationStatement>())));

                variableDeclarationStatement.IsField = true;
                variableDeclarationStatement.Field = fieldDefinition;

                Console.WriteLine("Adding reference to field " + variableDeclarationStatement.Name);

                Module.TopLevelTypes.First().Fields.Add(fieldDefinition);
            }
        }

        void GenerateFieldDeclarations(StatementList statements)
        {
            MethodDefinition constructor = Module.TopLevelTypes.First().GetOrCreateStaticConstructor();

            foreach (VariableDeclarationStatement variableDeclarationStatement in statements.OfType<VariableDeclarationStatement>())
            {
                if(variableDeclarationStatement.Expression != null)
                {
                    GenerateExpression(variableDeclarationStatement.Expression, statements.OfType<VariableDeclarationStatement>().ToList(), constructor.CilMethodBody);
                    constructor.CilMethodBody.Instructions.Add(CilOpCodes.Stsfld, variableDeclarationStatement.Field);
                }
            }
        }

        void GenerateRandFunction()
        {
            MethodDefinition randFunction = new MethodDefinition("rand", MethodAttributes.Public | MethodAttributes.Static, MethodSignature.CreateStatic(Module.CorLibTypeFactory.Int32,
                [
                    Module.CorLibTypeFactory.Int32,
                    Module.CorLibTypeFactory.Int32
                ]))
            {
                CilMethodBody = new CilMethodBody()
            };

            var randomTypeRef = Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Random");

            var ctorRef = randomTypeRef.CreateMemberReference(
                ".ctor",
                MethodSignature.CreateInstance(Module.CorLibTypeFactory.Void)
            );

            MethodDefinition appConstructor = Module.TopLevelTypes.First().GetOrCreateStaticConstructor();

            appConstructor.CilMethodBody.Instructions.Add(CilOpCodes.Newobj, ctorRef);

            FieldDefinition randomInstanceField = new FieldDefinition("randomInstance", FieldAttributes.Public | FieldAttributes.Static, randomTypeRef.ToTypeSignature(false));
            Module.TopLevelTypes.First().Fields.Add(randomInstanceField);

            appConstructor.CilMethodBody.Instructions.Add(CilOpCodes.Stsfld, randomInstanceField);



            randFunction.CilMethodBody.Instructions.Add(CilOpCodes.Ldsfld, randomInstanceField);

            randFunction.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_0);
            randFunction.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_1);

            var nextMethod = randomTypeRef.CreateMemberReference(
                "Next",
                MethodSignature.CreateInstance(
                    Module.CorLibTypeFactory.Int32,
                    new TypeSignature[] { Module.CorLibTypeFactory.Int32, Module.CorLibTypeFactory.Int32 }
                )
            );

            randFunction.CilMethodBody.Instructions.Add(CilOpCodes.Callvirt, nextMethod);
            randFunction.CilMethodBody.Instructions.Add(CilOpCodes.Ret);

            Module.TopLevelTypes.First().Methods.Add(randFunction);
        }

        void GenerateFunctionReferences(StatementList statements)
        {
            foreach (FunctionDeclarationStatement functionDeclaration in statements.OfType<FunctionDeclarationStatement>())
            {
                var returnType = ResolveCorLibType(functionDeclaration.ReturnType);
                var parameters = new List<TypeSignature>();

                if (functionDeclaration.Parameters != null)
                {
                    foreach (var parameter in functionDeclaration.Parameters)
                    {
                        parameters.Add(ResolveCorLibType(parameter.Type));
                    }
                }

                MethodDefinition function = new MethodDefinition(functionDeclaration.Name, MethodAttributes.Public | MethodAttributes.Static, MethodSignature.CreateStatic(returnType, parameters));
                functionDeclaration.Method = function;
                Module.TopLevelTypes.First().Methods.Add(function);

                if (functionDeclaration.Name == Settings.EntryPoint)
                {
                    Module.ManagedEntryPointMethod = function;
                }
            }
        }

        void GenerateFunctionDeclarations(StatementList statements)
        {
            foreach (FunctionDeclarationStatement functionDeclaration in statements.OfType<FunctionDeclarationStatement>())
            {
                if (functionDeclaration.Body != null && functionDeclaration.Body.Statements != null && functionDeclaration.Body.Statements.Count > 0)
                {
                    functionDeclaration.Method!.CilMethodBody = new CilMethodBody();
                    GenerateStatements(functionDeclaration.Method.CilMethodBody, functionDeclaration.Body.Statements, functionDeclaration.Body.Locals);

                    if (functionDeclaration.Method.CilMethodBody.Instructions.Last().OpCode != CilOpCodes.Ret)
                    {
                        if (functionDeclaration.ReturnType is TypeExpression typeExpression)
                        {
                            switch (typeExpression.Type)
                            {
                                case ReservedTypes.I32:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                    break;
                                case ReservedTypes.F64:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_R8, 0);
                                    break;
                                case ReservedTypes.I64:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I8, 0);
                                    break;
                                case ReservedTypes.Bool:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                    break;
                                case ReservedTypes.String:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldnull);
                                    break;
                                case ReservedTypes.Fn:
                                    break;
                                default:
                                    throw new Exception($"Return type '{typeExpression.Type}' is not supported in code generation.");
                            }
                        }
                        functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
                    }
                }
            }
        }


        void GenerateStructStatements(StatementList statements)
        {
            foreach(StructStatement structStatement in statements.OfType<StructStatement>())
            {
                TypeDefinition structDef = new TypeDefinition(Settings.Name, structStatement.Name, TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.NestedPublic, ValueTypeReference);

                foreach (VariableDeclarationStatement field in structStatement.Fields)
                {
                    ITypeDefOrRef fieldType = ResolveType(field.Type, new List<VariableDeclarationStatement>());

                    FieldDefinition fieldDefinition = new FieldDefinition(field.Name, FieldAttributes.Public, new FieldSignature(fieldType.ToTypeSignature(fieldType.GetIsValueType(Runtime))));
                    structDef.Fields.Add(fieldDefinition);
                }

                Module.TopLevelTypes.First().NestedTypes.Add(structDef);
            }
        }

        void GenerateIfStatement(CilMethodBody body, IfStatement ifStatement)
        {
            bool multipleBranches = ifStatement.ElseIfs.Count > 0 || ifStatement.Else.Statements.Count > 0;

            CilInstruction endBranch = new CilInstruction(CilOpCodes.Nop);
            ICilLabel endBranchLabel = endBranch.CreateLabel();

            GenerateExpression(ifStatement.Condition, ifStatement.Body.Locals, body);

            CilInstruction ifFalseBranch = new CilInstruction(CilOpCodes.Nop);
            body.Instructions.Add(CilOpCodes.Brfalse, ifFalseBranch.CreateLabel());

            GenerateStatements(body, ifStatement.Body.Statements, ifStatement.Body.Locals);

            if (multipleBranches)
            {
                body.Instructions.Add(CilOpCodes.Br, endBranchLabel);
            }

            body.Instructions.Add(ifFalseBranch);

            foreach (IfStatement elseIfStatement in ifStatement.ElseIfs)
            {
                GenerateExpression(elseIfStatement.Condition, elseIfStatement.Body.Locals, body);

                CilInstruction elseIfFalseBranch = new CilInstruction(CilOpCodes.Nop);
                body.Instructions.Add(CilOpCodes.Brfalse, elseIfFalseBranch.CreateLabel());

                GenerateStatements(body, elseIfStatement.Body.Statements, elseIfStatement.Body.Locals);

                body.Instructions.Add(CilOpCodes.Br, endBranchLabel);
                body.Instructions.Add(elseIfFalseBranch);
            }

            if (ifStatement.Else.Statements.Count > 0)
            {
                GenerateStatements(body, ifStatement.Else.Statements, ifStatement.Else.Locals);
            }

            if (multipleBranches)
            {
                body.Instructions.Add(endBranch);
            }
        }

        void GenerateVariableDeclarationStatement(CilMethodBody body, VariableDeclarationStatement variableDeclarationStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            CilLocalVariable localVariable = new CilLocalVariable(ResolveTypeSignature(variableDeclarationStatement.Type, variableDeclarationStatement.Expression, variableDeclarationStatements));
            body.LocalVariables.Add(localVariable);

            if (variableDeclarationStatement.Expression != null)
            {
                GenerateExpression(variableDeclarationStatement.Expression, variableDeclarationStatements, body);
                body.Instructions.Add(CilOpCodes.Stloc, localVariable);
            }

            variableDeclarationStatement.CilLocalVariable = localVariable;
        }

        void GenerateAssignmentStatement(CilMethodBody body, AssignmentStatement assignmentStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if (assignmentStatement.Variable is IdentifierExpression identifierExpression)
            {
                GenerateExpression(assignmentStatement.Expression, variableDeclarationStatements, body);

                List<FieldDefinition> fieldDefs = Module.TopLevelTypes.First().Fields.Where(t => t.Name == identifierExpression.Name).ToList();

                if(fieldDefs.Any())
                {
                    body.Instructions.Add(fieldDefs.First().IsStatic ? CilOpCodes.Stsfld : CilOpCodes.Stfld, fieldDefs.First());
                }
                else
                {
                    VariableDeclarationStatement variableDeclaration = variableDeclarationStatements.Where(t => t.Name == identifierExpression.Name).FirstOrDefault() ?? throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");

                    body.Instructions.Add(CilOpCodes.Stloc, variableDeclaration.CilLocalVariable!);
                }
                return;
            }
            if(assignmentStatement.Variable is IndexExpression indexExpression)
            {
                GenerateExpression(indexExpression.Expression, variableDeclarationStatements, body);
                GenerateExpression(indexExpression.Index, variableDeclarationStatements, body);
                GenerateExpression(assignmentStatement.Expression, variableDeclarationStatements, body);

                Emitter.EmitStelem(ResolveType(indexExpression.Expression, variableDeclarationStatements), body);
                return;
            }
            if(assignmentStatement.Variable is MemberExpression memberExpression)
            {
                GenerateExpression(memberExpression.Parent, variableDeclarationStatements, body);
                GenerateExpression(assignmentStatement.Expression, variableDeclarationStatements, body);

                GenerateGetOrSetMember(memberExpression, false, variableDeclarationStatements, body);
                return;
            }

            throw new Exception($"Assignment to expression of type '{assignmentStatement.Variable.GetType().Name}' is not supported in code generation.");
        }

        void GenerateGetOrSetMember(MemberExpression memberExpression, bool getMember, List<VariableDeclarationStatement> variableDeclarationStatements, CilMethodBody body)
        {
            ResolveType(memberExpression.Parent, variableDeclarationStatements).Resolve(Runtime, out TypeDefinition? parentType);
            if (parentType == null)
            {
                throw new Exception($"Could not convert parent to typedefinition");
            }

            string memberName = "";

            if (memberExpression.Member is IdentifierExpression identifier)
            {
                memberName = identifier.Name;
            }
            else
            {
                throw new Exception($"{memberExpression.Member.GetType().FullName} is not supported in member expression member.");
            }

            FieldDefinition memberField = parentType.Fields.First(f => f.Name == memberName);

            if (memberField != null)
            {
                if(getMember)
                {
                    body.Instructions.Add(memberField.IsStatic ? CilOpCodes.Ldsfld : CilOpCodes.Ldfld, memberField);
                }
                else
                {

                    body.Instructions.Add(memberField.IsStatic ? CilOpCodes.Stsfld : CilOpCodes.Stfld, memberField);
                }
            }
            else
            {
                throw new Exception($"Field {memberName} does not exist in type {parentType.Name}");
            }
        }

        void GenerateForStatement(CilMethodBody body, ForStatement forStatement)
        {
            forStatement.Variable.Expression = forStatement.Range.Start;
            GenerateVariableDeclarationStatement(body, forStatement.Variable, forStatement.Body.Locals);

            CilInstruction loopStart = new CilInstruction(CilOpCodes.Nop);
            CilInstruction conditionStart = new CilInstruction(CilOpCodes.Nop);

            body.Instructions.Add(CilOpCodes.Br, conditionStart.CreateLabel());
            body.Instructions.Add(loopStart);
            GenerateStatements(body, forStatement.Body.Statements, forStatement.Body.Locals);

            Emitter.EmitLdc_I4(1, body);
            body.Instructions.Add(CilOpCodes.Ldloc, forStatement.Variable.CilLocalVariable!);
            body.Instructions.Add(CilOpCodes.Add);
            body.Instructions.Add(CilOpCodes.Stloc, forStatement.Variable.CilLocalVariable!);

            body.Instructions.Add(conditionStart);
            GenerateExpression(forStatement.Range.End, forStatement.Body.Locals, body);
            body.Instructions.Add(CilOpCodes.Ldloc, forStatement.Variable.CilLocalVariable!);
            body.Instructions.Add(CilOpCodes.Cgt);
            //body.Instructions.Add(CilOpCodes.Ldc_I4_0);
            //body.Instructions.Add(CilOpCodes.Ceq);

            body.Instructions.Add(CilOpCodes.Brtrue, loopStart.CreateLabel());
        }

        void GenerateWhileStatement(CilMethodBody body, WhileStatement whileStatement)
        {
            CilInstruction loopStart = new CilInstruction(CilOpCodes.Nop);
            CilInstruction conditionStart = new CilInstruction(CilOpCodes.Nop);

            body.Instructions.Add(CilOpCodes.Br, conditionStart.CreateLabel());
            body.Instructions.Add(loopStart);

            GenerateStatements(body, whileStatement.Body.Statements, whileStatement.Body.Locals);

            body.Instructions.Add(conditionStart);
            GenerateExpression(whileStatement.Expression, whileStatement.Body.Locals, body);
            body.Instructions.Add(CilOpCodes.Brtrue, loopStart.CreateLabel());
        }

        void GenerateReturnStatement(CilMethodBody body, ReturnStatement returnStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if(returnStatement.Expression != null)
            {
                GenerateExpression(returnStatement.Expression, variableDeclarationStatements, body);
            }

            body.Instructions.Add(CilOpCodes.Ret);
        }

        void GenerateStatements(CilMethodBody body, StatementList statementList, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            foreach (Statement statement in statementList)
            {
                if (statement is CallStatement callStatement)
                {
                    GenerateCallStatement(callStatement, body, variableDeclarationStatements);
                    continue;
                }

                if (statement is IfStatement ifStatement)
                {
                    GenerateIfStatement(body, ifStatement);
                    continue;
                }

                if (statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    GenerateVariableDeclarationStatement(body, variableDeclarationStatement, variableDeclarationStatements);
                    continue;
                }

                if (statement is AssignmentStatement assignmentStatement)
                {
                    GenerateAssignmentStatement(body, assignmentStatement, variableDeclarationStatements);
                    continue;
                }

                if(statement is ForStatement forStatement)
                {
                    GenerateForStatement(body, forStatement);
                    continue;
                }

                if(statement is ReturnStatement returnStatement)
                {
                    GenerateReturnStatement(body, returnStatement, variableDeclarationStatements);
                    continue;
                }

                if(statement is WhileStatement whileStatement)
                {
                    GenerateWhileStatement(body, whileStatement);
                    continue;
                }

                throw new Exception($"Statement of type '{statement.GetType().Name}' is not supported in code generation.");
            }
        }

        // TODO: Add support for member expressions, array accesses, etc.
        // Add type checking
        void GenerateCallStatement(CallStatement callStatement, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if (callStatement.Expression is IdentifierExpression identifierExpression)
            {
                if(identifierExpression.Name == "logline" && callStatement.Arguments.Any())
                {
                    GenerateExpressions(callStatement.Arguments, variableDeclarationStatements, body);

                    Expression argumentExpression = callStatement.Arguments.First();
                    ITypeDefOrRef argumentType = ResolveType(argumentExpression, variableDeclarationStatements);

                    body.Instructions.Add(CilOpCodes.Call, Module.CorLibTypeFactory.CorLibScope
                        .CreateTypeReference("System", "Console")
                        .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                        returnType: Module.CorLibTypeFactory.Void,
                        parameterTypes: [argumentType.ToTypeSignature(argumentType.GetIsValueType(Runtime))]
                        )));

                    return;
                }

                if (identifierExpression.Name == "log" && callStatement.Arguments.Any())
                {
                    GenerateExpressions(callStatement.Arguments, variableDeclarationStatements, body);

                    Expression argumentExpression = callStatement.Arguments.First();
                    ITypeDefOrRef argumentType = ResolveType(argumentExpression, variableDeclarationStatements);

                    body.Instructions.Add(CilOpCodes.Call, Module.CorLibTypeFactory.CorLibScope
                        .CreateTypeReference("System", "Console")
                        .CreateMemberReference("Write", MethodSignature.CreateStatic(
                        returnType: Module.CorLibTypeFactory.Void,
                        parameterTypes: [argumentType.ToTypeSignature(argumentType.GetIsValueType(Runtime))]
                        )));

                    return;
                }

                if (Module.TopLevelTypes.First().Methods.Where(m => m.Name == identifierExpression.Name).FirstOrDefault() is MethodDefinition method)
                {
                    GenerateExpressions(callStatement.Arguments, variableDeclarationStatements, body);

                    body.Instructions.Add(CilOpCodes.Call, method);
                    return;
                }

                throw new Exception($"Function '{identifierExpression.Name}' is not declared in the current scope.");
            }
            else
            {
                throw new Exception($"Call expression of type '{callStatement.Expression.GetType().Name}' is not supported in code generation.");
            }

            throw new Exception($"Function '{identifierExpression.Name}' is not supported in code generation.");
        }

        void GenerateExpressions(ExpressionList expressionList, List<VariableDeclarationStatement> variableDeclarationStatements, CilMethodBody body)
        {
            if(expressionList == null)
            {
                return;
            }

            foreach (Expression expression in expressionList)
            {
                GenerateExpression(expression, variableDeclarationStatements, body);
            }
        }

        void GenerateExpression(Expression expression, List<VariableDeclarationStatement> variableDeclarationStatements, CilMethodBody body)
        {
            if (expression is StringExpression stringExpression)
            {
                GenerateStringExpression(stringExpression, body);
                return;
            }

            if (expression is NumberExpression numberExpression)
            {
                GenerateNumberExpression(numberExpression, body);
                return;
            }

            if (expression is BooleanExpression booleanExpression)
            {
                GenerateBooleanExpression(booleanExpression, body);
                return;
            }

            if (expression is ArithmeticExpression arithmeticExpression)
            {
                GenerateArithmeticExpression(arithmeticExpression, body, variableDeclarationStatements);
                return;
            }

            if (expression is RelationalExpression relationalExpression)
            {
                GenerateRelationalExpression(relationalExpression, body, variableDeclarationStatements);
                return;
            }

            if (expression is IdentifierExpression identifierExpression)
            {
                GenerateLoadVariable(identifierExpression, variableDeclarationStatements, body);
                return;
            }

            if(expression is IndexExpression indexExpression)
            {
                GenerateIndexExpression(indexExpression, variableDeclarationStatements, body);
                return;
            }

            if(expression is ArrayExpression arrayExpression)
            {
                GenerateArrayExpression(arrayExpression, variableDeclarationStatements, body);
                return;
            }

            if(expression is CallExpression callExpression)
            {
                GenerateCallExpression(callExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is LengthExpression lengthExpression)
            {
                GenerateLengthExpresssion(lengthExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                GenerateArrayInitializerExpression(arrayInitializerExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is NotExpression notExpression)
            {
                GenerateNotExpression(notExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is NegateExpression negateExpression)
            {
                GenerateNegateExpression(negateExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is ParenthesizedExpression parenthesizedExpression)
            {
                GenerateParenthesizedExpression(parenthesizedExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is ObjectInitializerExpression objectInitializerExpression)
            {
                GenerateObjectInitializerExpression(objectInitializerExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is MemberExpression memberExpression)
            {
                GenerateMemberExpression(memberExpression, body, variableDeclarationStatements);
                return;
            }

            throw new Exception($"Expression of type '{expression.GetType().Name}' is not supported in code generation.");
        }

        void GenerateMemberExpression(MemberExpression memberExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(memberExpression.Parent, variableDeclarationStatements, body);

            GenerateGetOrSetMember(memberExpression, true, variableDeclarationStatements, body);
        }

        void GenerateObjectInitializerExpression(ObjectInitializerExpression objectInitializerExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if(objectInitializerExpression.Expression is IdentifierExpression objectInitializerIdentifier)
            {
                List<TypeDefinition> types = Module.TopLevelTypes.First().NestedTypes.Where(t => t.Name == objectInitializerIdentifier.Name).ToList();
                if (types.Any())
                {
                    TypeDefinition type = types.First();
                    if (Runtime == null)
                    {
                        throw new Exception("Runtime context is not set");
                    }

                    CilLocalVariable temp = new CilLocalVariable(type.ToTypeSignature(Runtime));
                    body.LocalVariables.Add(temp);

                    Emitter.EmitLdloca(temp, body);
                    body.Instructions.Add(CilOpCodes.Initobj, type);


                    foreach (AssignmentStatement propertyAssignments in objectInitializerExpression.PropertyAssignments)
                    {
                        Emitter.EmitLdloca(temp, body);
                        GenerateExpression(propertyAssignments.Expression, variableDeclarationStatements, body);

                        if (propertyAssignments.Variable is IdentifierExpression property)
                        {
                            List<FieldDefinition> fieldDefinitions = type.Fields.Where(f => f.Name == property.Name).ToList();
                            if (fieldDefinitions.Any())
                            {
                                body.Instructions.Add(CilOpCodes.Stfld, fieldDefinitions.First());
                            }
                            else
                            {
                                throw new Exception($"Field '{property.Name}' of type '{objectInitializerIdentifier.Name}' does not exist");
                            }
                        }
                        else
                        {
                            throw new Exception($"Type expression {objectInitializerExpression.Expression.GetType().FullName} or field expression {propertyAssignments.Variable} is not supported");
                        }
                    }

                    body.Instructions.Add(CilOpCodes.Ldloc, temp);
                }
            }
        }

        void GenerateParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(parenthesizedExpression.Expression, variableDeclarationStatements, body);
        }

        void GenerateNotExpression(NotExpression notExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(notExpression.Expression, variableDeclarationStatements, body);
            body.Instructions.Add(CilOpCodes.Ldc_I4_0);
            body.Instructions.Add(CilOpCodes.Ceq);
        }

        void GenerateNegateExpression(NegateExpression negateExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(negateExpression.Expression, variableDeclarationStatements, body);
            body.Instructions.Add(CilOpCodes.Neg);
        }

        void GenerateArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(arrayInitializerExpression.Initializer.Index, variableDeclarationStatements, body);
            ITypeDefOrRef arrayType = ResolveType(arrayInitializerExpression.Initializer.Expression, variableDeclarationStatements);
            body.Instructions.Add(CilOpCodes.Newarr, arrayType);

            for (int i = 0; i < arrayInitializerExpression.Array.Expressions.Count; i++)
            {
                body.Instructions.Add(CilOpCodes.Dup);
                Emitter.EmitLdc_I4(i, body);
                GenerateExpression(arrayInitializerExpression.Array.Expressions[i], variableDeclarationStatements, body);

                Emitter.EmitStelem(arrayType, body);
            }
        }

        void GenerateLengthExpresssion(LengthExpression lengthExpression, CilMethodBody body , List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(lengthExpression.Expression, variableDeclarationStatements, body);
            body.Instructions.Add(CilOpCodes.Ldlen);
            body.Instructions.Add(CilOpCodes.Conv_I4);
        }

        void GenerateStringExpression(StringExpression stringExpression, CilMethodBody body)
        {
            body.Instructions.Add(CilOpCodes.Ldstr, stringExpression.Value);
        }

        void GenerateNumberExpression(NumberExpression numberExpression, CilMethodBody body)
        {
            if(numberExpression.IsDouble)
            {
                Emitter.EmitLdc_R8(double.Parse(numberExpression.Value, NumberStyles.Any, CultureInfo.InvariantCulture), body);
            }
            else
            {
                Emitter.EmitLdc_I4(int.Parse(numberExpression.Value), body);
            }
        }

        

        void GenerateBooleanExpression(BooleanExpression booleanExpression, CilMethodBody body)
        {
            body.Instructions.Add(booleanExpression.Value ? CilOpCodes.Ldc_I4_1 : CilOpCodes.Ldc_I4_0);
        }

        void GenerateArithmeticExpression(ArithmeticExpression arithmeticExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            switch (arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    ITypeDefOrRef leftExpressionType = ResolveType(arithmeticExpression.Left, variableDeclarationStatements).Resolve(Runtime);

                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);

                    if (leftExpressionType.Namespace == "System" && leftExpressionType.Name == "String")
                    {
                        body.Instructions.Add(CilOpCodes.Call, Module.CorLibTypeFactory.CorLibScope
                        .CreateTypeReference("System", "String")
                        .CreateMemberReference("Concat", MethodSignature.CreateStatic(
                        returnType: Module.CorLibTypeFactory.String,
                        parameterTypes: [
                                Module.CorLibTypeFactory.String,
                                Module.CorLibTypeFactory.String
                            ]
                        )));
                    }
                    else
                    {
                        body.Instructions.Add(CilOpCodes.Add);
                    }

                    break;

                case ArithmeticOperator.Subtraction:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Sub);
                    break;

                case ArithmeticOperator.Multiplication:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Mul);
                    break;

                case ArithmeticOperator.Division:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Div);
                    break;

                case ArithmeticOperator.Modulus:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Rem);
                    break;

                case ArithmeticOperator.Exponential:
                    throw new Exception("Exponential operations is not yet implemented");

                case ArithmeticOperator.LeftShift:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Shl);
                    break;

                case ArithmeticOperator.RightShift:
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Shr);
                    break;
            }
        }

        void GenerateRelationalExpression(RelationalExpression relationalExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            switch (relationalExpression.Operator)
            {
                case RelationalOperators.EqualTo:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Ceq);
                    break;

                case RelationalOperators.NotEqualTo:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Ceq);
                    body.Instructions.Add(CilOpCodes.Ldc_I4_0);
                    body.Instructions.Add(CilOpCodes.Ceq);
                    break;

                case RelationalOperators.BiggerThan:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Cgt);
                    break;

                case RelationalOperators.BiggerOrEqual:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Clt);
                    body.Instructions.Add(CilOpCodes.Ldc_I4_0);
                    body.Instructions.Add(CilOpCodes.Ceq);
                    break;

                case RelationalOperators.SmallerThan:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Clt);
                    break;

                case RelationalOperators.SmallerOrEqual:
                    GenerateExpression(relationalExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(relationalExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Cgt);
                    body.Instructions.Add(CilOpCodes.Ldc_I4_0);
                    body.Instructions.Add(CilOpCodes.Ceq);
                    break;
            }
        }

        VariableDeclarationStatement? GenerateLoadVariable(IdentifierExpression identifierExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if (variableDeclarations.Where(t => t.Name == identifierExpression.Name).Count() > 0)
            {
                VariableDeclarationStatement variableDeclaration = variableDeclarations.Where(t => t.Name == identifierExpression.Name).First();

                if (variableDeclaration.IsParameter)
                {
                    Emitter.EmitLdarg(variableDeclaration.ParameterIndex, body);
                    return null;
                }


                // TODO: This breaks array accessing and fixes other stuff, but when changing to ...VariableType is SzArrayTypeSignature - does the opposite.
                body.Instructions.Add(CilOpCodes.Ldloc, variableDeclaration.CilLocalVariable!);

                return variableDeclaration;
            }

            List<FieldDefinition> fieldDefs = Module.TopLevelTypes.First().Fields.Where(t => t.Name == identifierExpression.Name).ToList();
            if (fieldDefs.Any())
            {
                body.Instructions.Add(fieldDefs.First().IsStatic ? CilOpCodes.Ldsfld : CilOpCodes.Ldfld, fieldDefs.First());
                return null;
            }

            throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
        }

        void GenerateIndexExpression(IndexExpression indexExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            ITypeDefOrRef expressionType = ResolveType(indexExpression.Expression, variableDeclarations);

            if (expressionType == null)
            {
                throw new Exception($"Could not resolve type of index expression {indexExpression.Expression}");
            }

            GenerateExpression(indexExpression.Expression, variableDeclarations, body);
            GenerateExpression(indexExpression.Index, variableDeclarations, body);

            if (expressionType.Namespace == "System" 
                && expressionType.Name == "String"
                && expressionType.ToTypeSignature(expressionType.GetIsValueType(Runtime)) is not SzArrayTypeSignature)
            {
                MethodSignature getCharsSignature = MethodSignature.CreateStatic(
                        returnType: Module.CorLibTypeFactory.Char,
                        parameterTypes: [
                                Module.CorLibTypeFactory.Int32,
                            ]
                        );

                getCharsSignature.HasThis = true;

                body.Instructions.Add(CilOpCodes.Callvirt, Module.CorLibTypeFactory.CorLibScope
                        .CreateTypeReference("System", "String")
                        .CreateMemberReference("get_Chars", getCharsSignature));
            }
            else
            {
                Emitter.EmitLdelem(expressionType, body);
            }
        }

        void GenerateArrayExpression(ArrayExpression arrayExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if (arrayExpression.Expressions.Count == 0)
            {
                throw new Exception("Array expression without any elements is not supported in code generation (yet).");
            }

            Emitter.EmitLdc_I4(arrayExpression.Expressions.Count, body);
            ITypeDefOrRef arrayType = ResolveType(arrayExpression.Expressions[0], variableDeclarations);
            body.Instructions.Add(CilOpCodes.Newarr, arrayType); // Temporary workaround until proper type checking is implemented

            for(int i = 0; i < arrayExpression.Expressions.Count; i++)
            {
                body.Instructions.Add(CilOpCodes.Dup);
                Emitter.EmitLdc_I4(i, body);
                GenerateExpression(arrayExpression.Expressions[i], variableDeclarations, body);

                Emitter.EmitStelem(arrayType, body);
            }
        }

        

        void GenerateCallExpression(CallExpression callExpression, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if (callExpression.Expression is IdentifierExpression identifierExpression)
            {
                GenerateExpressions(callExpression.Arguments, variableDeclarationStatements, body);

                if (Module.TopLevelTypes.First().Methods.Where(m => m.Name == identifierExpression.Name).FirstOrDefault() is MethodDefinition method)
                {
                    body.Instructions.Add(CilOpCodes.Call, method);
                    return;
                }

                throw new Exception($"Function '{identifierExpression.Name}' is not declared in the current scope.");
            }
            else
            {
                throw new Exception($"Call expression of type '{callExpression.Expression.GetType().Name}' is not supported in code generation.");
            }

            throw new Exception($"Function '{identifierExpression.Name}' is not supported in code generation.");
        }
    }
}
