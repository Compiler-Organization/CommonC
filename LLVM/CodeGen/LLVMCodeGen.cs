using CommonC.Error;
using CommonC.Liveness.Statements;
using CommonC.Parser.AST;
using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonC.Semantic.Objects;
using LLVMSharp.Interop;
using System.Data;

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
        ClassStatement CurrentClass { get; set; }

        Functions Functions = new Functions();
        Dictionary<string, StructStatement> Structs = new Dictionary<string, StructStatement>();
        Dictionary<string, EnumStatement> Enums = new Dictionary<string, EnumStatement>();
        Dictionary<string, ClassStatement> Classes = new Dictionary<string, ClassStatement>();

        public LLVMModuleRef GenerateLLVMModule()
        {
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);
            Context = Module.Context;

            CreateEnumReferences(UpperClosure);
            CreateStructReferences(UpperClosure);
            CreateClassReferences(UpperClosure);
            CreateFunctionReferences(UpperClosure);
            CreateGlobalReferences(UpperClosure);

            EmitStatements(UpperClosure.Statements, new Variables());

            return Module;
        }

        void CreateStructReferences(ClosureStatement closure)
        {
            foreach (StructStatement structStatement in closure.Statements.OfType<StructStatement>())
            {
                Structs.Add(structStatement.Name, structStatement);
            }

            foreach (StructStatement structReference in Structs.Values)
            {
                LLVMTypeRef[] fields = structReference.Fields.Select(f => f.Type.TypeAnnotation.ToLLVMType()).ToArray();
                structReference.LLVMStructType = LLVMTypeRef.CreateStruct(fields, false);
            }
        }

        void CreateClassReferences(ClosureStatement closure)
        {
            foreach (ClassStatement classStatement in closure.Statements.OfType<ClassStatement>())
            {
                Classes.Add(classStatement.Name, classStatement);
            }

            foreach (ClassStatement classReference in Classes.Values)
            {
                Variables classFields = classReference.GetFields();

                int index = 0;
                foreach (VariableDeclarationStatement field in classFields)
                {
                    field.FieldIndex = index++;
                }

                LLVMTypeRef[] fields = classFields.Select(f => f.Type.TypeAnnotation.ToLLVMType()).ToArray();
                classReference.Struct = new StructStatement
                {
                    Name = $"{classReference.Name}",
                    LLVMStructType = LLVMTypeRef.CreateStruct(fields, false),
                    Fields = classFields,
                    TypeAnnotation = new TypeAnnotation
                    {
                        IsStruct = true,
                        Struct = classReference.Struct
                    }
                };
            }
        }

        void CreateEnumReferences(ClosureStatement closure)
        {
            foreach (EnumStatement enumStatement in closure.Statements.OfType<EnumStatement>())
            {
                Enums.Add(enumStatement.Name, enumStatement);
            }
        }

        void CreateFunctionReferences(ClosureStatement closure, bool isClassFunction = false)
        {
            foreach (FunctionDeclarationStatement functionDeclarationStatement in closure.Statements.OfType<FunctionDeclarationStatement>())
            {
                if (functionDeclarationStatement.IsExtern)
                {
                    CreateExtern(
                        name: functionDeclarationStatement.Name,
                        returnType: functionDeclarationStatement.ReturnType.TypeAnnotation.ToLLVMType(),
                        parameters: functionDeclarationStatement.Parameters,
                        isVarArg: functionDeclarationStatement.Parameters.IsVararg
                    );
                    continue;
                }

                LLVMTypeRef returnType = functionDeclarationStatement.ReturnType.TypeAnnotation.ToLLVMType();
                LLVMTypeRef[] parameterTypes = functionDeclarationStatement.Parameters.Select(p => p.TypeAnnotation.ToLLVMType()).ToArray();

                LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType, 
                    isClassFunction 
                    ? [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), ..parameterTypes] 
                    : parameterTypes, 
                    false);

                if(isClassFunction)
                {
                    functionDeclarationStatement.Name = GetMangledName(CurrentClass.Name, functionDeclarationStatement.Name);
                }

                LLVMValueRef function = Module.AddFunction(functionDeclarationStatement.Name, functionType);

                LLVMValueRef[] parameters = function.GetParams();
                int classParamNegation = isClassFunction ? 1 : 0;
                for (int i = classParamNegation; i < parameters.Length; i++)
                {
                    parameters[i].Name = functionDeclarationStatement.Parameters[i - classParamNegation].Name;
                }

                function.AppendBasicBlock("");

                functionDeclarationStatement.LLVMFunction = function;
                functionDeclarationStatement.LLVMFunctionType = functionType;

                Functions.Add(functionDeclarationStatement);
            }
        }

        string GetMangledName(string className, string functionName)
        {
            return $"{className}.{functionName}";
        }

        void CreateGlobalReferences(ClosureStatement closure)
        {
            foreach (VariableDeclarationStatement variableDeclarationStatement in closure.Statements.OfType<VariableDeclarationStatement>())
            {
                LLVMTypeRef type = variableDeclarationStatement.Type.TypeAnnotation.ToLLVMType();
                variableDeclarationStatement.LLVMType = LLVMTypeRef.CreatePointer(type, 0);
                variableDeclarationStatement.IsGlobal = true;
            }
        }

        LLVMValueRef CreateExtern(string name, LLVMTypeRef returnType, ParameterExpressionList parameters, bool isVarArg = false)
        {
            LLVMTypeRef[] param = parameters.Select(p => p.TypeAnnotation.ToLLVMType()).ToArray();

            LLVMTypeRef externFunctionType = LLVMTypeRef.CreateFunction(returnType, param, isVarArg);
            LLVMValueRef externFunction = Module.AddFunction(name, externFunctionType);


            Functions.Add(new FunctionDeclarationStatement
            {
                Name = name,
                Parameters = parameters,

                LLVMFunction = externFunction,
                LLVMFunctionType = externFunctionType
            });

            return externFunction;
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
                case WhileStatement whileStatement:
                    EmitWhileStatement(whileStatement, variables);
                    break;
                case ForStatement forStatement:
                    EmitForStatement(forStatement, variables);
                    break;
                case IfStatement ifStatement:
                    EmitIfStatement(ifStatement, variables);
                    break;
                case ClassStatement classStatement:
                    EmitClassStatement(classStatement, variables);
                    break;
                case SwitchStatement switchStatement:
                    EmitSwitchStatement(switchStatement, variables);
                    break;

                case EnumStatement enumStatement:
                    break;
                case StructStatement structStatement:
                    break;

                default:
                    throw ErrorHandler.CreateError($"Unsupported statement type: {statement.GetType().Name}", statement);
            }
        }

        public LLVMValueRef EmitSwitchStatement(SwitchStatement switchStmt, Variables variables)
        {
            if (switchStmt == null)
                throw new ArgumentNullException(nameof(switchStmt));

            LLVMValueRef switchValue = EmitExpression(switchStmt.Expression, variables);

            if(CurrentFunction == null)
            {
                throw ErrorHandler.CreateError($"Switch statement cannot be placed outside of a function", switchStmt);
            }

            LLVMBasicBlockRef defaultBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("switch.default");
            LLVMBasicBlockRef mergeBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("switch.merge");

            uint expectedCasesCount = (uint)switchStmt.Cases.Count;
            LLVMValueRef switchInstr = Builder.BuildSwitch(switchValue, defaultBlock, expectedCasesCount);

            var caseProjections = new List<(LLVMBasicBlockRef Block, SwitchCase AstNode)>();

            for (int i = 0; i < switchStmt.Cases.Count; i++)
            {
                SwitchCase @case = switchStmt.Cases[i];

                LLVMBasicBlockRef caseBlock = CurrentFunction.LLVMFunction.AppendBasicBlock($"switch.case.{i}");

                LLVMValueRef caseConst = EmitExpression(@case.Expression, variables);

                switchInstr.AddCase(caseConst, caseBlock);
                caseProjections.Add((caseBlock, @case));
            }

            foreach (var projection in caseProjections)
            {
                Builder.PositionAtEnd(projection.Block);

                EmitStatements(projection.AstNode.Body.Statements, variables);

                EnsureBlockTerminator(mergeBlock);
            }

            Builder.PositionAtEnd(defaultBlock);
            if (switchStmt.DefaultCase?.Body != null)
            {
                EmitStatements(switchStmt.DefaultCase.Body.Statements, variables);
            }
            EnsureBlockTerminator(mergeBlock);

            Builder.PositionAtEnd(mergeBlock);

            return switchValue;
        }

        private void EnsureBlockTerminator(LLVMBasicBlockRef mergeDestination)
        {
            LLVMBasicBlockRef currentBlock = Builder.InsertBlock;

            if (currentBlock.LastInstruction.Handle == IntPtr.Zero || currentBlock.LastInstruction.IsATerminatorInst.Handle == IntPtr.Zero)
            {
                Builder.BuildBr(mergeDestination);
            }
        }

        void EmitClassStatement(ClassStatement classStatement, Variables variables)
        {
            CurrentClass = classStatement;
            CreateFunctionReferences(classStatement.Body, isClassFunction: true);

            EmitStatements(classStatement.Body.Statements, classStatement.Body.Variables);
            CurrentClass = null;
        }

        void EmitIfStatement(IfStatement ifStatement, Variables variables)
        {
            if (CurrentFunction == null)
            {
                throw ErrorHandler.CreateError("Current function is not set when emitting if statement.", ifStatement);
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
            EmitStatements(ifStatement.Body.Statements, ifStatement.Body.Variables);

            if (Builder.InsertBlock.Terminator == null)
            {
                Builder.BuildBr(mergeBlock);
            }

            if (hasElse)
            {
                Builder.PositionAtEnd(elseBlock);
                EmitStatements(ifStatement.Else.Statements, ifStatement.Else.Variables);

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
                throw ErrorHandler.CreateError("Current function is not set when emitting for statement.", forStatement);
            }

            LLVMBasicBlockRef preheaderBlock = Builder.InsertBlock;

            LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.cond");
            LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.body");
            LLVMBasicBlockRef loopIncrementBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.inc");
            LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.end");

            LLVMTypeRef loopVarType = forStatement.Variable.Type.TypeAnnotation.ToLLVMType();

            LLVMValueRef startValue = EmitExpression(forStatement.Range.Start, variables);
            LLVMValueRef endValue = EmitExpression(forStatement.Range.End, variables);

            Builder.BuildBr(loopConditionBlock);

            Builder.PositionAtEnd(loopConditionBlock);

            LLVMValueRef loopVarPhi = Builder.BuildPhi(loopVarType, $"{forStatement.Variable.Name}_phi");

            forStatement.Variable.LLVMAlloca = loopVarPhi;
            forStatement.Variable.HasAllocation = false;

            LLVMValueRef condition = CoerceRelationalType(RelationalOperators.LessThan, loopVarPhi, endValue, forStatement.Range);
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);

            Builder.PositionAtEnd(loopBodyBlock);
            EmitStatements(forStatement.Body.Statements, forStatement.Body.Variables);

            if (Builder.InsertBlock.Terminator == null)
            {
                Builder.BuildBr(loopIncrementBlock);
            }

            Builder.PositionAtEnd(loopIncrementBlock);

            LLVMValueRef incrementedValue = Builder.BuildAdd(
                loopVarPhi,
                LLVMValueRef.CreateConstInt(loopVarType, 1, false),
                "loopinc"
            );
            Builder.BuildBr(loopConditionBlock);

            loopVarPhi.AddIncoming(
                new LLVMValueRef[] { startValue, incrementedValue },
                new LLVMBasicBlockRef[] { preheaderBlock, loopIncrementBlock },
                2
            );

            Builder.PositionAtEnd(loopEndBlock);
        }

        //void EmitForStatement(ForStatement forStatement, Variables variables)
        //{
        //    if (CurrentFunction == null)
        //    {
        //        throw ErrorHandler.CreateError("Current function is not set when emitting for statement.", forStatement);
        //    }

        //    LLVMBasicBlockRef loopConditionBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.cond");
        //    LLVMBasicBlockRef loopBodyBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.body");
        //    LLVMBasicBlockRef loopIncrementBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.inc");
        //    LLVMBasicBlockRef loopEndBlock = CurrentFunction.LLVMFunction.AppendBasicBlock("for.end");

        //    LLVMTypeRef loopVarType = forStatement.Variable.Type.TypeAnnotation.ToLLVMType();
        //    forStatement.Variable.LLVMAlloca = Builder.BuildAlloca(loopVarType, forStatement.Variable.Name);

        //    LLVMValueRef startValue = EmitExpression(forStatement.Range.Start, variables);
        //    Builder.BuildStore(startValue, forStatement.Variable.LLVMAlloca);

        //    LLVMValueRef endValue = EmitExpression(forStatement.Range.End, variables);

        //    Builder.BuildBr(loopConditionBlock);

        //    Builder.PositionAtEnd(loopConditionBlock);
        //    LLVMValueRef loopVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
        //    LLVMValueRef condition = Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, loopVar, endValue, "loopcond");
        //    Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);

        //    Builder.PositionAtEnd(loopBodyBlock);
        //    EmitStatements(forStatement.Body.Statements, forStatement.Body.Variables);

        //    if (Builder.InsertBlock.Terminator == null)
        //    {
        //        Builder.BuildBr(loopIncrementBlock);
        //    }

        //    Builder.PositionAtEnd(loopIncrementBlock);
        //    LLVMValueRef incrementVar = Builder.BuildLoad2(loopVarType, forStatement.Variable.LLVMAlloca);
        //    LLVMValueRef incrementedValue = Builder.BuildAdd(incrementVar, LLVMValueRef.CreateConstInt(loopVarType, 1, false), "loopinc");
        //    Builder.BuildStore(incrementedValue, forStatement.Variable.LLVMAlloca);
        //    Builder.BuildBr(loopConditionBlock);

        //    Builder.PositionAtEnd(loopEndBlock);
        //}



        void EmitWhileStatement(WhileStatement whileStatement, Variables variables)
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
            LLVMValueRef condition = EmitExpression(whileStatement.Expression, variables);
            Builder.BuildCondBr(condition, loopBodyBlock, loopEndBlock);
            Builder.PositionAtEnd(loopBodyBlock);
            EmitStatements(whileStatement.Body.Statements, whileStatement.Body.Variables);
            if (whileStatement.Body.Statements.Count == 0 || whileStatement.Body.Statements.Last() is not ReturnStatement)
            {
                Builder.BuildBr(loopConditionBlock);
            }
            Builder.PositionAtEnd(loopEndBlock);
        }

        void EmitAssignmentStatement(AssignmentStatement assignmentStatement, Variables variables)
        {
            LLVMValueRef valueToStore;

            if (assignmentStatement.TypeAnnotation.IsVector && assignmentStatement.Expression is ArrayExpression arrayExpression)
            {
                Console.WriteLine("__________________ VECTOR CREATED");
                valueToStore = CreateVectorFromArray(assignmentStatement.TypeAnnotation.VectorType, arrayExpression, variables);
            }
            else
            {
                valueToStore = EmitExpression(assignmentStatement.Expression, variables);
            }

            LLVMValueRef destinationPointer = EmitLValueAddress(assignmentStatement.Variable, variables, pointerOnly: true);

            LLVMTypeRef targetType;
            if (assignmentStatement.Variable is IndexExpression indexExpr)
            {
                bool isString = indexExpr.Expression.TypeAnnotation.ReservedType == ReservedTypes.String;
                targetType = isString ? LLVMTypeRef.Int8 : indexExpr.TypeAnnotation.ToLLVMType(destructArray: true);
            }
            else
            {
                targetType = assignmentStatement.Variable.TypeAnnotation.ToLLVMType();
            }

            if (assignmentStatement.Operator != AssignmentOperator.Equals)
            {
                LLVMValueRef currentValue = Builder.BuildLoad2(targetType, destinationPointer, "compound.load");

                bool isFloatingPoint = targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind ||
                                      targetType.Kind == LLVMTypeKind.LLVMDoubleTypeKind ||
                                      targetType.Kind == LLVMTypeKind.LLVMHalfTypeKind ||
                                      targetType.Kind == LLVMTypeKind.LLVMFP128TypeKind;

                bool isInteger = targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind;

                bool isSigned = true;

                if (isInteger && valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                {
                    if (currentValue.TypeOf.IntWidth < valueToStore.TypeOf.IntWidth)
                    {
                        currentValue = isSigned
                            ? Builder.BuildSExt(currentValue, valueToStore.TypeOf, "compound.sext")
                            : Builder.BuildZExt(currentValue, valueToStore.TypeOf, "compound.zext");
                    }
                    else if (currentValue.TypeOf.IntWidth > valueToStore.TypeOf.IntWidth)
                    {
                        valueToStore = isSigned
                            ? Builder.BuildSExt(valueToStore, currentValue.TypeOf, "compound.rhs.sext")
                            : Builder.BuildZExt(valueToStore, currentValue.TypeOf, "compound.rhs.zext");
                    }
                }

                else if (isFloatingPoint && (valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMFloatTypeKind ||
                                             valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMDoubleTypeKind ||
                                             valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMHalfTypeKind ||
                                             valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMFP128TypeKind))
                {
                    int GetFPOrder(LLVMTypeKind kind) => kind switch
                    {
                        LLVMTypeKind.LLVMHalfTypeKind => 1,
                        LLVMTypeKind.LLVMFloatTypeKind => 2,
                        LLVMTypeKind.LLVMDoubleTypeKind => 3,
                        LLVMTypeKind.LLVMFP128TypeKind => 4,
                        _ => 0
                    };

                    if (GetFPOrder(targetType.Kind) < GetFPOrder(valueToStore.TypeOf.Kind))
                        currentValue = Builder.BuildFPExt(currentValue, valueToStore.TypeOf, "compound.fpext");
                    else if (GetFPOrder(targetType.Kind) > GetFPOrder(valueToStore.TypeOf.Kind))
                        valueToStore = Builder.BuildFPExt(valueToStore, currentValue.TypeOf, "compound.rhs.fpext");
                }

                valueToStore = assignmentStatement.Operator switch
                {
                    AssignmentOperator.CompoundAdd => isFloatingPoint
                        ? Builder.BuildFAdd(currentValue, valueToStore, "compound.fadd")
                        : Builder.BuildAdd(currentValue, valueToStore, "compound.add"),

                    AssignmentOperator.CompoundSubtract => isFloatingPoint
                        ? Builder.BuildFSub(currentValue, valueToStore, "compound.fsub")
                        : Builder.BuildSub(currentValue, valueToStore, "compound.sub"),

                    AssignmentOperator.CompoundMultiply => isFloatingPoint
                        ? Builder.BuildFMul(currentValue, valueToStore, "compound.fmul")
                        : Builder.BuildMul(currentValue, valueToStore, "compound.mul"),

                    AssignmentOperator.CompoundDivide => isFloatingPoint
                        ? Builder.BuildFDiv(currentValue, valueToStore, "compound.fdiv")
                        : (isSigned ? Builder.BuildSDiv(currentValue, valueToStore, "compound.sdiv")
                                    : Builder.BuildUDiv(currentValue, valueToStore, "compound.udiv")),

                    AssignmentOperator.CompoundModulo => isFloatingPoint
                        ? Builder.BuildFRem(currentValue, valueToStore, "compound.frem")
                        : (isSigned ? Builder.BuildSRem(currentValue, valueToStore, "compound.srem")
                                    : Builder.BuildURem(currentValue, valueToStore, "compound.urem")),

                    AssignmentOperator.CompoundBitwiseAnd when isInteger =>
                        Builder.BuildAnd(currentValue, valueToStore, "compound.and"),

                    AssignmentOperator.CompoundBitwiseOr when isInteger =>
                        Builder.BuildOr(currentValue, valueToStore, "compound.or"),

                    AssignmentOperator.CompoundXor when isInteger =>
                        Builder.BuildXor(currentValue, valueToStore, "compound.xor"),

                    AssignmentOperator.CompoundLeftShift when isInteger =>
                        Builder.BuildShl(currentValue, valueToStore, "compound.shl"),

                    AssignmentOperator.CompoundRightShift when isInteger => isSigned
                        ? Builder.BuildAShr(currentValue, valueToStore, "compound.ashr")
                        : Builder.BuildLShr(currentValue, valueToStore, "compound.lshr"),

                    AssignmentOperator.CompoundExp => EmitPowerExpression(currentValue, valueToStore, currentValue.TypeOf),

                    _ => throw ErrorHandler.CreateError($"Unsupported or invalid compound assignment operator: {assignmentStatement.Operator}", assignmentStatement)
                };


                if (valueToStore.TypeOf != targetType)
                {
                    if (isInteger)
                    {
                        valueToStore = Builder.BuildTrunc(valueToStore, targetType, "compound.trunc");
                    }
                    else if (isFloatingPoint)
                    {
                        valueToStore = Builder.BuildFPTrunc(valueToStore, targetType, "compound.fptrunc");
                    }
                }
            }

            if (targetType.Kind == LLVMTypeKind.LLVMStructTypeKind)
            {
                uint elementCount = targetType.StructElementTypesCount;
                for (uint i = 0; i < elementCount; i++)
                {
                    LLVMValueRef fieldSrcPtr = Builder.BuildGEP2(targetType, valueToStore, [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i)], $"assign.src.field.{i}".AsSpan());
                    LLVMValueRef fieldDstPtr = Builder.BuildGEP2(targetType, destinationPointer, [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)i)], $"assign.dst.field.{i}".AsSpan());
                    LLVMValueRef value = Builder.BuildLoad2(targetType.StructGetTypeAtIndex(i), fieldSrcPtr, $"assign.ld.{i}");
                    Builder.BuildStore(value, fieldDstPtr);
                }

                return;
            }
            else
            {
                if (targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind && valueToStore.TypeOf.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                {
                    if (targetType.IntWidth < valueToStore.TypeOf.IntWidth)
                    {
                        valueToStore = Builder.BuildTrunc(valueToStore, targetType, "truncated.assign.val");
                    }
                    else if (targetType.IntWidth > valueToStore.TypeOf.IntWidth)
                    {
                        valueToStore = Builder.BuildZExt(valueToStore, targetType, "extended.assign.val");
                    }
                }

                Builder.BuildStore(valueToStore, destinationPointer);
                return;
            }

            throw ErrorHandler.CreateError("Could not emit assignment", assignmentStatement);
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
                        throw ErrorHandler.CreateError($"Cannot return void from function '{CurrentFunction.Name}' which expects a {expectedReturnType} return type.", returnStatement);
                    }
                }

                Builder.BuildRetVoid();
            }
        }

        void EmitFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {
            if(functionDeclarationStatement.Body == null || functionDeclarationStatement.IsExtern)
            {
                return;
            }

            Builder.PositionAtEnd(functionDeclarationStatement.LLVMFunction.EntryBasicBlock);
            CurrentFunction = functionDeclarationStatement;

            if (functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Count > 0)
            {
                foreach (VariableDeclarationStatement parameter in functionDeclarationStatement.Body.Variables.Where(local => local.IsParameter))
                {
                    if(parameter.Type.TypeAnnotation.IsPointerType())
                    {
                        parameter.LLVMAlloca = CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex);
                    }
                    else
                    {
                        LLVMTypeRef parameterType = parameter.Type.TypeAnnotation.IsStruct
                            ? LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)
                            : parameter.Type.TypeAnnotation.ToLLVMType();

                        parameter.LLVMType = parameterType;
                        parameter.LLVMAlloca = Builder.BuildAlloca(parameterType, $"{parameter.Name}.addr");
                        Builder.BuildStore(CurrentFunction.LLVMFunction.GetParam((uint)parameter.ParameterIndex), parameter.LLVMAlloca);
                    }
                    
                }

                EmitStatements(functionDeclarationStatement.Body.Statements, functionDeclarationStatement.Body.Variables);

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
            }

            CurrentFunction = null;
        }

        void EmitCallStatement(CallStatement callStatement, Variables variables)
        {
            if(callStatement.Expression is IdentifierExpression identifierExpression)
            {
                FunctionDeclarationStatement functionDecl = Functions.GetFunction(identifierExpression.Name, callStatement.Arguments, callStatement);

                LLVMValueRef[] arguments = callStatement.Arguments == null 
                    ? Array.Empty<LLVMValueRef>() 
                    : callStatement.Arguments
                        .Select(argExpr => EmitExpression(argExpr, variables))
                        .ToArray();

                LLVMValueRef callInst = Builder.BuildCall2(
                    functionDecl.LLVMFunctionType,
                    functionDecl.LLVMFunction,
                    arguments,
                    functionDecl.LLVMFunctionType.ReturnType == LLVMTypeRef.Void 
                        ? "" 
                        : $"{identifierExpression.Name}_call"
                );

                if (identifierExpression.Name.Contains('@')) // Temporary workaround for 32-bit Win32 API calls
                {
                    callInst.InstructionCallConv = 64;
                }

                return;
            }

            if(callStatement.Expression is MemberExpression memberExpression)
            {
                LLVMValueRef callExpression = EmitMemberExpression(memberExpression, variables);
                callExpression.Name = "";
                return;
            }

            throw ErrorHandler.CreateError($"Call statement's expression cannot be a '{callStatement.Expression.GetType().Name}'", callStatement);
        }

        LLVMValueRef CreateVectorFromArray(VectorTypeExpression vectorTypeExpression, ArrayExpression arrayExpression, Variables variables)
        {
            bool isConstVector = true;
            foreach(Expression arrayElement in arrayExpression.Expressions)
            {
                if(arrayElement is not NumberExpression)
                {
                    isConstVector = false;
                }
            }

            if(isConstVector)
            {
                return LLVMValueRef.CreateConstVector(arrayExpression.Expressions.Select(e => EmitNumberExpression(e as NumberExpression, vectorTypeExpression.Type.TypeAnnotation)).ToArray());
            }
            else
            {
                LLVMValueRef[] elements = arrayExpression.Expressions
                    .Select(e => EmitExpression(e, variables))
                    .ToArray();

                uint laneCount = (uint)elements.Length;

                LLVMTypeRef elementType = elements[0].TypeOf;
                LLVMTypeRef vectorType = LLVMTypeRef.CreateVector(elementType, laneCount);

                LLVMValueRef dynamicVector = vectorType.Undef;
                for (uint i = 0; i < laneCount; i++)
                {
                    LLVMValueRef indexValue = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, i);
                    dynamicVector = Builder.BuildInsertElement(
                        dynamicVector,
                        elements[i],
                        indexValue,
                        $"vector_init_lane_{i}"
                    );
                }

                return dynamicVector;
            }

        }

        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclaration, Variables variables)
        {
            if (variableDeclaration.IsGlobal)
            {
                LLVMTypeRef globalType = variableDeclaration.TypeAnnotation.ToLLVMType();
                LLVMValueRef global = Module.AddGlobal(globalType, variableDeclaration.Name);

                global.Initializer = LLVMValueRef.CreateConstNull(globalType);
                global.IsGlobalConstant = false;

                variableDeclaration.LLVMAlloca = global;
                variableDeclaration.LLVMType = globalType;

                if (variableDeclaration.Expression != null)
                {
                    if (!Functions.Contains(Settings.EntryPoint))
                    {
                        throw ErrorHandler.CreateError($"Entry point function {Settings.EntryPoint} does not exist!");
                    }

                    LLVMBasicBlockRef previousBlock = Builder.InsertBlock;

                    FunctionDeclarationStatement entryPointFunction = Functions.GetFunction(Settings.EntryPoint);

                    Builder.PositionAtEnd(entryPointFunction.LLVMFunction.EntryBasicBlock);

                    LLVMValueRef val = EmitExpression(variableDeclaration.Expression, variables);

                    if (val.TypeOf != globalType)
                    {
                        val = CoerceType(val, globalType, "global.init.cast", variableDeclaration);
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
                if (CurrentClass != null)
                    return;

                throw ErrorHandler.CreateError($"Cannot declare local variable '{variableDeclaration.Name}' outside of a function context.", variableDeclaration);
            }

            LLVMTypeRef varType = variableDeclaration.TypeAnnotation.ToLLVMType();



            if (variableDeclaration.Expression == null)
            {
                throw ErrorHandler.CreateError("Variable declarations cannot be left uninitialized", variableDeclaration);
            }

            LLVMValueRef initValue;

            if (variableDeclaration.Type.TypeAnnotation.IsVector && variableDeclaration.Expression is ArrayExpression arrayExpression)
            {
                Console.WriteLine("__________________ VECTOR CREATED");
                initValue = CreateVectorFromArray(variableDeclaration.Type.TypeAnnotation.VectorType, arrayExpression, variables);
            }
            else
            {
                initValue = EmitExpression(variableDeclaration.Expression, variables);
            }


            if (variableDeclaration.Type.TypeAnnotation.IsPointerType())
            {
                variableDeclaration.LLVMAlloca = initValue;
            }
            else
            {
                if (initValue.TypeOf != varType)
                {
                    initValue = CoerceType(initValue, varType, "local.init.cast", variableDeclaration);
                }

                LLVMBasicBlockRef currentBlock = Builder.InsertBlock;

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

                LLVMValueRef allocaPtr = Builder.BuildAlloca(varType, variableDeclaration.Name);
                variableDeclaration.LLVMAlloca = allocaPtr;

                Builder.PositionAtEnd(currentBlock);

                Builder.BuildStore(initValue, allocaPtr);
            }

        }

        private LLVMValueRef CoerceType(LLVMValueRef value, LLVMTypeRef targetType, string name, object errorObject)
        {
            if (value.TypeOf == targetType)
            {
                return value;
            }

            if (value.TypeOf.Kind == LLVMTypeKind.LLVMIntegerTypeKind && targetType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                uint sourceWidth = value.TypeOf.IntWidth;
                uint targetWidth = targetType.IntWidth;

                if (sourceWidth < targetWidth)
                    return Builder.BuildSExt(value, targetType, name);
                if (sourceWidth > targetWidth)
                    return Builder.BuildTrunc(value, targetType, name);
            }

            if ((value.TypeOf.Kind == LLVMTypeKind.LLVMFloatTypeKind || value.TypeOf.Kind == LLVMTypeKind.LLVMDoubleTypeKind) &&
                (targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind || targetType.Kind == LLVMTypeKind.LLVMDoubleTypeKind))
            {
                if (value.TypeOf.Kind == LLVMTypeKind.LLVMFloatTypeKind && targetType.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
                {
                    return Builder.BuildFPExt(value, targetType, name);
                }

                if (value.TypeOf.Kind == LLVMTypeKind.LLVMDoubleTypeKind && targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind)
                {
                    return Builder.BuildFPTrunc(value, targetType, name);
                }
            }

            if (value.TypeOf.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
                (targetType.Kind == LLVMTypeKind.LLVMFloatTypeKind || targetType.Kind == LLVMTypeKind.LLVMDoubleTypeKind))
            {
                return Builder.BuildSIToFP(value, targetType, name);
            }

            if (value.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind && targetType.Kind == LLVMTypeKind.LLVMStructTypeKind)
            {
                return Builder.BuildLoad2(targetType, value, name);
            }

            if (value.TypeOf.Kind == LLVMTypeKind.LLVMVectorTypeKind && targetType.Kind == LLVMTypeKind.LLVMVectorTypeKind)
            {
                if (value.TypeOf.VectorSize != targetType.VectorSize)
                {
                    throw ErrorHandler.CreateError($"Vector length mismatch in type conversion: Cannot convert {value.TypeOf} to {targetType}.", errorObject);
                }

                LLVMTypeRef srcElementT = value.TypeOf.ElementType;
                LLVMTypeRef dstElementT = targetType.ElementType;

                if (srcElementT.Kind == LLVMTypeKind.LLVMIntegerTypeKind && dstElementT.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                {
                    if (srcElementT.IntWidth < dstElementT.IntWidth)
                    {
                        return Builder.BuildZExt(value, targetType, name ?? "v.zext");
                    }
                    else if (srcElementT.IntWidth > dstElementT.IntWidth)
                    {
                        return Builder.BuildTrunc(value, targetType, name ?? "v.trunc");
                    }
                }

                if (srcElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind && dstElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
                    return Builder.BuildFPExt(value, targetType, name ?? "v.fpext");
                if (srcElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind && dstElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind)
                    return Builder.BuildFPTrunc(value, targetType, name ?? "v.fptrunc");
            }

            throw ErrorHandler.CreateError($"Implicit type conversion from {value.TypeOf} to {targetType} is unsupported.", errorObject);
        }




        LLVMValueRef EmitStringExpression(StringExpression stringExpression)
        {
            return Builder.BuildGlobalStringPtr(stringExpression.Value, stringExpression.Value.Substring(0, Math.Min(stringExpression.Value.Length, 5)));
        }

        // Rewrite this so it accruately creates a number given the expression using it
        LLVMValueRef EmitNumberExpression(NumberExpression numberExpression, TypeAnnotation? numberType = null)
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;

            if (numberType != null)
            {
                LLVMTypeRef numberLLVMType = numberType.ToLLVMType();

                if (numberLLVMType.Kind == LLVMTypeKind.LLVMDoubleTypeKind ||
                    numberLLVMType.Kind == LLVMTypeKind.LLVMFloatTypeKind ||
                    numberLLVMType.Kind == LLVMTypeKind.LLVMHalfTypeKind ||
                    numberLLVMType.Kind == LLVMTypeKind.LLVMFP128TypeKind)
                {
                    return LLVMValueRef.CreateConstReal(numberLLVMType, numberExpression.ToDouble());
                }

                if (numberLLVMType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                {
                    if (ulong.TryParse(numberExpression.Value, culture, out ulong integerValue))
                    {
                        return LLVMValueRef.CreateConstInt(numberLLVMType, integerValue, numberType.IsSigned());
                    }
                    if (long.TryParse(numberExpression.Value, culture, out long signedNumberValue))
                    {
                        return LLVMValueRef.CreateConstInt(numberLLVMType, (ulong)signedNumberValue, true);
                    }
                }

                throw ErrorHandler.CreateError($"Cannot cast numeric literal '{numberExpression.Value}' to requested type: {numberType.ToString()}", numberExpression);
            }

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

            throw ErrorHandler.CreateError($"Invalid literal numeric format encountered: '{numberExpression.Value}'", numberExpression);
        }


        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression, Variables variables, bool pointerOnly = false)
        {
            if (Functions.Contains(identifierExpression.Name))
                return Functions.GetFunction(identifierExpression.Name).LLVMFunction;

            VariableDeclarationStatement variable = variables.GetVariable(identifierExpression.Name);

            LLVMValueRef pointer = variable.LLVMAlloca;
            LLVMTypeRef valueType = variable.Type.TypeAnnotation.ToLLVMType();

            if(!variable.HasAllocation)
            {
                return pointer;
            }

            if(pointerOnly)
            {
                return pointer;
            }

            if(variable.Type.TypeAnnotation.IsPointerType())
            {
                return pointer;
            }
            else
            {
                return Builder.BuildLoad2(valueType, pointer, $"{identifierExpression.Name}_val");
            }
        }

        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left, variables);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right, variables);

            if (left == null || right == null)
            {
                throw ErrorHandler.CreateError("Left or right operand expression evaluated to null.", arithmeticExpression);
            }

            UnifyArithmeticOperands(ref left, ref right, arithmeticExpression);

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
                    if (isFloat) throw ErrorHandler.CreateError("Left shift operator is not supported on floating-point types.", arithmeticExpression);
                    return Builder.BuildShl(left, right, "bitwise.leftshift");

                case ArithmeticOperator.RightShift:
                    if (isFloat) throw ErrorHandler.CreateError("Right shift operator is not supported on floating-point types.", arithmeticExpression);
                    return Builder.BuildAShr(left, right, "bitwise.rightshift");

                case ArithmeticOperator.Xor:
                    if (isFloat) throw ErrorHandler.CreateError("XOR operator is not supported on floating-point types.", arithmeticExpression);
                    return Builder.BuildXor(left, right, "bitwise.xor");

                case ArithmeticOperator.BitwiseAnd:
                    if (isFloat) throw ErrorHandler.CreateError("XOR operator is not supported on floating-point types.", arithmeticExpression);
                    return Builder.BuildAnd(left, right, "bitwise.and");

                case ArithmeticOperator.BitwiseOr:
                    if (isFloat) throw ErrorHandler.CreateError("XOR operator is not supported on floating-point types.", arithmeticExpression);
                    return Builder.BuildOr(left, right, "bitwise.or");

                case ArithmeticOperator.Exponentiation:
                    return EmitPowerExpression(left, right, commonType);

                default:
                    throw ErrorHandler.CreateError($"Arithmetic operator {arithmeticExpression.Operator} is not supported when emitting LLVM arithmetic expressions.", arithmeticExpression);
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

        private LLVMValueRef EmitPowerExpression(LLVMValueRef left, LLVMValueRef right, LLVMTypeRef targetType)
        {
            LLVMTypeRef originalType = targetType;
            bool wasInteger = originalType.Kind == LLVMTypeKind.LLVMIntegerTypeKind;

            if (wasInteger)
            {
                left = Builder.BuildSIToFP(left, LLVMTypeRef.Double, "pow.cast.left");
                right = Builder.BuildSIToFP(right, LLVMTypeRef.Double, "pow.cast.right");
                targetType = LLVMTypeRef.Double;
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
                result = Builder.BuildFPToSI(result, originalType, "pow.cast.back");
            }

            return result;
        }



        LLVMValueRef EmitCallExpression(CallExpression callExpression, Variables variables)
        {
            if (callExpression.Expression is IdentifierExpression identifierExpression)
            {
                FunctionDeclarationStatement functionDecl = Functions.GetFunction(identifierExpression.Name, callExpression.Arguments, callExpression);

                LLVMValueRef[] arguments = callExpression.Arguments == null
                    ? Array.Empty<LLVMValueRef>()
                    : callExpression.Arguments
                        .Select(argExpr => EmitExpression(argExpr, variables))
                        .ToArray();

                LLVMValueRef callInst = Builder.BuildCall2(
                    functionDecl.LLVMFunctionType,
                    functionDecl.LLVMFunction,
                    arguments,
                    identifierExpression.Name + "_call"
                );

                if (identifierExpression.Name.Contains('@'))
                {
                    callInst.InstructionCallConv = 64;
                }

                return callInst;
            }
            else
            {
                throw ErrorHandler.CreateError("Unsupported function expression type in call: " + callExpression.Expression.GetType().Name, callExpression);
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
            else if (arrayInitializerExpression.Index.Expression.TypeAnnotation.IsArray && (elementType == LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0) || elementType.Kind == LLVMTypeKind.LLVMPointerTypeKind))
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

        LLVMValueRef EmitLValueAddress(Expression expression, Variables variables, bool pointerOnly = false)
        {
            return expression switch
            {
                IdentifierExpression id => variables.GetVariable(id.Name).LLVMAlloca,
                IndexExpression index => EmitIndexExpressionAddress(index, variables),
                MemberExpression member => EmitMemberExpressionAddress(member, variables, pointerOnly),

                _ => throw new NotSupportedException($"Expression type '{expression.GetType().Name}' is not a valid L-Value target.")
            };
        }

        LLVMValueRef GetArrayIndexPointer(LLVMValueRef arrayPointer, LLVMTypeRef arrayType, LLVMValueRef indexValue)
        {
            return Builder.BuildInBoundsGEP2(
                arrayType,
                arrayPointer,
                new[] { indexValue },
                "str.or.array.index.gep"
            );
        }

        LLVMValueRef GetArrayIndexPointerValue(LLVMValueRef arrayPointer, LLVMTypeRef arrayType, LLVMValueRef indexValue)
        {
            return Builder.BuildLoad2(
                arrayType,
                GetArrayIndexPointer(
                    arrayPointer, 
                    arrayType, 
                    indexValue
                    )
                );
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
            else if (indexExpression.Expression is IdentifierExpression indexIdentifier)
            {
                arrayPtr = EmitIdentifierExpression(indexIdentifier, variables, pointerOnly: true);
            }
            else
            {
                arrayPtr = EmitExpression(indexExpression.Expression, variables);
            }

            LLVMValueRef indexValue = EmitExpression(indexExpression.Index, variables);


            LLVMTypeRef elementType = indexExpression.Expression.TypeAnnotation.ReservedType == ReservedTypes.String
                ? LLVMTypeRef.Int8
                : indexExpression.TypeAnnotation.ToLLVMType(destructArray: true);

            return Builder.BuildInBoundsGEP2(
                elementType,
                arrayPtr,
                new[] { indexValue },
                "str.or.array.index.gep"
            );
        }

        LLVMValueRef EmitIndexExpression(IndexExpression indexExpression, Variables variables)
        {
            LLVMValueRef elementPtr = EmitIndexExpressionAddress(indexExpression, variables);

            LLVMTypeRef elementType = indexExpression.Expression.TypeAnnotation.ReservedType == ReservedTypes.String
                ? LLVMTypeRef.Int8
                : indexExpression.TypeAnnotation.ToLLVMType(destructArray: true);

            return Builder.BuildLoad2(elementType, elementPtr, "index.load");
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

                LLVMValueRef evaluatedVal;

                if (arrayExpression.Expressions[i].TypeAnnotation.IsVector && arrayExpression.Expressions[i] is ArrayExpression innerArray)
                {
                    evaluatedVal = CreateVectorFromArray(arrayExpression.Expressions[i].TypeAnnotation.VectorType, innerArray, variables);
                }
                else
                {
                    // Fallback for regular scalars or true multidimensional arrays
                    evaluatedVal = EmitExpression(arrayExpression.Expressions[i], variables);
                }
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
                ArrayExpression expr => GetInnerIdentifierExpression(expr.Expressions.Any() ? expr.Expressions.First() : throw ErrorHandler.CreateError($"Cannot resolve inner identifier expression of empty array", expression)),
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
                _ => throw ErrorHandler.CreateError($"Inner identifier expression of type {expression.GetType().Name} is not supported.", expression)
            };
        }

        LLVMValueRef EmitMemberExpressionAddress(MemberExpression memberExpression, Variables variables, bool pointerOnly = false)
        {
            ExpressionList memberChain = memberExpression.Flatten();

            bool endsWithCall = memberChain.Last() is CallExpression;

            StructStatement? currentStruct = null;
            LLVMValueRef currentPointer = default;
            bool pointerInitialized = false;

            Expression firstMember = memberChain.First();

            if (firstMember is CallExpression firstMemberCall)
            {
                (currentStruct, currentPointer) = ResolveHeadCall(firstMemberCall, memberExpression, variables);
                pointerInitialized = true;
            }
            else if (firstMember is IndexExpression firstMemberIndex)
            {
                (currentStruct, currentPointer) = ResolveHeadIndex(firstMemberIndex, memberExpression, variables);
                pointerInitialized = true;
            }
            else if (firstMember is IdentifierExpression firstMemberIdentifier)
            {
                (currentStruct, currentPointer) = ResolveHeadIdentifier(firstMemberIdentifier, memberExpression, variables);
                pointerInitialized = true;
            }

            if (!pointerInitialized || currentStruct == null || currentPointer.Handle == IntPtr.Zero)
                throw ErrorHandler.CreateError($"Cannot access member of parent with type '{firstMember.GetType().Name}'.", memberExpression);

            IEnumerable<Expression> tail = memberChain.Skip(1);

            foreach (Expression member in tail)
            {
                if (currentStruct == null)
                    throw ErrorHandler.CreateError($"Cannot dereference member '{member}': preceding expression has no struct type.", memberExpression);

                if (member is IdentifierExpression memberIdentifier)
                {
                    (currentStruct, currentPointer) =
                        StepIdentifier(memberIdentifier, currentStruct, currentPointer, memberExpression);
                }
                else if (member is IndexExpression memberIndex)
                {
                    (currentStruct, currentPointer) =
                        StepIndex(memberIndex, currentStruct, currentPointer, memberExpression, variables);
                }
                else if (member is CallExpression memberCall)
                {
                    LLVMValueRef result = StepCall(
                        memberCall, ref currentStruct, ref currentPointer,
                        memberExpression, variables);

                    if (currentStruct == null)
                        return result;
                }
                else
                {
                    throw ErrorHandler.CreateError($"Unsupported member expression component type: '{member.GetType().Name}'.", memberExpression);
                }
            }

            if (endsWithCall)
                return currentPointer;

            if(pointerOnly)
            {
                return currentPointer;
            }
            else
            {
                LLVMTypeRef expectedType = memberExpression.TypeAnnotation.ToLLVMType();
                return Builder.BuildLoad2(expectedType, currentPointer, "member.load");
            }
        }

        private (StructStatement Struct, LLVMValueRef Pointer) ResolveHeadCall(CallExpression call, MemberExpression context, Variables variables)
        {
            IdentifierExpression callId = GetInnerIdentifierExpression(call) ?? throw ErrorHandler.CreateError("Could not resolve inner identifier of call in member expression.", context);

            FunctionDeclarationStatement fn = Functions.GetFunction(callId.Name, call.Arguments, context);

            StructStatement s = ResolveStructOrClass(fn.ReturnType, context, $"Cannot access member of a function returning '{fn.ReturnType?.GetType().Name}'.");

            LLVMValueRef callVal = EmitCallExpression(call, variables);
            LLVMValueRef ptr = Builder.BuildAlloca(s.LLVMStructType, "call_result_tmp");
            Builder.BuildStore(callVal, ptr);

            return (s, ptr);
        }

        private (StructStatement Struct, LLVMValueRef Pointer) ResolveHeadIndex(IndexExpression index, MemberExpression context, Variables variables)
        {
            IdentifierExpression indexId = GetInnerIdentifierExpression(index) ?? throw ErrorHandler.CreateError("Could not resolve inner identifier of index in member expression.", context);

            VariableDeclarationStatement var = variables.GetVariable(indexId.Name);
            IdentifierExpression? typeId = var.Type as IdentifierExpression ?? GetInnerIdentifierExpression(var.Type);

            if (typeId == null)
                throw ErrorHandler.CreateError("Type could not be determined for indexed expression.", context);

            StructStatement s = ResolveStructOrClass(typeId, context, $"Struct or class type '{typeId.Name}' does not exist.");

            return (s, EmitLValueAddress(index, variables));
        }

        private (StructStatement Struct, LLVMValueRef Pointer) ResolveHeadIdentifier(IdentifierExpression id, MemberExpression context, Variables variables)
        {
            VariableDeclarationStatement var = variables.GetVariable(id.Name);

            if (var.Type is not IdentifierExpression typeId)
                throw ErrorHandler.CreateError($"Cannot access member of '{id.Name}' with type '{var.Type?.GetType().Name}'.", context);

            StructStatement s = ResolveStructOrClass(typeId, context, $"Struct or class '{typeId.Name}' does not exist.");

            return (s, var.LLVMAlloca);
        }

        private (StructStatement? Struct, LLVMValueRef Pointer) StepIdentifier(IdentifierExpression id, StructStatement currentStruct, LLVMValueRef currentPointer, MemberExpression context)
        {
            LLVMValueRef fieldPtr;
            try
            {
                fieldPtr = GetStructPropertyPointer(currentPointer, currentStruct, id.Name);
            }
            catch (Exception ex)
            {
                throw ErrorHandler.CreateError($"Field '{id.Name}' does not exist in '{currentStruct.Name}': {ex.Message}", context);
            }

            VariableDeclarationStatement field = currentStruct.Fields.GetVariable(id.Name) ?? throw ErrorHandler.CreateError($"Field '{id.Name}' not found in '{currentStruct.Name}'.", context);

            IdentifierExpression? fieldTypeId = field.Type is IdentifierExpression d ? d : GetInnerIdentifierExpression(field.Type);

            if (fieldTypeId == null)
                return (null, fieldPtr);

            if (Structs.TryGetValue(fieldTypeId.Name, out StructStatement? nextStruct))
            {
                LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(nextStruct.LLVMStructType, 0);
                LLVMValueRef derefPtr = Builder.BuildLoad2(ptrType, fieldPtr, $"{id.Name}.deref");
                return (nextStruct, derefPtr);
            }

            if (Classes.TryGetValue(fieldTypeId.Name, out ClassStatement? nextClass))
            {
                LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(nextClass.Struct.LLVMStructType, 0);
                LLVMValueRef derefPtr = Builder.BuildLoad2(ptrType, fieldPtr, $"{id.Name}.deref");
                return (nextClass.Struct, derefPtr);
            }

            return (null, fieldPtr);
        }

        private (StructStatement? Struct, LLVMValueRef Pointer) StepIndex(IndexExpression index, StructStatement currentStruct, LLVMValueRef currentPointer, MemberExpression context, Variables variables)
        {
            IdentifierExpression indexId = GetInnerIdentifierExpression(index) ?? throw ErrorHandler.CreateError("Could not resolve inner identifier of index expression.", context);
            VariableDeclarationStatement field = currentStruct.Fields.GetVariable(indexId.Name) ?? throw ErrorHandler.CreateError($"Field '{indexId.Name}' does not exist in '{currentStruct.Name}'.", context);

            LLVMValueRef fieldPtr = GetStructPropertyPointer(currentPointer, currentStruct, indexId.Name);

            LLVMTypeRef fieldLLVMType = field.Type.TypeAnnotation.ToLLVMType();
            LLVMValueRef arrayBasePtr = Builder.BuildLoad2(fieldLLVMType, fieldPtr, "array.field.base");

            LLVMValueRef elementPtr = EmitIndexExpressionAddress(index, variables);

            IdentifierExpression? elemTypeId =
                field.Type is IdentifierExpression d ? d : GetInnerIdentifierExpression(field.Type);

            if (elemTypeId == null)
                return (null, elementPtr);

            if (Structs.TryGetValue(elemTypeId.Name, out StructStatement? elemStruct))
                return (elemStruct, elementPtr);

            if (Classes.TryGetValue(elemTypeId.Name, out ClassStatement? elemClass))
                return (elemClass.Struct, elementPtr);

            return (null, elementPtr);
        }

        private LLVMValueRef StepCall(CallExpression call, ref StructStatement? currentStruct, ref LLVMValueRef currentPointer, MemberExpression context, Variables variables)
        {
            IdentifierExpression callId = GetInnerIdentifierExpression(call) ?? throw ErrorHandler.CreateError("Could not resolve inner identifier of call expression.", context);

            string mangledName = $"{currentStruct!.Name}.{callId.Name}";
            FunctionDeclarationStatement fn = Functions.GetFunction(mangledName, call.Arguments, context);

            LLVMValueRef[] args =
            [
                currentPointer,
                .. call.Arguments == null ? [] : EmitExpressions(call.Arguments, variables)
            ];

            if(call.Arguments != null && !fn.Parameters.MatchTypes(call.Arguments, false))
            {
                throw ErrorHandler.CreateError($"Call arguments to function '{fn.Name}' ({call.Arguments.PrettyPrint()}) does not match its parameters ({fn.Parameters.PrettyPrint()})", call);
            }

            LLVMValueRef callResult = Builder.BuildCall2(fn.LLVMFunctionType, fn.LLVMFunction, args, "chain.call.tmp");

            IdentifierExpression? retTypeId = fn.ReturnType is IdentifierExpression d ? d : GetInnerIdentifierExpression(fn.ReturnType);

            if (retTypeId == null)
            {
                currentStruct = null;
                return callResult;
            }

            if (Structs.TryGetValue(retTypeId.Name, out StructStatement? retStruct))
            {
                currentStruct = retStruct;
                currentPointer = Builder.BuildAlloca(retStruct.LLVMStructType, "chain.struct.tmp");
                Builder.BuildStore(callResult, currentPointer);
                return currentPointer;
            }

            if (Classes.TryGetValue(retTypeId.Name, out ClassStatement? retClass))
            {
                currentStruct = retClass.Struct;
                currentPointer = callResult;
                return currentPointer;
            }

            currentStruct = null;
            return callResult;
        }

        private StructStatement ResolveStructOrClass(Expression? typeExpr, MemberExpression context, string errorMessage)
        {
            IdentifierExpression? id = typeExpr as IdentifierExpression ?? GetInnerIdentifierExpression(typeExpr);

            if (id == null)
                throw ErrorHandler.CreateError(errorMessage, context);

            if (Structs.TryGetValue(id.Name, out StructStatement? s))
                return s;

            if (Classes.TryGetValue(id.Name, out ClassStatement? c))
                return c.Struct;

            throw ErrorHandler.CreateError(errorMessage, context);
        }

        public LLVMValueRef EmitMemberExpression(MemberExpression memberExpression, Variables variables)
        {
            if (memberExpression.Parent is IdentifierExpression enumId
                && Enums.TryGetValue(enumId.Name, out EnumStatement? enumStmt)
                && memberExpression.Member is IdentifierExpression variantId)
            {
                return LLVMValueRef.CreateConstInt(
                    enumStmt.TypeAnnotation.ToLLVMType(),
                    enumStmt.GetVariant(variantId.Name).Expression.ToUlong());
            }

            return EmitMemberExpressionAddress(memberExpression, variables);
        }


        LLVMValueRef EmitObjectInitializerExpression(ObjectInitializerExpression objectInitializerExpression, Variables variables)
        {
            if (objectInitializerExpression.Expression is IdentifierExpression identifier)
            {
                if(objectInitializerExpression.TypeAnnotation.IsClass && Classes.ContainsKey(identifier.Name))
                {
                    ClassStatement classStatement = Classes[identifier.Name];
                    LLVMValueRef structPointer = CreateStructMalloc(classStatement.Struct);

                    HashSet<string> propertiesAssigned = new HashSet<string>();

                    foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                    {
                        if (propertyAssignment.Variable is IdentifierExpression propertyName)
                        {
                            LLVMValueRef val = EmitExpression(propertyAssignment.Expression, variables);

                            if(val.TypeOf != propertyName.TypeAnnotation.ToLLVMType())
                            {
                                val = CoerceType(val, propertyName.TypeAnnotation.ToLLVMType(), "prop.assign.class.coerced", propertyAssignment.Variable);
                            }

                            SetStructProperty(structPointer, classStatement.Struct, propertyName.Name, val);
                            propertiesAssigned.Add(propertyName.Name);
                        }
                    }

                    // Obviously overwrites the user-defined property assignment with the default value(if any).
                    Variables fieldDefaultAssignments = classStatement.GetFields();
                    foreach (VariableDeclarationStatement field in fieldDefaultAssignments)
                    {
                        if (!propertiesAssigned.Contains(field.Name) && field.Expression != null)
                        {
                            LLVMValueRef val = EmitExpression(field.Expression, variables);

                            SetStructProperty(structPointer, classStatement.Struct, field.Name, val);
                        }
                    }

                    return structPointer;
                }

                if (objectInitializerExpression.TypeAnnotation.IsStruct && Structs.ContainsKey(identifier.Name))
                {
                    StructStatement structStatement = Structs[identifier.Name];
                    LLVMValueRef structPointer = CreateStructMalloc(structStatement);

                    foreach (AssignmentStatement propertyAssignment in objectInitializerExpression.Fields)
                    {
                        if (propertyAssignment.Variable is IdentifierExpression propertyName)
                        {
                            LLVMValueRef val = EmitExpression(propertyAssignment.Expression, variables);

                            if (val.TypeOf != propertyName.TypeAnnotation.ToLLVMType())
                            {
                                val = CoerceType(val, propertyName.TypeAnnotation.ToLLVMType(), "prop.assign.struct.coerced", propertyAssignment.Variable);
                            }

                            SetStructProperty(structPointer, structStatement, propertyName.Name, val);
                        }
                    }

                    return structPointer;
                }

                throw ErrorHandler.CreateError($"Could not initialize object {identifier.Name} as it does not exist.", objectInitializerExpression);
            }
            throw ErrorHandler.CreateError("Unsupported object initializer syntax.", objectInitializerExpression);
        }

        /// <summary>
        /// Sets the property of an allocated struct
        /// </summary>
        /// <param name="structPointer"></param>
        /// <param name="structDefinition"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        void SetStructProperty(LLVMValueRef structPointer, StructStatement structDefinition, string propertyName, LLVMValueRef value)
        {
            VariableDeclarationStatement field = structDefinition.Fields.GetVariable(propertyName);

            LLVMValueRef fieldPtr = Builder.BuildGEP2(
                structDefinition.LLVMStructType,
                structPointer,
                [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)],
                $"set_{propertyName}_field_ptr".AsSpan()
            );

            Builder.BuildStore(value, fieldPtr);
        }

        /// <summary>
        /// Gets the pointer to the property of an allocated struct
        /// </summary>
        /// <param name="structPointer"></param>
        /// <param name="structDefinition"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        LLVMValueRef GetStructPropertyPointer(LLVMValueRef structPointer, StructStatement structDefinition, string propertyName)
        {
            VariableDeclarationStatement field = structDefinition.Fields.GetVariable(propertyName);

            LLVMValueRef fieldPtr = Builder.BuildGEP2(
                structDefinition.LLVMStructType,
                structPointer,
                [LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)field.FieldIndex)],
                $"get_{propertyName}_field_ptr".AsSpan()
            );

            return fieldPtr;
        }

        /// <summary>
        /// Returns a pointer to the newly allocated struct
        /// </summary>
        /// <param name="structName"></param>
        /// <param name="structType"></param>
        /// <returns></returns>
        LLVMValueRef CreateStructMalloc(string structName, LLVMTypeRef structType)
        {
            return Builder.BuildMalloc(structType, $"{structName}_struct_instance");
        }

        /// <summary>
        /// Returns a pointer to the newly allocated struct
        /// </summary>
        /// <param name="structStatement"></param>
        /// <returns></returns>
        LLVMValueRef CreateStructMalloc(StructStatement structStatement)
            => CreateStructMalloc(structStatement.Name, structStatement.LLVMStructType);

        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left, variables);
            LLVMValueRef right = EmitExpression(relationalExpression.Right, variables);

            if (left == null || right == null)
            {
                throw ErrorHandler.CreateError("Left or right operand expression evaluated to null.", relationalExpression);
            }

            return CoerceRelationalType(relationalExpression.Operator, left, right, relationalExpression);
        }

        LLVMValueRef CoerceRelationalType(RelationalOperators relationalOperator, LLVMValueRef left, LLVMValueRef right, object errorObject)
        {
            LLVMTypeKind leftKind = left.TypeOf.Kind;
            LLVMTypeKind rightKind = right.TypeOf.Kind;

            if (leftKind == LLVMTypeKind.LLVMVectorTypeKind || rightKind == LLVMTypeKind.LLVMVectorTypeKind)
            {
                if (leftKind != rightKind)
                {
                    throw ErrorHandler.CreateError($"Type mismatch: Cannot compare vector with non-vector type.", errorObject);
                }

                LLVMTypeRef leftElementT = left.TypeOf.ElementType;
                LLVMTypeRef rightElementT = right.TypeOf.ElementType;

                if (left.TypeOf.VectorSize != right.TypeOf.VectorSize)
                {
                    throw ErrorHandler.CreateError($"Vector length mismatch: Cannot compare vector of size {left.TypeOf.VectorSize} with size {right.TypeOf.VectorSize}.", errorObject);
                }

                if (leftElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind || leftElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind ||
                    rightElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind || rightElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
                {
                    if (leftElementT != rightElementT)
                    {
                        if (leftElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind && rightElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
                        {
                            LLVMTypeRef targetVecT = LLVMTypeRef.CreateVector(LLVMTypeRef.Double, left.TypeOf.VectorSize);
                            left = Builder.BuildFPExt(left, targetVecT, "v.fpext.left");
                        }
                        else if (leftElementT.Kind == LLVMTypeKind.LLVMDoubleTypeKind && rightElementT.Kind == LLVMTypeKind.LLVMFloatTypeKind)
                        {
                            LLVMTypeRef targetVecT = LLVMTypeRef.CreateVector(LLVMTypeRef.Double, right.TypeOf.VectorSize);
                            right = Builder.BuildFPExt(right, targetVecT, "v.fpext.right");
                        }
                    }

                    switch (relationalOperator)
                    {
                        case RelationalOperators.Equal: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, left, right, "v.fcmp");
                        case RelationalOperators.NotEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, left, right, "v.fcmp");
                        case RelationalOperators.GreaterThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, left, right, "v.fcmp");
                        case RelationalOperators.LessThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, left, right, "v.fcmp");
                        case RelationalOperators.GreaterThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, left, right, "v.fcmp");
                        case RelationalOperators.LessThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, left, right, "v.fcmp");
                        default: throw ErrorHandler.CreateError($"Unsupported float vector relational operator: {relationalOperator}", errorObject);
                    }
                }

                if (leftElementT.Kind == LLVMTypeKind.LLVMIntegerTypeKind && rightElementT.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                {
                    uint leftWidth = leftElementT.IntWidth;
                    uint rightWidth = rightElementT.IntWidth;

                    if (leftWidth != rightWidth)
                    {
                        if (leftWidth < rightWidth)
                        {
                            LLVMTypeRef targetVecT = LLVMTypeRef.CreateVector(rightElementT, left.TypeOf.VectorSize);
                            left = Builder.BuildSExt(left, targetVecT, "v.sext.left");
                        }
                        else
                        {
                            LLVMTypeRef targetVecT = LLVMTypeRef.CreateVector(leftElementT, right.TypeOf.VectorSize);
                            right = Builder.BuildSExt(right, targetVecT, "v.sext.right");
                        }
                    }

                    switch (relationalOperator)
                    {
                        case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "v.icmp");
                        case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "v.icmp");
                        case RelationalOperators.GreaterThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "v.icmp");
                        case RelationalOperators.LessThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "v.icmp");
                        case RelationalOperators.GreaterThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "v.icmp");
                        case RelationalOperators.LessThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "v.icmp");
                        default: throw ErrorHandler.CreateError($"Unsupported integer vector relational operator: {relationalOperator}", errorObject);
                    }
                }

                throw ErrorHandler.CreateError($"Unsupported vector element type comparison.", errorObject);
            }

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

                switch (relationalOperator)
                {
                    case RelationalOperators.Equal: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, left, right, "fcmp");
                    case RelationalOperators.NotEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, left, right, "fcmp");
                    case RelationalOperators.GreaterThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, left, right, "fcmp");
                    case RelationalOperators.LessThan: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, left, right, "fcmp");
                    case RelationalOperators.GreaterThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, left, right, "fcmp");
                    case RelationalOperators.LessThanOrEqual: return Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, left, right, "fcmp");
                    default: throw ErrorHandler.CreateError($"Unsupported float relational operator: {relationalOperator}", errorObject);
                }
            }

            if (leftKind == LLVMTypeKind.LLVMPointerTypeKind || rightKind == LLVMTypeKind.LLVMPointerTypeKind)
            {
                if (leftKind == LLVMTypeKind.LLVMIntegerTypeKind && left.IsConstant)
                {
                    left = Builder.BuildIntToPtr(left, right.TypeOf, "int2ptr.cast");
                }

                if (rightKind == LLVMTypeKind.LLVMIntegerTypeKind && right.IsConstant)
                {
                    right = Builder.BuildIntToPtr(right, left.TypeOf, "int2ptr.cast");
                }

                if (left.TypeOf != right.TypeOf)
                {
                    throw ErrorHandler.CreateError($"Type mismatch: Cannot compare pointer types {left.TypeOf} and {right.TypeOf}.", errorObject);
                }

                switch (relationalOperator)
                {
                    case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "ptr.icmp");
                    case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "ptr.icmp");
                    default: throw ErrorHandler.CreateError($"Operator {relationalOperator} is invalid for pointer types.", errorObject);
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

                switch (relationalOperator)
                {
                    case RelationalOperators.Equal: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "icmp");
                    case RelationalOperators.NotEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "icmp");
                    case RelationalOperators.GreaterThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "icmp");
                    case RelationalOperators.LessThan: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "icmp");
                    case RelationalOperators.GreaterThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "icmp");
                    case RelationalOperators.LessThanOrEqual: return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "icmp");
                    default: throw ErrorHandler.CreateError($"Unsupported integer relational operator: {relationalOperator}", errorObject);
                }
            }

            throw ErrorHandler.CreateError($"Cannot emit comparison between unhandled types: {leftKind} and {rightKind}.", errorObject);
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

            if (value.Handle == IntPtr.Zero)
            {
                throw ErrorHandler.CreateError("Expression inside 'Not' statement failed to generate valid LLVM IR value.", notExpression);
            }

            LLVMTypeRef valType = value.TypeOf;
            if (valType.Handle == IntPtr.Zero)
            {
                throw ErrorHandler.CreateError("Unable to resolve LLVM type information for 'Not' expression target.", notExpression);
            }

            if (valType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                if (valType.IntWidth == 1)
                {
                    LLVMValueRef trueVal = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false);
                    return Builder.BuildXor(value, trueVal, "logical.not");
                }

                return Builder.BuildNot(value, "bitwise.not");
            }

            throw ErrorHandler.CreateError($"The 'Not' unary operation is invalid for type '{valType}' (Kind: {valType.Kind}).", notExpression);
        }


        LLVMValueRef EmitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression, Variables variables)
        {
            if (parenthesizedExpression?.Expression == null)
            {
                throw ErrorHandler.CreateError("Parenthesized expression context contains no underlying target AST node.", parenthesizedExpression);
            }

            return EmitExpression(parenthesizedExpression.Expression, variables);
        }

        LLVMValueRef EmitBooleanExpression(BooleanExpression booleanExpression)
        {
            return booleanExpression.Value
                ? LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false)
                : LLVMValueRef.CreateConstNull(LLVMTypeRef.Int1);
        }

        LLVMValueRef EmitCharacterExpression(CharacterExpression characterExpression)
        {
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int8, (ulong)characterExpression.Value, false);
        }

        LLVMValueRef EmitNegateExpression(NegateExpression negateExpression, Variables variables)
        {
            LLVMValueRef valueToNegate = EmitExpression(negateExpression.Expression, variables);
            TypeAnnotation annotation = negateExpression.Expression.TypeAnnotation;

            if (annotation.IsArray || annotation.IsStruct)
            {
                throw ErrorHandler.CreateError($"Compile Error: Cannot apply negation operator to complex structural type: {annotation}", negateExpression);
            }

            if (annotation.IsReservedType)
            {
                switch (annotation.ReservedType)
                {
                    case ReservedTypes.Bool:
                        return Builder.BuildNot(valueToNegate, "logical.not");

                    case ReservedTypes.F32:
                    case ReservedTypes.F64:
                        return Builder.BuildFNeg(valueToNegate, "fp.neg");

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
                        return Builder.BuildNeg(valueToNegate, "int.neg");

                    case ReservedTypes.String:
                    case ReservedTypes.Fn:
                    default:
                        throw ErrorHandler.CreateError($"Compile Error: Negation operator is invalid for type primitive: '{annotation.ReservedType}'", negateExpression);
                }
            }

            throw ErrorHandler.CreateError($"Compile Error: Unknown type annotation state encountered during negation emission.", negateExpression);
        }

        LLVMValueRef EmitNullExpression()
        {
            LLVMTypeRef opaquePointerType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

            return LLVMValueRef.CreateConstPointerNull(opaquePointerType);
        }

        LLVMValueRef EmitLogicalExpression(LogicalExpression logicalExpression, Variables variables)
        {
            LLVMValueRef left = EmitExpression(logicalExpression.Left, variables);

            if (left == null)
            {
                throw ErrorHandler.CreateError("Left operand expression evaluated to null.", logicalExpression);
            }

            if (left.TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw ErrorHandler.CreateError("Logical operators require boolean operands.", logicalExpression);
            }
            if (left.TypeOf.IntWidth != 1)
            {
                left = Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, LLVMValueRef.CreateConstInt(left.TypeOf, 0, false), "left.tobool");
            }

            LLVMBasicBlockRef lhsEndBlock = Builder.InsertBlock;
            LLVMValueRef currentFunction = lhsEndBlock.Parent;

            LLVMBasicBlockRef rhsBlock = currentFunction.AppendBasicBlock("logical.rhs");
            LLVMBasicBlockRef mergeBlock = currentFunction.AppendBasicBlock("logical.merge");

            switch (logicalExpression.Operator)
            {
                case LogicalOperator.And:
                    Builder.BuildCondBr(left, rhsBlock, mergeBlock);
                    break;

                case LogicalOperator.Or:
                    Builder.BuildCondBr(left, mergeBlock, rhsBlock);
                    break;

                default:
                    throw ErrorHandler.CreateError($"Logical operator {logicalExpression.Operator} is not supported.", logicalExpression);
            }

            Builder.PositionAtEnd(rhsBlock);
            LLVMValueRef right = EmitExpression(logicalExpression.Right, variables);

            if (right == null)
            {
                throw ErrorHandler.CreateError("Right operand expression evaluated to null.", logicalExpression);
            }

            if (right.TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
            {
                throw ErrorHandler.CreateError("Logical operators require boolean operands.", logicalExpression);
            }
            if (right.TypeOf.IntWidth != 1)
            {
                right = Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, right, LLVMValueRef.CreateConstInt(right.TypeOf, 0, false), "right.tobool");
            }

            LLVMBasicBlockRef rhsEndBlock = Builder.InsertBlock;
            Builder.BuildBr(mergeBlock);

            Builder.PositionAtEnd(mergeBlock);
            LLVMValueRef phi = Builder.BuildPhi(LLVMTypeRef.Int1, "logical.result");

            if (logicalExpression.Operator == LogicalOperator.And)
            {
                phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0, false), right }, new[] { lhsEndBlock, rhsEndBlock }, 2);
            }
            else
            {
                phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1, false), right }, new[] { lhsEndBlock, rhsEndBlock }, 2);
            }

            return phi;
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
                CharacterExpression characterExpression => EmitCharacterExpression(characterExpression),
                NegateExpression negateExpression => EmitNegateExpression(negateExpression, variables),
                NullExpression nullExpression => EmitNullExpression(),
                LogicalExpression logicalExpression => EmitLogicalExpression(logicalExpression, variables),
                _ => throw ErrorHandler.CreateError($"Unsupported expression type: {expression.GetType().Name}", expression)
            };
        }

        LLVMValueRef[] EmitExpressions(List<Expression> expressions, Variables variables)
        {
            return expressions.Select(expr => EmitExpression(expr, variables)).ToArray();
        }
    }
}