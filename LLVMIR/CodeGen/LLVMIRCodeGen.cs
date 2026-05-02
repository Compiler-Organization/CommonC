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

        LLVMValueRef CurrentFunction { get; set; }

        Dictionary<string, FunctionDeclarationStatement> Functions = new Dictionary<string, FunctionDeclarationStatement>();

        public LLVMModuleRef GenerateLLVMModule()
        {
            Module = LLVMModuleRef.CreateWithName(Settings.Name);
            Builder = LLVMBuilderRef.Create(Module.Context);

            CreateExtern(name: "puts", returnType: LLVMTypeRef.Int32, parameters: [LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)]);
            CreateFunctionReferences();
            EmitStatements(Statements);

            return Module;
        }

        LLVMTypeRef ResolveLLVMTypeFromExpression(Expression expression)
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

            throw new Exception($"Expression {expression.GetType().Name} could not be resolved to an LLVM type.");
        }

        void CreateFunctionReferences()
        {
            foreach(FunctionDeclarationStatement functionDeclarationStatement in Statements.OfType<FunctionDeclarationStatement>())
            {
                LLVMTypeRef returnType = ResolveLLVMTypeFromExpression(functionDeclarationStatement.ReturnType);
                LLVMTypeRef[] parameterTypes = functionDeclarationStatement.Parameters.Select(p => ResolveLLVMTypeFromExpression(p.Type)).ToArray();
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

        void EmitCallStatement(CallStatement callStatement)
        {
            if(callStatement.Expression is IdentifierExpression identifierExpression)
            {
                if(!Functions.ContainsKey(identifierExpression.Name))
                {
                    throw new Exception($"Function {identifierExpression.Name} is not defined.");
                }

                FunctionDeclarationStatement function = Functions[identifierExpression.Name];
                Builder.BuildCall2(function.LLVMFunctionType, function.LLVMFunction, EmitExpressions(callStatement.Arguments), "");
                return;
            }

            throw new Exception($"Call expression of type {callStatement.Expression.GetType().Name} is not supported when emitting LLVM call statements.");
        }

        void EmitFunctionDeclarationStatement(FunctionDeclarationStatement functionDeclarationStatement)
        {
            LLVMBasicBlockRef entryBlock = functionDeclarationStatement.LLVMFunction.EntryBasicBlock;
            Builder.PositionAtEnd(entryBlock);

            CurrentFunction = functionDeclarationStatement.LLVMFunction;

            if (functionDeclarationStatement.Body != null && functionDeclarationStatement.Body.Statements.Count > 0)
            {
                EmitStatements(functionDeclarationStatement.Body.Statements);

                if(functionDeclarationStatement.Body.Statements.Last() is not ReturnStatement)
                {
                    if(functionDeclarationStatement.ReturnType is TypeExpression typeExpression && typeExpression.Type == ReservedTypes.Fn)
                    {
                        Builder.BuildRetVoid();
                    }
                    else
                    {
                        throw new Exception($"Function {functionDeclarationStatement.Name} does not have a return statement, but its return type is not void.");
                    }
                }
            }
        }

        void EmitIfStatement(IfStatement ifStatement)
        {
                LLVMBasicBlockRef thenBlock = CurrentFunction.AppendBasicBlock("then");
                LLVMBasicBlockRef elseBlock = CurrentFunction.AppendBasicBlock("else");
                LLVMBasicBlockRef mergeBlock = CurrentFunction.AppendBasicBlock("ifcont");
    
                LLVMValueRef condition = EmitExpression(ifStatement.Condition);
                Builder.BuildCondBr(condition, thenBlock, elseBlock);
    
                // Then block
                Builder.PositionAtEnd(thenBlock);
                EmitStatements(ifStatement.Body.Statements);
                Builder.BuildBr(mergeBlock);
    
                // Else block
                Builder.PositionAtEnd(elseBlock);
                if(ifStatement.ElseIfs != null)
                {
                    foreach(IfStatement elseIf in ifStatement.ElseIfs)
                    {
                        EmitIfStatement(elseIf);
                    }
                }
                if(ifStatement.Else != null)
                {
                    EmitStatements(ifStatement.Else.Statements);
                }
                Builder.BuildBr(mergeBlock);
    
                // Merge block
                Builder.PositionAtEnd(mergeBlock);
        }

        void EmitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
        {
            LLVMTypeRef variableType = ResolveLLVMTypeFromExpression(variableDeclarationStatement.Type);
            LLVMValueRef alloca = Builder.BuildAlloca(variableType, variableDeclarationStatement.Name);
            if(variableDeclarationStatement.Expression != null)
            {
                LLVMValueRef initializerValue = EmitExpression(variableDeclarationStatement.Expression);
                Builder.BuildStore(initializerValue, alloca);
            }
        }

        void EmitStatements(StatementList statements)
        {
            foreach (Statement statement in statements)
            {
                if (statement is FunctionDeclarationStatement functionDeclarationStatement)
                {
                    EmitFunctionDeclarationStatement(functionDeclarationStatement);
                    continue;
                }

                if (statement is CallStatement callStatement)
                {
                    EmitCallStatement(callStatement);
                    continue;
                }

                if(statement is IfStatement ifStatement) 
                { 
                    EmitIfStatement(ifStatement);
                    continue;
                }

                if(statement is VariableDeclarationStatement variableDeclarationStatement)
                {
                    EmitVariableDeclarationStatement(variableDeclarationStatement);
                    continue;
                }

                throw new Exception($"Statement {statement.GetType().Name} is not supported in when emitting LLVM statements.");
            }
        }

        LLVMValueRef[] EmitExpressions(ExpressionList expressions)
        {
            return expressions.Select(EmitExpression).ToArray();
        }

        LLVMValueRef EmitExpression(Expression expression)
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
                return EmitArithmeticExpression(arithmeticExpression);
            }

            if(expression is RelationalExpression relationalExpression)
            {
                return EmitRelationalExpression(relationalExpression);
            }

            throw new Exception($"Expression {expression.GetType().Name} is not supported when emitting LLVM expressions.");
        }

        LLVMValueRef EmitIdentifierExpression(IdentifierExpression identifierExpression)
        {
            
        }

        LLVMValueRef EmitRelationalExpression(RelationalExpression relationalExpression)
        {
            LLVMValueRef left = EmitExpression(relationalExpression.Left);
            LLVMValueRef right = EmitExpression(relationalExpression.Right);
            switch(relationalExpression.Operator)
            {
                case RelationalOperators.Equal:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "");
                case RelationalOperators.NotEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "");
                case RelationalOperators.GreaterThan:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "");
                case RelationalOperators.LessThan:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "");
                case RelationalOperators.GreaterThanOrEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "");
                case RelationalOperators.LessThanOrEqual:
                    return Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "");
                default:
                    throw new Exception($"Relational operator {relationalExpression.Operator} is not supported when emitting LLVM relational expressions.");
            }
        }

        // TODO: Support power and right shift operators.
        LLVMValueRef EmitArithmeticExpression(ArithmeticExpression arithmeticExpression)
        {
            LLVMValueRef left = EmitExpression(arithmeticExpression.Left);
            LLVMValueRef right = EmitExpression(arithmeticExpression.Right);
            switch(arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    return Builder.BuildAdd(left, right, "");
                case ArithmeticOperator.Subtraction:
                    return Builder.BuildSub(left, right, "");
                case ArithmeticOperator.Multiplication:
                    return Builder.BuildMul(left, right, "");
                case ArithmeticOperator.Division:
                    return Builder.BuildSDiv(left, right, "");
                case ArithmeticOperator.Modulus:
                    return Builder.BuildSRem(left, right, "");
                case ArithmeticOperator.LeftShift:
                    return Builder.BuildShl(left, right, "");
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
            return Builder.BuildGlobalStringPtr(stringExpression.Value);
        }
    }
}
