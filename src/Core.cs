using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Serialization;

namespace AtajosLibres
{
    [Flags]
    public enum Modifiers { None = 0, Win = 1, Ctrl = 2, Alt = 4, Shift = 8 }

    public class Shortcut
    {
        public string Name { get; set; }
        public Modifiers Modifiers { get; set; }
        public int Key { get; set; }
        public string Action { get; set; }
        public string Target { get; set; }
        public string AppProcess { get; set; }
        public Modifiers AppModifiers { get; set; }
        public int AppKey { get; set; }
        public bool Enabled { get; set; }

        public Shortcut() { Name = ""; Action = "open"; Target = ""; AppProcess = ""; Enabled = true; }
        public Shortcut Clone()
        {
            return (Shortcut)MemberwiseClone();
        }
    }

    [XmlRoot("AtajosLibres")]
    public class ShortcutConfig
    {
        [XmlArray("Atajos")]
        [XmlArrayItem("Atajo")]
        public List<Shortcut> Shortcuts { get; set; }
        public ShortcutConfig() { Shortcuts = new List<Shortcut>(); }
    }

    public static class ConfigStore
    {
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtajosLibres"); } }
        public static string FilePath { get { return Path.Combine(Folder, "atajos.xml"); } }

        public static ShortcutConfig Load()
        {
            if (!File.Exists(FilePath))
            {
                ShortcutConfig initial = new ShortcutConfig();
                initial.Shortcuts.Add(new Shortcut {
                    Name = "ChatGPT", Modifiers = Modifiers.Win, Key = 0x43,
                    Action = "open", Target = "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App", Enabled = true
                });
                Save(initial);
                return initial;
            }
            using (FileStream stream = File.OpenRead(FilePath))
                return (ShortcutConfig)new XmlSerializer(typeof(ShortcutConfig)).Deserialize(stream);
        }

        public static void Save(ShortcutConfig config)
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".new";
            using (FileStream stream = File.Create(temp))
                new XmlSerializer(typeof(ShortcutConfig)).Serialize(stream, config);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
    }

    public class MatchResult
    {
        public bool Suppress;
        public bool MaskMenu;
        public List<Shortcut> Run = new List<Shortcut>();
    }

    public class ShortcutMatcher
    {
        private readonly HashSet<int> downModifiers = new HashSet<int>();
        private readonly HashSet<int> consumedKeys = new HashSet<int>();
        private readonly List<Shortcut> pending = new List<Shortcut>();
        private Shortcut[] bindings = new Shortcut[0];
        private bool maskOnModifierRelease;

        public void SetBindings(IList<Shortcut> source)
        {
            Shortcut[] copy = new Shortcut[source.Count];
            for (int i = 0; i < source.Count; ++i) copy[i] = source[i].Clone();
            Interlocked.Exchange(ref bindings, copy);
        }

        public MatchResult Process(int key, bool isDown)
        {
            MatchResult result = new MatchResult();
            if (IsModifier(key))
            {
                if (isDown) downModifiers.Add(key);
                else
                {
                    if (maskOnModifierRelease && (IsWin(key) || IsAlt(key))) result.MaskMenu = true;
                    downModifiers.Remove(key);
                    if (IsCtrl(key)) downModifiers.Remove(0x11);
                    else if (IsAlt(key)) downModifiers.Remove(0x12);
                    else if (IsShift(key)) downModifiers.Remove(0x10);
                    if (key == 0x11) { downModifiers.Remove(0xA2); downModifiers.Remove(0xA3); }
                    if (key == 0x12) { downModifiers.Remove(0xA4); downModifiers.Remove(0xA5); }
                    if (key == 0x10) { downModifiers.Remove(0xA0); downModifiers.Remove(0xA1); }
                    if (downModifiers.Count == 0)
                    {
                        result.Run.AddRange(pending);
                        pending.Clear();
                        maskOnModifierRelease = false;
                    }
                }
                return result;
            }

            if (!isDown)
            {
                result.Suppress = consumedKeys.Remove(key);
                return result;
            }
            if (consumedKeys.Contains(key))
            {
                result.Suppress = true;
                return result;
            }

            Modifiers current = CurrentModifiers();
            if (current == Modifiers.None) return result;
            Shortcut[] snapshot = bindings;
            foreach (Shortcut shortcut in snapshot)
            {
                if (shortcut.Enabled && shortcut.Key == key && shortcut.Modifiers == current)
                {
                    consumedKeys.Add(key);
                    pending.Add(shortcut);
                    result.Suppress = true;
                    result.MaskMenu = (current & (Modifiers.Win | Modifiers.Alt)) != 0;
                    maskOnModifierRelease |= result.MaskMenu;
                    return result;
                }
            }
            return result;
        }

