namespace RMPCS.Core;
public sealed record PageOption(string Key,string Label)
{
    public override string ToString() => Label;
}
public static class PageCatalog
{
    public static IReadOnlyList<PageOption> All { get; }=Array.AsReadOnly(new[]{new PageOption("Health","Overview"),new("Maintenance","Maintenance"),new("Automation","Schedules"),new("Startup","Startup apps"),new("History","History"),new("Settings","Settings"),new("Help","Help"),new("Diagnostics","Diagnostics"),new("Protection","Recovery")});
}
