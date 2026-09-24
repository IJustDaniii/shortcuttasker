using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AtajosLibres;
using Shortcut = AtajosLibres.Shortcut;

class MouseHookTests
{
    [STAThread]
    static void Main()
    {
        using (ManualResetEventSlim fired = new ManualResetEventSlim(false))
        using (KeyboardHook hook = new KeyboardHook())
        {
            int fireCount = 0;
            hook.SetBindings(new List<Shortcut> {
                new Shortcut { Name = "Rueda de prueba", Modifiers = Modifiers.Ctrl | Modifiers.Alt | Modifiers.Shift,
                    Key = InputCode.WheelUp, Enabled = true }
            });
            hook.Triggered += delegate(Shortcut shortcut) { if (shortcut.Name == "Rueda de prueba") { Interlocked.Increment(ref fireCount); fired.Set(); } };
            hook.Start();

            ushort[] modifiers = { 0x11, 0x12, 0x10 };
            try
            {
                foreach (ushort key in modifiers) SendKey(key, false);
                SendWheel(40);
                SendWheel(40);
                if (fired.Wait(150)) throw new Exception("La rueda se activó antes de completar un paso.");
                SendWheel(40);
                if (!fired.Wait(2000)) throw new Exception("El atajo con rueda no se ejecutó.");
                if (fireCount != 1) throw new Exception("El paso de rueda se ejecutó más de una vez.");
                Console.WriteLine("Mouse wheel shortcut fired on movement");
            }
            finally
            {
                for (int i = modifiers.Length - 1; i >= 0; --i) SendKey(modifiers[i], true);
            }
        }
    }

    static void SendKey(ushort key, bool up)
    {
        Native.INPUT input = KeyboardHook.MakeKey(key, up);
        input.data.ki.dwExtraInfo = UIntPtr.Zero;
        if (Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.INPUT))) != 1)
            throw new Exception("No se pudo inyectar un modificador de prueba.");
    }

    static void SendWheel(uint delta)
    {
        Native.INPUT wheel = new Native.INPUT();
        wheel.type = 0;
        wheel.data.mi.mouseData = delta;
        wheel.data.mi.dwFlags = 0x0800;
        if (Native.SendInput(1, new[] { wheel }, Marshal.SizeOf(typeof(Native.INPUT))) != 1)
            throw new Exception("No se pudo inyectar la rueda de prueba.");
    }
}
