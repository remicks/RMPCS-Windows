namespace RMPCS.Core;
public enum StartupSwitchResult { Unchanged, Denied, Cancelled, Changed }
public static class StartupSwitchFlow
{
    public static StartupSwitchResult Change(StartupItem snapshot,bool requested,bool allowed,Func<bool> confirm,Action<StartupItem> disable,Action<StartupItem> restore)
    {
        if(requested==snapshot.Enabled)return StartupSwitchResult.Unchanged;
        if(!allowed)return StartupSwitchResult.Denied;
        if(!confirm())return StartupSwitchResult.Cancelled;
        if(requested)restore(snapshot);else disable(snapshot);
        return StartupSwitchResult.Changed;
    }
}
