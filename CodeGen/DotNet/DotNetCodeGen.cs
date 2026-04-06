using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using System;
using System.Collections.Generic;
using System.Text;

using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.DotNet.Builder;
using AsmResolver.PE;
using AsmResolver.PE.File;
using AsmResolver.PE.Builder;

namespace CommonC.CodeGen.DotNet
{
    public class DotNetCodeGen
    {
        DotNetCodeGenSettings Settings { get; set; }

        public DotNetCodeGen(DotNetCodeGenSettings settings)
        {
            Settings = settings;
        }

        IMethodDescriptor? WriteLine { get; set; }

        CorLibTypeSignature ResolveReservedType(ModuleDefinition module, ReservedTypes type)
        {
            switch (type)
            {
                case ReservedTypes.String:
                    return module.CorLibTypeFactory.String;
                case ReservedTypes.Int:
                    return module.CorLibTypeFactory.Int32;
                case ReservedTypes.Bool:
                    return module.CorLibTypeFactory.Boolean;

                default:
                    throw new Exception($"Type '{type}' does not exist and could not be translated.");
            }
        }

        CorLibTypeSignature ResolveType(ModuleDefinition module, Expression expression)
        {
            if (expression is TypeExpression typeExpression)
            {
                return ResolveReservedType(module, typeExpression.Type);
            }
            else
            {
                throw new Exception($"Unknown type expression '{expression}' could not be translated.");
            }
        }


        public PEFile Generate(StatementList statements)
        {
            ModuleDefinition module = new ModuleDefinition(Settings.Name); // , DotNetRuntimeInfo.NetCoreApp(10, 0)

            AssemblyDefinition assembly = new AssemblyDefinition(Settings.Name, Settings.Version);
            assembly.Modules.Add(module);

            WriteLine = module.CorLibTypeFactory.CorLibScope
            .CreateTypeReference("System", "Console")
            .CreateMemberReference("WriteLine", MethodSignature.CreateStatic(
                returnType: module.CorLibTypeFactory.Void,
                parameterTypes: [module.CorLibTypeFactory.String]
            ));

            GenerateFunctionDeclarations(module, statements);

            ManagedPEImageBuilder builder = new ManagedPEImageBuilder();
            PEImage peImage = module.ToPEImage(builder);

            PEFile peFile = peImage.ToPEFile(new ManagedPEFileBuilder());

            foreach (var item in module.AssemblyReferences)
            {
                Console.WriteLine(item);
            }

            return peFile;
        }

