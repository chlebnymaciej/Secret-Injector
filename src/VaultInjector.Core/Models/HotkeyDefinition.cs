namespace VaultInjector.Core.Models;

/// <summary>
/// Mirrors the Win32 RegisterHotKey MOD_* flag values exactly (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8)
/// so the numeric value can be passed straight through to the native API without translation.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>The global hotkey that opens the secret-picker context menu.</summary>
public sealed class HotkeyDefinition
{
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    /// <summary>Win32 virtual-key code, e.g. 0x56 for 'V'.</summary>
    public int VirtualKeyCode { get; set; } = 0x56;

    public bool IsAssigned => VirtualKeyCode != 0;

    public HotkeyDefinition Clone() => new() { Modifiers = Modifiers, VirtualKeyCode = VirtualKeyCode };
}
