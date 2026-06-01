using CommonC.Liveness.Statements;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CommonC.LLVMIR.CodeGen
{
    public class LLVMIRCodeGen
    {
        LLVMIRCodeGenSettings Settings { get; set; }

        /// <summary>
        /// The topmost closure of the tree. Contains all statements, functions, structs and globals.
        /// </summary>
        ClosureStatement UpperClosure { get; set; }

        public LLVMIRCodeGen(LLVMIRCodeGenSettings settings, ClosureStatement closure)
        {
            UpperClosure = closure;
            Settings = settings;
        }

        LLVMModuleRef Module { get; set; }

        LLVMBuilderRef Builder { get; set; }

        LLVMContextRef Context { get; set; }

        FunctionDeclarationStatement? CurrentFunction { get; set; }

        Dictionary<string, FunctionDeclarationStatement> Functions = new Dictionary<string, FunctionDeclarationStatement>();
        Dictionary<string, StructStatement> Structs = new Dictionary<string, StructStatement>();

        ReferenceManager ReferenceManager { get; set; }

        public LLVMModuleRef GenerateLLVMModule()
        {
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);
            Context = Module.Context;
            ReferenceManager = new ReferenceManager(Builder);

            CreateExtern(name: "printf", returnType: LLVMTypeRef.Int32, parameters: [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)], isVarArg: true);
            CreateExtern("llvm.memcpy.p0.p0.i64", LLVMTypeRef.Void, [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int64, LLVMTypeRef.Int1], isVarArg: false);
            CreateExtern("llvm.ubsantrap", LLVMTypeRef.Void, [LLVMTypeRef.Int8], isVarArg: false);

            CreateStructReferences();
            CreateFunctionReferences();
            CreateGlobalReferences();

            EmitStatements(UpperClosure.Statements, new Variables());

            return Module;
        }

        LLVMTypeRef ResolveLLVMTypeFromExpression(Expression expression, Variables? variables)
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

                if(Structs.ContainsKey(identifierExpression.Name))
                {
                    return Structs[identifierExpression.Name].LLVMStructType;
                }

				return ResolveLLVMTypeFromExpression(variables.GetVariable(identifierExpression.Name).Type, variables);
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
                else
                {
                    return ResolveLLVMTypeFromExpression(indexExpression.Expression, variables);
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
            if (expression is MemberExpression memberExpression)
            {
                ExpressionList memberChain = memberExpression.Flatten();
                StructStatement? currentStruct = null;

                if(memberChain.Count <= 0)
                {
                    throw new Exception("Invalid member expression when solving LLVM type, cannot be 0!");
                }

                if(memberChain.First() is IdentifierExpression firstIdentifier)
                {
                    if(variables == null)
                    {
                        throw new Exception($"Variables cannot be null when resolving LLVM type from member expression with identifier {firstIdentifier.Name} as first member.");
                    }

                    VariableDeclarationStatement parentVariable = variables.GetVariable(firstIdentifier.Name);
                    IdentifierExpression? innerIdentifier = GetInnerIdentifierExpression(parentVariable.Type);
                    if(innerIdentifier == null)
                    {
                        throw new Exception($"Could not resolve inner identifier expression for variable {firstIdentifier.Name}.");
                    }

                    currentStruct = Structs[innerIdentifier.Name];
                }

                if(currentStruct == null)
                {
                    throw new Exception($"Could not resolve struct for member expression when resolving LLVM type.");
                }

                foreach(Expression member in memberChain.Skip(1))
                {
                    if(member is IdentifierExpression memberIdentifier)
                    {
                        if(Structs.ContainsKey(memberIdentifier.Name))
                        {
                            currentStruct = Structs[memberIdentifier.Name];
                            continue;
                        }

                        VariableDeclarationStatement field = currentStruct.GetField(memberIdentifier.Name);
                        return ResolveLLVMTypeFromExpression(field.Type, variables); // may cause issues as variables include all variables in the current scope, including parameters and local variables.
                    }
                }
            }

            throw new Exception($"Expression {expression.GetType().Name} could not be resolved to an LLVM type.");
        }

        IdentifierExpression? GetInnerIdentifierExpression(Expression expression)
        {
            return expression switch
            {
                CallExpression expr => GetInnerIdentifierExpression(expr.Expression),
                IndexExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ArithmeticExpression expr => GetInnerIdentifierExpression(expr.Left),
                RelationalExpression expr => GetInnerIdentifierExpression(expr.Left),
                ArrayExpression expr => GetInnerIdentifierExpression(expr.Expressions.Any() ? expr.Expressions.First() : throw new Exception($"Cannot resolve inner identifier expression of empty array")),
                LengthExpression expr => GetInnerIdentifierExpression(expr.Expression),
                MemberExpression expr => GetInnerIdentifierExpression(expr.Parent),
                NegateExpression expr => GetInnerIdentifierExpression(expr.Expression),
                NotExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ObjectInitializerExpression expr => GetInnerIdentifierExpression(expr.Expression),
                ParameterExpression expr => GetInnerIdentifierExpression(expr.Type),
                ParenthesizedExpression expr => GetInnerIdentifierExpression(expr.Expression),
                SizeOfExpression expr => GetInnerIdentifierExpression(expr.Expression),
                UnpackExpression expr => GetInnerIdentifierExpression(expr.Left),
                TypeExpression => null,
                IdentifierExpression expr => expr,
                _ => throw new Exception($"Inner identifier expression of type {expression.GetType().Name} is not supported.")
            };
        }

        void CreateStructReferences()
        {
            foreach(StructStatement structStatement in UpperClosure.Statements.OfType<StructStatement>())
            {
                Structs.Add(structStatement.Name, structStatement);
            }

            foreach(StructStatement structReference in Structs.Values)
            {
                LLVMTypeRef[] fields = structReference.Fields.Select(f => ResolveLLVMTypeFromExpression(f.Type, [])).ToArray();
                structReference.LLVMStructType = LLVMTypeRef.CreateStruct(fields, false);

                // structReference.LLVMStructGlobal = Module.AddGlobal(structReference.LLVMStructType, structReference.Name);
            }
        }

        void CreateFunctionReferences()
        {
            foreach(FunctionDeclarationStatement functionDeclarationStatement in UpperClosure.Statements.OfType<FunctionDeclarationStatement>())
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

        void CreateGlobalReferences()
        {
            foreach(VariableDeclarationStatement variableDeclarationStatement in UpperClosure.Statements.OfType<VariableDeclarationStatement>())
            {
                LLVMTypeRef type = ResolveLLVMTypeFromExpression(variableDeclarationStatement.Type, null);
                variableDeclarationStatement.LLVMType = LLVMTypeRef.CreatePointer(type, 0);
                variableDeclarationStatement.IsGlobal = true;
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

        void EmitCallStatement(CallStatement callStatement, Variables variables)
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
                        
                        switch(argumentType.ToString())
                        {
                            case "ptr":
                                format += "%s";
                                break;

                            case "float":
                            case "double":
                                format += "%g";
                                break;

                            default:
                                format += "%d";
                                break;
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

        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement, Variables variables)
        {
            if (variableDeclarationStatement.IsGlobal)
            {
                LLVMTypeRef globalType = ResolveLLVMTypeFromExpression(variableDeclarationStatement.Type, variables);
                LLVMValueRef global = Module.AddGlobal(globalType, variableDeclarationStatement.Name);

                global.Initializer = LLVMValueRef.CreateConstNull(globalType);
                global.IsGlobalConstant = false;

                variableDeclarationStatement.LLVMAlloca = global;
                variableDeclarationStatement.LLVMType = globalType;

                if (variableDeclarationStatement.Expression != null)
                {
                    if (!Functions.ContainsKey(Settings.EntryPoint))
                    {
                        throw new Exception($"Entry point function {Settings.EntryPoint} does not exist!");
                    }

                    FunctionDeclarationStatement entryPointFunction = Functions[Settings.EntryPoint];
                    Builder.PositionAtEnd(entryPointFunction.LLVMFunction.EntryBasicBlock);

                    LLVMValueRef val = EmitExpression(variableDeclarationStatement.Expression, variables);
                    Builder.BuildStore(val, global);
                }

                return;
            }

            LLVMTypeRef type;

            if (variableDeclarationStatement.Type is IndexExpression indexExpr)
            {
                IdentifierExpression? innerTypeIdent = GetInnerIdentifierExpression(indexExpr);
                if (innerTypeIdent != null && Structs.ContainsKey(innerTypeIdent.Name))
                {
                    type = LLVMTypeRef.CreatePointer(Structs[innerTypeIdent.Name].LLVMStructType, 0);
                }
                else
                {
                    type = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                }
            }
            else
            {
                type = ResolveLLVMTypeFromExpression(variableDeclarationStatement.Type, variables);
            }

            if (variableDeclarationStatement.Expression != null)
            {
                LLVMValueRef value = EmitExpression(variableDeclarationStatement.Expression, variables);

                if(type.StructElementTypesCount > 0)
                {
                    variableDeclarationStatement.LLVMAlloca = value;
                }
                else
                {
                    variableDeclarationStatement.LLVMAlloca = Builder.BuildAlloca(type, variableDeclarationStatement.Name);
                    Builder.BuildStore(value, variableDeclarationStatement.LLVMAlloca);
                }

            }

            variableDeclarationStatement.LLVMType = type;
        }

        void EmitAssignmentStatement(AssignmentStatement assignmentStatement, Variables variables)
        {
            if(assignmentStatement.Variable is IdentifierExpression identifierExpression)
            {
				VariableDeclarationStatement variable = variables.GetVariable(identifierExpression.Name);

				if (assignmentStatement.Operator == AssignmentOperator.Equals)
				{
					Builder.BuildStore(EmitExpression(assignmentStatement.Expression, variables), variable.LLVMAlloca);
					return;
				}

				LLVMValueRef assignment = EmitArithmeticExpression(new ArithmeticExpression
				{
					Left = new IdentifierExpression { Name = variable.Name },
					Right = assignmentStatement.Expression,
					Operator = assignmentStatement.Operator == AssignmentOperator.CompoundAdd ? ArithmeticOperator.Addition :
							   assignmentStatement.Operator == AssignmentOperator.CompoundSubtract ? ArithmeticOperator.Subtraction :
							   assignmentStatement.Operator == AssignmentOperator.CompoundMultiply ? ArithmeticOperator.Multiplication :
							   assignmentStatement.Operator == AssignmentOperator.CompoundDivide ? ArithmeticOperator.Division :
							   assignmentStatement.Operator == AssignmentOperator.CompoundModulo ? ArithmeticOperator.Modulo :
							   throw new Exception($"Unsupported assignment operator {assignmentStatement.Operator}.")
				}, variables);

				Builder.BuildStore(assignment, variable.LLVMAlloca);

				return;

			}

            if (assignmentStatement.Variable is IndexExpression indexExpression)
            {
                if (indexExpression.Expression is IdentifierExpression indexIdentifierExpression)
                {
                    VariableDeclarationStatement variable = variables.GetVariable(indexIdentifierExpression.Name);

					LLVMTypeRef elementType = ResolveLLVMTypeFromExpression(variable.Type, variables);
					LLVMValueRef index = EmitExpression(indexExpression.Index, variables);
					LLVMValueRef heapPointer = Builder.BuildLoad2(LLVMTypeRef.CreatePointer(elementType, 0), variable.LLVMAlloca, "ptr.from.stack");

					LLVMValueRef elementPointer = Builder.BuildInBoundsGEP2(elementType, heapPointer, [index]);
					LLVMValueRef assignmentValue = EmitExpression(assignmentStatement.Expression, variables);

					Builder.BuildStore(assignmentValue, elementPointer);
					return;
                }
            }

            if(assignmentStatement.Variable is MemberExpression memberExpression)
            {
                LLVMValueRef memberPointer = EmitMemberExpression(memberExpression, variables, true);
                LLVMValueRef assignmentValue = EmitExpression(assignmentStatement.Expression, variables);
                Builder.BuildStore(assignmentValue, memberPointer);
                return;
            }


            throw new Exception($"{assignmentStatement.Variable.GetType().Name} is not supported as an assignment variable.");
        }

        void EmitReturnStatement(ReturnStatement returnStatement, Variables variables)
        {
            if(returnStatement.Expression != null)
            {
                if(CurrentFunction != null)
                {
                    if(CurrentFunction.ReturnType is IdentifierExpression returnIdentifier)
                    {
                        if(Structs.ContainsKey(returnIdentifier.Name))
                        {
                            StructStatement structT = Structs[returnIdentifier.Name];

                            LLVMValueRef structReturnValue = EmitExpression(returnStatement.Expression, variables);
                            Builder.BuildRet(structReturnValue);
                            return;
                        }
                    }
                }

                LLVMValueRef returnValue = EmitExpression(returnStatement.Expression, variables);
                Builder.BuildRet(returnValue);
            }
            else
            {
                Builder.BuildRetVoid();
            }
        }

        void EmitForStatement(ForStatement forStatement, Variables variables)
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

        void EmitWhileStatement(WhileStatement whileStatement, Variables variables)
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

        void EmitFreeStatement(FreeStatement freeStatement, Variables variables)
        {
            if(freeStatement.Expression is IdentifierExpression identifier)
            {
                VariableDeclarationStatement variable = variables.GetVariable(identifier.Name);

                Builder.BuildFree(variable.LLVMAlloca);
                return;
            }

            throw new Exception($"Free statement with expression of type {freeStatement.Expression.GetType().Name} is not supported.");
        }

        void EmitStatements(StatementList statements, Variables variables)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FreeStatement freeStatement)
                {
                    EmitFreeStatement(freeStatement, variables);
                    continue;
                }

                if (statement is WhileStatement whileStatement)
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

                if(statement is StructStatement structStatement)
                {
                    continue;
                }


                throw new Exception($"Statement {statement.GetType().Name} is not supported in when emitting LLVM statements.");
            }
        }

        LLVMValueRef[] EmitExpressions(ExpressionList expressions, Variables variables)
        {
            if(expressions == null || expressions.Count == 0)
            {
                return Array.Empty<LLVMValueRef>();
            }

            return expressions.Select(n => EmitExpression(n, variables)).ToArray();
        }

        LLVMValueRef EmitExpression(Expression expression, Variables variables, bool reference = false)
        {
            return expression switch
            {
                StringExpression expr => EmitStringExpression(expr),
                NumberExpression expr => EmitNumberExpression(expr),
                ArithmeticExpression expr => EmitArithmeticExpression(expr, variables),
                RelationalExpression expr => EmitRelationalExpression(expr, variables),
                IdentifierExpression expr => EmitIdentifierExpression(expr, variables, reference),
                BooleanExpression expr => EmitBooleanExpression(expr),
                CallExpression expr => EmitCallExpression(expr, variables),
                ArrayInitializerExpression expr => EmitArrayInitializerExpression(expr, variables),
                IndexExpression expr => EmitIndexExpression(expr, variables),
                ParenthesizedExpression expr => EmitParenthesizedExpression(expr, variables),
                NotExpression expr => EmitNotExpression(expr, variables),
                SizeOfExpression expr => EmitSizeOfExpression(expr, variables),
                LengthExpression expr => EmitLengthExpression(expr, variables),
                ObjectInitializerExpression expr => EmitObjectInitializerExpression(expr, variables),
                MemberExpression expr => EmitMemberExpression(expr, variables, false),
                _ => throw new Exception($"Expression {expression.GetType().Name} is not supported when emitting LLVM expressions.")
            };
        }

        LLVMValueRef EmitMemberExpression(MemberExpression memberExpression, Variables variables, bool isWrite)
        {
            ExpressionList memberChain = memberExpression.Flatten();

            StructStatement? currentStruct = null;
            LLVMValueRef currentPointer = null;
            bool isPointer = false;

            if (memberChain.First() is CallExpression firstMemberCall)
            {
                IdentifierExpression? callIdentifier = GetInnerIdentifierExpression(firstMemberCall) 
                    ?? throw new Exception($"Could not resolve inner identifier of call in member expression."); ;

                if (!Functions.TryGetValue(callIdentifier.Name, out var function))
                {
                    throw new Exception($"Function {callIdentifier.Name} does not exist");
                }

                if (function.ReturnType is IdentifierExpression funcIdentifier)
                {
                    if (!Structs.ContainsKey(funcIdentifier.Name))
                    {
                        throw new Exception($"Struct {funcIdentifier.Name} does not exist.");
                    }

                    currentStruct = Structs[funcIdentifier.Name];
                    currentPointer = EmitCallExpression(firstMemberCall, variables);
                    isPointer = true;
                }
                else
                {
                    throw new Exception($"Cannot access member of parent with type {function.ReturnType.GetType().Name}");
                }
            }

            if(memberChain.First() is IndexExpression firstMemberIndex)
            {
                IdentifierExpression? indexIdentifier = GetInnerIdentifierExpression(firstMemberIndex);

                if (indexIdentifier == null)
                {
                    throw new Exception($"Could not resolve inner identifier of call in member expression.");
                }

                VariableDeclarationStatement matchingVariable = variables.GetVariable(indexIdentifier.Name);

                if (matchingVariable.Type is IdentifierExpression variableIdentifierType)
                {
                    if (!Structs.ContainsKey(variableIdentifierType.Name))
                    {
                        throw new Exception($"Struct {variableIdentifierType.Name} does not exist.");
                    }

                    currentStruct = Structs[variableIdentifierType.Name];
                    currentPointer = EmitIndexExpression(firstMemberIndex, variables);
                    isPointer = false;
					// currentPointer = matchingVariable.LLVMAlloca;
				}
				else if(matchingVariable.Type is IndexExpression variableIndexType) // Here were making sure it recognizes variables declared as arrays
                {
                    IdentifierExpression? variableIndexTypeIdentifier = GetInnerIdentifierExpression(variableIndexType);

                    if(variableIndexTypeIdentifier == null)
                    {
                        throw new Exception($"Could not resolve inner identifier of index in member expression.");
                    }

                    if (!Structs.ContainsKey(variableIndexTypeIdentifier.Name))
                    {
                        throw new Exception($"Struct {variableIndexTypeIdentifier.Name} does not exist.");
                    }

                    currentStruct = Structs[variableIndexTypeIdentifier.Name];
                    currentPointer = EmitIndexExpression(firstMemberIndex, variables);
                    isPointer = false;
					// currentPointer = matchingVariable.LLVMAlloca;
				}
				else
                {
                    throw new Exception($"Cannot access member of parent with type {matchingVariable.Type.GetType().Name}");
                }
            }

            if(memberChain.First() is IdentifierExpression firstMemberIdentifier)
            {
                VariableDeclarationStatement matchingVariable = variables.GetVariable(firstMemberIdentifier.Name);

                if (matchingVariable.Type is IdentifierExpression variableIdentifier)
                {
                    if (!Structs.ContainsKey(variableIdentifier.Name))
                    {
                        throw new Exception($"Struct {variableIdentifier.Name} does not exist.");
                    }

                    currentStruct = Structs[variableIdentifier.Name];
                    currentPointer = matchingVariable.LLVMAlloca;
                    isPointer = false;
                }
                else
                {
                    throw new Exception($"Cannot access member of parent with type {matchingVariable.Type.GetType().Name}");
                }
            }

            if(currentStruct == null || currentPointer == null)
            {
                throw new Exception($"Cannot access member of parent with type {memberChain.First().GetType().Name}");
            }
            
            foreach(Expression member in memberChain.Skip(1))
            {
                if(member is IdentifierExpression memberIdentifier)
                {
                    VariableDeclarationStatement field = currentStruct.GetField(memberIdentifier.Name);

                    if(isPointer)
                    {
                        Builder.BuildExtractValue(currentPointer, (uint)field.FieldIndex, $"{memberIdentifier.Name}_extract_value");
                    }
                    else
                    {
                        var indices = new LLVMValueRef[] {
                                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0),
                                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)
                            };

                        LLVMTypeRef fieldType = ResolveLLVMTypeFromExpression(field.Type, variables);
                        LLVMValueRef fieldPtr = Builder.BuildGEP2(currentStruct.LLVMStructType, currentPointer, indices, $"{memberIdentifier.Name}_field_ptr");

                        if(isWrite && memberChain.Last() == member)
                        {
                            return fieldPtr;
                        }

                        // LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)
                        currentPointer = Builder.BuildLoad2(fieldType, fieldPtr, $"{memberIdentifier.Name}_loaded_field_val");
                    }

                    IdentifierExpression? fieldTypeIdentifier = GetInnerIdentifierExpression(field.Type);
                    if (fieldTypeIdentifier != null && Structs.ContainsKey(fieldTypeIdentifier.Name))
                    {
                        currentStruct = Structs[fieldTypeIdentifier.Name];
                    }
                }
            }

            return currentPointer;
        }
        
        LLVMValueRef EmitObjectInitializerExpression(ObjectInitializerExpression objectInitializerExpression, Variables variables)
        {
            if(objectInitializerExpression.Expression is IdentifierExpression identifier)
            {
                if(!Structs.ContainsKey(identifier.Name))
                {
                    throw new Exception($"Could not initialize object {identifier.Name} as it does not exist.");
                }

                StructStatement structStatement = Structs[identifier.Name];

                LLVMTypeRef structType = structStatement.LLVMStructType;
                LLVMValueRef structPtr = Builder.BuildAlloca(structType, $"{identifier.Name}_struct_instance");
                structStatement.LLVMStructPointer = structPtr;

                foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                {
                    if(propertyAssignment.Variable is IdentifierExpression propertyName)
                    {
                        VariableDeclarationStatement field = structStatement.Fields.GetVariable(propertyName.Name);

						LLVMValueRef val = EmitExpression(propertyAssignment.Expression, variables);
						LLVMValueRef fieldPtr = Builder.BuildGEP2(structType, structPtr, new[] {
								LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0),
								LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)
							}, $"set_{propertyName.Name}_field_ptr");
						Builder.BuildStore(val, fieldPtr);
					}
                    else
                    {
                        throw new Exception($"{propertyAssignment.Variable.GetType().Name} is not supported as a name in object property assignment.");
                    }
                }

                return structPtr;
            }

            throw new Exception($"{objectInitializerExpression.Expression.GetType().Name} is not supported as a object initializer name.");
        }

        LLVMValueRef EmitLengthExpression(LengthExpression lengthExpression, Variables variables)
        {
            throw new Exception($"Length operator is not implemented");
        }


        LLVMValueRef EmitSizeOfExpression(SizeOfExpression sizeOfExpression, Variables variables)
        {
            throw new Exception($"Length operator is not implemented");
        }

        LLVMValueRef EmitNotExpression(NotExpression notExpression, Variables variables)
        {
            LLVMValueRef value = EmitExpression(notExpression.Expression, variables);
            return Builder.BuildNot(value, "not");
        }

        LLVMValueRef EmitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, Variables variables)
        {
            return EmitExpression(parenthesizedExpression.Expression, variables);
        }

        LLVMValueRef EmitIndexExpression(IndexExpression indexExpression, Variables variables)
        {
            LLVMTypeRef elementType = ResolveLLVMTypeFromExpression(indexExpression.Expression, variables);

            LLVMValueRef variableAddress = EmitExpression(indexExpression.Expression, variables, true);

            LLVMValueRef arrayPtr = Builder.BuildLoad2(LLVMTypeRef.CreatePointer(elementType, 0), variableAddress, "array.ptr.load");

            LLVMValueRef index = EmitExpression(indexExpression.Index, variables);
            LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr, new[] { index }, "element.ptr");

            return Builder.BuildLoad2(elementType, elementPtr, "element.val");
        }

        unsafe LLVMValueRef EmitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression, Variables variables)
        {
            var elementType = ResolveLLVMTypeFromExpression(arrayInitializerExpression.Index.Expression, variables);
            LLVMValueRef numElements = EmitExpression(arrayInitializerExpression.Index.Index, variables, false);
            LLVMValueRef numElementsI64 = Builder.BuildIntCast(numElements, LLVMTypeRef.Int64, "num.elements.cast");
            LLVMValueRef sizeInBytes = Builder.BuildMul(numElementsI64, elementType.SizeOf, "malloc.size");

            LLVMValueRef finalSize = Builder.BuildIntCast(sizeInBytes, LLVMTypeRef.Int32, "final.size");
            LLVMValueRef arrayPtr = Builder.BuildMalloc(elementType, "array.ptr");

            arrayPtr.SetOperand(0, finalSize);


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


        LLVMValueRef EmitCallExpression(CallExpression callExpression, Variables variables)
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

        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression, Variables variables, bool reference = false)
        {
            VariableDeclarationStatement variable = variables.GetVariable(identifierExpression.Name);

			if (reference)
			{
				return variable.LLVMAlloca;
			}

			return Builder.BuildLoad2(ResolveLLVMTypeFromExpression(variable.Type, variables), variable.LLVMAlloca);
        }

        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left, variables);
            LLVMValueRef right = EmitExpression(relationalExpression.Right, variables);
            
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
        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left, variables);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right, variables);

            switch(arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    {
                        if(left.TypeOf == LLVMTypeRef.Double
                            || left.TypeOf == LLVMTypeRef.Float)
                        {
                            return Builder.BuildFAdd(left, right, "fadd");
                        }
                        else
                        {
                            return Builder.BuildAdd(left, right, "add");
                        }
                    }
                case ArithmeticOperator.Subtraction:
                    {
                        if (left.TypeOf == LLVMTypeRef.Double
                            || left.TypeOf == LLVMTypeRef.Float)
                        {
                            return Builder.BuildFSub(left, right, "fsub");
                        }
                        else
                        {
                            return Builder.BuildSub(left, right, "sub");
                        }
                    }
                case ArithmeticOperator.Multiplication:
                    {
                        if (left.TypeOf == LLVMTypeRef.Double
                            || left.TypeOf == LLVMTypeRef.Float)
                        {
                            return Builder.BuildFMul(left, right, "fmul");
                        }
                        else
                        {
                            return Builder.BuildMul(left, right, "mul");
                        }
                    }
                case ArithmeticOperator.Division:
                    {
                        if (left.TypeOf == LLVMTypeRef.Double
                            || left.TypeOf == LLVMTypeRef.Float)
                        {
                            return Builder.BuildFDiv(left, right, "fdiv");
                        }
                        else
                        {
                            return Builder.BuildSDiv(left, right, "sdiv");
                        }
                    }
                case ArithmeticOperator.Modulo:
                    {
                        if (left.TypeOf == LLVMTypeRef.Double
                            || left.TypeOf == LLVMTypeRef.Float)
                        {
                            return Builder.BuildFRem(left, right, "fmod");
                        }
                        else
                        {
                            return Builder.BuildSRem(left, right, "mod");
                        }
                    }
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
                if(double.TryParse(numberExpression.Value, CultureInfo.InvariantCulture, out double result))
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