        public void ReconcileModifiers(Func<int, bool> isPressed)
        {
            int[] keys = { 0x5B, 0x5C, 0xA2, 0xA3, 0xA4, 0xA5, 0xA0, 0xA1 };
            foreach (int key in keys)
            {
                if (isPressed(key)) downModifiers.Add(key);
                else downModifiers.Remove(key);
            }
        }

        private Modifiers CurrentModifiers()
        {
            Modifiers result = Modifiers.None;
            foreach (int key in downModifiers)
            {
                if (IsWin(key)) result |= Modifiers.Win;
                else if (IsCtrl(key)) result |= Modifiers.Ctrl;
                else if (IsAlt(key)) result |= Modifiers.Alt;
                else if (IsShift(key)) result |= Modifiers.Shift;
            }
            return result;
        }

        public static bool IsWin(int key) { return key == 0x5B || key == 0x5C; }
        public static bool IsCtrl(int key) { return key == 0x11 || key == 0xA2 || key == 0xA3; }
        public static bool IsAlt(int key) { return key == 0x12 || key == 0xA4 || key == 0xA5; }
        public static bool IsShift(int key) { return key == 0x10 || key == 0xA0 || key == 0xA1; }
        public static bool IsModifier(int key) { return IsWin(key) || IsCtrl(key) || IsAlt(key) || IsShift(key); }
    }

    internal static class Native
    {
        internal const int WH_KEYBOARD_LL = 13;
        internal const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        internal const int LLKHF_INJECTED = 0x10;
        internal const int INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;
        internal const uint OwnInputMarker = 0xC0DEC0DE;

        [StructLayout(LayoutKind.Sequential)]
        internal struct KBDLLHOOKSTRUCT
        {
            public uint vkCode, scanCode, flags, time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public int type;
            public INPUTUNION data;
        }

        internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] internal static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
    }

    public class KeyboardHook : IDisposable
    {
        private readonly ShortcutMatcher matcher = new ShortcutMatcher();
        private readonly Native.HookProc callback;
        private IntPtr hook;
        private Thread thread;
        private uint threadId;
        private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
        private Exception startError;
        public event Action<Shortcut> Triggered;

        public KeyboardHook() { callback = Handle; }
        public void SetBindings(IList<Shortcut> shortcuts) { matcher.SetBindings(shortcuts); }

        public void Start()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!ready.Wait(5000)) throw new Exception("El capturador de teclado no respondió.");
            if (startError != null) throw startError;
        }

        private void Run()
        {
            threadId = Native.GetCurrentThreadId();
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, callback, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                startError = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                ready.Set();
                return;
            }
            ready.Set();
            try { System.Windows.Forms.Application.Run(); }
            finally { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }

        private IntPtr Handle(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0)
                {
                    int message = wParam.ToInt32();
                    if (message == Native.WM_KEYDOWN || message == Native.WM_KEYUP ||
                        message == Native.WM_SYSKEYDOWN || message == Native.WM_SYSKEYUP)
                    {
                        Native.KBDLLHOOKSTRUCT key = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                        if ((key.flags & Native.LLKHF_INJECTED) != 0 && key.dwExtraInfo.ToUInt64() == Native.OwnInputMarker)
                            return Native.CallNextHookEx(hook, code, wParam, lParam);
                        bool down = message == Native.WM_KEYDOWN || message == Native.WM_SYSKEYDOWN;
                        if (down && !ShortcutMatcher.IsModifier((int)key.vkCode))
                            matcher.ReconcileModifiers(delegate(int vk) { return (Native.GetAsyncKeyState(vk) & 0x8000) != 0; });
                        MatchResult result = matcher.Process((int)key.vkCode, down);
                        bool replacedRelease = false;
                        if (result.MaskMenu)
                        {
                            if (!down && ShortcutMatcher.IsModifier((int)key.vkCode))
                            {
                                replacedRelease = MaskAndRelease((ushort)key.vkCode);
                            }
                            else MaskMenu();
                        }
                        foreach (Shortcut shortcut in result.Run)
                        {
                            Action<Shortcut> handler = Triggered;
                            if (handler != null) ThreadPool.QueueUserWorkItem(delegate { handler(shortcut); });
                        }
                        if (result.Suppress || replacedRelease) return new IntPtr(1);
                    }
                }
            }
            catch { /* A keyboard hook must never crash the desktop input chain. */ }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        private static void MaskMenu()
        {
            Native.INPUT[] inputs = new Native.INPUT[2];
            inputs[0] = MakeKey(0xE8, false);
            inputs[1] = MakeKey(0xE8, true);
            Native.SendInput(2, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        private static bool MaskAndRelease(ushort modifier)
        {
            Native.INPUT[] inputs = { MakeKey(0xE8, false), MakeKey(0xE8, true), MakeKey(modifier, true) };
            return Native.SendInput(3, inputs, Marshal.SizeOf(typeof(Native.INPUT))) == 3;
        }

        internal static Native.INPUT MakeKey(ushort key, bool up)
        {
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.data.ki.wVk = key;
            input.data.ki.dwFlags = up ? (uint)Native.KEYEVENTF_KEYUP : 0u;
            if (key == 0x5B || key == 0x5C || key == 0xA5) input.data.ki.dwFlags |= 1u;
            input.data.ki.dwExtraInfo = new UIntPtr(Native.OwnInputMarker);
            return input;
        }

        public void Dispose()
        {
            if (threadId != 0) Native.PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
            if (thread != null) thread.Join(1000);
        }
    }

    public static class ActionRunner
    {
        public static void Run(Shortcut shortcut)
        {
            for (int attempt = 0; attempt < 200; ++attempt)
            {
                if ((Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 &&
                    (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0 &&
                    (Native.GetAsyncKeyState(0x10) & 0x8000) == 0 &&
                    (Native.GetAsyncKeyState(0x11) & 0x8000) == 0 &&
                    (Native.GetAsyncKeyState(0x12) & 0x8000) == 0) break;
                if (attempt == 199) throw new Exception("Las teclas del atajo siguen pulsadas.");
                Thread.Sleep(10);
            }
            string target = shortcut.Target == null ? "" : shortcut.Target.Trim();
            if (shortcut.Action == "open")
            {
                AppAutomation.OpenOrActivate(target, shortcut.AppProcess);
            }
            else if (shortcut.Action == "web")
            {
                Uri uri;
                if (!Uri.TryCreate(target, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                    throw new ArgumentException("La dirección web debe empezar por https:// o http://.");
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            else if (shortcut.Action == "command")
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/d /c " + target) { UseShellExecute = false, CreateNoWindow = true });
            }
            else if (shortcut.Action == "text")
            {
                foreach (char c in target)
                {
                    Native.INPUT[] inputs = new Native.INPUT[2];
                    inputs[0].type = inputs[1].type = Native.INPUT_KEYBOARD;
                    inputs[0].data.ki.wScan = inputs[1].data.ki.wScan = c;
                    inputs[0].data.ki.dwFlags = Native.KEYEVENTF_UNICODE;
                    inputs[1].data.ki.dwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP;
                    inputs[0].data.ki.dwExtraInfo = inputs[1].data.ki.dwExtraInfo = new UIntPtr(Native.OwnInputMarker);
                    if (Native.SendInput(2, inputs, Marshal.SizeOf(typeof(Native.INPUT))) != 2)
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            else if (shortcut.Action == "media")
            {
                ushort key = 0;
                if (target == "Reproducir / pausar") key = 0xB3;
                else if (target == "Siguiente pista") key = 0xB0;
                else if (target == "Pista anterior") key = 0xB1;
                else if (target == "Subir volumen") key = 0xAF;
                else if (target == "Bajar volumen") key = 0xAE;
                else if (target == "Silenciar") key = 0xAD;
                else throw new ArgumentException("Control multimedia desconocido.");
                Native.INPUT[] inputs = { KeyboardHook.MakeKey(key, false), KeyboardHook.MakeKey(key, true) };
                if (Native.SendInput(2, inputs, Marshal.SizeOf(typeof(Native.INPUT))) != 2)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (shortcut.Action == "appkey")
            {
                AppAutomation.SendShortcut(shortcut.AppProcess, shortcut.AppModifiers, shortcut.AppKey);
            }
            else if (shortcut.Action == "discord_mute")
            {
                AppAutomation.SendShortcut("Discord", Modifiers.Ctrl | Modifiers.Shift, 0x4D);
            }
            else if (shortcut.Action == "discord_deafen")
            {
                AppAutomation.SendShortcut("Discord", Modifiers.Ctrl | Modifiers.Shift, 0x44);
            }
            else if (shortcut.Action == "discord_person")
            {
                AppAutomation.ToggleDiscordParticipant(target);
            }
            else throw new ArgumentException("Acción desconocida.");
        }
    }
}
