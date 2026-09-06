using System.Text.Json;
using Weft.Backend.DotNet;
using Weft.Backend.Jvm;
using Weft.Compiler;
using Weft.Compiler.Backends;
using Weft.Compiler.Text;

namespace Weft.Tests;

public sealed record ExpectedDiagnostic(string Code, string File, int Line, int Column);
public sealed record ConformanceCase
{
    public required string Id { get; init; }
    public required Dictionary<string, string> Sources { get; init; }
    public string Entry { get; init; } = "Main";
    public string? Stdout { get; init; }
    public string Stderr { get; init; } = "";
    public int ExitCode { get; init; }
    public ExpectedDiagnostic[] Diagnostics { get; init; } = [];
}

public sealed class ConformanceTests
{
    public static IEnumerable<object[]> Cases => Directory.GetFiles(Path.Combine(TestWorkspace.Repository, "tests", "conformance"), "*.json").Order(StringComparer.Ordinal)
        .SelectMany(path => new[] { new object[] { path, "dotnet" }, new object[] { path, "jvm" } });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Both_targets_satisfy_independent_expectations(string path, string backend)
    {
        var specification = JsonSerializer.Deserialize<ConformanceCase>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotEmpty(specification.Id);
        Assert.NotEmpty(specification.Sources);
        using var workspace = new TestWorkspace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var sources = specification.Sources.Select(pair => new SourceText(workspace.Write(pair.Key, pair.Value), pair.Value)).ToArray();
        var compilation = Compilation.Analyze(specification.Id, sources);
        if (specification.Diagnostics.Length > 0)
        {
            Assert.Null(specification.Stdout);
            Assert.Null(compilation.Module);
            var actual = compilation.Diagnostics.Select(d => new ExpectedDiagnostic(d.Code, Path.GetRelativePath(workspace.Root, d.Location.File), d.Location.Line, d.Location.Column));
            Assert.Equal(specification.Diagnostics, actual);
            return;
        }
        Assert.NotNull(specification.Stdout); // A missing expectation must never turn into a passing test.
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.Module);
        var destination = Path.Combine(workspace.Root, backend);
        var built = backend == "dotnet"
            ? await new DotNetEmitter().BuildAsync(compilation.Module, specification.Entry, destination, cancellation: timeout.Token)
            : await new JvmEmitter().BuildAsync(compilation.Module, specification.Entry, destination, cancellation: timeout.Token);
        Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
        var result = await ToolProcess.RunAsync(backend == "dotnet" ? "dotnet" : "java", backend == "dotnet" ? [built.Artifact!] : ["-jar", built.Artifact!], workspace.Root, timeout.Token);
        Assert.Equal(specification.ExitCode, result.ExitCode);
        Assert.Equal(specification.Stdout, result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(specification.Stderr, result.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
