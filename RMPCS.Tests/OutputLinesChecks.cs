using RMPCS.Core;
internal static class OutputLinesChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var split = new OutputLines();
        var first = split.Push("Image Version: 10.0.26".AsSpan());
        var second = split.Push("300.1\r\n[====    20.0%    ]\r".AsSpan());
        check(first.Count == 0 && second.SequenceEqual(["Image Version: 10.0.26300.1", "[====    20.0%    ]"]), "tool output split across read buffers is logged as whole lines");
        check(split.Push("\0T\0h\0e\0 \0o\0p\0".AsSpan()).Count == 0 && split.Flush().SequenceEqual(["The op"]), "UTF-16 null padding is removed and a trailing partial line is flushed at exit");
        check(split.Push("\r\n\r\n   \n".AsSpan()).Count == 0 && split.Flush().Count == 0, "blank and whitespace-only lines are not logged");
        var bounded = new OutputLines(8);
        check(bounded.Push(new string('x', 20).AsSpan()).All(l => l.Length <= 8) && bounded.Flush().Single().Length == 4, "output without newlines is chunked at the line cap instead of growing without bound");
    }
}
