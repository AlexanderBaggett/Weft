using Weft.Cli;

namespace Weft.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void Manifest_discovers_source_groups_and_rule_files_once_in_stable_order()
    {
        using var workspace = new TestWorkspace();
        var manifest = workspace.Write("weft.toml", "[project]\nname = 'demo'\nsources = ['*.weft', '**/*.rules']\n[source-groups]\nCommon = ['common/**/*.weft']\n[build]\nbackend = 'jvm'\n");
        workspace.Write("main.weft", "void Main() {}");
        workspace.Write("policy.rules", "scope project;");
        workspace.Write("common/lib.weft", "int F() => 1;");
        workspace.Write("bin/ignored.rules", "invalid");
        var result = ProjectLoader.Load(manifest);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Project);
        Assert.Equal("jvm", result.Project.Backend);
        Assert.Equal(new[] { "common/lib.weft", "main.weft", "policy.rules" }, result.Project.Sources.Select(p => Path.GetRelativePath(workspace.Root, p)));
    }

    [Theory]
    [InlineData("[project]\nname =", "WF5002")]
    [InlineData("[project]\nname = '../bad'", "WF5003")]
    [InlineData("[project]\nname = 'demo'\nsources=['missing.weft']", "WF5004")]
    [InlineData("[project]\nname = 'demo'\n[bind]\nStore='Sql'", "WF5003")]
    public void Invalid_manifest_has_actionable_diagnostic(string text, string code)
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("weft.toml", text);
        workspace.Write("main.weft", "void Main() {}");
        var result = ProjectLoader.Load(path);
        Assert.Null(result.Project);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Location.File == path);
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("jvm")]
    public async Task Cli_checks_emits_builds_and_runs_a_manifest_project(string backend)
    {
        using var workspace = new TestWorkspace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var path = workspace.Write("weft.toml", "[project]\nname = 'cli'\n");
        workspace.Write("main.weft", "void Main() { Print(\"cli works\"); }");
        foreach (var command in new[] { "check", "emit", "build", "run" })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(0, await Weft.Cli.Program.ExecuteAsync([command, "--project", path, "--backend", backend], output, error, timeout.Token));
            Assert.Equal("", error.ToString());
            if (command == "run") Assert.Equal("cli works\n", output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        }
    }
}
