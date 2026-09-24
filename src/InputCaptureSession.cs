using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AtajosLibres
{
    // Installed only while the user is choosing one input. Its hooks run on the editor's UI thread.
    internal sealed class InputCaptureSession : IDisposable
    {
        private readonly bool allowMouse;
        private readonly Func<Point, bool> cancelClick;
        private readonly Native.HookProc keyboardCallback, mouseCallback;
        private readonly HashSet<int> swallowedKeys = new HashSet<int>();
        private IntPtr keyboardHook, mouseHook;
        private int swallowedMouse;
        private bool captured, disposed;
        internal event Action<int, Modifiers> Captured;
        internal event Action Finished;

        internal InputCaptureSession(bool allowMouse, Func<Point, bool> cancelClick)
        {
            this.allowMouse = allowMouse;
            this.cancelClick = cancelClick;
            keyboardCallback = OnKeyboard;
            mouseCallback = OnMouse;
        }

        internal void Start()
        {
            keyboardHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyboardCallback, Native.GetModuleHandle(null), 0);
            if (keyboardHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!allowMouse) return;
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseCallback, Native.GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Dispose();
                throw new Win32Exception(error);
            }
        }

        private IntPtr OnKeyboard(int code, IntPtr wParam, IntPtr lParam)
        {
            try { return HandleKeyboard(code, wParam, lParam); }
            catch { return Native.CallNextHookEx(keyboardHook, code, wParam, lParam); }
        }

        private IntPtr HandleKeyboard(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || disposed) return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
            int message = wParam.ToInt32();
            if (message != Native.WM_KEYDOWN && message != Native.WM_SYSKEYDOWN &&
                message != Native.WM_KEYUP && message != Native.WM_SYSKEYUP)
                return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
            Native.KBDLLHOOKSTRUCT input = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
            if ((input.flags & Native.LLKHF_INJECTED) != 0 && input.dwExtraInfo.ToUInt64() == Native.OwnInputMarker)
                return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
            int key = (int)input.vkCode;
            bool down = message == Native.WM_KEYDOWN || message == Native.WM_SYSKEYDOWN;
            if (down)
            {
                if (ShortcutMatcher.IsModifier(key)) { swallowedKeys.Add(key); return new IntPtr(1); }
                if (!captured)
                {
                    captured = true;
                    swallowedKeys.Add(key);
                    Action<int, Modifiers> handler = Captured;
                    if (handler != null) handler(key, CurrentModifiers());
                    return new IntPtr(1);
                }
                if (swallowedKeys.Contains(key)) return new IntPtr(1);
            }
            else if (ReleaseSwallowedKey(key))
            {
                FinishIfReleased();
                return new IntPtr(1);
            }
            return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            try { return HandleMouse(code, wParam, lParam); }
            catch { return Native.CallNextHookEx(mouseHook, code, wParam, lParam); }
        }

        private IntPtr HandleMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || disposed) return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
            Native.MSLLHOOKSTRUCT input = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
            if ((input.flags & Native.LLMHF_INJECTED) != 0 && input.dwExtraInfo.ToUInt64() == Native.OwnInputMarker)
                return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
            int selected; bool down, pulse;
            if (!InputCode.TryDecodeMouse(wParam.ToInt32(), input.mouseData, out selected, out down, out pulse))
                return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
            if (!captured && selected == InputCode.MouseLeft && down && cancelClick != null &&
                cancelClick(new Point(input.pt.x, input.pt.y)))
                return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
            if (down && !captured)
            {
                captured = true;
                if (!pulse) swallowedMouse = selected;
                Action<int, Modifiers> handler = Captured;
                if (handler != null) handler(selected, CurrentModifiers());
                if (pulse) FinishIfReleased();
                return new IntPtr(1);
            }
            if (!down && swallowedMouse == selected)
            {
                swallowedMouse = 0;
                FinishIfReleased();
                return new IntPtr(1);
            }
            return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        private void FinishIfReleased()
        {
            if (!captured || swallowedMouse != 0 || swallowedKeys.Count != 0) return;
            Action handler = Finished;
            if (handler != null) handler();
        }

        private Modifiers CurrentModifiers()
        {
            Modifiers result = Modifiers.None;
            foreach (int key in swallowedKeys)
            {
                if (ShortcutMatcher.IsWin(key)) result |= Modifiers.Win;
                else if (ShortcutMatcher.IsCtrl(key)) result |= Modifiers.Ctrl;
                else if (ShortcutMatcher.IsAlt(key)) result |= Modifiers.Alt;
                else if (ShortcutMatcher.IsShift(key)) result |= Modifiers.Shift;
            }
            return result;
        }

        private bool ReleaseSwallowedKey(int key)
        {
            bool removed = swallowedKeys.Remove(key);
            if (ShortcutMatcher.IsCtrl(key)) removed |= swallowedKeys.Remove(0x11);
            if (ShortcutMatcher.IsAlt(key)) removed |= swallowedKeys.Remove(0x12);
            if (ShortcutMatcher.IsShift(key)) removed |= swallowedKeys.Remove(0x10);
            if (key == 0x11) { removed |= swallowedKeys.Remove(0xA2); removed |= swallowedKeys.Remove(0xA3); }
            if (key == 0x12) { removed |= swallowedKeys.Remove(0xA4); removed |= swallowedKeys.Remove(0xA5); }
            if (key == 0x10) { removed |= swallowedKeys.Remove(0xA0); removed |= swallowedKeys.Remove(0xA1); }
            return removed;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
            if (keyboardHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyboardHook); keyboardHook = IntPtr.Zero; }
        }
    }
}
