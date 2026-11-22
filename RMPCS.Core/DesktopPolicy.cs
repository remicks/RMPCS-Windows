namespace RMPCS.Core;

public static class DesktopPolicy
{
    public static bool StartHidden(IEnumerable<string> args) => args.Contains("--startup");
    public static bool HideOnClose(bool trayAvailable, bool exitRequested, bool testMode) => trayAvailable && !exitRequested && !testMode;
    public static bool CanExit(bool busy) => !busy;
}
