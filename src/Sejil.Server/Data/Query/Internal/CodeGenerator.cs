﻿// Copyright (C) 2017 Alaa Masoud
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Sejil.Data.Query.Internal
{
    internal static class TokenExtensions
    {
        public static string NegateIfNonInclusion(this Token token) => token.Type == TokenType.NotEqual
            ? "="
            : token.Type == TokenType.NotLike
                ? "LIKE"
                : token.Text;

        public static bool IsExluding(this Token token) => token.Type == TokenType.NotEqual || token.Type == TokenType.NotLike;
    }

    internal class CodeGenerator : Expr.IVisitor
    {
        private readonly StringBuilder _sql = new StringBuilder();
        private bool _insidePropBlock;

        public string Generate(Expr expr)
        {
            Resolve(expr);
            if (_insidePropBlock)
            {
                _sql.Append(")");
            }
            return _sql.ToString();
        }

        private void Resolve(Expr expr) => expr.Accept(this);

        public void Visit(Expr.Binary expr)
        {
            if (!(expr.Left is Expr.Variable))
            {
                throw new QueryEngineException($"Error at position '{expr.Operator.Position - 1}': Left side of comparison can only be a property/non property name.");
            }

            if (!(expr.Right is Expr.Literal))
            {
                throw new QueryEngineException($"Error at position '{expr.Operator.Position + expr.Operator.Text.Length + 1}': Right side of comparison can only be a value.");
            }

            if (expr.Operator.Type == TokenType.Like || expr.Operator.Type == TokenType.NotLike)
            {
                if (!(expr.Right is Expr.Literal literal) || !(literal.Value is string))
                {
                    throw new QueryEngineException($"Error at position '{expr.Operator.Position + expr.Operator.Text.Length + 2}': 'like'/'not like' keywords can only be used with strings.");
                }
            }

            CheckPropertyScope(expr);

            Resolve(expr.Left);

            _sql.Append(expr.IsProperty
                ? $"value {expr.Operator.NegateIfNonInclusion().ToUpper()} "
                : $" {expr.Operator.Text.ToUpper()} ");

            Resolve(expr.Right);

            if (expr.Left.IsProperty)
            {
                _sql.Append($") {(expr.Operator.IsExluding() ? "=" : ">")} 0");
            }
        }

        public void Visit(Expr.Grouping expr)
        {
            CheckPropertyScope(expr);

            _sql.Append("(");
            Resolve(expr.Expression);
            _sql.Append(")");
        }

        public void Visit(Expr.Literal expr) => _sql.Append($"'{expr.Value}'");

        public void Visit(Expr.Logical expr)
        {
            Resolve(expr.Left);
            if (_insidePropBlock && !expr.Right.IsProperty)
            {
                _sql.Append(")");
            }
            _sql.Append($" {expr.Operator.Text.ToUpper()} ");
            Resolve(expr.Right);
        }

        public void Visit(Expr.Variable expr) => _sql.Append(expr.IsProperty
            ? $"SUM(name = '{expr.Token.Text}' AND "
            : expr.Token.Text);

        private void CheckPropertyScope(Expr expr)
        {
            if (expr.IsProperty && !_insidePropBlock)
            {
                _sql.Append("id IN (SELECT logId FROM log_property GROUP BY logId HAVING ");
                _insidePropBlock = true;
            }
            else if (!expr.IsProperty)
            {
                _insidePropBlock = false;
            }
        }
    }
}
