using CommonC.Parser.AST.Expressions;
using CommonC.Parser.AST.Statements;
using CommonIR;
using CommonIR.Generators.WASM;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace CommonC.Targets.CommonIR.CodeGen
{
    public class CommonIRCodeGen
    {
        /// <summary>
        /// The topmost closure of the tree. Contains all statements, functions, structs and globals.
        /// </summary>
        ClosureStatement UpperClosure { get; set; }

        IRModule Module { get; set; } = new IRModule();

        IRBuilder? Builder { get; set; }

        public CommonIRCodeGen(ClosureStatement closure)
        {
            UpperClosure = closure;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            CreateFunctions();
            WasmGenerator generator = new WasmGenerator(Module);
            return generator.GenerateSourceFiles();
        }

        void CreateFunctions()
        {
            foreach(FunctionDeclarationStatement function in UpperClosure.Statements.Where(s => s is FunctionDeclarationStatement))
            {
                if(function.IsExtern)
                {
                    string[] nameParts = function.Name.Split("__"); // Note: Temporary workaround for name mangling. Should be replaced with a proper name mangling system.
                    Module.CreateFunctionImport(nameParts[0], nameParts[1], function.ReturnType.TypeAnnotation.ToCommonIRType(), [.. function.Parameters.Select(p => new IRLocal(p.Name, p.Type.TypeAnnotation.ToCommonIRType(), false))]);
                }
                else
                {
                    IRFunction iRFunction = Module.CreateFunction(function.Name, function.ReturnType.TypeAnnotation.ToCommonIRType(), [.. function.Parameters.Select(p => new IRLocal(p.Name, p.Type.TypeAnnotation.ToCommonIRType(), false))]);
                    EmitFunction(function, iRFunction);
                }
            }
        }

        void EmitFunction(FunctionDeclarationStatement function, IRFunction iRFunction)
        {
            if(Builder == null)
            {
                Builder = new IRBuilder(this.Module, iRFunction, iRFunction.Entryblock);
            }
            else
            {
                Builder.PositionAtStart(iRFunction, iRFunction.Entryblock);
            }

            EmitStatements(function.Body.Statements);
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
            switch(statement)
            {
                case ReturnStatement returnStatement:
                    EmitReturnStatement(returnStatement);
                    break;

                case CallStatement callStatement:
                    EmitCallStatement(callStatement);
                    break;
            }
        }

        void EmitCallStatement(CallStatement callStatement)
        {
            if(callStatement.Expression is IdentifierExpression callTarget)
            {
                if(Module.TryGetFunction(callTarget.Name, out IRFunction? function))
                {

                }
            }
        }

        void EmitReturnStatement(ReturnStatement returnStatement)
        {
            if (returnStatement.Expression is not null)
            {
                Builder.BuildReturn(EmitExpression(returnStatement.Expression));
            }
            else
            {
                Builder.BuildReturn();
            }
        }

        IRValueInstruction EmitExpression(Expression expression)
        {
            switch (expression)
            {
                case IdentifierExpression identifierExpression:
                    return EmitIdentifierExpression(identifierExpression);
                case NumberExpression numberExpression:
                    return EmitNumberExpression(numberExpression);
                case ArithmeticExpression arithmeticExpression:
                    return EmitArithmeticExpression(arithmeticExpression);
                default:
                    throw new NotImplementedException($"Expression type {expression.GetType().Name} is not implemented.");
            }
        }

        IRValueInstruction EmitIdentifierExpression(IdentifierExpression identifierExpression)
        {
            List<IRLocal> locals = Builder.Function.Locals.Where(l => l.Name == identifierExpression.Name).ToList();
            if (locals.Count == 1)
            {
                return Builder.BuildLoad(locals[0]);
            }

            List<IRLocal> parameters = Builder.Function.Parameters.Where(p => p.Name == identifierExpression.Name).ToList();
            if(parameters.Count == 1)
            {
                return Builder.BuildLoad(parameters[0]);
            }

            throw new Exception($"Identifier {identifierExpression.Name} not found in function {Builder.Function.Name}.");
        }

        IRValueInstruction EmitNumberExpression(NumberExpression numberExpression)
        {
            if(long.TryParse(numberExpression.Value, out long value))
            {
                return Builder.BuildConstantInteger(IRDataTypes.Int32, value);
            }
            else
            {
                throw new NotImplementedException($"Number expression with value '{numberExpression.Value}' could not be parsed as a long.");
            }
        }

        IRValueInstruction EmitArithmeticExpression(ArithmeticExpression arithmeticExpression)
        {
            IRValueInstruction left = EmitExpression(arithmeticExpression.Left);
            IRValueInstruction right = EmitExpression(arithmeticExpression.Right);
            
            switch(arithmeticExpression.Operator)
            {
                case ArithmeticOperator.Addition:
                    return Builder.BuildAdd(left, right);
                default:
                    throw new NotImplementedException($"Arithmetic operator {arithmeticExpression.Operator} is not implemented.");
            }
        }
    }
}

#pragma warning restore CS8602 // Dereference of a possibly null reference.