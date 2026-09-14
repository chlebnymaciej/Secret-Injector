namespace VaultInjector.App.Native;

/// <summary>
/// Windows normally refuses SetForegroundWindow from a background process. Attaching the calling
/// thread's input queue to the target window's thread is the standard, documented workaround.
/// </summary>
internal static class ForegroundWindowHelper
{
    public static void ForceSetForegroundWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return;
        }

        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var targetThreadId = NativeMethods.GetWindowThreadProcessId(targetWindow, out _);

        var attached = false;
        if (targetThreadId != 0 && targetThreadId != currentThreadId)
        {
            attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        try
        {
            NativeMethods.SetForegroundWindow(targetWindow);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }
}
