using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Text;

namespace Weft.Compiler.Syntax;

public enum TokenKind { Identifier, Number, String, InterpolatedString, Symbol, End }
public sealed record SyntaxToken(TokenKind Kind, string Text, SourceLocation Location, string? StringValue = null);
public sealed record LexResult(ImmutableArray<SyntaxToken> Tokens, ImmutableArray<Diagnostic> Diagnostics);

public sealed class Lexer(SourceText source)
{
    private readonly DiagnosticBag diagnostics = [];
    private int position;
    private char Current => position < source.Text.Length ? source.Text[position] : '\0';
    private char Peek(int offset) => position + offset < source.Text.Length ? source.Text[position + offset] : '\0';
    private static readonly HashSet<string> Pairs = ["=>", "->", "==", "!=", "<=", ">=", "&&", "||", "??", "?.", "..", "++", "--", "+=", "-=", "*=", "/=", "%=", "::"];

    public LexResult Lex()
    {
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();
        while (position < source.Text.Length)
        {
            if (char.IsWhiteSpace(Current)) { position++; continue; }
            if (Current == '/' && Peek(1) == '/')
            {
                while (position < source.Text.Length && Current is not ('\r' or '\n')) position++;
                continue;
            }
            if (Current == '/' && Peek(1) == '*')
            {
                var commentStart = position;
                position += 2;
                while (position < source.Text.Length && !(Current == '*' && Peek(1) == '/')) position++;
                if (position == source.Text.Length) diagnostics.Error("WF1001", "Unterminated block comment.", source.Location(commentStart, position - commentStart));
                else position += 2;
                continue;
            }
            var start = position;
            if (Current == '"' || (Current is '@' or '$' && Peek(1) == '"') ||
                (Current == '$' && Peek(1) == '@' && Peek(2) == '"'))
            {
                tokens.Add(ReadString());
            }
            else if (char.IsLetter(Current) || Current == '_')
            {
                position++;
                while (char.IsLetterOrDigit(Current) || Current == '_') position++;
                tokens.Add(Token(TokenKind.Identifier, start));
            }
            else if (char.IsDigit(Current))
            {
                position++;
                while (char.IsLetterOrDigit(Current) || Current == '_' ||
                    (Current == '.' && char.IsDigit(Peek(1)))) position++;
                tokens.Add(Token(TokenKind.Number, start));
            }
            else if ("{}()[];:,.+-*/%!=<>?&|^~".Contains(Current))
            {
                var pair = $"{Current}{Peek(1)}";
                position += Pairs.Contains(pair) ? 2 : 1;
                tokens.Add(Token(TokenKind.Symbol, start));
            }
            else
            {
                diagnostics.Error("WF1002", $"Unexpected character U+{(int)Current:X4}.", source.Location(position, 1));
                position++;
            }
        }
        tokens.Add(new(TokenKind.End, "", source.Location(position)));
        return new(tokens.ToImmutable(), diagnostics.ToImmutableArray());
    }

    private SyntaxToken Token(TokenKind kind, int start, string? value = null) =>
        new(kind, source.Text[start..position], source.Location(start, position - start), value);

    private SyntaxToken ReadString()
    {
        var start = position;
        var interpolated = Current == '$';
        var verbatim = Current == '@' || (Current == '$' && Peek(1) == '@');
        while (Current != '"') position++;
        position++;
        var value = new StringBuilder();
        var closed = false;
        while (position < source.Text.Length)
        {
            var ch = Current;
            position++;
            if (interpolated && ch == '{')
            {
                if (Current == '{') { value.Append("{{"); position++; continue; }
                var interpolationStart = position - 1;
                var depth = 1;
                while (position < source.Text.Length && depth > 0)
                {
                    if (Current == '"' || (Current is '@' or '$' && Peek(1) == '"') || (Current == '$' && Peek(1) == '@' && Peek(2) == '"')) { ReadString(); continue; }
                    if (Current == '/' && Peek(1) == '/')
                    {
                        while (position < source.Text.Length && Current is not ('\r' or '\n')) position++;
                        continue;
                    }
                    if (Current == '/' && Peek(1) == '*')
                    {
                        position += 2;
                        while (position < source.Text.Length && !(Current == '*' && Peek(1) == '/')) position++;
                        if (position < source.Text.Length) position += 2;
                        continue;
                    }
                    if (Current == '{') depth++;
                    if (Current == '}') depth--;
                    position++;
                }
                if (depth != 0) diagnostics.Error("WF1005", "Unterminated interpolation expression.", source.Location(interpolationStart, position - interpolationStart));
                value.Append(source.Text.AsSpan(interpolationStart, position - interpolationStart));
                continue;
            }
            if (ch == '"')
            {
                if (verbatim && Current == '"') { value.Append('"'); position++; continue; }
                closed = true; break;
            }
            if (!verbatim && ch is '\r' or '\n') break;
            if (ch == '\\' && !verbatim)
            {
                var escapePosition = position - 1;
                var escaped = Current;
                if (position < source.Text.Length) position++;
                switch (escaped)
                {
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case '0': value.Append('\0'); break;
                    case '\\': value.Append('\\'); break;
                    case '"': value.Append('"'); break;
                    case 'u':
                        if (position + 4 <= source.Text.Length && ushort.TryParse(source.Text.AsSpan(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        { value.Append((char)code); position += 4; }
                        else diagnostics.Error("WF1003", "A Unicode escape requires four hexadecimal digits.", source.Location(escapePosition, position - escapePosition));
                        break;
                    default: diagnostics.Error("WF1003", $"Invalid escape sequence '\\{escaped}'.", source.Location(escapePosition, position - escapePosition)); break;
                }
            }
            else value.Append(ch);
        }
        if (!closed) diagnostics.Error("WF1004", "Unterminated string literal.", source.Location(start, position - start));
        return Token(interpolated ? TokenKind.InterpolatedString : TokenKind.String, start, value.ToString());
    }
}
