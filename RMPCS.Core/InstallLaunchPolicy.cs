namespace RMPCS.Core;

public static class InstallLaunchPolicy
{
    public static bool RequestElevation(bool protectedInstallation, bool alreadyAdministrator, string? savedChoice) =>
        protectedInstallation && !alreadyAdministrator && savedChoice == "1";
}
