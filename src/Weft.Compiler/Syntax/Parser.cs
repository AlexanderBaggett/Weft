using System.Collections.Immutable;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Text;

namespace Weft.Compiler.Syntax;

public sealed class Parser
{
    private readonly SourceText source;
    private readonly ImmutableArray<SyntaxToken> tokens;
    private readonly DiagnosticBag diagnostics = [];
    private int position;
    private SyntaxToken Current => tokens[Math.Min(position, tokens.Length - 1)];
    private SyntaxToken Peek(int offset) => tokens[Math.Min(position + offset, tokens.Length - 1)];
    private static readonly HashSet<string> Modifiers = ["public", "private", "internal", "static", "async", "pure", "idempotent", "external"];
    private static readonly Dictionary<string, ConstructKind> Constructs = Enum.GetValues<ConstructKind>()
        .Where(k => k != ConstructKind.SwitchGroup).ToDictionary(k => k.ToString().ToLowerInvariant());

    public Parser(SourceText source)
    {
        this.source = source;
        var lex = new Lexer(source).Lex();
        tokens = lex.Tokens;
        diagnostics.AddRange(lex.Diagnostics);
    }

    public SyntaxTree Parse() => new(source, ParseDeclarations(false), diagnostics.ToImmutableArray());
    private SyntaxToken Next() { var token = Current; if (Current.Kind != TokenKind.End) position++; return token; }
    private bool Take(string text) { if (Current.Text != text) return false; Next(); return true; }
    private SyntaxToken Expect(string text)
    {
        if (Current.Text == text) return Next();
        diagnostics.Error("WF1101", $"Expected '{text}', found '{Display(Current)}'.", Current.Location);
        return new(TokenKind.Symbol, text, Current.Location);
    }
    private SyntaxToken Identifier()
    {
        if (Current.Kind == TokenKind.Identifier) return Next();
        diagnostics.Error("WF1102", $"Expected an identifier, found '{Display(Current)}'.", Current.Location);
        if (Current.Kind != TokenKind.End) return Next();
        return Current;
    }
    private static string Display(SyntaxToken token) => token.Kind == TokenKind.End ? "end of file" : token.Text;

