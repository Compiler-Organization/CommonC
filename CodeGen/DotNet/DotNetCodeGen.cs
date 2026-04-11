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

namespace CommonC.CodeGen.DotNet
{
    public class DotNetCodeGen
    {
        DotNetCodeGenSettings Settings { get; set; }

        ModuleDefinition Module { get; set; } = null!;

        CILEmitter Emitter { get; set; }

        public DotNetCodeGen(DotNetCodeGenSettings settings)
        {
            Settings = settings;
            Emitter = new CILEmitter();
        }

        IMethodDescriptor? WriteLineInt { get; set; }
        IMethodDescriptor? WriteLineDouble { get; set; }
        IMethodDescriptor? WriteLineLong { get; set; }
        IMethodDescriptor? WriteLineStr { get; set; }
        IMethodDescriptor? WriteLineBool { get; set; }
        IMethodDescriptor? WriteLineObj { get; set; }

        CorLibTypeSignature ResolveReservedCorLibType(ReservedTypes type)
        {
            switch (type)
            {
                case ReservedTypes.String:
                    return Module.CorLibTypeFactory.String;
                case ReservedTypes.Int:
                    return Module.CorLibTypeFactory.Int32;
                case ReservedTypes.Double:
                    return Module.CorLibTypeFactory.Double;
                case ReservedTypes.Long:
                    return Module.CorLibTypeFactory.Int64;
                case ReservedTypes.Bool:
                    return Module.CorLibTypeFactory.Boolean;
                case ReservedTypes.Void:
                    return Module.CorLibTypeFactory.Void;

                default:
                    throw new Exception($"Type '{type}' does not exist and could not be resolved.");
            }
        }

        CorLibTypeSignature ResolveCorLibType(Expression expression)
        {
            if (expression is TypeExpression typeExpression)
            {
                return ResolveReservedCorLibType(typeExpression.Type);
            }
            else
            {
                throw new Exception($"Unknown type expression '{expression}' could not be resolved.");
            }
        }

        TypeReference ResolveType(Expression expression, List<VariableDeclarationStatement> locals)
        {
            if (expression is TypeExpression typeExpression)
            {
                switch (typeExpression.Type)
                {
                    case ReservedTypes.String:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "String");
                    case ReservedTypes.Int:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int32");
                    case ReservedTypes.Long:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int64");
                    case ReservedTypes.Double:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Double");
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
                List<VariableDeclarationStatement> existingLocals = locals.Where(l => l.Name == identifierExpression.Name).ToList();

                if (existingLocals.Any())
                {
                    return ResolveType(existingLocals.FirstOrDefault()!.Type, locals);
                }

                throw new Exception($"Exception occured when resolving type: Local '{identifierExpression.Name}' does not exist in the current scope.");
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

            throw new Exception($"Unknown type expression '{expression}' could not be resolved.");
        }

        public PEFile Generate(StatementList statements)
        {
            Module = new ModuleDefinition(Settings.Name); // , DotNetRuntimeInfo.NetCoreApp(10, 0)

            AssemblyDefinition assembly = new AssemblyDefinition(Settings.Name, Settings.Version);
            assembly.Modules.Add(Module);

            WriteLineInt = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.Int32]
            ));

