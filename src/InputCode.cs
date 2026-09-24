using System;

namespace AtajosLibres
{
    internal static class InputCode
    {
        internal const int MouseLeft = 0x100;
        internal const int MouseRight = 0x101;
        internal const int MouseMiddle = 0x102;
        internal const int MouseX1 = 0x103;
        internal const int MouseX2 = 0x104;
        internal const int WheelUp = 0x105;
        internal const int WheelDown = 0x106;
        internal const int WheelLeft = 0x107;
        internal const int WheelRight = 0x108;

        internal static bool IsMouse(int code) { return code >= MouseLeft && code <= WheelRight; }
        internal static bool IsWheel(int code) { return code >= WheelUp && code <= WheelRight; }

        internal static bool TryDecodeMouse(int message, uint data, out int code, out bool down, out bool pulse)
        {
            code = 0; down = false; pulse = false;
            switch (message)
            {
                case Native.WM_LBUTTONDOWN: case Native.WM_LBUTTONDBLCLK: code = MouseLeft; down = true; break;
                case Native.WM_LBUTTONUP: code = MouseLeft; break;
                case Native.WM_RBUTTONDOWN: case Native.WM_RBUTTONDBLCLK: code = MouseRight; down = true; break;
                case Native.WM_RBUTTONUP: code = MouseRight; break;
                case Native.WM_MBUTTONDOWN: case Native.WM_MBUTTONDBLCLK: code = MouseMiddle; down = true; break;
                case Native.WM_MBUTTONUP: code = MouseMiddle; break;
                case Native.WM_XBUTTONDOWN: case Native.WM_XBUTTONDBLCLK:
                case Native.WM_XBUTTONUP:
                    ushort x = (ushort)(data >> 16);
                    if (x != 1 && x != 2) return false;
                    code = x == 1 ? MouseX1 : MouseX2;
                    down = message != Native.WM_XBUTTONUP;
                    break;
                case Native.WM_MOUSEWHEEL:
                case Native.WM_MOUSEHWHEEL:
                    short delta = unchecked((short)(data >> 16));
                    if (delta == 0) return false;
                    code = message == Native.WM_MOUSEWHEEL
                        ? (delta > 0 ? WheelUp : WheelDown)
                        : (delta > 0 ? WheelRight : WheelLeft);
                    down = pulse = true;
                    break;
                default: return false;
            }
            return true;
        }
    }

    internal sealed class WheelAccumulator
    {
        private int vertical, horizontal;

        internal int Add(int code, int delta)
        {
            int old = code == InputCode.WheelUp || code == InputCode.WheelDown ? vertical : horizontal;
            if (old != 0 && Math.Sign(old) != Math.Sign(delta)) old = 0;
            int total = old + delta;
            int steps = Math.Abs(total) / 120;
            if (code == InputCode.WheelUp || code == InputCode.WheelDown) vertical = total % 120;
            else horizontal = total % 120;
            return steps;
        }

        internal void Reset(int code)
        {
            if (code == InputCode.WheelUp || code == InputCode.WheelDown) vertical = 0;
            else horizontal = 0;
        }

        internal void ResetAll() { vertical = horizontal = 0; }
    }
}
