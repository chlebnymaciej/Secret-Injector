using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Native;
using VaultInjector.Core.Models;

namespace VaultInjector.App.Services;

/// <summary>
/// Owns a hidden message-only window used solely to receive WM_HOTKEY, and registers/unregisters the
/// single configured global hotkey against it.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0xA100;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly ILogger<GlobalHotkeyService> _logger;
    private HwndSource? _source;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var parameters = new HwndSourceParameters("VaultInjectorHotkeyWindow")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool Register(HotkeyDefinition hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        Unregister();

        if (!hotkey.IsAssigned || _source is null)
        {
            return false;
        }

        _isRegistered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, (uint)hotkey.Modifiers, (uint)hotkey.VirtualKeyCode);
        if (_isRegistered)
        {
            _logger.LogInformation("Registered global hotkey (modifiers={Modifiers}, vk=0x{VirtualKey:X2})", hotkey.Modifiers, hotkey.VirtualKeyCode);
        }
        else
        {
            _logger.LogWarning("Failed to register global hotkey (modifiers={Modifiers}, vk=0x{VirtualKey:X2}) - it may already be in use by another application", hotkey.Modifiers, hotkey.VirtualKeyCode);
        }

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _isRegistered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }
}