            WriteLineStr = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.String]
            ));

            WriteLineDouble = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.Double]
            ));

            WriteLineLong = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.Int64]
            ));

            WriteLineBool = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.Boolean]
            ));

            WriteLineObj = Module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: Module.CorLibTypeFactory.Void,
                parameterTypes: [Module.CorLibTypeFactory.Object]
            ));

            GenerateFieldReferences(statements);
            GenerateFunctionReferences(statements);

            GenerateFieldDeclarations(statements);
            GenerateFunctionDeclarations(statements);

            ManagedPEImageBuilder builder = new ManagedPEImageBuilder();
            PEImage peImage = Module.ToPEImage(builder);

            PEFile peFile = peImage.ToPEFile(new ManagedPEFileBuilder());

            foreach (var item in Module.AssemblyReferences)
            {
                Console.WriteLine(item);
            }


            foreach(MethodDefinition method in Module.TopLevelTypes.First().Methods)
            {
                if(method.CilMethodBody == null)
                {
                    continue;
                }

                Console.WriteLine(method.Name);
                foreach (CilInstruction instruction in method.CilMethodBody.Instructions)
                {
                    Console.WriteLine(instruction);
                }
                Console.WriteLine();
            }

            return peFile;
        }

        void GenerateFieldReferences(StatementList statements)
        {
            foreach (VariableDeclarationStatement variableDeclarationStatement in statements.OfType<VariableDeclarationStatement>())
            {
                FieldDefinition fieldDefinition = new FieldDefinition(variableDeclarationStatement.Name, FieldAttributes.Public | FieldAttributes.Static, new FieldSignature(ResolveCorLibType(variableDeclarationStatement.Type)));

                variableDeclarationStatement.IsField = true;
                variableDeclarationStatement.Field = fieldDefinition;

                Module.TopLevelTypes.First().Fields.Add(fieldDefinition);
            }
        }

        void GenerateFieldDeclarations(StatementList statements)
        {
            MethodDefinition constructor = Module.TopLevelTypes.First().GetOrCreateStaticConstructor();

            if(constructor.CilMethodBody!.Instructions.First().OpCode == CilOpCodes.Ret)
            {
                constructor.CilMethodBody.Instructions.RemoveAt(0);
            }

            foreach (VariableDeclarationStatement variableDeclarationStatement in statements.OfType<VariableDeclarationStatement>())
            {
                if(variableDeclarationStatement.Expression != null)
                {
                    GenerateExpression(variableDeclarationStatement.Expression, statements.OfType<VariableDeclarationStatement>().ToList(), constructor.CilMethodBody);
                    constructor.CilMethodBody.Instructions.Add(CilOpCodes.Stsfld, variableDeclarationStatement.Field);
                }
            }

            constructor.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        }

        void GenerateFunctionReferences(StatementList statements)
        {
            foreach (FunctionDeclarationStatement functionDeclaration in statements.OfType<FunctionDeclarationStatement>())
            {
                var returnType = ResolveCorLibType(functionDeclaration.ReturnType);
                var parameters = new List<CorLibTypeSignature>();

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

                if (functionDeclaration.Name == "main")
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
                                case ReservedTypes.Int:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                    break;
                                case ReservedTypes.Double:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_R8, 0);
                                    break;
                                case ReservedTypes.Long:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I8, 0);
                                    break;
                                case ReservedTypes.Bool:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                    break;
                                case ReservedTypes.String:
                                    functionDeclaration.Method.CilMethodBody.Instructions.Add(CilOpCodes.Ldnull);
                                    break;
                                case ReservedTypes.Void:
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

        // TODO: Add support for else if
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

            // ...



            if (ifStatement.Else.Statements.Count > 0)
            {
                GenerateStatements(body, ifStatement.Else.Statements, ifStatement.Else.Locals);
            }

            if (multipleBranches)
            {
                body.Instructions.Add(endBranch);
            }
        }

        void GenerateVariableDeclarationStateement(CilMethodBody body, VariableDeclarationStatement variableDeclarationStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            CilLocalVariable localVariable = new CilLocalVariable(ResolveCorLibType(variableDeclarationStatement.Type));
            body.LocalVariables.Add(localVariable);

            GenerateExpression(variableDeclarationStatement.Expression, variableDeclarationStatements, body);
            body.Instructions.Add(CilOpCodes.Stloc, localVariable);

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
                    body.Instructions.Add(CilOpCodes.Stsfld, fieldDefs.First());
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

            throw new Exception($"Assignment to expression of type '{assignmentStatement.Variable.GetType().Name}' is not supported in code generation.");
        }

        void GenerateForStatement(CilMethodBody body, ForStatement forStatement)
        {
            forStatement.Variable.Expression = forStatement.Range.Start;
            GenerateVariableDeclarationStateement(body, forStatement.Variable, forStatement.Body.Locals);

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
                    GenerateVariableDeclarationStateement(body, variableDeclarationStatement, variableDeclarationStatements);
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
                GenerateExpressions(callStatement.Arguments, variableDeclarationStatements, body);

                // Temporary solution until typechecking has been added
                switch(identifierExpression.Name)
                {
                    case "logint":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineInt!);
                        return;
                    case "logdbl":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineDouble!);
                        return;
                    case "loglong":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineLong!);
                        return;
                    case "logstr":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineStr!);
                        return;
                    case "logbool":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineBool!);
                        return;
                    case "lobobj":
                        body.Instructions.Add(CilOpCodes.Call, WriteLineObj!);
                        return;
                }

                if (Module.TopLevelTypes.First().Methods.Where(m => m.Name == identifierExpression.Name).FirstOrDefault() is MethodDefinition method)
                {
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

            throw new Exception($"Expression of type '{expression.GetType().Name}' is not supported in code generation.");
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
            TypeReference arrayType = ResolveType(arrayInitializerExpression.Initializer.Expression, variableDeclarationStatements);
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
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Add);
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
                    GenerateExpression(arithmeticExpression.Left, variableDeclarationStatements, body);
                    GenerateExpression(arithmeticExpression.Right, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Shl);
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

        void GenerateLoadVariable(IdentifierExpression identifierExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if (variableDeclarations.Where(t => t.Name == identifierExpression.Name).Count() > 0)
            {
                VariableDeclarationStatement variableDeclaration = variableDeclarations.Where(t => t.Name == identifierExpression.Name).First();

                if (variableDeclaration.IsParameter)
                {
                    Emitter.EmitLdarg(variableDeclaration.ParameterIndex, body);
                    return;
                }

                body.Instructions.Add(CilOpCodes.Ldloc, variableDeclaration.CilLocalVariable!);

                return;
            }

            List<FieldDefinition> fieldDefs = Module.TopLevelTypes.First().Fields.Where(t => t.Name == identifierExpression.Name).ToList();

            if (fieldDefs.Any())
            {
                body.Instructions.Add(CilOpCodes.Ldsfld, fieldDefs.First());
                return;
            }

            throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
        }

        

        void GenerateIndexExpression(IndexExpression indexExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            GenerateExpression(indexExpression.Expression, variableDeclarations, body);
            GenerateExpression(indexExpression.Index, variableDeclarations, body);

            Emitter.EmitLdelem(ResolveType(indexExpression.Expression, variableDeclarations), body);
        }

        void GenerateArrayExpression(ArrayExpression arrayExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if (arrayExpression.Expressions.Count == 0)
            {
                throw new Exception("Array expression without any elements is not supported in code generation (yet).");
            }

            Emitter.EmitLdc_I4(arrayExpression.Expressions.Count, body);
            TypeReference arrayType = ResolveType(arrayExpression.Expressions[0], variableDeclarations);
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