    private ImmutableArray<DeclarationSyntax> ParseDeclarations(bool nested)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationSyntax>();
        while (Current.Kind != TokenKind.End && (!nested || Current.Text != "}"))
        {
            var before = position;
            declarations.Add(ParseDeclaration());
            if (position == before) Next();
        }
        return declarations.ToImmutable();
    }

    private DeclarationSyntax ParseDeclaration()
    {
        var location = Current.Location;
        var modifiers = ImmutableArray.CreateBuilder<string>();
        while (Modifiers.Contains(Current.Text)) modifiers.Add(Next().Text);
        if (Take("namespace"))
        {
            var name = QualifiedName();
            if (Take(";")) return new NamespaceSyntax(name, true, [], location);
            Expect("{");
            var members = ParseDeclarations(true);
            Expect("}");
            return new NamespaceSyntax(name, false, members, location);
        }
        if (Current.Text == "class" && modifiers.Contains("static") && Peek(2).Text == "{")
        {
            Next(); var name = Identifier().Text; Expect("{");
            var members = ParseDeclarations(true); Expect("}");
            return new StaticClassSyntax(name, members, modifiers.ToImmutable(), location);
        }
        if (Constructs.TryGetValue(Current.Text, out var kind) || Current.Text == "switch")
        {
            if (Next().Text == "switch") { Expect("group"); kind = ConstructKind.SwitchGroup; }
            var name = kind is ConstructKind.Scope or ConstructKind.Use or ConstructKind.Suppress or ConstructKind.Extern
                ? $"<{kind.ToString().ToLowerInvariant()}>" : QualifiedName();
            var header = ImmutableArray.CreateBuilder<SyntaxElement>();
            GroupElement? body = null;
            while (Current.Kind != TokenKind.End && Current.Text != ";")
            {
                if (Current.Text == "{")
                {
                    var group = ReadGroup();
                    if (kind == ConstructKind.Table) { header.Add(group); continue; }
                    body = group; break;
                }
                if (Current.Text == "}") break;
                header.Add(ReadElement());
            }
            if (body is null) Expect(";"); else Take(";");
            return new ConstructSyntax(kind, name, modifiers.ToImmutable(), header.ToImmutable(), body, location);
        }
        var returnType = ParseType();
        var functionName = Identifier().Text;
        Expect("(");
        var parameters = ImmutableArray.CreateBuilder<ParameterSyntax>();
        while (Current.Kind != TokenKind.End && Current.Text is not (")" or "{" or ";" or "}"))
        {
            var before = position;
            var type = ParseType();
            var name = Identifier();
            var defaultValue = Take("=") ? ParseExpression() : null;
            parameters.Add(new(type, name.Text, name.Location, defaultValue));
            if (!Take(",")) break;
            if (before == position) break;
        }
        Expect(")");
        BlockSyntax? functionBody = null;
        if (Take("=>"))
        {
            var expression = ParseExpression();
            StatementSyntax statement = returnType.Name == "void" ? new ExpressionStatementSyntax(expression, expression.Location) : new ReturnSyntax(expression, expression.Location);
            functionBody = new([statement], expression.Location);
            Expect(";");
        }
        else if (Current.Text == "{") functionBody = ParseBlock();
        else Expect(";");
        return new FunctionSyntax(functionName, returnType, parameters.ToImmutable(), functionBody, modifiers.ToImmutable(), location);
    }

    private string QualifiedName()
    {
        var parts = new List<string> { Identifier().Text };
        while (Current.Text == "." && Peek(1).Kind == TokenKind.Identifier) { Next(); parts.Add(Next().Text); }
        return string.Join('.', parts);
    }

    private TypeSyntax ParseType()
    {
        var location = Current.Location;
        var name = QualifiedName();
        var arguments = ImmutableArray.CreateBuilder<TypeSyntax>();
        if (Take("<"))
        {
            do { arguments.Add(ParseType()); } while (Take(","));
            Expect(">");
        }
        var rank = 0;
        while (Take("[")) { Expect("]"); rank++; }
        var nullable = Take("?");
        return new(name, arguments.ToImmutable(), rank, nullable, location);
    }

    private SyntaxElement ReadElement()
    {
        if (Current.Text is "(" or "[" or "{") return ReadGroup();
        return new TokenElement(Next());
    }
    private GroupElement ReadGroup()
    {
        var open = Next();
        var close = open.Text switch { "(" => ")", "[" => "]", _ => "}" };
        var items = ImmutableArray.CreateBuilder<SyntaxElement>();
        while (Current.Kind != TokenKind.End && Current.Text != close)
        {
            if (Current.Text is ")" or "]" or "}")
            {
                diagnostics.Error("WF1103", $"Mismatched delimiter '{Current.Text}'; expected '{close}'.", Current.Location);
                break;
            }
            items.Add(ReadElement());
        }
        Expect(close);
        return new(open.Text + close, items.ToImmutable(), open.Location);
    }

    private BlockSyntax ParseBlock()
    {
        var start = Expect("{").Location;
        var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
        while (Current.Kind != TokenKind.End && Current.Text != "}")
        {
            var before = position;
            statements.Add(ParseStatement());
            if (before == position) Next();
        }
        Expect("}");
        return new(statements.ToImmutable(), start);
    }

    private StatementSyntax ParseStatement()
    {
        var location = Current.Location;
        if (Current.Text == "{") return ParseBlock();
        if (Take(";")) return new EmptySyntax(location);
        if (Take("break")) { Expect(";"); return new BreakSyntax(location); }
        if (Take("continue")) { Expect(";"); return new ContinueSyntax(location); }
        if (Take("for")) return ParseFor(location);
        if (Take("do"))
        {
            var body = ParseStatement(); Expect("while"); Expect("(");
            var condition = ParseExpression(); Expect(")"); Expect(";");
            return new DoWhileSyntax(body, condition, location);
        }
        if (Take("return"))
        {
            var expression = Current.Text == ";" ? null : ParseExpression();
            Expect(";");
            return new ReturnSyntax(expression, location);
        }
        if (Take("if"))
        {
            Expect("("); var condition = ParseExpression(); Expect(")");
            var then = ParseStatement();
            return new IfSyntax(condition, then, Take("else") ? ParseStatement() : null, location);
        }
        if (Take("while"))
        {
            Expect("("); var condition = ParseExpression(); Expect(")");
            return new WhileSyntax(condition, ParseStatement(), location);
        }
        if (Current.Text is "transaction" or "deadline" or "retry" or "saga" or "guard")
        {
            var kind = Next().Text;
            var header = ImmutableArray.CreateBuilder<SyntaxElement>();
            while (Current.Kind != TokenKind.End && Current.Text is not ("{" or "}" or ";")) header.Add(ReadElement());
            GroupElement body;
            if (Current.Text == "{") body = ReadGroup();
            else { Expect("{"); body = new("{}", [], Current.Location); }
            return new EffectScopeSyntax(kind, header.ToImmutable(), body, location);
        }
        if (Current.Text == "var" || Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Identifier)
        {
            TypeSyntax? type = Take("var") ? null : ParseType();
            var name = Identifier().Text;
            Expect("="); var initializer = ParseExpression(); Expect(";");
            return new VariableSyntax(type, name, initializer, location);
        }
        var value = ParseExpression(); Expect(";");
        return new ExpressionStatementSyntax(value, location);
    }

    private ForSyntax ParseFor(SourceLocation location)
    {
        Expect("(");
        var initializers = ImmutableArray.CreateBuilder<StatementSyntax>();
        if (Current.Text != ";")
        {
            if (Current.Text == "var" || Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Identifier)
            {
                TypeSyntax? type = Take("var") ? null : ParseType();
                do
                {
                    var name = Identifier(); Expect("=");
                    initializers.Add(new VariableSyntax(type, name.Text, ParseExpression(), name.Location));
                } while (Take(","));
                if (type is null && initializers.Count > 1)
                    diagnostics.Error("WF1104", "A var declaration must declare one variable; use an explicit type for multiple for-loop variables.", location);
            }
            else do
            {
                var expression = ParseExpression();
                initializers.Add(new ExpressionStatementSyntax(expression, expression.Location));
            } while (Take(","));
        }
        Expect(";");
        var condition = Current.Text == ";" ? null : ParseExpression(); Expect(";");
        var iterators = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        if (Current.Text != ")") do { iterators.Add(ParseExpression()); } while (Take(","));
        Expect(")");
        return new(initializers.ToImmutable(), condition, iterators.ToImmutable(), ParseStatement(), location);
    }

    private static int Precedence(string token) => token switch
    { "=" or "+=" or "-=" or "*=" or "/=" or "%=" => 1, "||" => 3, "&&" => 4, "==" or "!=" => 5, "<" or ">" or "<=" or ">=" => 6, "+" or "-" => 7, "*" or "/" or "%" => 8, _ => 0 };

    private ExpressionSyntax ParseExpression(int parent = 0)
    {
        ExpressionSyntax left;
        if (Current.Text is "++" or "--")
        {
            var op = Next(); left = new UpdateSyntax(op.Text, ParseExpression(9), false, op.Location);
        }
        else if (Current.Text is "!" or "-" or "+")
        {
            var op = Next();
            left = new UnarySyntax(op.Text, ParseExpression(9), op.Location);
        }
        else if (Take("(")) { left = ParseExpression(); Expect(")"); }
        else if (Current.Kind is TokenKind.Number or TokenKind.String or TokenKind.InterpolatedString || Current.Text is "true" or "false" or "null") left = new LiteralSyntax(Next());
        else
        {
            var token = Identifier();
            left = new NameSyntax(token.Text, token.Location);
        }
        while (true)
        {
            if (Take(".")) { var member = Identifier(); left = new MemberSyntax(left, member.Text, member.Location); continue; }
            if (Take("("))
            {
                var arguments = ImmutableArray.CreateBuilder<ArgumentSyntax>();
                if (Current.Text != ")") do
                {
                    var argumentLocation = Current.Location;
                    string? name = null;
                    if (Current.Kind == TokenKind.Identifier && Peek(1).Text == ":") { name = Next().Text; Next(); }
                    arguments.Add(new(ParseExpression(), name, argumentLocation));
                } while (Take(","));
                Expect(")");
                left = new CallSyntax(left, arguments.ToImmutable(), left.Location);
                continue;
            }
            if (Current.Text is "++" or "--")
            {
                var update = Next(); left = new UpdateSyntax(update.Text, left, true, update.Location); continue;
            }
            if (Current.Text == "?" && parent < 2)
            {
                var question = Next(); var whenTrue = ParseExpression(); Expect(":");
                left = new ConditionalSyntax(left, whenTrue, ParseExpression(), question.Location);
                continue;
            }
            var precedence = Precedence(Current.Text);
            if (precedence == 0 || precedence <= parent) break;
            var op = Next();
            left = new BinarySyntax(left, op.Text, ParseExpression(precedence == 1 ? precedence - 1 : precedence), op.Location);
        }
        return left;
    }
}
