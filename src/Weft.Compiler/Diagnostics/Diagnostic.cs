using Weft.Compiler.Text;

namespace Weft.Compiler.Diagnostics;

public enum DiagnosticSeverity { Info, Warning, Error }

public sealed record Diagnostic(string Code, string Message, SourceLocation Location,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error, IReadOnlyList<SourceLocation>? Related = null)
{
    public override string ToString() => $"{Location}: {Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
}

public sealed class DiagnosticBag : List<Diagnostic>
{
    public bool HasErrors => this.Any(d => d.Severity == DiagnosticSeverity.Error);
    public void Error(string code, string message, SourceLocation location) => Add(new(code, message, location));
}
