using System.Windows.Input;
using System.Windows.Interop;
using VaultInjector.Core.Models;
using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace VaultInjector.App.Views;

/// <summary>A text box that, instead of accepting typed text, records the next key combination pressed as a hotkey.</summary>
public partial class HotkeyRecorderControl : UserControl
{
    public HotkeyDefinition Hotkey { get; private set; } = new();

    public HotkeyRecorderControl()
    {
        InitializeComponent();
    }

    public void SetHotkey(HotkeyDefinition hotkey)
    {
        Hotkey = hotkey.Clone();
        UpdateDisplay();
    }

    private void DisplayBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Let normal keyboard navigation keep working instead of swallowing it as a hotkey attempt.
        if (key is Key.Tab or Key.Escape)
        {
            return;
        }

        e.Handled = true;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            // A bare modifier key isn't a usable combination on its own; wait for the following key.
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None)
        {
            DisplayBox.Text = "Use at least one modifier (Ctrl/Alt/Shift/Win) plus a key";
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        Hotkey = new HotkeyDefinition { Modifiers = modifiers, VirtualKeyCode = virtualKey };
        UpdateDisplay();
    }

    private void UpdateDisplay() => DisplayBox.Text = FormatHotkey(Hotkey);

    private static string FormatHotkey(HotkeyDefinition hotkey)
    {
        if (!hotkey.IsAssigned)
        {
            return "(none)";
        }

        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");

        var wpfKey = KeyInterop.KeyFromVirtualKey(hotkey.VirtualKeyCode);
        parts.Add(wpfKey.ToString());

        return string.Join("+", parts);
    }
}