        void GenerateFunctionDeclarations(ModuleDefinition module, StatementList statements)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclaration)
                {
                    var returnType = ResolveType(module, functionDeclaration.ReturnType);
                    var parameters = new List<CorLibTypeSignature>();

                    if (functionDeclaration.Parameters != null)
                    {
                        foreach (var parameter in functionDeclaration.Parameters)
                        {
                            parameters.Add(ResolveType(module, parameter));
                        }
                    }

                    MethodDefinition function = new MethodDefinition(functionDeclaration.Name, MethodAttributes.Public | MethodAttributes.Static, MethodSignature.CreateStatic(returnType, parameters));

                    if (functionDeclaration.Name == "main")
                    {
                        module.ManagedEntryPointMethod = function;
                    }

                    if (functionDeclaration.Body != null && functionDeclaration.Body.Statements != null && functionDeclaration.Body.Statements.Count > 0)
                    {
                        function.CilMethodBody = new CilMethodBody();
                        GenerateStatements(module, function.CilMethodBody, functionDeclaration.Body.Statements, functionDeclaration.Body.VariableDeclarations);

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
                                    default:
                                        throw new Exception($"Return type '{typeExpression.Type}' is not supported in code generation.");
                                }
                            }
                            function.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
                        }
                    }


                    module.TopLevelTypes.First().Methods.Add(function);

                    foreach (CilInstruction instruction in function.CilMethodBody.Instructions)
                    {
                        Console.WriteLine(instruction);
                    }
                }
            }
        }

        // Add support for else if
        void GenerateIfStatement(ModuleDefinition module, CilMethodBody body, IfStatement ifStatement)
        {

            GenerateExpression(ifStatement.Condition, ifStatement.Body.VariableDeclarations, body);

            CilInstruction falseBranchNop = new CilInstruction(CilOpCodes.Nop);
            ICilLabel falseBranchLabel = falseBranchNop.CreateLabel();
            body.Instructions.Add(CilOpCodes.Brfalse, falseBranchLabel);

            GenerateStatements(module, body, ifStatement.Body.Statements, ifStatement.Body.VariableDeclarations);

            CilInstruction skipElseBranchNop = new CilInstruction(CilOpCodes.Nop);
            ICilLabel skipElseBranchLabel = skipElseBranchNop.CreateLabel();
            if (ifStatement.Else.Statements.Count > 0)
            {
                body.Instructions.Add(CilOpCodes.Br, skipElseBranchLabel);
            }
            body.Instructions.Add(falseBranchNop);

            if(ifStatement.Else.Statements.Count > 0)
            {
                GenerateStatements(module, body, ifStatement.Else.Statements, ifStatement.Else.VariableDeclarations);
                body.Instructions.Add(skipElseBranchNop);
            }
        }

        void GenerateVariableDeclarationStateement(ModuleDefinition module, CilMethodBody body, VariableDeclarationStatement variableDeclarationStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            CilLocalVariable localVariable = new CilLocalVariable(ResolveType(module, variableDeclarationStatement.Type));
            body.LocalVariables.Add(localVariable);

            GenerateExpression(variableDeclarationStatement.Expression, variableDeclarationStatements, body);
            body.Instructions.Add(CilOpCodes.Stloc, localVariable);

            variableDeclarationStatement.CilLocalVaraible = localVariable;
        }

        void GenerateAssignmentStatement(ModuleDefinition module, CilMethodBody body, AssignmentStatement assignmentStatement, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            GenerateExpression(assignmentStatement.Expression, variableDeclarationStatements, body);

            if(assignmentStatement.Variable is IdentifierExpression identifierExpression)
            {
                VariableDeclarationStatement variableDeclaration = variableDeclarationStatements.Where(t => t.Name == identifierExpression.Name).FirstOrDefault() ?? throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
                body.Instructions.Add(CilOpCodes.Stloc, variableDeclaration.CilLocalVaraible!);
                return;
            }

            throw new Exception($"Assignment to expression of type '{assignmentStatement.Variable.GetType().Name}' is not supported in code generation.");
        }

        void GenerateStatements(ModuleDefinition module, CilMethodBody body, StatementList statementList, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            foreach (Statement statement in statementList)
            {
                if (statement is CallStatement callStatement)
                {
                    GenerateCallStatement(callStatement, body, variableDeclarationStatements);
                    continue;
                }

                if(statement is IfStatement ifStatement)
                {
                    GenerateIfStatement(module, body, ifStatement);
                    continue;
                }

                if (statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    GenerateVariableDeclarationStateement(module, body, variableDeclarationStatement, variableDeclarationStatements);
                    continue;
                }

                if(statement is AssignmentStatement assignmentStatement)
                {
                    GenerateAssignmentStatement(module, body, assignmentStatement, variableDeclarationStatements);
                    continue;
                }

                throw new Exception($"Statement of type '{statement.GetType().Name}' is not supported in code generation.");
            }
        }

        void GenerateCallStatement(CallStatement callStatement, CilMethodBody body, List<VariableDeclarationStatement> variableDeclarationStatements)
        {
            if (callStatement.Expression is IdentifierExpression identifierExpression)
            {
                if (identifierExpression.Name == "log")
                {
                    GenerateExpressions(callStatement.Arguments, variableDeclarationStatements, body);
                    body.Instructions.Add(CilOpCodes.Call, WriteLine!);
                    return;
                }
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

            if(expression is ArithmeticExpression arithmeticExpression)
            {
                GenerateArithmeticExpression(arithmeticExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is RelationalExpression relationalExpression)
            {
                GenerateRelationalExpression(relationalExpression, body, variableDeclarationStatements);
                return;
            }

            if(expression is IdentifierExpression identifierExpression)
            {
                GenerateVariable(identifierExpression, variableDeclarationStatements, body);
                return;
            }

            else
            {
                throw new Exception($"Expression of type '{expression.GetType().Name}' is not supported in code generation.");
            }
        }

        void GenerateStringExpression(StringExpression stringExpression, CilMethodBody body)
        {
            body.Instructions.Add(CilOpCodes.Ldstr, stringExpression.Value);
        }

        void GenerateNumberExpression(NumberExpression integerExpression, CilMethodBody body)
        {
            int number = int.Parse(integerExpression.Value);

            switch(number)
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
                    body.Instructions.Add(CilOpCodes.Ldc_I4, number);
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
            switch(relationalExpression.Operator)
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

        void GenerateVariable(IdentifierExpression identifierExpression, List<VariableDeclarationStatement> variableDeclarations, CilMethodBody body)
        {
            if(variableDeclarations.Where(t => t.Name == identifierExpression.Name).Count() > 0)
            {
                VariableDeclarationStatement variableDeclaration = variableDeclarations.Where(t => t.Name == identifierExpression.Name).First();
                body.Instructions.Add(CilOpCodes.Ldloc, variableDeclaration.CilLocalVaraible!);
                return;
            }

            throw new Exception($"Variable '{identifierExpression.Name}' is not declared in the current scope.");
        }
    }
}
