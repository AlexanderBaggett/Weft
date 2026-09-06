using System.Collections.Immutable;

namespace Weft.Compiler.Text;

public sealed class SourceText
{
    private readonly ImmutableArray<int> lineStarts;
    public string Path { get; }
    public string Text { get; }

    public SourceText(string path, string text)
    {
        Path = path;
        Text = text;
        var starts = ImmutableArray.CreateBuilder<int>();
        starts.Add(0);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                starts.Add(i + 1);
            }
            else if (text[i] == '\n') starts.Add(i + 1);
        }
        lineStarts = starts.ToImmutable();
    }

    public SourceLocation Location(int start, int length = 0)
    {
        if (start < 0 || start > Text.Length || length < 0 || start + length > Text.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        var index = lineStarts.BinarySearch(start);
        if (index < 0) index = ~index - 1;
        return new(Path, start, length, index + 1, start - lineStarts[index] + 1);
    }
}

public readonly record struct SourceLocation(string File, int Start, int Length, int Line, int Column)
{
    public override string ToString() => $"{File}({Line},{Column})";
}

public sealed record SourceOrigin(SourceLocation Location, string? GeneratedBy = null, SourceOrigin? Parent = null);
