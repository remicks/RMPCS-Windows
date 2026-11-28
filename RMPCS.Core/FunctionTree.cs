namespace RMPCS.Core;
public sealed record FunctionNode(string Label, string? Page, IReadOnlyList<FunctionNode> Children);
public static class FunctionTree
{
    private static FunctionNode Leaf(string key) => new(PageCatalog.All.Single(p => p.Key == key).Label, key, []);
    public static IReadOnlyList<FunctionNode> Roots { get; } = new[] {
        new FunctionNode("System", null, [Leaf("Health")]), new("Care & optimization", null, [Leaf("Maintenance"), Leaf("Drives")]),
        new("Startup & automation", null, [Leaf("Startup"), Leaf("Automation")]),
        new("Diagnostics & recovery", null, [Leaf("Diagnostics"), Leaf("Protection"), Leaf("History")]),
        new("Personalization & support", null, [Leaf("Settings"), Leaf("Help")]) };
}
