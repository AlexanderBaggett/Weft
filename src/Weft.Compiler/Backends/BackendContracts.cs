using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;
using Weft.Compiler.Text;

namespace Weft.Compiler.Backends;

public sealed record GeneratedLine(int Line, SourceOrigin Origin);
public sealed record GeneratedSource(string FileName, string Text, ImmutableArray<GeneratedLine> Lines)
{
    public SourceLocation Location(int line) => Lines.LastOrDefault(item => item.Line <= line)?.Origin.Location
        ?? new SourceLocation(FileName, 0, 0, 1, 1);
}

public sealed class SourceWriter
{
    private readonly StringBuilder text = new();
    private readonly List<GeneratedLine> lines = [];
    private int line = 1;
    public void WriteLine(string value, SourceOrigin? origin = null)
    {
        if (origin is not null) lines.Add(new(line, origin));
        text.Append(value).Append('\n');
        line += value.Count(c => c == '\n') + 1;
    }
    public GeneratedSource Finish(string name) => new(name, text.ToString(), lines.ToImmutableArray());
}

public sealed record RuntimeDescriptor(string Abi, ImmutableArray<IntrinsicSignature> Operations)
{
    public static RuntimeDescriptor Bootstrap { get; } = new(Intrinsics.AbiVersion, Intrinsics.Bootstrap);
    public ImmutableArray<Diagnostic> Validate(IrModule module)
    {
        var errors = new DiagnosticBag();
        var location = module.Functions.FirstOrDefault()?.Origin.Location ?? new SourceLocation(module.Name, 0, 0, 1, 1);
        if (Abi != module.RuntimeAbi) errors.Error("WF4001", $"Runtime ABI '{Abi}' is incompatible with required ABI '{module.RuntimeAbi}'.", location);
        foreach (var required in module.RequiredIntrinsics)
            if (!Operations.Any(actual => actual.Name == required.Name && actual.Result == required.Result && actual.Parameters.SequenceEqual(required.Parameters)))
                errors.Error("WF4002", $"Runtime operation '{required.Name}' is missing or has an incompatible signature.", location);
        return errors.ToImmutableArray();
    }
}

public sealed record BackendResult(string? Artifact, GeneratedSource Source, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Artifact is not null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

public static class EntryPoint
{
    public static FunctionSymbol? Resolve(IrModule module, string name, DiagnosticBag diagnostics)
    {
        var named = module.Functions.Where(f => f.Symbol.Name == name).ToArray();
        var candidates = named.Where(f => f.Symbol.Parameters.Length == 0 && (f.Symbol.ReturnType == WeftType.Void || f.Symbol.ReturnType == WeftType.Int32)).ToArray();
        var function = candidates.Length == 1 ? candidates[0] : named.FirstOrDefault();
        var location = function?.Origin.Location ?? module.Functions.FirstOrDefault()?.Origin.Location ?? new SourceLocation(module.Name, 0, 0, 1, 1);
        if (candidates.Length != 1 || function is null)
        {
            diagnostics.Error("WF4003", $"Entry '{name}' must be a parameterless function returning void or int32.", location);
            return null;
        }
        return function.Symbol;
    }
}

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
public static class ToolProcess
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments, string? directory = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (directory is not null) info.WorkingDirectory = directory;
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException($"Unable to start '{executable}'.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var error = process.StandardError.ReadToEndAsync(cancellation);
        try { await process.WaitForExitAsync(cancellation); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        return new(process.ExitCode, await output, await error);
    }
}
