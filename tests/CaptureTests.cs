using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AtajosLibres;

class CaptureTests
{
    static object Dispatch(InputCaptureSession session, string method, int message, object input)
    {
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(input));
        try
        {
            Marshal.StructureToPtr(input, pointer, false);
            return typeof(InputCaptureSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, new object[] { 0, new IntPtr(message), pointer });
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    [STAThread]
    static void Main()
    {
        int selected = 0, finished = 0;
        Modifiers modifiers = Modifiers.None;
        using (InputCaptureSession session = new InputCaptureSession(true, null))
        {
            session.Captured += delegate(int key, Modifiers value) { selected = key; modifiers = value; };
            session.Finished += delegate { ++finished; };
            Dispatch(session, "HandleKeyboard", Native.WM_KEYDOWN, new Native.KBDLLHOOKSTRUCT { vkCode = 0x11 });
            Dispatch(session, "HandleMouse", Native.WM_XBUTTONDOWN, new Native.MSLLHOOKSTRUCT { mouseData = 2u << 16 });
            if (selected != InputCode.MouseX2 || modifiers != Modifiers.Ctrl || finished != 0)
                throw new Exception("La captura del botón lateral con Ctrl falló.");
            Dispatch(session, "HandleMouse", Native.WM_XBUTTONUP, new Native.MSLLHOOKSTRUCT { mouseData = 2u << 16 });
            if (finished != 0) throw new Exception("La captura terminó antes de soltar Ctrl.");
            Dispatch(session, "HandleKeyboard", Native.WM_KEYUP, new Native.KBDLLHOOKSTRUCT { vkCode = 0x11 });
            if (finished != 1) throw new Exception("La captura no terminó al soltar los botones.");
        }
        Console.WriteLine("Mouse button capture and modifier release passed");

        selected = 0; finished = 0;
        using (InputCaptureSession session = new InputCaptureSession(true, null))
        {
            session.Captured += delegate(int key, Modifiers value) { selected = key; };
            session.Finished += delegate { ++finished; };
            session.Start();
            Native.INPUT wheel = new Native.INPUT();
            wheel.type = 0;
            wheel.data.mi.mouseData = unchecked((uint)-120);
            wheel.data.mi.dwFlags = 0x0800;
            if (Native.SendInput(1, new[] { wheel }, Marshal.SizeOf(typeof(Native.INPUT))) != 1)
                throw new Exception("No se pudo inyectar la rueda para la prueba de captura.");
            for (int i = 0; i < 100 && finished == 0; ++i) { Application.DoEvents(); Thread.Sleep(10); }
            if (selected != InputCode.WheelDown || finished != 1)
                throw new Exception("La captura real de la rueda falló.");
        }
        Console.WriteLine("Mouse wheel capture hook passed");

        selected = 0; finished = 0;
        using (InputCaptureSession session = new InputCaptureSession(false, null))
        {
            session.Captured += delegate(int key, Modifiers value) { selected = key; };
            session.Finished += delegate { ++finished; };
            session.Start();
            Native.INPUT down = KeyboardHook.MakeKey(0x09, false);
            Native.INPUT up = KeyboardHook.MakeKey(0x09, true);
            down.data.ki.dwExtraInfo = up.data.ki.dwExtraInfo = UIntPtr.Zero;
            if (Native.SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(Native.INPUT))) != 2)
                throw new Exception("No se pudo inyectar Tab para la prueba de captura.");
            for (int i = 0; i < 100 && finished == 0; ++i) { Application.DoEvents(); Thread.Sleep(10); }
            if (selected != 0x09 || finished != 1) throw new Exception("La captura real de Tab falló.");
        }
        Console.WriteLine("Keyboard Tab capture hook passed");

        using (ShortcutEditor editor = new ShortcutEditor(null))
        {
            editor.StartPosition = FormStartPosition.Manual;
            editor.Location = new Point(-10000, -10000);
            editor.Show();
            Application.DoEvents();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ShortcutEditor).GetMethod("BeginCapture", flags).Invoke(editor, new object[] { false });
            Native.INPUT ctrlDown = KeyboardHook.MakeKey(0x11, false);
            Native.INPUT ctrlUp = KeyboardHook.MakeKey(0x11, true);
            ctrlDown.data.ki.dwExtraInfo = ctrlUp.data.ki.dwExtraInfo = UIntPtr.Zero;
            Native.INPUT mouseDown = new Native.INPUT(), mouseUp = new Native.INPUT();
            mouseDown.type = mouseUp.type = 0;
            mouseDown.data.mi.mouseData = mouseUp.data.mi.mouseData = 2;
            mouseDown.data.mi.dwFlags = 0x0080;
            mouseUp.data.mi.dwFlags = 0x0100;
            Native.INPUT[] batch = { ctrlDown, mouseDown, mouseUp, ctrlUp };
            if (Native.SendInput((uint)batch.Length, batch, Marshal.SizeOf(typeof(Native.INPUT))) != batch.Length)
                throw new Exception("No se pudo inyectar la combinación de prueba.");
            Button captureButton = (Button)typeof(ShortcutEditor).GetField("keyCaptureButton", flags).GetValue(editor);
            for (int i = 0; i < 100 && captureButton.Text != "Capturar"; ++i) { Application.DoEvents(); Thread.Sleep(10); }
            TextBox display = (TextBox)typeof(ShortcutEditor).GetField("keyCapture", flags).GetValue(editor);
            CheckBox ctrl = (CheckBox)typeof(ShortcutEditor).GetField("ctrl", flags).GetValue(editor);
            if (display.Text != "Botón lateral 2" || !ctrl.Checked || captureButton.Text != "Capturar")
                throw new Exception("El formulario no guardó Ctrl + botón lateral 2.");
            editor.Close();
        }
        Console.WriteLine("Editor captured a mouse shortcut and its modifier");
    }
}
