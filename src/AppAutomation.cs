using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace AtajosLibres
{
    public static class AppAutomation
    {
        private const int SW_RESTORE = 9;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
        private static readonly Dictionary<string, double> rememberedVolumes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        private delegate bool EnumWindowProc(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

        public static void OpenOrActivate(string target, string appProcess)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Indica una aplicación, archivo o carpeta.");
            string process = NormalizeProcessName(appProcess);
            string fullPath = null;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target) &&
                (string.IsNullOrEmpty(process) || string.Equals(process, Path.GetFileNameWithoutExtension(target), StringComparison.OrdinalIgnoreCase)))
            {
                fullPath = Path.GetFullPath(target);
                process = Path.GetFileNameWithoutExtension(target);
            }
            if (!string.IsNullOrEmpty(process))
            {
                IntPtr existing = FindWindow(process, fullPath, null);
                if (existing != IntPtr.Zero)
                {
                    Activate(existing);
                    return;
                }
            }
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        public static void SendShortcut(string process, Modifiers modifiers, int key)
        {
            SendShortcutToApp(process, null, null, modifiers, key);
        }

        public static void SendShortcutToApp(string process, string launchTarget, string windowTitle, Modifiers modifiers, int key)
        {
            if (key <= 0 || key > 255) throw new ArgumentException("Elige la tecla de la acción.");
            process = NormalizeProcessName(process);
            if (process.Length == 0) throw new ArgumentException("No se conoce el proceso de la aplicación.");
            string fullPath = null;
            if (!string.IsNullOrEmpty(launchTarget) && launchTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(launchTarget) && string.Equals(process, Path.GetFileNameWithoutExtension(launchTarget), StringComparison.OrdinalIgnoreCase))
                fullPath = Path.GetFullPath(launchTarget);
            IntPtr window = FindWindow(process, fullPath, windowTitle);
            if (window == IntPtr.Zero && !string.IsNullOrEmpty(launchTarget))
            {
                Process.Start(new ProcessStartInfo(launchTarget) { UseShellExecute = true });
                for (int i = 0; i < 80 && window == IntPtr.Zero; ++i)
                {
                    Thread.Sleep(100);
                    window = FindWindow(process, fullPath, windowTitle);
                }
            }
            if (window == IntPtr.Zero) throw new InvalidOperationException("La aplicación no está abierta o no tiene ventana visible.");
            Activate(window);
            // Release all keys in the same SendInput batch so toggles never leave a modifier pressed.
            List<Native.INPUT> inputs = new List<Native.INPUT>();
            if ((modifiers & Modifiers.Win) != 0) inputs.Add(KeyboardHook.MakeKey(0x5B, false));
            if ((modifiers & Modifiers.Ctrl) != 0) inputs.Add(KeyboardHook.MakeKey(0x11, false));
            if ((modifiers & Modifiers.Alt) != 0) inputs.Add(KeyboardHook.MakeKey(0x12, false));
            if ((modifiers & Modifiers.Shift) != 0) inputs.Add(KeyboardHook.MakeKey(0x10, false));
            inputs.Add(KeyboardHook.MakeKey((ushort)key, false));
            inputs.Add(KeyboardHook.MakeKey((ushort)key, true));
            if ((modifiers & Modifiers.Shift) != 0) inputs.Add(KeyboardHook.MakeKey(0x10, true));
            if ((modifiers & Modifiers.Alt) != 0) inputs.Add(KeyboardHook.MakeKey(0x12, true));
            if ((modifiers & Modifiers.Ctrl) != 0) inputs.Add(KeyboardHook.MakeKey(0x11, true));
            if ((modifiers & Modifiers.Win) != 0) inputs.Add(KeyboardHook.MakeKey(0x5B, true));
            KeyboardHook.SendWithNeutralModifiers(inputs);
        }

        public static void ControlSpotify(string action)
        {
            SpotifySession.Control(action);
        }

        public static void ToggleDiscordVoice(bool deafen)
        {
            IntPtr window = FindWindow("Discord", null, null, true);
            if (window == IntPtr.Zero) throw new InvalidOperationException("Discord no tiene una ventana disponible. ShortcutTasker no abrirá la aplicación para ejecutar esta acción.");
            AutomationElement root = AutomationElement.FromHandle(window);
            AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            AutomationElement chosen = null;
            bool useToggle = false;
            foreach (AutomationElement button in buttons)
            {
                string name;
                try { name = button.Current.Name ?? ""; }
                catch (ElementNotAvailableException) { continue; }
                if (!button.Current.IsEnabled || !IsVoiceButton(button, deafen, name)) continue;
                object pattern;
                bool toggle = button.TryGetCurrentPattern(TogglePattern.Pattern, out pattern);
                if (!toggle && !button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) continue;
                if (chosen != null) throw new InvalidOperationException("Discord muestra más de un control válido; no se cambió ningún ajuste.");
                chosen = button;
                useToggle = toggle;
            }
            if (chosen == null)
                throw new InvalidOperationException(deafen
                    ? "No se encuentra el botón de ensordecimiento de Discord. Comprueba que Discord está abierto y conectado a una llamada."
                    : "No se encuentra el botón de micrófono de Discord. Comprueba que Discord está abierto y conectado a una llamada.");
            if (useToggle) ((TogglePattern)chosen.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            else ((InvokePattern)chosen.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }

        private static bool IsVoiceButton(AutomationElement button, bool deafen, string name)
        {
            string normalized = name.Trim().ToLowerInvariant();
            bool voiceAction = deafen
                ? normalized.Contains("ensordec") || normalized.Contains("deafen")
                : normalized.Contains("silenciar") || normalized.Contains("microfono") || normalized.Contains("micrófono") ||
                    normalized == "mute" || normalized.StartsWith("unmute");
            if (!voiceAction) return false;
            AutomationElement parent = button;
            for (int i = 0; i < 6 && parent != null; ++i)
            {
                try
                {
                    if (parent.Current.ControlType == ControlType.Group)
                    {
                        string group = (parent.Current.Name ?? "").Trim();
                        if (group.Equals("Estado y ajustes del usuario", StringComparison.OrdinalIgnoreCase) ||
                            group.Equals("User status and settings", StringComparison.OrdinalIgnoreCase) ||
                            group.Equals("User Status and Settings", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    parent = TreeWalker.ControlViewWalker.GetParent(parent);
                }
                catch (ElementNotAvailableException) { return false; }
            }
            return false;
        }

        public static void ToggleDiscordParticipant(string participant)
        {
            if (string.IsNullOrWhiteSpace(participant)) throw new ArgumentException("Indica el nombre visible del participante.");
            IntPtr window = FindWindow("Discord", null, null);
            if (window == IntPtr.Zero) throw new InvalidOperationException("Discord debe estar abierto.");
            Activate(window);
            AutomationElement root = AutomationElement.FromHandle(window);
            AutomationElementCollection descendants = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            List<AutomationElement> matches = new List<AutomationElement>();
            foreach (AutomationElement element in descendants)
            {
                string name;
                try { name = element.Current.Name; }
                catch (ElementNotAvailableException) { continue; }
                if (string.Equals(name, participant.Trim(), StringComparison.OrdinalIgnoreCase) && !element.Current.BoundingRectangle.IsEmpty)
                    matches.Add(element);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException(matches.Count == 0 ? "No encuentro a esa persona visible en Discord." : "Hay varias personas o elementos con ese nombre en Discord.");
            System.Windows.Rect bounds = matches[0].Current.BoundingRectangle;
            POINT oldPoint;
            GetCursorPos(out oldPoint);
            try
            {
                if (!SetCursorPos((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                Native.INPUT[] click = new Native.INPUT[2];
                click[0].type = click[1].type = 0;
                click[0].data.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
                click[1].data.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
                if (Native.SendInput(2, click, Marshal.SizeOf(typeof(Native.INPUT))) != 2)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                Thread.Sleep(250);
                AutomationElementCollection sliders = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider));
                List<AutomationElement> adjustable = new List<AutomationElement>();
                foreach (AutomationElement slider in sliders)
                {
                    object pattern;
                    System.Windows.Rect sliderBounds = slider.Current.BoundingRectangle;
                    if (sliderBounds.IsEmpty || !slider.TryGetCurrentPattern(RangeValuePattern.Pattern, out pattern)) continue;
                    // The participant's context menu should appear near their name.
                    if (Math.Abs(sliderBounds.Left - bounds.Left) > 600 || Math.Abs(sliderBounds.Top - bounds.Top) > 600) continue;
                    string sliderName = slider.Current.Name ?? "";
                    if (sliderName.Length > 0 && sliderName.IndexOf("volume", StringComparison.OrdinalIgnoreCase) < 0 &&
                        sliderName.IndexOf("volumen", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    adjustable.Add(slider);
                }
                if (adjustable.Count != 1) throw new InvalidOperationException("No puedo identificar de forma segura el volumen de esa persona en el menú de Discord.");
                RangeValuePattern range = (RangeValuePattern)adjustable[0].GetCurrentPattern(RangeValuePattern.Pattern);
                double current = range.Current.Value;
                string identity = participant.Trim();
                if (current > range.Current.Minimum)
                {
                    rememberedVolumes[identity] = current;
                    range.SetValue(range.Current.Minimum);
                }
                else
                {
                    double previous;
                    if (!rememberedVolumes.TryGetValue(identity, out previous)) previous = Math.Min(100, range.Current.Maximum);
                    range.SetValue(Math.Max(range.Current.Minimum, Math.Min(previous, range.Current.Maximum)));
                }
            }
            finally
            {
                SetCursorPos(oldPoint.X, oldPoint.Y);
                Native.INPUT[] escape = { KeyboardHook.MakeKey(0x1B, false), KeyboardHook.MakeKey(0x1B, true) };
                Native.SendInput(2, escape, Marshal.SizeOf(typeof(Native.INPUT)));
            }
        }

        public static string NormalizeProcessName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string value = name.Trim();
            if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - 4);
            if (value.IndexOfAny(new char[] { '\\', '/', ':', '*', '?' }) >= 0) throw new ArgumentException("Escribe solo el nombre del proceso, sin ruta.");
            return value;
        }

        private static IntPtr FindWindow(string process, string fullPath, string title, bool includeHidden = false)
        {
            if (string.IsNullOrEmpty(process)) return IntPtr.Zero;
            HashSet<int> ids = new HashSet<int>();
            foreach (Process running in Process.GetProcessesByName(process))
            {
                try
                {
                    if (fullPath == null || string.Equals(running.MainModule.FileName, fullPath, StringComparison.OrdinalIgnoreCase))
                        ids.Add(running.Id);
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { running.Dispose(); }
            }
            IntPtr found = IntPtr.Zero;
            IntPtr hidden = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (!ids.Contains((int)pid) || !MatchesTitle(window, title)) return true;
                if (IsWindowVisible(window))
                {
                    found = window;
                    return false;
                }
                if (includeHidden && hidden == IntPtr.Zero)
                {
                    int length = GetWindowTextLength(window);
                    if (length > 0)
                    {
                        StringBuilder caption = new StringBuilder(length + 1);
                        GetWindowText(window, caption, caption.Capacity);
                        string value = caption.ToString();
                        if (value.Equals("Discord", StringComparison.OrdinalIgnoreCase) ||
                            value.EndsWith(" - Discord", StringComparison.OrdinalIgnoreCase)) hidden = window;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found != IntPtr.Zero ? found : hidden;
        }

        private static bool MatchesTitle(IntPtr window, string title)
        {
            if (string.IsNullOrEmpty(title)) return true;
            int length = GetWindowTextLength(window);
            if (length != title.Length) return false;
            StringBuilder actual = new StringBuilder(length + 1);
            GetWindowText(window, actual, actual.Capacity);
            return string.Equals(actual.ToString(), title, StringComparison.OrdinalIgnoreCase);
        }

        private static void Activate(IntPtr window)
        {
            if (IsIconic(window)) ShowWindowAsync(window, SW_RESTORE);
            SetForegroundWindow(window);
            if (GetForegroundWindow() == window) return;
            uint ignored;
            IntPtr old = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(old, out ignored);
            uint currentThread = GetCurrentThreadId();
            if (foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true))
            {
                try { BringWindowToTop(window); SetForegroundWindow(window); }
                finally { AttachThreadInput(currentThread, foregroundThread, false); }
            }
            for (int i = 0; i < 10 && GetForegroundWindow() != window; ++i) Thread.Sleep(20);
            if (GetForegroundWindow() != window) throw new InvalidOperationException("Windows no permitió activar la ventana de la aplicación.");
        }
    }
}
