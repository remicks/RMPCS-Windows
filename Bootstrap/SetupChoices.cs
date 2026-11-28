using System;
namespace RMPCS.Bootstrap
{
    internal static class SetupChoices
    {
        internal static bool OfferSensors(bool selected,int installExit,bool prerequisiteRestart)
        {return selected && installExit==0 && !prerequisiteRestart;}
        internal static string MsiArguments(string msi, bool desktop, bool menu, bool admin, bool repair = false)
        {
            if (String.IsNullOrEmpty(msi) || msi.IndexOfAny(new[] {'"','\r','\n'}) >= 0) throw new ArgumentException("Invalid MSI path.");
            return "/i \""+msi+"\" /qb /norestart CREATE_DESKTOP="+(desktop?"1":"0")+" CREATE_STARTMENU="+(menu?"1":"0")+" ALWAYS_ADMIN="+(admin?"1":"0")+(repair?" REINSTALL=ALL REINSTALLMODE=omus":"");
        }
    }
}
