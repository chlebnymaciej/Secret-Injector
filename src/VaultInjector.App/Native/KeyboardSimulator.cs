namespace VaultInjector.App.Native;

/// <summary>Simulates a Ctrl+V keystroke via SendInput so the paste lands wherever the target window's focus is.</summary>
internal static class KeyboardSimulator
{
    public static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyDown(NativeMethods.VK_CONTROL),
            KeyDown(NativeMethods.VK_V),
            KeyUp(NativeMethods.VK_V),
            KeyUp(NativeMethods.VK_CONTROL)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk } }
    };

    private static NativeMethods.INPUT KeyUp(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
    };
}
