namespace RMPCS.Core;

// Section identifiers only: no delegates, commands, control state or maintenance actions.
public sealed class SectionHistory
{
    private readonly List<string> previous = [];
    public string Current { get; private set; }
    public bool CanGoBack => previous.Count > 0;
    public SectionHistory(string initial) => Current = Valid(initial) ? initial : "Health";
    private static bool Valid(string key) => key == "Results" || AppPreferences.Pages.Contains(key, StringComparer.Ordinal);
    public void Visit(string key)
    {
        if (!Valid(key) || key == Current) return;
        if (previous.Count == 64) previous.RemoveAt(0);
        previous.Add(Current); Current = key;
    }
    public string? Back()
    {
        if (!CanGoBack) return null;
        Current = previous[^1]; previous.RemoveAt(previous.Count - 1); return Current;
    }
}
