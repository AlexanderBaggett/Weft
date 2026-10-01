using Weft.Compiler;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Tests;

public sealed class FrontendTests
{
    [Theory]
    [InlineData("void Main() { var Log = 1; Log(\"hello\"); }", "WF2003")]
    [InlineData("void F() {} void Main() { var F = 1; F(); }", "WF2003")]
    [InlineData("namespace Lib { void F() {} } void Main() { var Lib = 1; Lib.F(); }", "WF2001")]
    public void A_local_call_target_cannot_fall_back_to_a_global_function(string text, string code)
    {
        var result = Compilation.Analyze("resolution", [new("resolution.weft", text)]);
        Assert.Null(result.Module);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("resolution.weft", diagnostic.Location.File);
    }

    [Fact]
    public void Locations_use_utf16_and_recognize_all_newline_forms()
    {
        var source = new SourceText("sample.weft", "a\r\n😀\rb\nc");
        Assert.Equal(new SourceLocation("sample.weft", 5, 1, 2, 3), source.Location(5, 1));
        Assert.Equal(3, source.Location(6).Line);
        Assert.Equal(4, source.Location(8).Line);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Location(10));
    }

    [Fact]
    public void Lexer_keeps_contextual_words_as_identifiers_and_decodes_strings()
    {
        var lexed = new Lexer(new("strings.weft", "from next mode \"a\\n\\u263a\" @\"a\"\"b\" // ignored\n /* ignored */ 20ms")).Lex();
        Assert.Empty(lexed.Diagnostics);
        Assert.All(lexed.Tokens.Take(3), token => Assert.Equal(TokenKind.Identifier, token.Kind));
        Assert.Equal("a\n☺", lexed.Tokens[3].StringValue);
        Assert.Equal("a\"b", lexed.Tokens[4].StringValue);
        Assert.Equal("20ms", lexed.Tokens[5].Text);
    }

    [Theory]
    [InlineData("origin X { envelope { string Name; ] }", "WF1103")]
    [InlineData("void Main() { Print(\"unfinished", "WF1004")]
    [InlineData("/* unfinished", "WF1001")]
    [InlineData("void Main() { Print(\"\\q\"); }", "WF1003")]
    public void Malformed_input_recovers_with_source_diagnostics(string text, string code)
    {
        var tree = new Parser(new("broken.weft", text)).Parse();
        Assert.Contains(tree.Diagnostics, d => d.Code == code && d.Location.File == "broken.weft");
        Assert.All(tree.Diagnostics, d => Assert.InRange(d.Location.Start, 0, text.Length));
    }

    [Fact]
    public void Nested_strings_in_interpolation_do_not_end_the_outer_string()
    {
        var text = "receiver X { void F() { Print($\"{Format(\"}\")}\"); } }";
        var tree = new Parser(new("interpolation.weft", text)).Parse();
        Assert.Empty(tree.Diagnostics);
        Assert.Single(tree.Declarations);
        var lexed = new Lexer(new("interpolation.weft", text)).Lex();
        Assert.Single(lexed.Tokens.Where(token => token.Kind == TokenKind.InterpolatedString));
    }

    [Theory]
    [InlineData("orders.weft")]
    [InlineData("input.rules")]
    [InlineData("switches.weft")]
    [InlineData("project-pipelines/Shared/middleware.weft")]
    [InlineData("project-pipelines/Orders/pipeline.weft")]
    [InlineData("project-pipelines/SharedOnly/pipeline.weft")]
    public void Design_examples_have_lossless_declaration_representation(string file)
    {
        var path = Path.Combine(TestWorkspace.Repository, "examples", file);
        var tree = new Parser(new(path, File.ReadAllText(path))).Parse();
        Assert.Empty(tree.Diagnostics);
        Assert.NotEmpty(tree.Declarations);
        Assert.All(tree.Declarations, declaration => Assert.IsType<ConstructSyntax>(declaration));
        // Representation is not evidence that policy or graph checking is implemented.
        var result = Compilation.Analyze("design", [tree.Source]);
        Assert.Null(result.Module);
        Assert.All(result.Diagnostics, d => Assert.Equal("WF2009", d.Code));
    }

    [Fact]
    public void Every_declaration_kind_has_a_structural_node()
    {
        foreach (var kind in Enum.GetValues<ConstructKind>())
        {
            var keyword = kind == ConstructKind.SwitchGroup ? "switch group" : kind.ToString().ToLowerInvariant();
            var tree = new Parser(new("inventory.weft", keyword + " Example;")).Parse();
            Assert.Empty(tree.Diagnostics);
            var declaration = Assert.Single(tree.Declarations);
            if (kind == ConstructKind.Record) Assert.Equal(kind, Assert.IsType<ClassSyntax>(declaration).Kind);
            else Assert.Equal(kind, Assert.IsType<ConstructSyntax>(declaration).Kind);
        }
    }

    [Fact]
    public void Nested_scopes_resolve_outer_variables_without_leaking_inner_names()
    {
        var result = Compilation.Analyze("scopes", [new("scopes.weft", "void Main() { var x = 1; { var y = x; Print(y); } Print(y); }")]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == "WF2001" && d.Message.Contains("'y'", StringComparison.Ordinal));
    }
}
