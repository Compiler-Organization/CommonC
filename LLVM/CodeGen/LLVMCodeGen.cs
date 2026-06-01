using CommonC.Liveness.Statements;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace CommonC.LLVM.CodeGen
{
    // Rewrite
    public class LLVMCodeGen
    {
        LLVMCodeGenSettings Settings { get; set; }

        /// <summary>
        /// The topmost closure of the tree. Contains all statements, functions, structs and globals.
        /// </summary>
        ClosureStatement UpperClosure { get; set; }

        public LLVMCodeGen(LLVMCodeGenSettings settings, ClosureStatement closure)
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

        public LLVMModuleRef GenerateLLVMModule()
        {
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);
            Context = Module.Context;

            CreateExtern(name: "printf", returnType: LLVMTypeRef.Int32, parameters: [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)], isVarArg: true);
            CreateExtern("llvm.memcpy.p0.p0.i64", LLVMTypeRef.Void, [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int64, LLVMTypeRef.Int1], isVarArg: false);
            CreateExtern("llvm.ubsantrap", LLVMTypeRef.Void, [LLVMTypeRef.Int8], isVarArg: false);

            CreateStructReferences();
            CreateFunctionReferences();
            CreateGlobalReferences();

            EmitStatements(UpperClosure.Statements, new Variables());

            return Module;
        }

        void CreateStructReferences()
        {
            foreach (StructStatement structStatement in UpperClosure.Statements.OfType<StructStatement>())
            {
                Structs.Add(structStatement.Name, structStatement);
            }

            foreach (StructStatement structReference in Structs.Values)
            {
                LLVMTypeRef[] fields = structReference.Fields.Select(f => f.Type.TypeAnnotation.ToLLVMType()).ToArray();
                structReference.LLVMStructType = LLVMTypeRef.CreateStruct(fields, false);
            }
        }

        void CreateFunctionReferences()
        {
            foreach (FunctionDeclarationStatement functionDeclarationStatement in UpperClosure.Statements.OfType<FunctionDeclarationStatement>())
            {
                LLVMTypeRef returnType = functionDeclarationStatement.ReturnType.TypeAnnotation.ToLLVMType();
                LLVMTypeRef[] parameterTypes = functionDeclarationStatement.Parameters.Select(p => p.Type.TypeAnnotation.ToLLVMType()).ToArray();
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
            foreach (VariableDeclarationStatement variableDeclarationStatement in UpperClosure.Statements.OfType<VariableDeclarationStatement>())
            {
                LLVMTypeRef type = variableDeclarationStatement.Type.TypeAnnotation.ToLLVMType();
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

        void EmitClosure(ClosureStatement closure)
        {
            EmitStatements(closure.Statements, closure.Locals);
        }

        void EmitStatements(List<Statement> statements, Variables variables)
        {
            foreach (Statement statement in statements)
            {
                EmitStatement(statement, variables);
            }
        }

        void EmitStatement(Statement statement, Variables variables)
        {
            switch (statement)
            {
                case VariableDeclarationStatement variableDeclarationStatement:
                    EmitVariableDeclarationStatement(variableDeclarationStatement, variables);
                    break;
                case CallStatement callStatement:
                    EmitCallStatement(callStatement, variables);
                    break;
                case FunctionDeclarationStatement functionDeclarationStatement:
                    EmitFunctionDeclarationStatement(functionDeclarationStatement);
                    break;
                case ReturnStatement returnStatement:
                    EmitReturnStatement(returnStatement, variables);
                    break;
                case FreeStatement freeStatement:
                    EmitFreeStatement(freeStatement, variables);
                    break;
                case AssignmentStatement assignmentStatement:
                    EmitAssignmentStatement(assignmentStatement, variables);
                    break;
                case StructStatement structStatement:
                    break;
                case WhileStatement whileStatement:
                    EmitWhileStatement(whileStatement, variables);
                    break;
                case ForStatement forStatement:
                    EmitForStatement(forStatement, variables);
                    break;
                case IfStatement ifStatement:
                    EmitIfStatement(ifStatement, variables);
                    break;
                default:
                    throw new Exception($"Unsupported statement type: {statement.GetType().Name}");
            }
        }

        void EmitIfStatement(IfStatement ifStatement, Variables variables)
        {
            if (CurrentFunction == null)
            {
                throw new Exception("Current function is not set when emitting if statement.");
            }

            LLVMValueRef condition = EmitExpression(ifStatement.Condition, variables);

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
            EmitStatements(ifStatement.Body.Statements, ifStatement.Body.Locals);

            if (Builder.InsertBlock.Terminator == null)
            {
                Builder.BuildBr(mergeBlock);
            }

            if (hasElse)
            {
                Builder.PositionAtEnd(elseBlock);
                EmitStatements(ifStatement.Else.Statements, ifStatement.Else.Locals);

                if (Builder.InsertBlock.Terminator == null)
                {
                    Builder.BuildBr(mergeBlock);
                }
            }

            Builder.PositionAtEnd(mergeBlock);
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

            LLVMTypeRef loopVarType = forStatement.Variable.Type.TypeAnnotation.ToLLVMType();
            forStatement.Variable.LLVMAlloca = Builder.BuildAlloca(loopVarType, forStatement.Variable.Name);

            LLVMValueRef startValue = EmitExpression(forStatement.Range.Start, variables);
            Builder.BuildStore(startValue, forStatement.Variable.LLVMAlloca);

            LLVMValueRef endValue = EmitExpression(forStatement.Range.End, variables);

            Builder.BuildBr(loopConditionBlock);

            Builder.PositionAtEnd(loopConditionBlock);
            LLVMValueRef loopVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
            LLVMValueRef condition = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, loopVar, endValue, "loopcond");
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);

            Builder.PositionAtEnd(loopBodyBlock);
            EmitStatements(forStatement.Body.Statements, forStatement.Body.Locals);

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
            if (whileStatement.Body.Statements.Count == 0 || whileStatement.Body.Statements.Last() is not ReturnStatement)
            {
                Builder.BuildBr(loopConditionBlock);
            }
            Builder.PositionAtEnd(loopEndBlock);
        }

        void EmitAssignmentStatement(AssignmentStatement assignmentStatement, Variables variables)
        {
            LLVMValueRef valueToStore = EmitExpression(assignmentStatement.Expression, variables);
            LLVMValueRef destinationPointer = EmitLValueAddress(assignmentStatement.Variable, variables);

            LLVMTypeRef destType = destinationPointer.TypeOf.ElementType;
            if (destType.Kind == LLVMTypeKind.LLVMStructTypeKind)
            {
                uint elementCount = destType.StructElementTypesCount;
                for (uint i = 0; i < elementCount; i++)
                {
                    LLVMValueRef fieldSrcPtr = Builder.BuildGEP2(destType, valueToStore, [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i)], $"assign.src.field.{i}".AsSpan());
                    LLVMValueRef fieldDstPtr = Builder.BuildGEP2(destType, destinationPointer, [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i)], $"assign.dst.field.{i}".AsSpan()); 
                    LLVMValueRef value = Builder.BuildLoad2(destType.StructGetTypeAtIndex(i), fieldSrcPtr, $"assign.ld.{i}"); Builder.BuildStore(value, fieldDstPtr);
                }
            }
            else 
            { 
                Builder.BuildStore(valueToStore, destinationPointer); 
            }
        }


        void EmitFreeStatement(FreeStatement freeStatement, Variables variables)
        {
            LLVMValueRef target = EmitExpression(freeStatement.Expression, variables);
            Builder.BuildFree(target);
        }

        void EmitReturnStatement(ReturnStatement returnStatement, Variables variables)
        {
            if (returnStatement.Expression != null)
            {
                LLVMValueRef returnValue = EmitExpression(returnStatement.Expression, variables);

                Builder.BuildRet(returnValue);
            }
            else
            {
                if (CurrentFunction != null)
                {
                    LLVMTypeRef expectedReturnType = CurrentFunction.ReturnType.TypeAnnotation.ToLLVMType();
                    if (expectedReturnType != LLVMTypeRef.Void)
                    {
                        throw new InvalidOperationException(
                            $"Cannot return void from function '{CurrentFunction.Name}' which expects a {expectedReturnType} return type."
                        );
                    }
                }

                Builder.BuildRetVoid();
            }
        }



        void EmitFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {
            LLVMBasicBlockRef startBlock = functionDeclarationStatement.LLVMFunction.EntryBasicBlock;

            Builder.PositionAtEnd(startBlock);
            CurrentFunction = functionDeclarationStatement;

            foreach (VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Locals.Where(local => local.IsParameter))
            {
                if (parameter.Expression != null)
                {
                    EmitVariableDeclarationStatement(parameter, functionDeclarationStatement.Body.Locals);
                }
            }

            if (functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Count > 0)
            {
                foreach (VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Locals.Where(local => local.IsParameter))
                {
                    LLVMTypeRef parameterType = parameter.Type.TypeAnnotation.ToLLVMType();
                    parameter.LLVMType = parameterType;
                    parameter.LLVMAlloca = Builder.BuildAlloca(parameterType, $"{parameter.Name}.addr");
                    Builder.BuildStore(CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex), parameter.LLVMAlloca);
                }

                EmitStatements(functionDeclarationStatement.Body.Statements, functionDeclarationStatement.Body.Locals);
            }

            if (functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Last() is not ReturnStatement)
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
        }

        void EmitLog(CallStatement callStatement, Variables variables, bool newLine)
        {
            FunctionDeclarationStatement printfFunction = Functions["printf"];

            string format = "";

            foreach (Expression expression in callStatement.Arguments)
            {
                LLVMTypeRef argumentType = expression.TypeAnnotation.ToLLVMType(true);

                switch (argumentType.ToString())
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

            List<LLVMValueRef> argRefs = [Builder.BuildGlobalStringPtr($"{format}{(newLine ? "\n" : "")}"), .. EmitExpressions(callStatement.Arguments, variables)];
            Builder.BuildCall2(printfFunction.LLVMFunctionType, printfFunction.LLVMFunction, argRefs.ToArray(), "");
            return;
        }

        void EmitCallStatement(CallStatement callStatement, Variables variables)
        {
            if(callStatement.Expression is IdentifierExpression identifierExpression)
            {
                if (identifierExpression.Name is "log" or "logl")
                {
                    EmitLog(callStatement, variables, identifierExpression.Name == "logl");
                    return;
                }

                if (!Functions.TryGetValue(identifierExpression.Name, out FunctionDeclarationStatement? functionDecl))
                {
                    throw new InvalidOperationException($"Function '{identifierExpression.Name}' is not defined.");
                }

                LLVMValueRef[] arguments = callStatement.Arguments == null 
                    ? Array.Empty<LLVMValueRef>() 
                    : callStatement.Arguments
                        .Select(argExpr => EmitExpression(argExpr, variables))
                        .ToArray();

                Builder.BuildCall2(
                    functionDecl.LLVMFunctionType,
                    functionDecl.LLVMFunction,
                    arguments,
                    functionDecl.LLVMFunctionType.ReturnType == LLVMTypeRef.Void 
                        ? "" 
                        : $"{identifierExpression.Name}_call"
                );
            }
        }

        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclaration, Variables variables)
        {
            if (variableDeclaration.IsGlobal)
            {
                LLVMTypeRef globalType = variableDeclaration.Type.TypeAnnotation.ToLLVMType();
                LLVMValueRef global = Module.AddGlobal(globalType, variableDeclaration.Name);

                global.Initializer = LLVMValueRef.CreateConstNull(globalType);
                global.IsGlobalConstant = false;

                variableDeclaration.LLVMAlloca = global;
                variableDeclaration.LLVMType = globalType;

                if (variableDeclaration.Expression != null)
                {
                    if (!Functions.ContainsKey(Settings.EntryPoint))
                    {
                        throw new Exception($"Entry point function {Settings.EntryPoint} does not exist!");
                    }

                    LLVMBasicBlockRef previousBlock = Builder.InsertBlock;

                    FunctionDeclarationStatement entryPointFunction = Functions[Settings.EntryPoint];

                    Builder.PositionAtEnd(entryPointFunction.LLVMFunction.EntryBasicBlock);

                    LLVMValueRef val = EmitExpression(variableDeclaration.Expression, variables);

                    if (val.TypeOf != globalType)
                    {
                        val = CoerceType(val, globalType, "global.init.cast");
                    }

                    Builder.BuildStore(val, global);

                    if (previousBlock != default)
                    {
                        Builder.PositionAtEnd(previousBlock);
                    }
                }

                return;
            }

            if (CurrentFunction == null)
            {
                throw new Exception($"Cannot declare local variable '{variableDeclaration.Name}' outside of a function context.");
            }

            LLVMTypeRef varType = variableDeclaration.Type.TypeAnnotation.ToLLVMType();

            LLVMBasicBlockRef currentBlock = Builder.InsertBlock;
            LLVMBasicBlockRef entryBlock = CurrentFunction.LLVMFunction.EntryBasicBlock;

            if (entryBlock.FirstInstruction != default)
            {
                Builder.PositionBefore(entryBlock.FirstInstruction);
            }
            else
            {
                Builder.PositionAtEnd(entryBlock);
            }

            LLVMValueRef allocaPtr = Builder.BuildAlloca(varType, variableDeclaration.Name);
            variableDeclaration.LLVMAlloca = allocaPtr;

            Builder.PositionAtEnd(currentBlock);

            if (variableDeclaration.Expression != null)
            {
                LLVMValueRef initValue = EmitExpression(variableDeclaration.Expression, variables);

                if (initValue.TypeOf != varType)
                {
                    initValue = CoerceType(initValue, varType, "local.init.cast");
                }

                Builder.BuildStore(initValue, allocaPtr);
            }
        }

        // Helper method to resolve casting between types safely
        private LLVMValueRef CoerceType(LLVMValueRef value, LLVMTypeRef targetType, string name)
        {
            if (value.TypeOf.Kind == LLVMTypeKind.LLVMIntegerTypeKind && targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                uint sourceWidth = value.TypeOf.IntWidth;
                uint targetWidth = targetType.IntWidth;

                if (sourceWidth < targetWidth)
                    return Builder.BuildSExt(value, targetType, name); // Assuming signed by default
                if (sourceWidth > targetWidth)
                    return Builder.BuildTrunc(value, targetType, name);
            }
            throw new Exception($"Implicit type conversion from {value.TypeOf} to {targetType} is unsupported.");
        }



        LLVMValueRef EmitStringExpression(StringExpression stringExpression)
        {
            return Builder.BuildGlobalStringPtr(stringExpression.Value);
        }

        // Rewrite this so it accruately creates a number given the expression using it
        LLVMValueRef EmitNumberExpression(NumberExpression numberExpression)
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;

            if (numberExpression.IsDouble)
            {
                double doubleValue = double.Parse(numberExpression.Value, culture);
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Double, doubleValue);
            }

            LLVMTypeRef integerType = LLVMTypeRef.Int32;
            bool isSigned = true;

            if (long.TryParse(numberExpression.Value, culture, out long signedValue))
            {
                if (signedValue < int.MinValue || signedValue > int.MaxValue)
                {
                    integerType = LLVMTypeRef.Int64;
                }
                return LLVMValueRef.CreateConstInt(integerType, (ulong)signedValue, isSigned);
            }
            else if (ulong.TryParse(numberExpression.Value, culture, out ulong unsignedValue))
            {
                integerType = LLVMTypeRef.Int64;
                isSigned = false;
                return LLVMValueRef.CreateConstInt(integerType, unsignedValue, isSigned);
            }

            throw new FormatException($"Invalid literal numeric format encountered: '{numberExpression.Value}'");
        }


        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression, Variables variables)
        {
            VariableDeclarationStatement variable = variables.GetVariable(identifierExpression.Name);

            LLVMValueRef pointer = variable.LLVMAlloca;
            LLVMTypeRef valueType = variable.Type.TypeAnnotation.ToLLVMType();

            return Builder.BuildLoad2(valueType, pointer, $"{identifierExpression.Name}_val");
        }

        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left, variables);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right, variables);

            if (left == null || right == null)
            {
                throw new Exception("Left or right operand expression evaluated to null.");
            }

            UnifyArithmeticOperands(ref left, ref right);

            LLVMTypeRef commonType = left.TypeOf;
            bool isFloat = commonType.Kind == LLVMTypeKind.LLVMFloatTypeKind || commonType.Kind == LLVMTypeKind.LLVMDoubleTypeKind;

            switch (arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    return isFloat ? Builder.BuildFAdd(left, right, "fadd") : Builder.BuildAdd(left, right, "add");

                case ArithmeticOperator.Subtraction:
                    return isFloat ? Builder.BuildFSub(left, right, "fsub") : Builder.BuildSub(left, right, "sub");

                case ArithmeticOperator.Multiplication:
                    return isFloat ? Builder.BuildFMul(left, right, "fmul") : Builder.BuildMul(left, right, "mul");

                case ArithmeticOperator.Division:
                    return isFloat ? Builder.BuildFDiv(left, right, "fdiv") : Builder.BuildSDiv(left, right, "sdiv");

                case ArithmeticOperator.Modulo:
                    return isFloat ? Builder.BuildFRem(left, right, "frem") : Builder.BuildSRem(left, right, "srem");

                case ArithmeticOperator.LeftShift:
                    if (isFloat) throw new Exception("Left shift operator is not supported on floating-point types.");
                    return Builder.BuildShl(left, right, "shl");

                case ArithmeticOperator.RightShift:
                    if (isFloat) throw new Exception("Right shift operator is not supported on floating-point types.");
                    return Builder.BuildAShr(left, right, "ashr");

                case ArithmeticOperator.Exponential:
                    return EmitPowerExpression(left, right, commonType);

                default:
                    throw new Exception($"Arithmetic operator {arithmeticExpression.Operator} is not supported when emitting LLVM arithmetic expressions.");
            }
        }

        private void UnifyArithmeticOperands(ref LLVMValueRef left, ref LLVMValueRef right)
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

            throw new Exception($"Cannot implicitly unify operand types: {leftType} and {rightType}.");
        }

        private bool IsFloatType(LLVMTypeRef type) =>
            type.Kind == LLVMTypeKind.LLVMFloatTypeKind || type.Kind == LLVMTypeKind.LLVMDoubleTypeKind;

        private LLVMValueRef EmitPowerExpression(LLVMValueRef left, LLVMValueRef right, LLVMTypeRef targetType)
        {
            bool wasInteger = targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind;
            if (wasInteger)
            {
                left = Builder.BuildSIToFP(left, LLVMTypeRef.Double, "pow.cast.left");
                right = Builder.BuildSIToFP(right, LLVMTypeRef.Double, "pow.cast.right");
                targetType = LLVMTypeRef.Double;
            }

            string intrinsicName = targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind ? "llvm.pow.f32" : "llvm.pow.f64";
            LLVMValueRef powFunc = Module.GetNamedFunction(intrinsicName);

            if (powFunc == default)
            {
                LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(targetType, [targetType, targetType], false);
                powFunc = Module.AddFunction(intrinsicName, funcType);
            }

            LLVMValueRef result = Builder.BuildCall2(targetType, powFunc, [left, right], "pow.res".AsSpan());

            if (wasInteger)
            {
                result = Builder.BuildFPToSI(result, LLVMTypeRef.Int32, "pow.cast.back");
            }

            return result;
        }


        LLVMValueRef EmitCallExpression(CallExpression callExpression, Variables variables)
        {
            if (callExpression.Expression is IdentifierExpression identifierExpression)
            {
                if (!Functions.TryGetValue(identifierExpression.Name, out FunctionDeclarationStatement? functionDecl))
                {
                    throw new InvalidOperationException($"Function '{identifierExpression.Name}' is not defined.");
                }

                LLVMValueRef[] arguments = callExpression.Arguments == null
                    ? Array.Empty<LLVMValueRef>()
                    : callExpression.Arguments
                        .Select(argExpr => EmitExpression(argExpr, variables))
                        .ToArray();

                return Builder.BuildCall2(
                    functionDecl.LLVMFunctionType,
                    functionDecl.LLVMFunction,
                    arguments,
                    identifierExpression.Name + "_call"
                );
            }
            else
            {
                throw new Exception("Unsupported function expression type in call: " + callExpression.Expression.GetType().Name);
            }
        }

        LLVMValueRef EmitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression, Variables variables)
        {
            LLVMTypeRef elementType = arrayInitializerExpression.Index.Expression.TypeAnnotation.ToLLVMType();

            LLVMValueRef numElements = arrayInitializerExpression.Index.Index == null
                ? LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)arrayInitializerExpression.Array.Expressions.Count, false)
                : EmitExpression(arrayInitializerExpression.Index.Index, variables);

            LLVMValueRef numElementsI32 = Builder.BuildIntCast(numElements, LLVMTypeRef.Int32, "num.elements.cast");

            LLVMValueRef arrayPtr = Builder.BuildArrayMalloc(elementType, numElementsI32, "array.ptr");

            if (arrayInitializerExpression.Array.Expressions != null && arrayInitializerExpression.Array.Expressions.Count > 0)
            {
                for (int i = 0; i < arrayInitializerExpression.Array.Expressions.Count; i++)
                {
                    LLVMValueRef indexValue = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i, false);
                    LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr, [indexValue]);

                    var childExpr = arrayInitializerExpression.Array.Expressions[i];
                    LLVMValueRef evaluatedVal = childExpr is ArrayInitializerExpression nestedInitializer
                        ? EmitArrayInitializerExpression(nestedInitializer, variables)
                        : EmitExpression(childExpr, variables);

                    Builder.BuildStore(evaluatedVal, elementPtr);
                }
            }
            else if (elementType == LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0) || elementType.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                LLVMValueRef currentFunc = Builder.InsertBlock.Parent;

                LLVMBasicBlockRef loopCondBB = currentFunc.AppendBasicBlock("matrix.init.cond");
                LLVMBasicBlockRef loopBodyBB = currentFunc.AppendBasicBlock("matrix.init.body");
                LLVMBasicBlockRef loopNextBB = currentFunc.AppendBasicBlock("matrix.init.next");

                LLVMValueRef counterAlloca = Builder.BuildAlloca(LLVMTypeRef.Int32, "matrix.init.i");
                Builder.BuildStore(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopCondBB);
                LLVMValueRef currentI = Builder.BuildLoad2(LLVMTypeRef.Int32, counterAlloca, "i.load");
                LLVMValueRef isLess = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, currentI, numElementsI32, "i.lt.size");
                Builder.BuildCondBr(isLess, loopBodyBB, loopNextBB);

                Builder.PositionAtEnd(loopBodyBB);

                var innerIndexExpr = (IndexExpression)arrayInitializerExpression.Index.Expression;

                var subInitializerPlaceholder = new ArrayInitializerExpression
                {
                    Index = innerIndexExpr,
                    Array = new ArrayExpression { Expressions = new ExpressionList() }
                };

                LLVMValueRef rowAllocationPtr = EmitArrayInitializerExpression(subInitializerPlaceholder, variables);

                LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(elementType, arrayPtr, [currentI]);
                Builder.BuildStore(rowAllocationPtr, elementPtr);

                LLVMValueRef nextI = Builder.BuildAdd(currentI, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "i.inc");
                Builder.BuildStore(nextI, counterAlloca);
                Builder.BuildBr(loopCondBB);

                Builder.PositionAtEnd(loopNextBB);
            }

            return arrayPtr;
        }

        LLVMValueRef EmitLValueAddress(Expression expression, Variables variables)
        {
            return expression switch
            {
                IdentifierExpression id => variables.GetVariable(id.Name).LLVMAlloca,
                IndexExpression index => EmitIndexExpressionAddress(index, variables),
                MemberExpression mem => EmitMemberExpressionAddress(mem, variables),

                _ => throw new NotSupportedException($"Expression type '{expression.GetType().Name}' is not a valid L-Value target.")
            };
        }

        LLVMValueRef EmitIndexExpressionAddress(IndexExpression indexExpression, Variables variables)
        {
            LLVMValueRef arrayPtr;

            if (indexExpression.Expression is IndexExpression nestedIndex)
            {
                LLVMValueRef innerGepAddr = EmitIndexExpressionAddress(nestedIndex, variables);

                LLVMTypeRef intermediateType = nestedIndex.TypeAnnotation.ToLLVMType();
                arrayPtr = Builder.BuildLoad2(intermediateType, innerGepAddr, "array.subptr.load");
            }
            else
            {
                arrayPtr = EmitExpression(indexExpression.Expression, variables);
            }

            LLVMValueRef indexValue = EmitExpression(indexExpression.Index, variables);

            LLVMTypeRef elementType = indexExpression.TypeAnnotation.ToLLVMType(destructArray: true);

            return Builder.BuildInBoundsGEP2(
                elementType,
                arrayPtr,
                new[] { indexValue },
                "array.index.gep"
            );
        }

        LLVMValueRef EmitIndexExpression(IndexExpression indexExpression, Variables variables)
        {
            LLVMValueRef elementPtr = EmitIndexExpressionAddress(indexExpression, variables);
            LLVMTypeRef elementType = indexExpression.Expression.TypeAnnotation.ToLLVMType(destructArray: true);

            return Builder.BuildLoad2(elementType, elementPtr, "array.index.load");
        }

        LLVMValueRef EmitArrayExpression(ArrayExpression arrayExpression, Variables variables)
        {
            LLVMTypeRef elementType = arrayExpression.TypeAnnotation.ToLLVMType(destructArray: true);

            LLVMValueRef numElements = LLVMValueRef.CreateConstInt(
                LLVMTypeRef.Int32,
                (ulong)arrayExpression.Expressions.Count,
                false
            );

            LLVMValueRef arrayPtr = Builder.BuildArrayMalloc(elementType, numElements, "array.expr.ptr");

            for (int i = 0; i < arrayExpression.Expressions.Count; i++)
            {
                LLVMValueRef indexValue = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i, false);

                LLVMValueRef elementPtr = Builder.BuildInBoundsGEP2(
                    elementType,
                    arrayPtr,
                    new[] { indexValue },
                    $"array.expr.gep.{i}"
                );

                LLVMValueRef evaluatedVal = EmitExpression(arrayExpression.Expressions[i], variables);
                Builder.BuildStore(evaluatedVal, elementPtr);
            }

            return arrayPtr;
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

        public LLVMValueRef EmitMemberExpressionAddress(MemberExpression memberExpression, Variables variables)
        {
            ExpressionList memberChain = memberExpression.Flatten();

            StructStatement? currentStruct = null;
            LLVMValueRef currentPointer = null;

            Expression firstMember = memberChain.First();

            if (firstMember is CallExpression firstMemberCall)
            {
                IdentifierExpression? callIdentifier = GetInnerIdentifierExpression(firstMemberCall)
                    ?? throw new Exception("Could not resolve inner identifier of call in member expression.");

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

                    LLVMValueRef callValue = EmitCallExpression(firstMemberCall, variables);
                    currentPointer = Builder.BuildAlloca(currentStruct.LLVMStructType, "call_result_temp");
                    Builder.BuildStore(callValue, currentPointer);
                }
                else
                {
                    throw new Exception($"Cannot access member of parent with type {function.ReturnType.GetType().Name}");
                }
            }
            else if (firstMember is IndexExpression firstMemberIndex)
            {
                IdentifierExpression? indexIdentifier = GetInnerIdentifierExpression(firstMemberIndex)
                    ?? throw new Exception("Could not resolve inner identifier of index in member expression.");

                VariableDeclarationStatement matchingVariable = variables.GetVariable(indexIdentifier.Name);
                IdentifierExpression? typeIdentifier = matchingVariable.Type as IdentifierExpression
                    ?? GetInnerIdentifierExpression(matchingVariable.Type);

                if (typeIdentifier == null || !Structs.ContainsKey(typeIdentifier.Name))
                {
                    throw new Exception($"Struct or underlying matrix type does not exist.");
                }

                currentStruct = Structs[typeIdentifier.Name];

                currentPointer = EmitLValueAddress(firstMemberIndex, variables);
            }
            else if (firstMember is IdentifierExpression firstMemberIdentifier)
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
                }
                else
                {
                    throw new Exception($"Cannot access member of parent with type {matchingVariable.Type.GetType().Name}");
                }
            }

            if (currentStruct == null || currentPointer == null)
            {
                throw new Exception($"Cannot access member of parent with type {firstMember.GetType().Name}");
            }

            foreach (Expression member in memberChain.Skip(1))
            {
                IdentifierExpression? memberIdentifier = null;
                IndexExpression? indexExpr = null;

                if (member is IdentifierExpression idExpr)
                {
                    memberIdentifier = idExpr;
                }
                else if (member is IndexExpression idxExpr)
                {
                    indexExpr = idxExpr;
                    memberIdentifier = GetInnerIdentifierExpression(idxExpr);
                }

                if (memberIdentifier != null)
                {
                    VariableDeclarationStatement field = currentStruct.GetField(memberIdentifier.Name);

                    var indices = new LLVMValueRef[] {
                        LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0),
                        LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)
                    };

                    LLVMValueRef fieldPtr = Builder.BuildGEP2(
                        currentStruct.LLVMStructType,
                        currentPointer,
                        indices,
                        $"{memberIdentifier.Name}_field_ptr".AsSpan()
                    );

                    currentPointer = fieldPtr;

                    if (indexExpr != null)
                    {
                        var indexChain = new List<IndexExpression>();
                        Expression? current = indexExpr;
                        while (current is IndexExpression nestedIndex)
                        {
                            indexChain.Insert(0, nestedIndex);
                            current = nestedIndex.Expression;
                        }

                        LLVMTypeRef fieldLLVMType = field.Type.TypeAnnotation.ToLLVMType();
                        currentPointer = Builder.BuildLoad2(fieldLLVMType, currentPointer, "array.member.base.load");

                        for (int i = 0; i < indexChain.Count; i++)
                        {
                            IndexExpression currentBracket = indexChain[i];
                            LLVMValueRef indexValue = EmitExpression(currentBracket.Index, variables);

                            LLVMTypeRef elementLLVMType = currentBracket.TypeAnnotation.ToLLVMType(destructArray: true);

                            currentPointer = Builder.BuildInBoundsGEP2(
                                elementLLVMType,
                                currentPointer,
                                new[] { indexValue },
                                "array.member.index.gep"
                            );

                            if (i < indexChain.Count - 1)
                            {
                                LLVMTypeRef nextPointerType = currentBracket.TypeAnnotation.ToLLVMType();
                                currentPointer = Builder.BuildLoad2(nextPointerType, currentPointer, "array.subptr.load");
                            }
                        }
                    }

                    IdentifierExpression? fieldTypeIdentifier = GetInnerIdentifierExpression(field.Type);
                    if (fieldTypeIdentifier != null && Structs.ContainsKey(fieldTypeIdentifier.Name))
                    {
                        currentStruct = Structs[fieldTypeIdentifier.Name];
                    }
                    continue;
                }

                throw new Exception($"Unsupported member expression component type: {member.GetType().Name}");
            }


            return currentPointer;
        }

        public LLVMValueRef EmitMemberExpression(MemberExpression memberExpression, Variables variables)
        {
            LLVMValueRef fieldAddressPtr = EmitMemberExpressionAddress(memberExpression, variables);
            LLVMTypeRef expectedFieldType = memberExpression.TypeAnnotation.ToLLVMType();

            return Builder.BuildLoad2(
                expectedFieldType,
                fieldAddressPtr,
                "struct.member.load"
            );
        }


        LLVMValueRef EmitObjectInitializerExpression(ObjectInitializerExpression objectInitializerExpression, Variables variables)
        {
            if (objectInitializerExpression.Expression is IdentifierExpression identifier)
            {
                if (!Structs.ContainsKey(identifier.Name))
                    throw new Exception($"Could not initialize object {identifier.Name} as it does not exist.");

                StructStatement structStatement = Structs[identifier.Name];
                LLVMTypeRef structType = structStatement.LLVMStructType;

                LLVMValueRef structPtr = Builder.BuildAlloca(structType, $"{identifier.Name}_struct_instance");

                foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                {
                    if (propertyAssignment.Variable is IdentifierExpression propertyName)
                    {
                        VariableDeclarationStatement field = structStatement.Fields.GetVariable(propertyName.Name);
                        LLVMValueRef val = EmitExpression(propertyAssignment.Expression, variables);

                        LLVMValueRef fieldPtr = Builder.BuildGEP2(
                            structType,
                            structPtr,
                            [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)],
                            $"set_{propertyName.Name.ToLower()}_field_ptr".AsSpan()
                        );

                        Builder.BuildStore(val, fieldPtr);
                    }
                }

                return Builder.BuildLoad2(structType, structPtr, $"{identifier.Name.ToLower()}_val");
            }
            throw new Exception("Unsupported object initializer syntax.");
        }

        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left, variables);
            LLVMValueRef right = EmitExpression(relationalExpression.Right, variables);

            if (left == null || right == null)
            {
                throw new Exception("Left or right operand expression evaluated to null.");
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
                    default: throw new Exception($"Unsupported float relational operator: {relationalExpression.Operator}");
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
                    throw new Exception($"Type mismatch: Cannot compare pointer types {left.TypeOf} and {right.TypeOf}.");
                }

                switch (relationalExpression.Operator)
                {
                    case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "ptr.icmp");
                    case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "ptr.icmp");
                    default: throw new Exception($"Operator {relationalExpression.Operator} is invalid for pointer types.");
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
                    default: throw new Exception($"Unsupported integer relational operator: {relationalExpression.Operator}");
                }
            }

            throw new Exception($"Cannot emit comparison between unhandled types: {leftKind} and {rightKind}.");
        }

        LLVMValueRef EmitSizeOfExpression(SizeOfExpression sizeOfExpression, Variables variables)
        {
            LLVMTypeRef targetType = sizeOfExpression.Expression.TypeAnnotation.ToLLVMType();

            LLVMValueRef nullPtr = LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(targetType, 0));

            LLVMValueRef offsetIndex = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false);
            LLVMValueRef sizeGep = Builder.BuildInBoundsGEP2(targetType, nullPtr, [offsetIndex], "sizeof.gep".AsSpan());

            return Builder.BuildPtrToInt(sizeGep, LLVMTypeRef.Int64, "sizeof.bits");
        }

        LLVMValueRef EmitNotExpression(NotExpression notExpression, Variables variables)
        {
            LLVMValueRef value = EmitExpression(notExpression.Expression, variables);
            if (value == null)
            {
                throw new Exception("Expression inside logical/bitwise 'Not' statement evaluated to null.");
            }

            LLVMTypeRef valType = value.TypeOf;

            if (valType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && valType.IntWidth == 1)
            {
                LLVMValueRef trueVal = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false);
                return Builder.BuildXor(value, trueVal, "logical.not");
            }

            if (valType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                return Builder.BuildNot(value, "bitwise.not");
            }

            throw new Exception($"The 'Not' unary operation is invalid for type kind: {valType.Kind}");
        }

        LLVMValueRef EmitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, Variables variables)
        {
            if (parenthesizedExpression?.Expression == null)
            {
                throw new Exception("Parenthesized expression context contains no underlying target AST node.");
            }

            return EmitExpression(parenthesizedExpression.Expression, variables);
        }

        LLVMValueRef EmitBooleanExpression(BooleanExpression booleanExpression)
        {
            if (booleanExpression == null)
            {
                throw new ArgumentNullException(nameof(booleanExpression), "Boolean expression node cannot be null.");
            }

            return booleanExpression.Value
                ? LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false)
                : LLVMValueRef.CreateConstNull(LLVMTypeRef.Int1);
        }

        LLVMValueRef EmitUnpackExpression(UnpackExpression unpackExpression, Variables variables)
        {
            if (unpackExpression == null)
            {
                throw new ArgumentNullException(nameof(unpackExpression), "Unpack expression node cannot be null.");
            }

            // 1. Evaluate the source array expression (Left side, e.g., 'arr')
            LLVMValueRef sourceArrayPtr = EmitExpression(unpackExpression.Left, variables);
            if (sourceArrayPtr == null)
            {
                throw new Exception("The source array expression (Left) for unpacking evaluated to null.");
            }

            LLVMTypeRef sourceArrayPtrType = sourceArrayPtr.TypeOf;
            if (sourceArrayPtrType.Kind != LLVMTypeKind.LLVMPointerTypeKind && sourceArrayPtrType.Kind != LLVMTypeKind.LLVMArrayTypeKind)
            {
                throw new Exception($"Cannot unpack type '{sourceArrayPtrType}'. Left side must evaluate to an array reference.");
            }

            // Resolve the individual element type (e.g., int, i32, f64)
            LLVMTypeRef elementType = sourceArrayPtrType.Kind == LLVMTypeKind.LLVMArrayTypeKind
                ? sourceArrayPtrType.ElementType
                : unpackExpression.Left.TypeAnnotation.ToLLVMType();

            // 2. Validate and cast the target Range Expression (Right side, e.g., '0..5')
            if (unpackExpression.Right is not RangeExpression rangeExpr)
            {
                throw new Exception($"The right side of a Common C unpack expression must be a RangeExpression, found '{unpackExpression.Right.GetType().Name}'.");
            }

            LLVMValueRef startIdx = EmitExpression(rangeExpr.Start, variables);
            LLVMValueRef endIdx = EmitExpression(rangeExpr.End, variables);

            if (startIdx == null || endIdx == null)
            {
                throw new Exception("Unpack slice range boundaries could not be successfully evaluated.");
            }

            // 3. Compute the length of the new slice: (EndIndex - StartIndex)
            LLVMValueRef sliceLength = Builder.BuildSub(endIdx, startIdx, "unpack.slice.len");
            LLVMValueRef sliceLengthI32 = Builder.BuildIntCast(sliceLength, LLVMTypeRef.Int32, "unpack.slice.len.i32");

            // 4. Allocate space for the new destination sub-array memory segment
            LLVMValueRef destinationArrayPtr = Builder.BuildArrayMalloc(elementType, sliceLengthI32, "unpack.slice.ptr");

            // 5. Emit a highly optimized runtime loop block to copy elements over
            LLVMValueRef currentFunc = Builder.InsertBlock.Parent;

            LLVMBasicBlockRef loopCondBB = currentFunc.AppendBasicBlock("unpack.copy.cond");
            LLVMBasicBlockRef loopBodyBB = currentFunc.AppendBasicBlock("unpack.copy.body");
            LLVMBasicBlockRef loopNextBB = currentFunc.AppendBasicBlock("unpack.copy.next");

            // Track loop iteration progress via an offset tracking index counter
            LLVMValueRef indexCounterAlloca = Builder.BuildAlloca(LLVMTypeRef.Int32, "unpack.counter");
            Builder.BuildStore(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), indexCounterAlloca);
            Builder.BuildBr(loopCondBB);

            // Condition Basic Block
            Builder.PositionAtEnd(loopCondBB);
            LLVMValueRef currentCounter = Builder.BuildLoad2(LLVMTypeRef.Int32, indexCounterAlloca, "counter.load");
            LLVMValueRef isLess = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, currentCounter, sliceLengthI32, "counter.lt.len");
            Builder.BuildCondBr(isLess, loopBodyBB, loopNextBB);

            // Body Basic Block (Copying indices)
            Builder.PositionAtEnd(loopBodyBB);

            // Compute original source index position: (StartIdx + currentCounter)
            LLVMValueRef absoluteSourceIdx = Builder.BuildAdd(startIdx, currentCounter, "src.idx.offset");

            // GEP + Load from source array address
            LLVMValueRef sourceElementPtr = Builder.BuildInBoundsGEP2(elementType, sourceArrayPtr, [absoluteSourceIdx], "src.elem.gep".AsSpan());
            LLVMValueRef elementsVal = Builder.BuildLoad2(elementType, sourceElementPtr, "src.elem.val");

            // GEP + Store to destination slice array address
            LLVMValueRef destinationElementPtr = Builder.BuildInBoundsGEP2(elementType, destinationArrayPtr, [currentCounter], "dest.elem.gep".AsSpan());
            Builder.BuildStore(elementsVal, destinationElementPtr);

            // Increment index counter tracking loop iteration
            LLVMValueRef nextCounterValue = Builder.BuildAdd(currentCounter, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "counter.inc");
            Builder.BuildStore(nextCounterValue, indexCounterAlloca);
            Builder.BuildBr(loopCondBB);

            // Exit Block
            Builder.PositionAtEnd(loopNextBB);

            // Returns a pointer referencing the newly populated array slice allocation
            return destinationArrayPtr;
        }





        LLVMValueRef EmitExpression(Expression expression, Variables variables)
        {
            return expression switch
            {
                StringExpression stringExpression => EmitStringExpression(stringExpression),
                NumberExpression numberExpression => EmitNumberExpression(numberExpression),
                IdentifierExpression identifierExpression => EmitIdentifierExpression(identifierExpression, variables),
                ArithmeticExpression arithmeticExpression => EmitArithmeticExpression(arithmeticExpression, variables),
                CallExpression callExpression => EmitCallExpression(callExpression, variables),
                ArrayInitializerExpression arrayInitializerExpression => EmitArrayInitializerExpression(arrayInitializerExpression, variables),
                IndexExpression indexExpression => EmitIndexExpression(indexExpression, variables),
                ArrayExpression arrayExpression => EmitArrayExpression(arrayExpression, variables),
                MemberExpression memberExpression => EmitMemberExpression(memberExpression, variables),
                ObjectInitializerExpression objectInitializerExpression => EmitObjectInitializerExpression(objectInitializerExpression, variables),
                RelationalExpression relationalExpression => EmitRelationalExpression(relationalExpression, variables),
                SizeOfExpression sizeOfExpression => EmitSizeOfExpression(sizeOfExpression, variables),
                NotExpression notExpression => EmitNotExpression(notExpression, variables),
                ParenthesizedExpression parenthesizedExpression => EmitParenthesizedExpression(parenthesizedExpression, variables),
                BooleanExpression booleanExpression => EmitBooleanExpression(booleanExpression),
                _ => throw new Exception($"Unsupported expression type: {expression.GetType().Name}")
            };
        }

        LLVMValueRef[] EmitExpressions(List<Expression> expressions, Variables variables)
        {
            return expressions.Select(expr => EmitExpression(expr, variables)).ToArray();
        }
    }
}