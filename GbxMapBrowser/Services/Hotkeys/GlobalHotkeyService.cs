using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GbxMapBrowser.Services.Hotkeys
{
    public sealed class GlobalHotkeyService : IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const int ForwardHotkeyId = 7101;
        private const int BackwardHotkeyId = 7102;

        private readonly Window _owner;
        private HwndSource _source;
        private IntPtr _windowHandle;

        public event EventHandler ForwardPressed;
        public event EventHandler BackwardPressed;

        public GlobalHotkeyService(Window owner)
        {
            _owner = owner;
        }

        public void Initialize()
        {
            _windowHandle = new WindowInteropHelper(_owner).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(WndProc);
        }

        public void Apply(MapNavigationHotkeySettings settings)
        {
            Unregister();

            if (settings == null || !settings.IsEnabled || !settings.HasBothHotkeys)
            {
                return;
            }

            Register(ForwardHotkeyId, settings.Forward);
            Register(BackwardHotkeyId, settings.Backward);
        }

        public void Unregister()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            UnregisterHotKey(_windowHandle, ForwardHotkeyId);
            UnregisterHotKey(_windowHandle, BackwardHotkeyId);
        }

        public void Dispose()
        {
            Unregister();
            _source?.RemoveHook(WndProc);
        }

        private void Register(int id, HotkeyGesture hotkey)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(hotkey.Key);
            uint modifiers = ToNativeModifiers(hotkey.Modifiers);

            if (!RegisterHotKey(_windowHandle, id, modifiers, (uint)virtualKey))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register hotkey {hotkey}.");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WmHotkey)
            {
                return IntPtr.Zero;
            }

            int id = wParam.ToInt32();

            if (id == ForwardHotkeyId)
            {
                ForwardPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (id == BackwardHotkeyId)
            {
                BackwardPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static uint ToNativeModifiers(ModifierKeys modifiers)
        {
            uint native = 0;

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                native |= 0x0001;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                native |= 0x0002;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                native |= 0x0004;
            }

            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                native |= 0x0008;
            }

            return native;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
