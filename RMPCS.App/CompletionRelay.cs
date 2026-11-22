using System.Runtime.InteropServices;
using System.Security.Principal;
using RMPCS.Core;
namespace RMPCS.App;

// One-way, bounded, informational notices. No paths, strings, credentials or commands cross the boundary.
internal static class CompletionRelay
{
    internal const int Message = 0x8002, Marker = 0x524D5043;
    private static string Caption(bool elevated) => "RMPCS.Notices." + WindowsIdentity.GetCurrent().User!.Value + (elevated ? ".Admin" : ".Standard");
    public static string CurrentCaption => Caption(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator));
    public static bool Send(CompletionNotice notice)
    {
        try
        {
            if (!Enum.IsDefined(notice)) return false;
            var target = FindWindow(null, Caption(true));
            if (target == IntPtr.Zero) target = FindWindow(null, Caption(false));
            return target != IntPtr.Zero && PostMessage(target, Message, (IntPtr)(int)notice, (IntPtr)Marker);
        }
        catch { return false; } // Reporting must never affect maintenance results.
    }
    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string caption);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
