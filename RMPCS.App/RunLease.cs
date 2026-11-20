namespace RMPCS.App;

internal sealed class RunLease : IDisposable
{
    private readonly Mutex mutex;
    private readonly bool owned;
    private RunLease(Mutex mutex, bool owned) { this.mutex = mutex; this.owned = owned; }
    public static RunLease? Acquire()
    {
        var mutex = new Mutex(false, @"Global\DegeneratesInc.RMPCS.Maintenance");
        bool obtained;
        try { obtained = mutex.WaitOne(0); } catch (AbandonedMutexException) { obtained = true; }
        if (!obtained) { mutex.Dispose(); return null; }
        return new RunLease(mutex, true);
    }
    // All callers acquire and dispose on the WPF dispatcher thread.
    public void Dispose() { if (owned) mutex.ReleaseMutex(); mutex.Dispose(); }
}
