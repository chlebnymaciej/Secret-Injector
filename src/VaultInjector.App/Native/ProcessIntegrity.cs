using System.Security.Principal;

namespace VaultInjector.App.Native;

/// <summary>
/// Compares Windows Mandatory Integrity Control levels between this process and a target window's
/// owning process. Windows' User Interface Privilege Isolation (UIPI) silently drops synthetic input
/// (SendInput) sent from a lower-integrity process into a higher-integrity window - e.g. an elevated
/// process, or a "Windows Security" credential prompt hosted at a higher level than a normal user app.
/// There is no legitimate way around that boundary from here; the best we can do is detect it up front
/// instead of silently failing, and tell the user to paste manually (the value is already on the
/// clipboard by the time this is checked - only the synthetic keystroke is what gets blocked).
/// </summary>
internal static class ProcessIntegrity
{
    /// <summary>
    /// True if the window at <paramref name="hWnd"/> belongs to a process running at a higher integrity
    /// level than this one. False both when it's the same/lower level and when the level couldn't be
    /// determined (so an unrelated failure here never blocks a paste attempt that might otherwise work).
    /// </summary>
    public static bool IsHigherIntegrityThanSelf(IntPtr hWnd)
    {
        var targetLevel = TryGetIntegrityLevel(hWnd);
        if (targetLevel is null)
        {
            return false;
        }

        var ownLevel = TryGetOwnIntegrityLevel();
        return ownLevel is not null && targetLevel > ownLevel;
    }

    private static int? TryGetIntegrityLevel(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            // Commonly denied for elevated/protected processes - itself a hint it's higher integrity,
            // but we can't confirm it, so treat as unknown rather than guessing.
            return null;
        }

        try
        {
            return TryGetIntegrityLevelFromProcessHandle(hProcess);
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    private static int? TryGetIntegrityLevelFromProcessHandle(IntPtr hProcess)
    {
        if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out var hToken))
        {
            return null;
        }

        try
        {
            using var identity = new WindowsIdentity(hToken);
            return GetIntegrityRid(identity);
        }
        finally
        {
            NativeMethods.CloseHandle(hToken);
        }
    }

    private static int? TryGetOwnIntegrityLevel()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return GetIntegrityRid(identity);
    }

    private static int? GetIntegrityRid(WindowsIdentity identity)
    {
        var integritySid = identity.Groups?
            .Select(g => g.Value)
            .FirstOrDefault(sid => sid.StartsWith("S-1-16-", StringComparison.Ordinal));

        if (integritySid is null)
        {
            return null;
        }

        var lastDash = integritySid.LastIndexOf('-');
        return int.TryParse(integritySid[(lastDash + 1)..], out var rid) ? rid : null;
    }
}
