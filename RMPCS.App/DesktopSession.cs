using System.Security.Principal;
using System.Threading;

namespace RMPCS.App;

internal sealed class DesktopSession : IDisposable
{
    private readonly Mutex mutex;
    private EventWaitHandle? signal;
    private RegisteredWaitHandle? wait;
    public bool IsOwner { get; }
    public DesktopSession()
    {
        string suffix = WindowsIdentity.GetCurrent().User!.Value + (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) ? ".Admin" : ".Standard");
        mutex = new Mutex(true, @"Local\RMPCS.Desktop." + suffix, out bool created);
        IsOwner = created;
        if (created) signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RMPCS.Open." + suffix);
        else
        {
            // Another copy may still be creating its signal. Retry briefly; never launch tasks.
            for (int i = 0; i < 10; i++)
            {
                try { using var existing = EventWaitHandle.OpenExisting(@"Local\RMPCS.Open." + suffix); existing.Set(); break; }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
            }
        }
    }
    public void Listen(Action open)
    { if (signal is not null) wait = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => open(), null, Timeout.Infinite, false); }
    public void Dispose()
    { wait?.Unregister(null); signal?.Dispose(); if (IsOwner) mutex.ReleaseMutex(); mutex.Dispose(); }
}
