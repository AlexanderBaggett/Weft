using System.Globalization;
using Weft.Compiler.Syntax;

namespace Weft.Compiler.Semantics;

// Parameter defaults are evaluated in the shared compiler, never by a host backend.
internal static class ConstantEvaluator
{
    public static ConstantValue? Evaluate(ExpressionSyntax expression)
    {
        try { return EvaluateCore(expression); }
        catch (ArithmeticException) { return null; }
    }

    private static ConstantValue? EvaluateCore(ExpressionSyntax expression)
    {
        if (expression is LiteralSyntax literal)
        {
            var token = literal.Token;
            if (token.Kind == TokenKind.String) return new(token.StringValue!, WeftType.String);
            if (token.Text is "true" or "false") return new(token.Text == "true", WeftType.Bool);
            var text = token.Text.Replace("_", "", StringComparison.Ordinal);
            var wide = text.EndsWith('L') || text.EndsWith('l');
            if (wide) text = text[..^1];
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return null;
            return !wide && number <= int.MaxValue ? new((int)number, WeftType.Int32) : new(number, WeftType.Int64);
        }
        if (expression is UnarySyntax unary)
        {
            if (unary.Operator == "-" && unary.Operand is LiteralSyntax negative)
            {
                var text = negative.Token.Text.Replace("_", "", StringComparison.Ordinal);
                if (text == "2147483648") return new(int.MinValue, WeftType.Int32);
                if (text is "9223372036854775808" or "9223372036854775808L" or "9223372036854775808l") return new(long.MinValue, WeftType.Int64);
            }
            var operand = EvaluateCore(unary.Operand);
            return (unary.Operator, operand?.Value) switch
            {
                ("!", bool value) => new(!value, WeftType.Bool),
                ("+", int or long) => operand,
                ("-", int value) => new(checked(-value), WeftType.Int32),
                ("-", long value) => new(checked(-value), WeftType.Int64),
                _ => null
            };
        }
        if (expression is not BinarySyntax binary) return null;
        var left = EvaluateCore(binary.Left); var right = EvaluateCore(binary.Right);
        if (left is null || right is null) return null;
        if (left.Value is string a && right.Value is string b)
            return binary.Operator switch
            { "+" => new(a + b, WeftType.String), "==" => new(a == b, WeftType.Bool), "!=" => new(a != b, WeftType.Bool), _ => null };
        if (left.Value is bool x && right.Value is bool y)
            return binary.Operator switch
            { "&&" => new(x && y, WeftType.Bool), "||" => new(x || y, WeftType.Bool), "==" => new(x == y, WeftType.Bool), "!=" => new(x != y, WeftType.Bool), _ => null };
        if (left.Value is not (int or long) || right.Value is not (int or long)) return null;
        var l = Convert.ToInt64(left.Value, CultureInfo.InvariantCulture);
        var r = Convert.ToInt64(right.Value, CultureInfo.InvariantCulture);
        if (binary.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=")
            return new(binary.Operator switch { "==" => l == r, "!=" => l != r, "<" => l < r, ">" => l > r, "<=" => l <= r, _ => l >= r }, WeftType.Bool);
        long result;
        checked
        {
            result = binary.Operator switch
            {
                "+" => l + r, "-" => l - r, "*" => l * r, "/" => l / r,
                "%" => l == long.MinValue && r == -1 ? 0 : l % r,
                _ => throw new ArithmeticException("Unsupported constant operator.")
            };
        }
        return left.Type == WeftType.Int64 || right.Type == WeftType.Int64
            ? new(result, WeftType.Int64) : new(checked((int)result), WeftType.Int32);
    }
}
