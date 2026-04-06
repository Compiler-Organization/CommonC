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
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CommonC.CodeGen.DotNet
{
    public class DotNetCodeGen
    {
        DotNetCodeGenSettings Settings { get; set; }

        ModuleDefinition Module { get; set; } = null!;

        public DotNetCodeGen(DotNetCodeGenSettings settings)
        {
            Settings = settings;
        }

        IMethodDescriptor? WriteLineInt { get; set; }
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

        TypeReference ResolveType(Expression expression)
        {
            if (expression is TypeExpression typeExpression)
            {
                switch (typeExpression.Type)
                {
                    case ReservedTypes.String:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "String");
                    case ReservedTypes.Int:
                        return Module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Int32");
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

        void GenerateFunctionDeclarations(StatementList statements)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclaration)
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
                    Module.TopLevelTypes.First().Methods.Add(function);

                    if (functionDeclaration.Name == "main")
                    {
                        Module.ManagedEntryPointMethod = function;
                    }

                    if (functionDeclaration.Body != null && functionDeclaration.Body.Statements != null && functionDeclaration.Body.Statements.Count > 0)
                    {
                        function.CilMethodBody = new CilMethodBody();
                        GenerateStatements(function.CilMethodBody, functionDeclaration.Body.Statements, functionDeclaration.Body.VariableDeclarations);

                        if (function.CilMethodBody.Instructions.Last().OpCode != CilOpCodes.Ret)
                        {
                            if (functionDeclaration.ReturnType is TypeExpression typeExpression)
                            {
                                switch (typeExpression.Type)
                                {
                                    case ReservedTypes.Int:
                                        function.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                        break;
                                    case ReservedTypes.Bool:
                                        function.CilMethodBody.Instructions.Add(CilOpCodes.Ldc_I4_0);
                                        break;
                                    case ReservedTypes.String:
                                        function.CilMethodBody.Instructions.Add(CilOpCodes.Ldnull);
                                        break;
                                    case ReservedTypes.Void:
                                        break;
                                    default:
                                        throw new Exception($"Return type '{typeExpression.Type}' is not supported in code generation.");
                                }
                            }
                            function.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
                        }
                    }
                }
            }
        }

        // Add support for else if
        void GenerateIfStatement(CilMethodBody body, IfStatement ifStatement)
        {
            bool multipleBranches = ifStatement.ElseIfs.Count > 0 || ifStatement.Else.Statements.Count > 0;

            CilInstruction endBranch = new CilInstruction(CilOpCodes.Nop);
            ICilLabel endBranchLabel = endBranch.CreateLabel();

            GenerateExpression(ifStatement.Condition, ifStatement.Body.VariableDeclarations, body);

            CilInstruction ifFalseBranch = new CilInstruction(CilOpCodes.Nop);
            body.Instructions.Add(CilOpCodes.Brfalse, ifFalseBranch.CreateLabel());

            GenerateStatements(body, ifStatement.Body.Statements, ifStatement.Body.VariableDeclarations);

            if (multipleBranches)
            {
                body.Instructions.Add(CilOpCodes.Br, endBranchLabel);
            }

            body.Instructions.Add(ifFalseBranch);


            foreach (IfStatement elseIfStatement in ifStatement.ElseIfs)
            {
                GenerateExpression(elseIfStatement.Condition, elseIfStatement.Body.VariableDeclarations, body);

                CilInstruction elseIfFalseBranch = new CilInstruction(CilOpCodes.Nop);
                body.Instructions.Add(CilOpCodes.Brfalse, elseIfFalseBranch.CreateLabel());

                GenerateStatements(body, elseIfStatement.Body.Statements, elseIfStatement.Body.VariableDeclarations);

                body.Instructions.Add(CilOpCodes.Br, endBranchLabel);
                body.Instructions.Add(elseIfFalseBranch);
            }

            // ...



            if (ifStatement.Else.Statements.Count > 0)
            {
                GenerateStatements(body, ifStatement.Else.Statements, ifStatement.Else.VariableDeclarations);
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

            variableDeclarationStatement.CilLocalVaraible = localVariable;
        }

        void GenerateAssignmentStatement(CilMethodBody body, AssignmentStatement assignmentStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(assignmentStatement.Expression, variableDeclarationStatements, body);

            if (assignmentStatement.Variable is IdentifierExpression identifierExpression)
            {
                VariableDeclarationStatement variableDeclaration = variableDeclarationStatements.Where(t => t.Name == identifierExpression.Name).FirstOrDefault() ?? throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
                body.Instructions.Add(CilOpCodes.Stloc, variableDeclaration.CilLocalVaraible!);
                return;
            }

            throw new Exception($"Assignment to expression of type '{assignmentStatement.Variable.GetType().Name}' is not supported in code generation.");
        }

        void GenerateForStatement(CilMethodBody body, ForStatement forStatement)
        {
            forStatement.Variable.Expression = forStatement.Range.Start;
            GenerateVariableDeclarationStateement(body, forStatement.Variable, forStatement.Body.VariableDeclarations);

            CilInstruction loopStart = new CilInstruction(CilOpCodes.Nop);
            body.Instructions.Add(loopStart);
            GenerateStatements(body, forStatement.Body.Statements, forStatement.Body.VariableDeclarations);

            EmitLdc_I4(1, body);
            body.Instructions.Add(CilOpCodes.Ldloc, forStatement.Variable.CilLocalVaraible!);
            body.Instructions.Add(CilOpCodes.Add);
            body.Instructions.Add(CilOpCodes.Stloc, forStatement.Variable.CilLocalVaraible!);


            body.Instructions.Add(CilOpCodes.Ldloc, forStatement.Variable.CilLocalVaraible!);
            GenerateExpression(forStatement.Range.End, forStatement.Body.VariableDeclarations, body);
            body.Instructions.Add(CilOpCodes.Cgt);
            body.Instructions.Add(CilOpCodes.Ldc_I4_0);
            body.Instructions.Add(CilOpCodes.Ceq);

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

            throw new Exception($"Expression of type '{expression.GetType().Name}' is not supported in code generation.");
        }

        void GenerateStringExpression(StringExpression stringExpression, CilMethodBody body)
        {
            body.Instructions.Add(CilOpCodes.Ldstr, stringExpression.Value);
        }

        void GenerateNumberExpression(NumberExpression integerExpression, CilMethodBody body)
        {
            EmitLdc_I4(int.Parse(integerExpression.Value), body);
        }

        void EmitLdc_I4(int value, CilMethodBody body)
        {
            switch (value)
            {
                case -1:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_M1);
                    return;
                case 0:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_0);
                    return;
                case 1:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_1);
                    return;
                case 2:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_2);
                    return;
                case 3:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_3);
                    return;
                case 4:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_4);
                    return;
                case 5:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_5);
                    return;
                case 6:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_6);
                    return;
                case 7:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_7);
                    return;
                case 8:
                    body.Instructions.Add(CilOpCodes.Ldc_I4_8);
                    return;
                default:
                    body.Instructions.Add(CilOpCodes.Ldc_I4, value);
                    return;
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

                if(variableDeclaration.isParameter)
                {
                    EmitLdarg(variableDeclaration.parameterIndex, body);
                    return;
                }

                body.Instructions.Add(CilOpCodes.Ldloc, variableDeclaration.CilLocalVaraible!);
                return;
            }

            throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
        }

        void EmitLdarg(int index, CilMethodBody body)
        {
            switch(index)
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

            if (index <= byte.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldarga_S, index);
                return;
            }

            if (index <= ushort.MaxValue)
            {
                body.Instructions.Add(CilOpCodes.Ldarg, index);
                return;
            }

            throw new Exception($"Argument at {index} is too big, max amount of arguments is {ushort.MaxValue}");
        }

        void GenerateIndexExpression(IndexExpression indexExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            GenerateExpression(indexExpression.Expression, variableDeclarations, body);
            GenerateExpression(indexExpression.Index, variableDeclarations, body);

            body.Instructions.Add(CilOpCodes.Ldelem_Ref);
        }

        void GenerateArrayExpression(ArrayExpression arrayExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if (arrayExpression.Expressions.Count == 0)
            {
                throw new Exception("Array expression without any elements is not supported in code generation (yet).");
            }

            EmitLdc_I4(arrayExpression.Expressions.Count, body);
            body.Instructions.Add(CilOpCodes.Newarr, ResolveType(arrayExpression.Expressions[0])); // Temporary workaround until proper type checking is implemented

            for(int i = 0; i < arrayExpression.Expressions.Count; i++)
            {
                body.Instructions.Add(CilOpCodes.Dup);
                EmitLdc_I4(i, body);
                GenerateExpression(arrayExpression.Expressions[i], variableDeclarations, body);
                body.Instructions.Add(CilOpCodes.Stelem_Ref);
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
