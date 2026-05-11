using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CommonC.LLVMIR.CodeGen
{
    public class LLVMIRCodeGen
    {
        LLVMIRCodeGenSettings Settings { get; set; }

        StatementList Statements { get; set; }

        public LLVMIRCodeGen(LLVMIRCodeGenSettings settings, StatementList statements)
        {
            Statements = statements;
            Settings = settings;
        }

        LLVMModuleRef Module { get; set; }

        LLVMBuilderRef Builder { get; set; }

        FunctionDeclarationStatement? CurrentFunction { get; set; }

        Dictionary<string, FunctionDeclarationStatement> Functions = new Dictionary<string, FunctionDeclarationStatement>();

        ReferenceManager ReferenceManager { get; set; }

        public LLVMModuleRef GenerateLLVMModule()
        {
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);

            ReferenceManager = new ReferenceManager(Builder);

            CreateExtern(name: "printf", returnType: LLVMTypeRef.Int32, parameters: [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)], isVarArg: true);
            CreateExtern("llvm.memcpy.p0.p0.i64", LLVMTypeRef.Void, [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int64, LLVMTypeRef.Int1], isVarArg: false);
            CreateExtern("llvm.ubsantrap", LLVMTypeRef.Void, [LLVMTypeRef.Int8], isVarArg: false);

            CreateFunctionReferences();
            EmitStatements(Statements, new List<VariableDeclarationStatement>(), true);

            return Module;
        }

        LLVMTypeRef ResolveLLVMTypeFromExpression(Expression expression, List<VariableDeclarationStatement>? variables)
        {
            if(expression is StringExpression)
            {
                return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            }
            if(expression is NumberExpression numberExpression) {
                if(numberExpression.IsDouble)
                {
                    return LLVMTypeRef.Double;
                }
                else
                {
                    return LLVMTypeRef.Int32;
                }
            }
            if(expression is TypeExpression typeExpression)
            {
                switch(typeExpression.Type)
                {
                    case ReservedTypes.U8:
                    case ReservedTypes.I8:
                        return LLVMTypeRef.Int8;

                    case ReservedTypes.U16:
                    case ReservedTypes.I16:
                        return LLVMTypeRef.Int16;

                    case ReservedTypes.U32:
                    case ReservedTypes.I32:
                        return LLVMTypeRef.Int32;

                    case ReservedTypes.U64:
                    case ReservedTypes.I64:
                        return LLVMTypeRef.Int64;

                    case ReservedTypes.Bool:
                        return LLVMTypeRef.Int1;

                    case ReservedTypes.String:
                        return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

                    case ReservedTypes.Fn:
                        return LLVMTypeRef.Void;

                    case ReservedTypes.F32:
                        return LLVMTypeRef.Float;

                    case ReservedTypes.F64:
                        return LLVMTypeRef.Double;
                }
            }
            if(expression is IdentifierExpression identifierExpression)
            {
                if(Functions.ContainsKey(identifierExpression.Name))
                {
                    return Functions[identifierExpression.Name].LLVMFunctionType;
                }

                List<VariableDeclarationStatement> matchingVariables = variables?.Where(v => v.Name == identifierExpression.Name).ToList() ?? new List<VariableDeclarationStatement>();
                if (matchingVariables.Any())
                {
                    VariableDeclarationStatement variable = matchingVariables.First();
                    return ResolveLLVMTypeFromExpression(variable.Type, variables);
                }

                throw new Exception($"Identifier {identifierExpression.Name} could not be resolved to an LLVM type.");
            }
            if(expression is CallExpression callExpression)
            {
                if(callExpression.Expression is IdentifierExpression callIdentifierExpression)
                {
                    if(Functions.ContainsKey(callIdentifierExpression.Name))
                    {
                        return Functions[callIdentifierExpression.Name].LLVMFunctionType;
                    }
                }

                throw new Exception($"Call expression of type {callExpression.Expression.GetType().Name} is not supported when resolving LLVM types from expressions.");
            }
            if(expression is IndexExpression indexExpression)
            {
                LLVMTypeRef arrayType = ResolveLLVMTypeFromExpression(indexExpression.Expression, variables);

                if(indexExpression.Index is NumberExpression indexNumberExpression)
                {
                    if(indexNumberExpression.IsDouble)
                    {
                        throw new Exception($"Array size cannot be a double.");
                    }
                    else
                    {
                        if(int.TryParse(indexNumberExpression.Value, out int arraySize))
                        {
                            return LLVMTypeRef.CreateArray(arrayType, (uint)arraySize);
                        }
                        throw new Exception($"Could not parse array size '{indexNumberExpression.Value}' as an integer.");
                    }
                }

                throw new Exception($"Array size expression of type {indexExpression.Index.GetType().Name} is not supported when resolving LLVM types from expressions.");
            }
            if(expression is NotExpression notExpression) 
            {                 
                return ResolveLLVMTypeFromExpression(notExpression.Expression, variables);
            }
            if(expression is BooleanExpression booleanExpression)
            {
                return LLVMTypeRef.Int1;
            }
            if(expression is ParenthesizedExpression parenthesizedExpression)
            {
                return ResolveLLVMTypeFromExpression(parenthesizedExpression.Expression, variables);
            }
            if(expression is RelationalExpression relationalExpression)
            {
                return ResolveLLVMTypeFromExpression(relationalExpression.Left, variables);
            }
            if(expression is ArithmeticExpression arithmeticExpression)
            {
                return ResolveLLVMTypeFromExpression(arithmeticExpression.Left, variables);
            }
            if(expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                return LLVMTypeRef.CreatePointer(ResolveLLVMTypeFromExpression(arrayInitializerExpression.Index.Expression, variables), 0);
            }
            if(expression is SizeOfExpression sizeOfExpression)
            {
                return ResolveLLVMTypeFromExpression(sizeOfExpression.Expression, variables);
            }
            if(expression is LengthExpression lengthExpression)
            {
                return ResolveLLVMTypeFromExpression(lengthExpression.Expression, variables);
            }

            throw new Exception($"Expression {expression.GetType().Name} could not be resolved to an LLVM type.");
        }

        void CreateFunctionReferences()
        {
            foreach(FunctionDeclarationStatement functionDeclarationStatement in Statements.OfType<FunctionDeclarationStatement>())
            {
                LLVMTypeRef returnType = ResolveLLVMTypeFromExpression(functionDeclarationStatement.ReturnType, null); // TODO: Hacky solution by using null here.
                LLVMTypeRef[] parameterTypes = functionDeclarationStatement.Parameters.Select(p => ResolveLLVMTypeFromExpression(p.Type, null)).ToArray();
                LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType, parameterTypes, false);

                LLVMValueRef function = Module.AddFunction(functionDeclarationStatement.Name, functionType);

                function.AppendBasicBlock("");

                functionDeclarationStatement.LLVMFunction = function;
                functionDeclarationStatement.LLVMFunctionType = functionType;

                Functions.Add(functionDeclarationStatement.Name, functionDeclarationStatement);
            }
        }

        LLVMValueRef CreateExtern(string name, LLVMTypeRef returnType, LLVMTypeRef[] parameters, bool isVarArg = false)
        {
            LLVMTypeRef externFunctionType = LLVMTypeRef.CreateFunction(returnType, parameters, isVarArg);
            LLVMValueRef externFunction = Module.AddFunction(name, externFunctionType);

            Functions.Add(name, new FunctionDeclarationStatement
            {
                Name = name,

                LLVMFunction = externFunction,
                LLVMFunctionType = externFunctionType
            });

            return externFunction;
        }

        void EmitPanic(string message, ulong code)
        {
            FunctionDeclarationStatement printfFunction = Functions["printf"];

            Builder.BuildCall2(printfFunction.LLVMFunctionType, printfFunction.LLVMFunction, [Builder.BuildGlobalStringPtr($"%s\n"), Builder.BuildGlobalStringPtr(message)], "asdasd".AsSpan());

            FunctionDeclarationStatement panicFunction = Functions["llvm.ubsantrap"];
            Builder.BuildCall2(panicFunction.LLVMFunctionType, panicFunction.LLVMFunction, [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int8, code)]);
        }

        void EmitCallStatement(CallStatement callStatement, List<VariableDeclarationStatement> variables)
        {
            if(callStatement.Expression is IdentifierExpression identifierExpression)
            {
                if(identifierExpression.Name == "logl" || identifierExpression.Name == "log")
                {
                    FunctionDeclarationStatement printfFunction = Functions["printf"];

                    string format = "";

                    foreach(Expression expression in callStatement.Arguments)
                    {
                        LLVMTypeRef argumentType = ResolveLLVMTypeFromExpression(expression, variables);
                        if (argumentType.ToString() == "ptr")
                        {
                            format += "%s";
                        }
                        else
                        {
                            format += "%d"; 
                        }
                    }

                    List<LLVMValueRef> argRefs = [Builder.BuildGlobalStringPtr($"{format}{(identifierExpression.Name == "log" ? "" : "\n")}"), .. EmitExpressions(callStatement.Arguments, variables)];
                    Builder.BuildCall2(printfFunction.LLVMFunctionType, printfFunction.LLVMFunction, argRefs.ToArray(), "");
                    return;
                }

                if (!Functions.ContainsKey(identifierExpression.Name))
                {
                    throw new Exception($"Function {identifierExpression.Name} is not defined.");
                }

                FunctionDeclarationStatement function = Functions[identifierExpression.Name];
                Builder.BuildCall2(function.LLVMFunctionType, function.LLVMFunction, EmitExpressions(callStatement.Arguments, variables), "");
                return;
            }

            throw new Exception($"Call expression of type {callStatement.Expression.GetType().Name} is not supported when emitting LLVM call statements.");
        }

        void EmitFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {
            LLVMBasicBlockRef startBlock = functionDeclarationStatement.LLVMFunction.EntryBasicBlock;

            Builder.PositionAtEnd(startBlock);
            CurrentFunction = functionDeclarationStatement;

            foreach(VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Locals.Where(local => local.IsParameter))
            {
                if(parameter.Expression != null)
                {
                    EmitVariableDeclarationStatement(parameter, functionDeclarationStatement.Body.Locals);
                }
            }

            if (functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Count > 0)
            {
                foreach(VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Locals.Where(local => local.IsParameter))
                {
                    LLVMTypeRef parameterType = ResolveLLVMTypeFromExpression(parameter.Type, functionDeclarationStatement.Body.Locals);
                    parameter.LLVMType = parameterType;
                    parameter.LLVMAlloca = Builder.BuildAlloca(parameterType, $"{parameter.Name}.addr");
                    Builder.BuildStore(CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex), parameter.LLVMAlloca);
                }

                EmitStatements(functionDeclarationStatement.Body.Statements, functionDeclarationStatement.Body.Locals);
            }

            //Builder.PositionAtEnd(functionDeclarationStatement.ReturnBlock);
            if(functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Last() is not ReturnStatement)
            {
                if (functionDeclarationStatement.ReturnType is TypeExpression returnTypeExpressionEnd && returnTypeExpressionEnd.Type == ReservedTypes.Fn)
                {
                    Builder.BuildRetVoid();
                }
                else
                {
                    LLVMValueRef returnValue = Builder.BuildLoad2(ResolveLLVMTypeFromExpression(functionDeclarationStatement.ReturnType, functionDeclarationStatement.Body.Locals), functionDeclarationStatement.ReturnReference);
                    Builder.BuildRet(returnValue);
                }
            }
        }

        void EmitIfStatement(IfStatement ifStatement)
        {
            if (CurrentFunction == null)
            {
                throw new Exception("Current function is not set when emitting if statement.");
            }

            LLVMBasicBlockRef thenBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("if.then");
            LLVMBasicBlockRef elseBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("if.else");
            LLVMBasicBlockRef mergeBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("if.end");

            LLVMValueRef condition = EmitExpression(ifStatement.Condition, ifStatement.Body.Locals);
            Builder.BuildCondBr(condition, thenBlock, elseBlock);

            Builder.PositionAtEnd(thenBlock);
            EmitStatements(ifStatement.Body.Statements, ifStatement.Body.Locals);
            if(ifStatement.Body.Statements.Any() && ifStatement.Body.Statements.Last() is not ReturnStatement)
            {
                Builder.BuildBr(mergeBlock);
            }

            Builder.PositionAtEnd(elseBlock);
            if(ifStatement.ElseIfs != null)
            {
                foreach(IfStatement elseIf in ifStatement.ElseIfs)
                {
                    EmitIfStatement(elseIf);
                }
            }
            if(ifStatement.Else.Statements.Any())
            {
                EmitStatements(ifStatement.Else.Statements, ifStatement.Else.Locals);
            }
            Builder.BuildBr(mergeBlock);

            Builder.PositionAtEnd(mergeBlock);
        }

        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement, List<VariableDeclarationStatement> variables)
        {
            if(variableDeclarationStatement.Expression != null)
            {
                LLVMTypeRef type = ResolveLLVMTypeFromExpression(variableDeclarationStatement.Type, variables);
                LLVMValueRef alloca = Builder.BuildAlloca(type, variableDeclarationStatement.Name);

                LLVMValueRef value = EmitExpression(variableDeclarationStatement.Expression, variables);
                LLVMValueRef store = Builder.BuildStore(value, alloca);

                variableDeclarationStatement.LLVMAlloca = alloca;
                variableDeclarationStatement.LLVMType = type;
            }
        }

        void EmitAssignmentStatement(AssignmentStatement assignmentStatement, List<VariableDeclarationStatement> variables)
        {
            if(assignmentStatement.Variable is IdentifierExpression identifierExpression)
            {
                List<VariableDeclarationStatement> matchingVariables = variables.Where(v => v.Name == identifierExpression.Name).ToList();
                if(matchingVariables.Any())
                {
                    VariableDeclarationStatement variable = matchingVariables.First();

                    if(assignmentStatement.Operator == AssignmentOperator.Equals)
                    {
                        Builder.BuildStore(EmitExpression(assignmentStatement.Expression, variables), variable.LLVMAlloca);
                        return;
                    }

                    LLVMValueRef assignment = EmitArithmeticExpression(new ArithmeticExpression {
                        Left = new IdentifierExpression { Name = variable.Name },
                        Right = assignmentStatement.Expression,
                        Operator = assignmentStatement.Operator == AssignmentOperator.CompoundAdd ? ArithmeticOperator.Addition :
                                   assignmentStatement.Operator == AssignmentOperator.CompoundSubtract ? ArithmeticOperator.Subtraction :
                                   assignmentStatement.Operator == AssignmentOperator.CompoundMultiply ? ArithmeticOperator.Multiplication :
                                   assignmentStatement.Operator == AssignmentOperator.CompoundDivide ? ArithmeticOperator.Division :
                                   assignmentStatement.Operator == AssignmentOperator.CompoundModulo ? ArithmeticOperator.Modulus :
                                   throw new Exception($"Unsupported assignment operator {assignmentStatement.Operator}.")
                    }, variables);

                    Builder.BuildStore(assignment, variable.LLVMAlloca);

                    return;
                }

                throw new Exception($"Variable {identifierExpression.Name} does not exist in the current scope.");
            }

            if (assignmentStatement.Variable is IndexExpression indexExpression)
            {
                if (indexExpression.Expression is IdentifierExpression indexIdentifierExpression)
                {
                    var variable = variables.FirstOrDefault(v => v.Name == indexIdentifierExpression.Name);
                    if (variable != null)
                    {
                        LLVMTypeRef elementType = ResolveLLVMTypeFromExpression(variable.Type, variables);
                        LLVMValueRef index = EmitExpression(indexExpression.Index, variables);

                        LLVMValueRef heapPointer = Builder.BuildLoad2(LLVMTypeRef.CreatePointer(elementType, 0), variable.LLVMAlloca, "ptr.from.stack");

                        LLVMValueRef elementPointer = Builder.BuildInBoundsGEP2(elementType, heapPointer, [index]);

                        LLVMValueRef assignmentValue = EmitExpression(assignmentStatement.Expression, variables);

                        Builder.BuildStore(assignmentValue, elementPointer);

                        return;
                    }
                    throw new Exception($"Variable {indexIdentifierExpression.Name} does not exist.");
                }
            }


            throw new Exception($"{assignmentStatement.Variable.GetType().Name} is not supported as an assignment variable.");
        }

        void EmitReturnStatement(ReturnStatement returnStatement, List<VariableDeclarationStatement> variables)
        {
            if(returnStatement.Expression != null)
            {
                LLVMValueRef returnValue = EmitExpression(returnStatement.Expression, variables);
                Builder.BuildRet(returnValue);
                // Builder.BuildStore(returnValue, CurrentFunction.ReturnReference);
                // Builder.BuildBr(CurrentFunction.ReturnBlock);
            }
            else
            {
                Builder.BuildRetVoid();
            }
        }

        void EmitForStatement(ForStatement forStatement, List<VariableDeclarationStatement> variables)
        {
            if (CurrentFunction == null)
            {
                throw new Exception("Current function is not set when emitting for statement.");
            }

            LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.cond");
            LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.body");
            LLVMBasicBlockRef loopIncrementBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.inc");
            LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.end");


            LLVMTypeRef loopVarType = ResolveLLVMTypeFromExpression(forStatement.Variable.Type, variables);
            forStatement.Variable.LLVMAlloca = Builder.BuildAlloca(loopVarType, forStatement.Variable.Name);

            LLVMValueRef startValue = EmitExpression(forStatement.Range.Start, variables);
            Builder.BuildStore(startValue, forStatement.Variable.LLVMAlloca);

            Builder.BuildBr(loopConditionBlock);


            Builder.PositionAtEnd(loopConditionBlock);
            LLVMValueRef loopVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
            LLVMValueRef endValue = EmitExpression(forStatement.Range.End, variables);
            LLVMValueRef condition = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, loopVar, endValue, "");
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);


            Builder.PositionAtEnd(loopBodyBlock);

            EmitStatements(forStatement.Body.Statements, forStatement.Body.Locals);

            if(forStatement.Body.Statements.Count == 0 || forStatement.Body.Statements.Last() is not ReturnStatement)
            {
                Builder.BuildBr(loopIncrementBlock);
            }


            Builder.PositionAtEnd(loopIncrementBlock);
            LLVMValueRef incrementVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
            LLVMValueRef incrementedValue = Builder.BuildAdd(incrementVar, LLVMValueRef.CreateConstInt(loopVarType, 1, false), "");
            Builder.BuildStore(incrementedValue, forStatement.Variable.LLVMAlloca);
            Builder.BuildBr(loopConditionBlock);


            Builder.PositionAtEnd(loopEndBlock);
        }

        void EmitWhileStatement(WhileStatement whileStatement, List<VariableDeclarationStatement> variables)
        {
            if (CurrentFunction == null)
            {
                throw new Exception("Current function is not set when emitting while statement.");
            }
            LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.cond");
            LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.body");
            LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.end");
            Builder.BuildBr(loopConditionBlock);
            Builder.PositionAtEnd(loopConditionBlock);
            LLVMValueRef condition = EmitExpression(whileStatement.Expression, variables);
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);
            Builder.PositionAtEnd(loopBodyBlock);
            EmitStatements(whileStatement.Body.Statements, whileStatement.Body.Locals);
            if(whileStatement.Body.Statements.Count == 0 || whileStatement.Body.Statements.Last() is not ReturnStatement)
            {
                Builder.BuildBr(loopConditionBlock);
            }
            Builder.PositionAtEnd(loopEndBlock);
        }

        void EmitStatements(StatementList statements, List<VariableDeclarationStatement> variables, bool isUpperScope = false)
        {
            ReferenceManager.EnterScope();
            foreach (Statement statement in statements)
            {
                if(statement is WhileStatement whileStatement)
                {
                    EmitWhileStatement(whileStatement, variables);
                    continue;
                }

                if (statement is ForStatement forStatement)
                {
                    EmitForStatement(forStatement, variables);
                    continue;
                }

                if(statement is ReturnStatement returnStatement)
                {
                    ReferenceManager.ExitScope();
                    EmitReturnStatement(returnStatement, variables);
                    return;
                }

                if(statement is AssignmentStatement assignmentStatement)
                {
                    EmitAssignmentStatement(assignmentStatement, variables);
                    continue;
                }

                if (statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    EmitVariableDeclarationStatement(variableDeclarationStatement, variables);
                    continue;
                }

                if (statement is IfStatement ifStatement)
                {
                    EmitIfStatement(ifStatement);
                    continue;
                }

                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    EmitFunctionDeclarationStatement(functionDeclarationStatement);
                    continue;
                }

                if (statement is CallStatement callStatement)
                {
                    EmitCallStatement(callStatement, variables);
                    continue;
                }


                throw new Exception($"Statement {statement.GetType().Name} is not supported in when emitting LLVM statements.");
            }

            if(!isUpperScope)
            {
                ReferenceManager.ExitScope();
            }
        }

        LLVMValueRef[] EmitExpressions(ExpressionList expressions, List<VariableDeclarationStatement> variables)
        {
            if(expressions == null || expressions.Count == 0)
            {
                return Array.Empty<LLVMValueRef>();
            }

            return expressions.Select(n => EmitExpression(n, variables)).ToArray();
        }

        LLVMValueRef EmitExpression(Expression expression, List<VariableDeclarationStatement> variables, bool reference = false)
        {
            if(expression is StringExpression stringExpression)
            {
                return EmitStringExpression(stringExpression);
            }

            if(expression is NumberExpression numberExpression)
            {
                return EmitNumberExpression(numberExpression);
            }

            if(expression is ArithmeticExpression arithmeticExpression)
            {
                return EmitArithmeticExpression(arithmeticExpression, variables);
            }

            if(expression is RelationalExpression relationalExpression)
            {
                return EmitRelationalExpression(relationalExpression, variables);
            }

            if(expression is IdentifierExpression identifierExpression)
            {
                return EmitIdentifierExpression(identifierExpression, variables, reference);
            }

            if(expression is BooleanExpression booleanExpression)
            {
                return EmitBooleanExpression(booleanExpression);
            }

            if(expression is CallExpression callExpression)
            {
                return EmitCallExpression(callExpression, variables);
            }

            if(expression is ArrayInitializerExpression arrayInitializerExpression)
            {
                return EmitArrayInitializerExpression(arrayInitializerExpression, variables);
            }

            if(expression is IndexExpression indexExpression)
            {
                return EmitIndexExpression(indexExpression, variables);
            }

            if(expression is ParenthesizedExpression parenthesizedExpression)
            {
                return EmitParenthesizedExpression(parenthesizedExpression, variables);
            }

            if(expression is NotExpression notExpression)
            {
                return EmitNotExpression(notExpression, variables);
            }

            if(expression is SizeOfExpression sizeOfExpression)
            {
                return EmitSizeOfExpression(sizeOfExpression, variables);
            }

            if(expression is LengthExpression lengthExpression)
            {
                return EmitLengthExpression(lengthExpression, variables);
            }

            throw new Exception($"Expression {expression.GetType().Name} is not supported when emitting LLVM expressions.");
        }

        LLVMValueRef EmitLengthExpression(LengthExpression lengthExpression, List<VariableDeclarationStatement> variables)
        {
            throw new Exception($"Length operator is not implemented");
        }


        LLVMValueRef EmitSizeOfExpression(SizeOfExpression sizeOfExpression, List<VariableDeclarationStatement> variables)
        {
            throw new Exception($"Length operator is not implemented");
        }

        LLVMValueRef EmitNotExpression(NotExpression notExpression, List<VariableDeclarationStatement> variables)
        {
            LLVMValueRef value = EmitExpression(notExpression.Expression, variables);
            return Builder.BuildNot(value, "not");
        }

        LLVMValueRef EmitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, List<VariableDeclarationStatement> variables)
        {
            return EmitExpression(parenthesizedExpression.Expression, variables);
        }

        LLVMValueRef EmitIndexExpression(IndexExpression indexExpression, List<VariableDeclarationStatement> variables)
        {
            LLVMTypeRef elementType = ResolveLLVMTypeFromExpression(indexExpression.Expression, variables);

            LLVMValueRef variableAddress = EmitExpression(indexExpression.Expression, variables, true);

            LLVMValueRef arrayPtr = Builder.BuildLoad2(LLVMTypeRef.CreatePointer(elementType, 0), variableAddress, "array.ptr.load");

            LLVMValueRef index = EmitExpression(indexExpression.Index, variables);
            LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr, new[] { index }, "element.ptr");

            EmitPanic("Bounds checking is not implemented yet, accessing array element without bounds checking.", 5);

            return Builder.BuildLoad2(elementType, elementPtr, "element.val");
        }

        unsafe LLVMValueRef EmitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression, List<VariableDeclarationStatement> variables)
        {
            var elementType = ResolveLLVMTypeFromExpression(arrayInitializerExpression.Index.Expression, variables);
            LLVMValueRef numElements = EmitExpression(arrayInitializerExpression.Index.Index, variables, false);
            LLVMValueRef numElementsI64 = Builder.BuildIntCast(numElements, LLVMTypeRef.Int64, "num.elements.cast");
            LLVMValueRef sizeInBytes = Builder.BuildMul(numElementsI64, elementType.SizeOf, "malloc.size");

            LLVMValueRef arrayPtr = Builder.BuildMalloc(elementType, "array.ptr");

            LLVMValueRef finalSize = Builder.BuildIntCast(sizeInBytes, LLVMTypeRef.Int32, "final.size");
            arrayPtr.SetOperand(0, finalSize);
            ReferenceManager.AddMalloc(arrayPtr);


            if (arrayInitializerExpression.Array.Expressions != null)
            {
                for (int i = 0; i < arrayInitializerExpression.Array.Expressions.Count; i++)
                {
                    LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr,
                        new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i, false) }, $"array.init.gep.{i}");

                    Builder.BuildStore(EmitExpression(arrayInitializerExpression.Array.Expressions[i], variables), elementPtr);
                }
            }

            return arrayPtr;
        }


        LLVMValueRef EmitCallExpression(CallExpression callExpression, List<VariableDeclarationStatement> variables)
        {
            if(callExpression.Expression is IdentifierExpression identifierExpression)
            {
                if(!Functions.ContainsKey(identifierExpression.Name))
                {
                    throw new Exception($"Function {identifierExpression.Name} is not defined.");
                }
                FunctionDeclarationStatement function = Functions[identifierExpression.Name];

                LLVMValueRef callInstruction = Builder.BuildCall2(function.LLVMFunctionType, function.LLVMFunction, EmitExpressions(callExpression.Arguments, variables), $"call.{identifierExpression.Name}");

                // TODO: Rewrite this so it supports functions with different overloads
                //if(identifierExpression.Name == CurrentFunction.Name)
                //{
                //    callInstruction.IsTailCall = true;
                //    callInstruction.InstructionCallConv = (uint)LLVMCallConv.LLVMFastCallConv;
                //    CurrentFunction.LLVMFunction.FunctionCallConv = (uint)LLVMCallConv.LLVMFastCallConv;
                //}

                return callInstruction;
            }
            throw new Exception($"Call expression of type {callExpression.Expression.GetType().Name} is not supported when emitting LLVM call expressions.");
        }

        LLVMValueRef EmitBooleanExpression(BooleanExpression booleanExpression)
        {
            return booleanExpression.Value ? LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false) : LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0, false);
        }

        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression, List<VariableDeclarationStatement> variables, bool reference = false)
        {
            List<VariableDeclarationStatement> matchingVariables = variables.Where(v => v.Name == identifierExpression.Name).ToList();
            if(matchingVariables.Any())
            {
                VariableDeclarationStatement variable = matchingVariables.First();

                if(reference)
                {
                    return variable.LLVMAlloca;
                }

                return Builder.BuildLoad2(ResolveLLVMTypeFromExpression(variable.Type, variables), variable.LLVMAlloca);
            }

            throw new Exception($"Variable {identifierExpression.Name} does not exist in the current scope.");
        }

        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression, List<VariableDeclarationStatement> variables)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left, variables);
            LLVMValueRef right = EmitExpression(relationalExpression.Right, variables);
            Console.WriteLine("-------------- build relational");
            switch(relationalExpression.Operator)
            {
                case RelationalOperators.Equal:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "icmp");
                case RelationalOperators.NotEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "icmp");
                case RelationalOperators.GreaterThan:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "icmp");
                case RelationalOperators.LessThan:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "icmp");
                case RelationalOperators.GreaterThanOrEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "icmp");
                case RelationalOperators.LessThanOrEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "icmp");
                default:
                    throw new Exception($"Relational operator {relationalExpression.Operator} is not supported when emitting LLVM relational expressions.");
            }
        }

        // TODO: Support power and right shift operators.
        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression, List<VariableDeclarationStatement> variables)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left, variables);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right, variables);
            switch(arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    return Builder.BuildAdd(left, right, "add");
                case ArithmeticOperator.Subtraction:
                    return Builder.BuildSub(left, right, "sub");
                case ArithmeticOperator.Multiplication:
                    return Builder.BuildMul(left, right, "mul");
                case ArithmeticOperator.Division:
                    return Builder.BuildSDiv(left, right, "div");
                case ArithmeticOperator.Modulus:
                    return Builder.BuildSRem(left, right, "mod");
                case ArithmeticOperator.LeftShift:
                    return Builder.BuildShl(left, right, "lshift");
                default:
                    throw new Exception($"Arithmetic operator {arithmeticExpression.Operator} is not supported when emitting LLVM arithmetic expressions.");
            }
        }

        LLVMValueRef EmitNumberExpression(NumberExpression numberExpression)
        {
            if(numberExpression.IsDouble)
            {
                if(double.TryParse(numberExpression.Value, out double result))
                {
                    return LLVMValueRef.CreateConstReal(LLVMTypeRef.Double, result);
                }

                throw new Exception($"Could not parse number expression value '{numberExpression.Value}' as a double.");
            }
            else
            {
                if(int.TryParse(numberExpression.Value, out int result))
                {
                    return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)result, true);
                }

                throw new Exception($"Could not parse number expression value '{numberExpression.Value}' as an integer.");
            }
        }

        LLVMValueRef EmitStringExpression(StringExpression stringExpression)
        {
            return Builder.BuildGlobalStringPtr(stringExpression.Value, "const.str");
        }
    }
}
