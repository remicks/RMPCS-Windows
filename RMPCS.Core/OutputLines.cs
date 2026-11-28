using System.Text;
namespace RMPCS.Core;

// Reassembles Windows tool output into whole lines. Redirected output arrives in arbitrary
// buffer-sized chunks, so splitting each chunk on its own broke DISM/SFC lines in two.
// Lines are capped so a tool that never emits a newline cannot grow memory without bound.
public sealed class OutputLines(int maximumLine = 4096)
{
    private readonly StringBuilder pending = new();
    public IReadOnlyList<string> Push(ReadOnlySpan<char> chunk)
    {
        var lines = new List<string>();
        foreach (char c in chunk)
        {
            if (c == '\0') continue; // UTF-16 tool output read as single-byte text
            if (c is '\r' or '\n') { Emit(lines); continue; }
            pending.Append(c);
            if (pending.Length >= maximumLine) Emit(lines);
        }
        return lines;
    }
    public IReadOnlyList<string> Flush() { var lines = new List<string>(); Emit(lines); return lines; }
    private void Emit(List<string> lines)
    {
        if (pending.Length == 0) return;
        string line = pending.ToString(); pending.Clear();
        if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
    }
}
