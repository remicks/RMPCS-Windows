namespace RMPCS.App;

internal sealed class RunLease : IDisposable
{
    private readonly Mutex mutex;
    private readonly bool owned;
    private RunLease(Mutex mutex, bool owned) { this.mutex = mutex; this.owned = owned; }
    public static RunLease? Acquire()
    {
        Mutex mutex;
        // Another Windows account (or a standard session, when this one is elevated) may already own the
        // global name with an ACL this token cannot open. That means another session holds it: report
        // "busy" through the existing null path instead of failing the run with a generic error.
        try { mutex = new Mutex(false, @"Global\DegeneratesInc.RMPCS.Maintenance"); }
        catch (UnauthorizedAccessException) { return null; }
        bool obtained;
        try { obtained = mutex.WaitOne(0); } catch (AbandonedMutexException) { obtained = true; }
        if (!obtained) { mutex.Dispose(); return null; }
        return new RunLease(mutex, true);
    }
    // All callers acquire and dispose on the WPF dispatcher thread.
    public void Dispose() { if (owned) mutex.ReleaseMutex(); mutex.Dispose(); }
}
