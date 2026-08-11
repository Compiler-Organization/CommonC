using CommonC.Error;
using CommonC.Liveness.Statements;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CommonC.Targets.LLVM.CodeGen
{
    public class LLVMCodeGen2
    {
        public LLVMContextRef Context { get; private set; }
        public LLVMModuleRef Module { get; private set; }
        public LLVMBuilderRef Builder { get; private set; }

        public CommonCCompilerSettings Settings { get; private set; }

        ClosureStatement CurrentClosure = new ClosureStatement();

        FunctionDeclarationStatement? CurrentFunction = null;

        ClassStatement? CurrentClass = null;

        public LLVMCodeGen2(CommonCCompilerSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(Settings.Name))
                throw new ArgumentException("Production Constraint: Generated assembly module name cannot be blank.", nameof(settings));

            if (string.IsNullOrWhiteSpace(Settings.EntryPoint))
                throw new ArgumentException("Production Constraint: Entry point function name binding cannot be empty.", nameof(settings));

            // For anyone reading, this order of initialization is crucial in saving you one hour of debugging!
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);
            Context = Module.Context;
        }

        /// <summary>
        /// Emits the syntax tree as LLVM IR and returns the compiled module
        /// </summary>
        /// <param name="closure"></param>
        /// <returns></returns>
        public LLVMModuleRef CreateModule(ClosureStatement closure)
        {
            EmitClosure(closure);
            return Module;
        }


        LLVMBasicBlockRef checkpointBlock = null;

        /// <summary>
        /// Sets a checkpoint for the builder's current location
        /// </summary>
        void SetBuilderCheckpoint()
        {
            checkpointBlock = Builder.InsertBlock;
        }

        /// <summary>
        /// Restores the builder's location to the previous checkpoint
        /// </summary>
        void RestoreBuilderCheckpoint()
        {
            if(checkpointBlock == null)
            {
                throw ErrorHandler.CreateError("Cannot restore checkpoint, no checkpoint set!");
            }

            Builder.PositionAtEnd(checkpointBlock);
        }

        /// <summary>
        /// Moves the builder to the absolute start of a function block
        /// </summary>
        void PlaceBuilderAtStartOfFunction()
        {
            if(CurrentFunction == null)
            {
                throw ErrorHandler.CreateError("Cannot place builder at the start of the current function, as the current scope is not within a function.");
            }

            LLVMBasicBlockRef entryBlock = CurrentFunction.LLVMFunction.EntryBasicBlock;
            LLVMValueRef terminator = entryBlock.LastInstruction;

            if (terminator.Handle != IntPtr.Zero && terminator.IsATerminatorInst != null)
            {
                Builder.PositionBefore(terminator);
            }
            else
            {
                Builder.PositionAtEnd(entryBlock);
            }
        }

        /// <summary>
        /// Sets the current closure and returns the previous closure
        /// </summary>
        /// <returns></returns>
        ClosureStatement SetCurrentClosure(ClosureStatement closure)
        {
            ClosureStatement temp = CurrentClosure;
            CurrentClosure = closure;
            return temp;
        }

        /// <summary>
        /// Sets the current closure and emits its statements
        /// </summary>
        /// <param name="closure"></param>
        void EmitClosure(ClosureStatement closure)
        {
            ClosureStatement previousClosure = SetCurrentClosure(closure);

            InitializeFunctions(closure.Functions);

            EmitStatements(closure.Statements);
            SetCurrentClosure(previousClosure);
        }

        void InitializeFunctions(Functions functions)
        {
            foreach(FunctionDeclarationStatement function in functions)
            {
                if (function.LLVMFunction.Handle != IntPtr.Zero)
                    continue;

                Console.WriteLine("----------------------------------------------------------------- " + function.Name);

                LLVMTypeRef returnType = function.ReturnType.TypeAnnotation.ToLLVMType();
                LLVMTypeRef[] parameterTypes = function.Parameters.Select(p => p.Type.TypeAnnotation.ToLLVMType()).ToArray();

                if (function.IsExtern)
                {
                    LLVMTypeRef externFunctionType = LLVMTypeRef.CreateFunction(returnType, parameterTypes, function.Parameters.IsVararg);
                    LLVMValueRef externFunction = Module.AddFunction(function.Name, externFunctionType);

                    function.LLVMFunction = externFunction;
                    function.LLVMFunctionType = externFunctionType;

                    return;
                }

                if (function.Body == null)
                {
                    throw ErrorHandler.CreateError($"Function '{function.Name}' contains no statements", function);
                }

                LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType,
                    function.IsClassFunction
                    ? [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), .. parameterTypes]
                    : parameterTypes,
                    function.Parameters.IsVararg);

                if (function.IsClassFunction)
                {
                    if (CurrentClass == null)
                    {
                        throw ErrorHandler.CreateError($"Function '{function.Name}' is a class function, but exists outside of a class", function);
                    }

                    foreach (VariableDeclarationStatement parameter in function.Body.Variables.Where(v => v.IsParameter))
                    {
                        parameter.ParameterIndex++;
                    }

                    TypeAnnotation typeAnnotation = new TypeAnnotation
                    {
                        IsClass = true,
                        Class = CurrentClass
                    };

                    IdentifierExpression typeExpression = new IdentifierExpression
                    {
                        Name = CurrentClass.Name,
                        TypeAnnotation = typeAnnotation
                    };

                    function.Parameters.Prepend(new ParameterExpression
                    {
                        Name = "this",
                        Type = typeExpression,
                        TypeAnnotation = typeAnnotation
                    });

                    function.Body.Variables.Add(new VariableDeclarationStatement
                    {
                        Name = "this",
                        ParameterIndex = 0,
                        IsParameter = true,
                        Type = typeExpression,
                        TypeAnnotation = typeAnnotation,
                    });

                    function.Name += $"_{CurrentClass.Name}";
                }


                LLVMValueRef llvmFunction = Module.AddFunction(function.Name, functionType);

                llvmFunction.AppendBasicBlock("");

                LLVMValueRef[] parameters = llvmFunction.GetParams();
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i].Name = function.Parameters[i].Name;
                }

                function.LLVMFunction = llvmFunction;
                function.LLVMFunctionType = functionType;
            }
        }

        void EmitStatements(StatementList statements)
        {
            foreach(Statement statement in statements)
            {
                EmitStatement(statement);
            }
        }

        void EmitStatement(Statement statement)
        {
            switch (statement)
            {
                case VariableDeclarationStatement variableDeclarationStatement:
                    EmitVariableDeclarationStatement(variableDeclarationStatement);
                    break;
                case FunctionDeclarationStatement functionDeclarationStatement:
                    EmitFunctionDeclarationStatement(functionDeclarationStatement);
                    break;
                case ClassStatement classStatement:
                    EmitClassStatement(classStatement);
                    break;
                case ReturnStatement returnStatement:
                    EmitReturnStatement(returnStatement);
                    break;
                case CallStatement callStatement:
                    EmitCallStatement(callStatement);
                    break;
                case IfStatement ifStatement:
                    EmitIfStatement(ifStatement);
                    break;
                case AssignmentStatement assignmentStatement:
                    EmitAssignmentStatement(assignmentStatement);
                    break;
                case WhileStatement whileStatement:
                    EmitWhileStatement(whileStatement);
                    break;
                case ForStatement forStatement:
                    EmitForStatement(forStatement);
                    break;
                default:
                    throw ErrorHandler.CreateError($"Statement of type '{statement.GetType().Name}' is not supported.", statement);
            };
        }


        void EmitForStatement(ForStatement forStatement)
        {
            if (CurrentFunction == null)
            {
                throw ErrorHandler.CreateError("Current function is not set when emitting for statement.", forStatement);
            }

            LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.cond");
            LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.body");
            LLVMBasicBlockRef loopIncrementBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.inc");
            LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.end");

            LLVMTypeRef loopVarType = forStatement.Variable.Type.TypeAnnotation.ToLLVMType();

            SetBuilderCheckpoint();
            PlaceBuilderAtStartOfFunction();
            forStatement.Variable.LLVMAlloca = Builder.BuildAlloca(loopVarType, forStatement.Variable.Name);
            RestoreBuilderCheckpoint();

            LLVMValueRef startValue = EmitExpression(forStatement.Range.Start);
            Builder.BuildStore(startValue, forStatement.Variable.LLVMAlloca);

            LLVMValueRef endValue = EmitExpression(forStatement.Range.End);

            Builder.BuildBr(loopConditionBlock);

            Builder.PositionAtEnd(loopConditionBlock);
            LLVMValueRef loopVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
            LLVMValueRef condition = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, loopVar, endValue, "loopcond");
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);

            Builder.PositionAtEnd(loopBodyBlock);
            EmitClosure(forStatement.Body);

            if (Builder.InsertBlock.Terminator == null)
            {
                Builder.BuildBr(loopIncrementBlock);
            }

            Builder.PositionAtEnd(loopIncrementBlock);
            LLVMValueRef incrementVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
            LLVMValueRef incrementedValue = Builder.BuildAdd(incrementVar, LLVMValueRef.CreateConstInt(loopVarType, 1, false), "loopinc");
            Builder.BuildStore(incrementedValue, forStatement.Variable.LLVMAlloca);
            Builder.BuildBr(loopConditionBlock);

            Builder.PositionAtEnd(loopEndBlock);
        }


        /// <summary>
        /// Builds a while loop over a condition.
        /// </summary>
        /// <param name="whileStatement"></param>
        void EmitWhileStatement(WhileStatement whileStatement)
        {
            if (CurrentFunction == null)
            {
                throw ErrorHandler.CreateError("Current function is not set when emitting while statement.", whileStatement);
            }

            LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.cond");
            LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.body");
            LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("while.end");

            Builder.BuildBr(loopConditionBlock);

            Builder.PositionAtEnd(loopConditionBlock);
            LLVMValueRef condition = EmitExpression(whileStatement.Expression);
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);

            Builder.PositionAtEnd(loopBodyBlock);
            EmitClosure(whileStatement.Body);

            if (Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
            {
                Builder.BuildBr(loopConditionBlock);
            }

            Builder.PositionAtEnd(loopEndBlock);
        }


        void EmitAssignmentStatement(AssignmentStatement assignmentStatement)
        {
            LLVMValueRef rightValue = EmitExpression(assignmentStatement.Expression);

            if (rightValue == null)
            {
                throw ErrorHandler.CreateError("Right-hand side expression cannot be a null value.", assignmentStatement);
            }

            if (assignmentStatement.Variable is IndexExpression indexExpr)
            {
                Builder.BuildStore(rightValue, EmitIndexExpressionAddress(indexExpr));
            }
            else if (assignmentStatement.Variable is IdentifierExpression identifierExpr)
            {
                if(!CurrentClosure.Variables.Contains(identifierExpr.Name))
                {
                    throw ErrorHandler.CreateError($"Variable '{identifierExpr}' does not exist in the current scope", assignmentStatement);
                }

                Builder.BuildStore(rightValue, CurrentClosure.Variables.GetVariable(identifierExpr.Name).LLVMAlloca);
                // CurrentClosure.Variables.GetVariable(identifierExpr.Name).LLVMAlloca = rightValue;
            }
            else if (assignmentStatement.Variable is MemberExpression memberExpr)
            {
                throw ErrorHandler.CreateError("Member expression assignments are not supported yet", assignmentStatement);
            }
            else
            {
                throw ErrorHandler.CreateError("Left-hand side of assignment must be an assignable l-value address (variable, array index, or object field).", assignmentStatement);
            }
        }


        /// <summary>
        /// Emits an if statement with the corresponding brancing.
        /// </summary>
        /// <param name="ifStatement"></param>
        void EmitIfStatement(IfStatement ifStatement)
        {
            if (CurrentFunction == null)
            {
                throw ErrorHandler.CreateError("Current function is not set when emitting if statement.", ifStatement);
            }

            LLVMValueRef condition = EmitExpression(ifStatement.Condition);

            if (condition.TypeOf.IntWidth != 1)
            {
                condition = Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, condition, LLVMValueRef.CreateConstInt(condition.TypeOf, 0, false), "if.cond.cast");
            }

            LLVMBasicBlockRef thenBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("if.then");

            bool hasElse = ifStatement.Else != null && ifStatement.Else.Statements.Count > 0;
            LLVMBasicBlockRef elseBlock = hasElse ? CurrentFunction.LLVMFunction.AppendBasicBlock("if.else") : default;

            LLVMBasicBlockRef mergeBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("if.merge");

            Builder.BuildCondBr(condition, thenBlock, hasElse ? elseBlock : mergeBlock);

            Builder.PositionAtEnd(thenBlock);
            EmitClosure(ifStatement.Body);

            if (Builder.InsertBlock.Terminator == null)
            {
                Builder.BuildBr(mergeBlock);
            }

            if (hasElse)
            {
                Builder.PositionAtEnd(elseBlock);
                EmitClosure(ifStatement.Else!);

                if (Builder.InsertBlock.Terminator == null)
                {
                    Builder.BuildBr(mergeBlock);
                }
            }

            Builder.PositionAtEnd(mergeBlock);
        }

        /// <summary>
        /// Emits a call to a declared function or extern with given arguments.
        /// If the call is made to a class, a ptr will be prepended to the arguments with the instance of the class.
        /// </summary>
        /// <param name="callStatement"></param>
        void EmitCallStatement(CallStatement callStatement)
        {
            if (callStatement.Expression is not IdentifierExpression callIdentifier)
            {
                throw ErrorHandler.CreateError($"Call's expression must be of type {nameof(IdentifierExpression)}!", callStatement);
            }

            if (!CurrentClosure.Functions.Contains(callIdentifier.Name))
            {
                throw ErrorHandler.CreateError($"Function '{callIdentifier.Name}' does not exist in the current scope", callStatement);
            }

            FunctionDeclarationStatement function = CurrentClosure.Functions.GetFunction(callIdentifier.Name, callStatement.Arguments, callStatement);

            LLVMValueRef callInst = Builder.BuildCall2(
                Ty: function.LLVMFunctionType,
                Fn: function.LLVMFunction,
                Args: EmitExpressions(callStatement.Arguments)
            );

            if (callIdentifier.Name.Contains('@'))
            {
                callInst.InstructionCallConv = 64;
            }
        }

        /// <summary>
        /// Emits a return statement with a value if an expression is defined
        /// </summary>
        /// <param name="returnStatement"></param>
        void EmitReturnStatement(ReturnStatement returnStatement)
        {
            if (returnStatement.Expression == null)
            {
                Builder.BuildRetVoid();
            }
            else
            {
                Builder.BuildRet(EmitExpression(returnStatement.Expression));
            }
        }

        /// <summary>
        /// Work in progress.
        /// </summary>
        /// <param name="classStatement"></param>
        void EmitClassStatement(ClassStatement classStatement)
        {
            ClassStatement? temp = CurrentClass;
            CurrentClass = classStatement;
           
            foreach(FunctionDeclarationStatement functionDeclarationStatement in classStatement.Body.Functions)
            {
                functionDeclarationStatement.Name = $"{classStatement.Name}.{functionDeclarationStatement.Name}";
                EmitFunctionDeclarationStatement(functionDeclarationStatement);
            }

            CurrentClass = temp;
        }

        /// <summary>
        /// Creates a function with a body.
        /// If the function is an extern, it will be emitted as such.
        /// </summary>
        /// <param name="functionDeclarationStatement"></param>
        void EmitFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {
            if(functionDeclarationStatement.IsExtern)
            {
                return;
            }

            if ((functionDeclarationStatement.Body == null || functionDeclarationStatement.Body.Statements.Count == 0))
            {
                throw ErrorHandler.CreateError("Function contains no statements", functionDeclarationStatement);
            }

            FunctionDeclarationStatement? temp = CurrentFunction;

            CurrentFunction = functionDeclarationStatement;
            Builder.PositionAtEnd(functionDeclarationStatement.LLVMFunction.EntryBasicBlock);

            foreach (VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Variables.Where(local => local.IsParameter))
            {
                //LLVMTypeRef parameterType = parameter.Type.TypeAnnotation.IsStruct
                //        ? LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)
                //        : parameter.Type.TypeAnnotation.ToLLVMType();

                //parameter.LLVMType = parameterType;
                parameter.LLVMAlloca = Builder.BuildAlloca(parameter.Type.TypeAnnotation.ToLLVMType(), $"{parameter.Name}.addr");
                Builder.BuildStore(CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex), parameter.LLVMAlloca);

                //parameter.LLVMAlloca = CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex);
            }

            EmitClosure(functionDeclarationStatement.Body);

            if (functionDeclarationStatement.Body.Statements.Last() is not ReturnStatement)
            {
                if (functionDeclarationStatement.ReturnType is TypeExpression returnTypeExpressionEnd && returnTypeExpressionEnd.Type == ReservedTypes.Fn)
                {
                    Builder.BuildRetVoid();
                }
                else
                {
                    LLVMValueRef returnValue = Builder.BuildLoad2(functionDeclarationStatement.ReturnType.TypeAnnotation.ToLLVMType(), functionDeclarationStatement.ReturnReference);
                    Builder.BuildRet(returnValue);
                }
            }

            CurrentFunction = temp;
        }

        /// <summary>
        /// Emits a variable declaration with the direct value as the initializer.
        /// Allocas for all variables has been removed until a good use case for this has been proved.
        /// </summary>
        /// <param name="variableDeclarationStatement"></param>
        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
        {
            if (variableDeclarationStatement.Expression == null)
            {
                throw ErrorHandler.CreateError($"Variable '{variableDeclarationStatement.Name}' has no initializer", variableDeclarationStatement);
            }

            variableDeclarationStatement.LLVMType = variableDeclarationStatement.TypeAnnotation.ToLLVMType();

            if (variableDeclarationStatement.IsGlobal)
            {
                LLVMTypeRef globalType = variableDeclarationStatement.TypeAnnotation.ToLLVMType();
                LLVMValueRef global = Module.AddGlobal(globalType, variableDeclarationStatement.Name);
                global.IsGlobalConstant = false;

                variableDeclarationStatement.LLVMAlloca = global;

                if (!CurrentClosure.Functions.Contains(Settings.EntryPoint))
                {
                    throw ErrorHandler.CreateError($"Entry point function {Settings.EntryPoint} does not exist!");
                }

                SetBuilderCheckpoint();
                FunctionDeclarationStatement entryPointFunction = CurrentClosure.Functions.GetFunction(Settings.EntryPoint);
                Builder.PositionAtEnd(entryPointFunction.LLVMFunction.EntryBasicBlock);

                LLVMValueRef initializerValue = EmitExpression(variableDeclarationStatement.Expression);
                Builder.BuildStore(initializerValue, variableDeclarationStatement.LLVMAlloca);

                RestoreBuilderCheckpoint();
            }
            else
            {
                SetBuilderCheckpoint();
                PlaceBuilderAtStartOfFunction();

                LLVMValueRef allocaSlot = Builder.BuildAlloca(variableDeclarationStatement.LLVMType, variableDeclarationStatement.Name);
                variableDeclarationStatement.LLVMAlloca = allocaSlot;

                RestoreBuilderCheckpoint();

                LLVMValueRef initializerValue = EmitExpression(variableDeclarationStatement.Expression);
                Builder.BuildStore(initializerValue, variableDeclarationStatement.LLVMAlloca);
            }
        }


        LLVMValueRef[] EmitExpressions(ExpressionList expressions)
            => (expressions == null || expressions.Count == 0) ? [] : expressions.Select(EmitExpression).ToArray();

        /// <summary>
        /// Emits an expression of any type
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        LLVMValueRef EmitExpression(Expression expression)
        {
            return expression switch
            {
                // Literal expressions
                StringExpression expr => EmitStringExpression(expr),
                NumberExpression expr => EmitNumberExpression(expr),
                BooleanExpression expr => EmitBooleanExpression(expr),
                CharacterExpression expr => EmitCharacterExpression(expr),

                // Single expressions
                ParenthesizedExpression expr => EmitParenthesizedExpression(expr),
                IdentifierExpression expr => EmitIdentifierExpression(expr),
                CallExpression expr => EmitCallExpression(expr),
                NegateExpression expr => EmitNegateExpression(expr),
                NullExpression => EmitNullExpression(),
                RelationalExpression expr => EmitRelationalExpression(expr),
                ArithmeticExpression expr => EmitArithmeticExpression(expr),
                IndexExpression expr => EmitIndexExpression(expr),
                ArrayInitializerExpression expr => EmitArrayInitializerExpression(expr),
                NotExpression expr => EmitNotExpression(expr),
                _ => throw ErrorHandler.CreateError($"Expression of type '{expression.GetType().Name}' is not supported.", expression)
            };
        }

        /// <summary>
        /// Emits a not operation, respecting the type its inverting.
        /// </summary>
        /// <param name="notExpression"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        LLVMValueRef EmitNotExpression(NotExpression notExpression)
        {
            LLVMValueRef targetValue = EmitExpression(notExpression.Expression);

            if (targetValue.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Value emission yielded a null reference for {notExpression.GetType().Name}.");
            }

            LLVMTypeRef valueType = targetValue.TypeOf;

            if (valueType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && valueType.IntWidth == 1)
            {
                LLVMValueRef trueConstant = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false);
                return Builder.BuildXor(targetValue, trueConstant, "");
            }

            if (valueType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                LLVMValueRef zeroConstant = LLVMValueRef.CreateConstInt(valueType, 0, false);

                return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, targetValue, zeroConstant, "");
            }

            if (valueType.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                LLVMValueRef nullPointerConstant = LLVMValueRef.CreateConstNull(valueType);
                return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, targetValue, nullPointerConstant, "");
            }

            throw new InvalidOperationException($"Code generator received an unverified unary target type '{valueType.Kind}'. This must be caught in Semantic Analysis.");
        }

        /// <summary>
        /// Creates a new array dynamically using either a declared size or through the element count.
        /// Supports dynamic matrix allocation of any level.
        /// </summary>
        /// <param name="arrayInitializerExpression"></param>
        /// <returns></returns>
        public LLVMValueRef EmitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
        {
            LLVMTypeRef elementType = arrayInitializerExpression.Index.Expression.TypeAnnotation.ToLLVMType(destructArray: true);
            LLVMTypeRef opaquePointerType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

            LLVMValueRef numElements = arrayInitializerExpression.Index.Index == null
                ? LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)arrayInitializerExpression.Array.Expressions.Count, false)
                : EmitExpression(arrayInitializerExpression.Index.Index);

            LLVMValueRef numElementsI64 = Builder.BuildIntCast(numElements, LLVMTypeRef.Int64, "num.elements.cast");

            LLVMValueRef arrayPtr = Builder.BuildArrayMalloc(elementType, numElementsI64, "array.ptr");

            if (arrayInitializerExpression.Array.Expressions != null && arrayInitializerExpression.Array.Expressions.Count > 0)
            {
                for (int i = 0; i < arrayInitializerExpression.Array.Expressions.Count; i++)
                {
                    LLVMValueRef indexValue = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)i, false);
                    LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr, [indexValue], $"array.init.gep.{i}".AsSpan());

                    var childExpr = arrayInitializerExpression.Array.Expressions[i];
                    LLVMValueRef evaluatedVal = EmitExpression(childExpr);

                    Builder.BuildStore(evaluatedVal, elementPtr);
                }
            }
            else if (arrayInitializerExpression.Index.Expression.TypeAnnotation.IsArray)
            {
                LLVMValueRef currentFunc = Builder.InsertBlock.Parent;

                LLVMBasicBlockRef loopCondBB = currentFunc.AppendBasicBlock("matrix.init.cond");
                LLVMBasicBlockRef loopBodyBB = currentFunc.AppendBasicBlock("matrix.init.body");
                LLVMBasicBlockRef loopNextBB = currentFunc.AppendBasicBlock("matrix.init.next");

                LLVMValueRef counterAlloca = Builder.BuildAlloca(LLVMTypeRef.Int64, "matrix.init.i");
                Builder.BuildStore(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 0), counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopCondBB);
                LLVMValueRef currentI = Builder.BuildLoad2(LLVMTypeRef.Int64, counterAlloca, "i.load");
                LLVMValueRef isLess = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, currentI, numElementsI64, "i.lt.size");
                Builder.BuildCondBr(isLess, loopBodyBB, loopNextBB);

                Builder.PositionAtEnd(loopBodyBB);

                var innerIndexExpr = (IndexExpression)arrayInitializerExpression.Index.Expression;

                LLVMValueRef rowAllocationPtr = EmitDynamicSubArrayAllocation(innerIndexExpr);

                LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(opaquePointerType, arrayPtr, [currentI], "matrix.row.gep".AsSpan());
                Builder.BuildStore(rowAllocationPtr, elementPtr);

                LLVMValueRef nextI = Builder.BuildAdd(currentI, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 1), "i.inc");
                Builder.BuildStore(nextI, counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopNextBB);
            }

            return arrayPtr;
        }

        /// <summary>
        /// Helper method for multi-level arrays.
        /// </summary>
        /// <param name="indexExpr"></param>
        /// <returns></returns>
        private LLVMValueRef EmitDynamicSubArrayAllocation(IndexExpression indexExpr)
        {
            LLVMTypeRef elementType = indexExpr.TypeAnnotation.ToLLVMType(destructArray: true);
            LLVMTypeRef opaquePointerType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

            if (indexExpr.Index == null)
            {
                throw ErrorHandler.CreateError("Multidimensional array dynamic allocation requires specified dimension sizes.", indexExpr);
            }

            LLVMValueRef numElements = EmitExpression(indexExpr.Index);
            LLVMValueRef numElementsI64 = Builder.BuildIntCast(numElements, LLVMTypeRef.Int64, "subarray.elements.cast");

            LLVMValueRef subArrayPtr = Builder.BuildArrayMalloc(elementType, numElementsI64, "subarray.ptr");

            if (indexExpr.Expression is IndexExpression nestedInnerIndex)
            {
                LLVMValueRef currentFunc = Builder.InsertBlock.Parent;

                LLVMBasicBlockRef loopCondBB = currentFunc.AppendBasicBlock("submatrix.init.cond");
                LLVMBasicBlockRef loopBodyBB = currentFunc.AppendBasicBlock("submatrix.init.body");
                LLVMBasicBlockRef loopNextBB = currentFunc.AppendBasicBlock("submatrix.init.next");

                LLVMValueRef counterAlloca = Builder.BuildAlloca(LLVMTypeRef.Int64, "submatrix.init.i");
                Builder.BuildStore(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 0), counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopCondBB);
                LLVMValueRef currentI = Builder.BuildLoad2(LLVMTypeRef.Int64, counterAlloca, "i.load");
                LLVMValueRef isLess = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, currentI, numElementsI64, "i.lt.size");
                Builder.BuildCondBr(isLess, loopBodyBB, loopNextBB);

                Builder.PositionAtEnd(loopBodyBB);

                LLVMValueRef deeplyNestedAllocationPtr = EmitDynamicSubArrayAllocation(nestedInnerIndex);

                LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(opaquePointerType, subArrayPtr, [currentI], "submatrix.row.gep".AsSpan());
                Builder.BuildStore(deeplyNestedAllocationPtr, elementPtr);

                LLVMValueRef nextI = Builder.BuildAdd(currentI, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 1), "i.inc");
                Builder.BuildStore(nextI, counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopNextBB);
            }

            return subArrayPtr;
        }

        /// <summary>
        /// Creates a pointer to a element within an array
        /// </summary>
        /// <param name="indexExpression"></param>
        /// <returns></returns>
        public LLVMValueRef EmitIndexExpressionAddress(IndexExpression indexExpression)
        {
            LLVMValueRef arrayPtr;

            if (indexExpression.Expression is IndexExpression nestedIndex)
            {
                LLVMValueRef innerGepAddr = EmitIndexExpressionAddress(nestedIndex);

                LLVMTypeRef pointerType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                arrayPtr = Builder.BuildLoad2(pointerType, innerGepAddr, "array.subptr.load");
            }
            else
            {
                arrayPtr = EmitExpression(indexExpression.Expression);
            }

            LLVMValueRef indexValue = EmitExpression(indexExpression.Index);

            if (indexValue.TypeOf.IntWidth < 64)
            {
                indexValue = Builder.BuildSExt(indexValue, LLVMTypeRef.Int64, "gep.index.sext");
            }

            LLVMTypeRef elementType;
            if (indexExpression.Expression.TypeAnnotation.ReservedType == ReservedTypes.String)
            {
                elementType = LLVMTypeRef.Int8;
            }
            else
            {
                elementType = indexExpression.TypeAnnotation.ToLLVMType();
            }

            return Builder.BuildInBoundsGEP2(
                elementType,
                arrayPtr,
                new[] { indexValue },
                "array.index.gep"
            );
        }

        /// <summary>
        /// Emits a pointer to a element in an array, then builds a load towards it.
        /// </summary>
        /// <param name="indexExpression"></param>
        /// <returns></returns>
        public LLVMValueRef EmitIndexExpression(IndexExpression indexExpression)
        {
            LLVMValueRef elementPtr = EmitIndexExpressionAddress(indexExpression);

            if (elementPtr == null)
            {
                throw ErrorHandler.CreateError("Codegen invariant failure: Failed to resolve index address pointer.", indexExpression);
            }

            LLVMTypeRef elementType;
            if (indexExpression.Expression.TypeAnnotation.ReservedType == ReservedTypes.String)
            {
                elementType = LLVMTypeRef.Int8;
            }
            else
            {
                elementType = indexExpression.TypeAnnotation.ToLLVMType(destructArray: false);
            }

            return Builder.BuildLoad2(elementType, elementPtr, "index.load");
        }



        /// <summary>
        /// Creates an arithmetic expression, respecting the types of each value.
        /// </summary>
        /// <param name="arithmeticExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right);

            if (left == null || right == null)
                throw ErrorHandler.CreateError("Operands cannot be null.", arithmeticExpression);

            UnifyArithmeticOperands(ref left, ref right, arithmeticExpression);

            LLVMTypeRef commonType = left.TypeOf;
            bool isFloat = commonType.Kind == LLVMTypeKind.LLVMFloatTypeKind || commonType.Kind == LLVMTypeKind.LLVMDoubleTypeKind;
            bool isSigned = arithmeticExpression.Left.TypeAnnotation.IsSigned();

            switch (arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    return isFloat ? Builder.BuildFAdd(left, right, "fadd") : Builder.BuildAdd(left, right, "add");

                case ArithmeticOperator.Subtraction:
                    return isFloat ? Builder.BuildFSub(left, right, "fsub") : Builder.BuildSub(left, right, "sub");

                case ArithmeticOperator.Multiplication:
                    return isFloat ? Builder.BuildFMul(left, right, "fmul") : Builder.BuildMul(left, right, "mul");

                case ArithmeticOperator.Division:
                    if (isFloat) 
                        return Builder.BuildFDiv(left, right, "fdiv");

                    return isSigned ? Builder.BuildSDiv(left, right, "sdiv") : Builder.BuildUDiv(left, right, "udiv");

                case ArithmeticOperator.Modulo:
                    if (isFloat) 
                        return Builder.BuildFRem(left, right, "frem");

                    return isSigned ? Builder.BuildSRem(left, right, "srem") : Builder.BuildURem(left, right, "urem");

                case ArithmeticOperator.LeftShift:
                    if (isFloat) 
                        throw ErrorHandler.CreateError("Bitwise left shift is not supported on floating-point types.", arithmeticExpression);

                    return Builder.BuildShl(left, right, "shl");

                case ArithmeticOperator.RightShift:
                    if (isFloat) 
                        throw ErrorHandler.CreateError("Bitwise right shift is not supported on floating-point types.", arithmeticExpression);

                    return isSigned ? Builder.BuildAShr(left, right, "ashr") : Builder.BuildLShr(left, right, "lshr");

                case ArithmeticOperator.Xor:
                    if (isFloat) 
                        throw ErrorHandler.CreateError("Bitwise XOR is not supported on floating-point types.", arithmeticExpression);

                    return Builder.BuildXor(left, right, "xor");

                case ArithmeticOperator.BitwiseAnd:
                    if (isFloat) 
                        throw ErrorHandler.CreateError("Bitwise AND is not supported on floating-point types.", arithmeticExpression);

                    return Builder.BuildAnd(left, right, "and");

                case ArithmeticOperator.BitwiseOr:
                    if (isFloat) 
                        throw ErrorHandler.CreateError("Bitwise OR is not supported on floating-point types.", arithmeticExpression);

                    return Builder.BuildOr(left, right, "or");

                case ArithmeticOperator.Exponentiation:
                    return EmitExponentiationExpression(left, right, arithmeticExpression.Left.TypeAnnotation.IsSigned());

                default:
                    throw ErrorHandler.CreateError($"Unsupported arithmetic operator: {arithmeticExpression.Operator}", arithmeticExpression);
            }
        }

        private void UnifyArithmeticOperands(ref LLVMValueRef left, ref LLVMValueRef right, Expression errorObject)
        {
            LLVMTypeRef leftType = left.TypeOf;
            LLVMTypeRef rightType = right.TypeOf;

            if (leftType == rightType) return;

            if (IsFloatType(leftType) && IsFloatType(rightType))
            {
                if (leftType.Kind == LLVMTypeKind.LLVMFloatTypeKind)
                    left = Builder.BuildFPExt(left, LLVMTypeRef.Double, "fpext.left");
                else
                    right = Builder.BuildFPExt(right, LLVMTypeRef.Double, "fpext.right");
                return;
            }

            if (IsFloatType(leftType) && rightType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                right = Builder.BuildSIToFP(right, leftType, "sitofp.right");
                return;
            }
            if (leftType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && IsFloatType(rightType))
            {
                left = Builder.BuildSIToFP(left, rightType, "sitofp.left");
                return;
            }

            if (leftType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && rightType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                uint leftWidth = leftType.IntWidth;
                uint rightWidth = rightType.IntWidth;

                if (leftWidth < rightWidth)
                    left = Builder.BuildSExt(left, rightType, "sext.left");
                else
                    right = Builder.BuildSExt(right, leftType, "sext.right");
                return;
            }

            throw ErrorHandler.CreateError($"Cannot implicitly unify operand types: {leftType} and {rightType}.", errorObject);
        }

        private bool IsFloatType(LLVMTypeRef type) =>
            type.Kind == LLVMTypeKind.LLVMFloatTypeKind || type.Kind == LLVMTypeKind.LLVMDoubleTypeKind;

        /// <summary>
        /// Helper method for creating exponentiation
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="isSigned"></param>
        /// <returns></returns>
        private LLVMValueRef EmitExponentiationExpression(LLVMValueRef left, LLVMValueRef right, bool isSigned)
        {
            LLVMTypeRef originalType = left.TypeOf;
            bool wasInteger = originalType.Kind == LLVMTypeKind.LLVMIntegerTypeKind;

            LLVMTypeRef targetType = originalType;

            if (wasInteger)
            {
                targetType = originalType.IntWidth <= 32 ? LLVMTypeRef.Float : LLVMTypeRef.Double;

                if (isSigned)
                {
                    left = Builder.BuildSIToFP(left, targetType, "pow.cast.left");
                    right = Builder.BuildSIToFP(right, targetType, "pow.cast.right");
                }
                else
                {
                    left = Builder.BuildUIToFP(left, targetType, "pow.cast.left");
                    right = Builder.BuildUIToFP(right, targetType, "pow.cast.right");
                }
            }

            string intrinsicName = targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind ? "llvm.pow.f32" : "llvm.pow.f64";
            LLVMValueRef powFunc = Module.GetNamedFunction(intrinsicName);
            LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(targetType, [targetType, targetType], false);

            if (powFunc == default)
            {
                powFunc = Module.AddFunction(intrinsicName, funcType);
            }

            LLVMValueRef result = Builder.BuildCall2(funcType, powFunc, [left, right], "pow.res".AsSpan());

            if (wasInteger)
            {
                result = isSigned
                    ? Builder.BuildFPToSI(result, originalType, "pow.cast.back")
                    : Builder.BuildFPToUI(result, originalType, "pow.cast.back");
            }

            return result;
        }

        /// <summary>
        /// Creates a comparison between two values, respecting wether the values are a float/double, integer or pointer.
        /// </summary>
        /// <param name="relationalExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left);
            LLVMValueRef right = EmitExpression(relationalExpression.Right);

            if (left == null || right == null)
            {
                throw ErrorHandler.CreateError("Left or right operand expression evaluated to null.", relationalExpression);
            }

            LLVMTypeKind leftKind = left.TypeOf.Kind;
            LLVMTypeKind rightKind = right.TypeOf.Kind;

            if (leftKind == LLVMTypeKind.LLVMFloatTypeKind || leftKind == LLVMTypeKind.LLVMDoubleTypeKind ||
                rightKind == LLVMTypeKind.LLVMFloatTypeKind || rightKind == LLVMTypeKind.LLVMDoubleTypeKind)
            {
                if (left.TypeOf != right.TypeOf)
                {
                    if (leftKind == LLVMTypeKind.LLVMFloatTypeKind && rightKind == LLVMTypeKind.LLVMDoubleTypeKind)
                        left = Builder.BuildFPExt(left, LLVMTypeRef.Double, "fpext.left");
                    else if (leftKind == LLVMTypeKind.LLVMDoubleTypeKind && rightKind == LLVMTypeKind.LLVMFloatTypeKind)
                        right = Builder.BuildFPExt(right, LLVMTypeRef.Double, "fpext.right");
                }

                switch (relationalExpression.Operator)
                {
                    case RelationalOperators.Equal: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, left, right, "fcmp");
                    case RelationalOperators.NotEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, left, right, "fcmp");
                    case RelationalOperators.GreaterThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, left, right, "fcmp");
                    case RelationalOperators.LessThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, left, right, "fcmp");
                    case RelationalOperators.GreaterThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, left, right, "fcmp");
                    case RelationalOperators.LessThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, left, right, "fcmp");
                    default: throw ErrorHandler.CreateError($"Unsupported float relational operator: {relationalExpression.Operator}", relationalExpression);
                }
            }

            if (leftKind == LLVMTypeKind.LLVMPointerTypeKind || rightKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && left.IsConstant && left.ConstIntZExt == 0)
                    left = Builder.BuildIntToPtr(left, right.TypeOf, "nullptr.cast");
                else if (rightKind == LLVMTypeKind.LLVMIntegerTypeKind && right.IsConstant && right.ConstIntZExt == 0)
                    right = Builder.BuildIntToPtr(right, left.TypeOf, "nullptr.cast");

                if (left.TypeOf != right.TypeOf)
                {
                    throw ErrorHandler.CreateError($"Type mismatch: Cannot compare pointer types {left.TypeOf} and {right.TypeOf}.", relationalExpression);
                }

                switch (relationalExpression.Operator)
                {
                    case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "ptr.icmp");
                    case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "ptr.icmp");
                    default: throw ErrorHandler.CreateError($"Operator {relationalExpression.Operator} is invalid for pointer types.", relationalExpression);
                }
            }

            if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && rightKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                uint leftWidth = left.TypeOf.IntWidth;
                uint rightWidth = right.TypeOf.IntWidth;

                if (leftWidth != rightWidth)
                {
                    if (leftWidth < rightWidth)
                        left = Builder.BuildSExt(left, right.TypeOf, "sext.left");
                    else
                        right = Builder.BuildSExt(right, left.TypeOf, "sext.right");
                }

                switch (relationalExpression.Operator)
                {
                    case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "icmp");
                    case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "icmp");
                    case RelationalOperators.GreaterThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "icmp");
                    case RelationalOperators.LessThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "icmp");
                    case RelationalOperators.GreaterThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "icmp");
                    case RelationalOperators.LessThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "icmp");
                    default: throw ErrorHandler.CreateError($"Unsupported integer relational operator: {relationalExpression.Operator}", relationalExpression);
                }
            }

            throw ErrorHandler.CreateError($"Cannot emit comparison between unhandled types: {leftKind} and {rightKind}.", relationalExpression);
        }



        LLVMValueRef EmitNullExpression()
        {
            LLVMTypeRef opaquePointerType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

            return LLVMValueRef.CreateConstPointerNull(opaquePointerType);
        }

        /// <summary>
        /// Emits a negate on a value, emitting different instructions depending on the type of the value.
        /// </summary>
        /// <param name="negateExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitNegateExpression(NegateExpression negateExpression)
        {
            LLVMValueRef valueToNegate = EmitExpression(negateExpression.Expression);
            TypeAnnotation annotation = negateExpression.Expression.TypeAnnotation;

            if (annotation.IsReservedType)
            {
                switch (annotation.ReservedType)
                {
                    case ReservedTypes.Bool:
                        return Builder.BuildNot(valueToNegate, "logical.not");

                    case ReservedTypes.F32:
                    case ReservedTypes.F64:
                        return Builder.BuildFNeg(valueToNegate, "floating.neg");

                    case ReservedTypes.I8:
                    case ReservedTypes.U8:
                    case ReservedTypes.Char:
                    case ReservedTypes.I16:
                    case ReservedTypes.U16:
                    case ReservedTypes.I32:
                    case ReservedTypes.U32:
                    case ReservedTypes.I64:
                    case ReservedTypes.U64:
                    case ReservedTypes.I128:
                    case ReservedTypes.U128:
                    case ReservedTypes.Ptr:
                        return Builder.BuildNeg(valueToNegate, "integer.neg");

                    case ReservedTypes.String:
                    case ReservedTypes.Fn:
                    default:
                        throw ErrorHandler.CreateError($"Negation operator is invalid for value of type '{annotation.ReservedType}'", negateExpression);
                }
            }

            throw ErrorHandler.CreateError($"Cannot negate a value of type '{annotation.ToString()}'", negateExpression);
        }

        /// <summary>
        /// Determines if a named variable exists in the current scope, then builds a load to the variable and returns it.
        /// </summary>
        /// <param name="identifierExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression)
        {
            if (!CurrentClosure.Variables.Contains(identifierExpression.Name))
            {
                throw ErrorHandler.CreateError($"Variable '{identifierExpression.Name}' does not exist in the current scope", identifierExpression);
            }

            VariableDeclarationStatement variable = CurrentClosure.Variables.GetVariable(identifierExpression.Name);

            LLVMValueRef pointer = variable.LLVMAlloca;

            Console.WriteLine($"{identifierExpression.Name} -> {variable.TypeAnnotation.ToString()}");

            //if(variable.Type.TypeAnnotation.IsArray || variable.TypeAnnotation.IsArray || variable.IsParameter)
            //{
            //    return pointer;
            //}
            //else
            //{
                return Builder.BuildLoad2(variable.Type.TypeAnnotation.ToLLVMType(), pointer, $"{identifierExpression.Name}_val");
            //}


            //return pointer;
        }


        /// <summary>
        /// Emits a call to a declared function or extern with given arguments.
        /// If the call is made to a class, a ptr will be prepended to the arguments with the instance of the class.
        /// </summary>
        /// <param name="callExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitCallExpression(CallExpression callExpression)
        {
            if(callExpression.Expression is not IdentifierExpression callIdentifier)
            {
                throw ErrorHandler.CreateError($"Call's expression must be of type {nameof(IdentifierExpression)}!", callExpression);
            }

            if(!CurrentClosure.Functions.Contains(callIdentifier.Name))
            {
                throw ErrorHandler.CreateError($"Function '{callIdentifier.Name}' does not exist in the current scope", callExpression);
            }

            FunctionDeclarationStatement function = CurrentClosure.Functions.GetFunction(callIdentifier.Name, callExpression.Arguments, callExpression);

            LLVMValueRef callInst = Builder.BuildCall2(
                Ty: function.LLVMFunctionType,
                Fn: function.LLVMFunction,
                Args: EmitExpressions(callExpression.Arguments),
                callIdentifier.Name
            );

            if (callIdentifier.Name.Contains('@'))
            {
                callInst.InstructionCallConv = 64;
            }

            return callInst;
        }

        /// <summary>
        /// Emits a string as a global, returns the pointer to it.
        /// </summary>
        /// <param name="stringExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitStringExpression(StringExpression stringExpression)
        {

            return Builder.BuildGlobalStringPtr(stringExpression.Value);
        }

        /// <summary>
        /// Emits a number as an integer or a float.
        /// </summary>
        /// <param name="numberExpression"></param>
        /// <returns></returns>
        private LLVMValueRef EmitNumberExpression(NumberExpression numberExpression)
        {
            LLVMTypeRef targetType = numberExpression.TypeAnnotation.ToLLVMType();

            if (numberExpression.IsDouble)
            {
                if (!double.TryParse(numberExpression.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    throw ErrorHandler.CreateError(
                        $"Could not parse '{numberExpression.Value}' as an IEEE-754 floating-point literal.", numberExpression);

                if (targetType == LLVMTypeRef.Float
                    && !double.IsInfinity(d) && !double.IsNaN(d)
                    && (d < float.MinValue || d > float.MaxValue))
                    throw ErrorHandler.CreateError(
                        $"Floating-point literal '{numberExpression.Value}' overflows a 32-bit float.", numberExpression);

                return LLVMValueRef.CreateConstReal(targetType, d);
            }
            else
            {
                ulong uval = numberExpression.ToUlong();
                uint bitWidth = targetType.IntWidth;

                if (bitWidth < 64)
                {
                    ulong maxVal = (1UL << (int)bitWidth) - 1;
                    if (uval > maxVal)
                        throw ErrorHandler.CreateError(
                            $"Integer literal '{uval}' overflows a {bitWidth}-bit integer.", numberExpression);
                }

                return LLVMValueRef.CreateConstInt(targetType, uval);
            }
        }

        /// <summary>
        /// Emits the inner expression of a parenthesized expression.
        /// </summary>
        /// <param name="parenthesizedExpression"></param>
        /// <param name="variables"></param>
        /// <returns></returns>
        LLVMValueRef EmitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression)
        {
            if (parenthesizedExpression.Expression == null)
            {
                throw ErrorHandler.CreateError("Parenthesized expression context contains no underlying target AST node.", parenthesizedExpression);
            }

            return EmitExpression(parenthesizedExpression.Expression);
        }

        /// <summary>
        /// Emits a boolean as an int 1.
        /// </summary>
        /// <param name="booleanExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitBooleanExpression(BooleanExpression booleanExpression)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, booleanExpression.Value ? 1UL : 0UL, false);
        }

        /// <summary>
        /// Emits a character as an int 8, converting to its ASCII representation.
        /// </summary>
        /// <param name="characterExpression"></param>
        /// <returns></returns>
        LLVMValueRef EmitCharacterExpression(CharacterExpression characterExpression)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int8, (ulong)characterExpression.Value, false);
        }
    }
}
