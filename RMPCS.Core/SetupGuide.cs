namespace RMPCS.Core;

public sealed record SetupGuideStep(string Key, string Title, string Body, string Page);
// v0.14.0 foundation: a bounded, side-effect-free guide catalogue/session. No automatic
// first-run detection, completion persistence, background setup or maintenance in v0.13.5.
public sealed class SetupGuide
{
    public static IReadOnlyList<SetupGuideStep> Steps { get; } = Array.AsReadOnly(new[] {
        new SetupGuideStep("Welcome", "Welcome to Rusty's PC Toolkit", "Start with read-only checks. Review each maintenance action before running it. This preview changes no preferences and starts no maintenance.", "Health"),
        new("Appearance", "Choose a comfortable look", "Settings offers themes, bundled fonts and Windows High Contrast support. The full v0.14.0 guide will help review these choices before saving them.", "Settings"),
        new("Window", "Decide how RMPCS stays available", "Review close/minimize-to-tray and sidebar behavior. Sign-in startup requires the protected installed copy and explicit approval; simply running in the tray performs no maintenance.", "Settings"),
        new("Monitoring", "Optional system monitoring", "The overlay is off by default. Choose metrics, update interval, display and opacity. Click-through and locking can be recovered through Settings, the tray or the optional Ctrl+Alt+O shortcut. No monitoring data is uploaded or saved.", "Settings"),
        new("Safety", "Understand protected actions", "Administrator approval is separate from interface choices. Drive optimization, cleanup and repairs require explicit review. Back up important files; a restore point does not recover deleted personal files.", "Help"),
        new("Finish", "Review before you begin", "The future setup guide will review selected preferences before applying them. This v0.13.5 preview is informational only. Close it at any time, then use the existing Settings and Help sections.", "Health")
    });
    public int Index { get; private set; }
    public SetupGuideStep Current => Steps[Index];
    public bool CanBack => Index > 0;
    public bool CanNext => Index + 1 < Steps.Count;
    public void Next() { if (CanNext) Index++; }
    public void Back() { if (CanBack) Index--; }
}
