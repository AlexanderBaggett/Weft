using System.Collections.Immutable;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler;

public static class Compilation
{
    public static BindResult Analyze(string name, IEnumerable<SourceText> sources)
    {
        var result = new Binder().Bind(name, sources.Select(source => new Parser(source).Parse()).ToImmutableArray());
        if (result.Module is null) return result;
        var diagnostics = result.Diagnostics.AddRange(IrValidator.Validate(result.Module));
        return result with { Module = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? null : result.Module, Diagnostics = diagnostics };
    }
}
