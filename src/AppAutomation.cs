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
            Native.INPUT[] batch = inputs.ToArray();
            if (Native.SendInput((uint)batch.Length, batch, Marshal.SizeOf(typeof(Native.INPUT))) != batch.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public static void ControlSpotify(string action, string launchTarget)
        {
            if (action != "spotify_playpause" && action != "spotify_next" && action != "spotify_previous")
                throw new ArgumentException("Control de Spotify desconocido.");
            IntPtr window = FindWindow("Spotify", null, null);
            if (window == IntPtr.Zero && !string.IsNullOrEmpty(launchTarget))
            {
                Process.Start(new ProcessStartInfo(launchTarget) { UseShellExecute = true });
                for (int i = 0; i < 80 && window == IntPtr.Zero; ++i)
                {
                    Thread.Sleep(100);
                    window = FindWindow("Spotify", null, null);
                }
            }
            if (window == IntPtr.Zero) throw new InvalidOperationException("Spotify debe estar abierto.");
            if (TryInvokeSpotifyPlayer(window, action)) return;
            // Spotify's accessible buttons can vary with its UI language and version.
            int key = action == "spotify_playpause" ? 0x20 : action == "spotify_next" ? 0x27 : 0x25;
            Modifiers modifiers = action == "spotify_playpause" ? Modifiers.None : Modifiers.Ctrl;
            SendShortcutToApp("Spotify", null, null, modifiers, key);
        }

        private static bool TryInvokeSpotifyPlayer(IntPtr window, string action)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(window);
                AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                List<AutomationElement> previous = new List<AutomationElement>();
                List<AutomationElement> next = new List<AutomationElement>();
                List<AutomationElement> play = new List<AutomationElement>();
                foreach (AutomationElement button in buttons)
                {
                    if (!button.Current.IsEnabled || button.Current.BoundingRectangle.IsEmpty) continue;
                    string name = button.Current.Name;
                    if (name == "Anterior" || name == "Previous" || name == "Previous track") previous.Add(button);
                    else if (name == "Siguiente" || name == "Next" || name == "Next track") next.Add(button);
                    else if (name == "Reproducir" || name == "Pausar" || name == "Play" || name == "Pause") play.Add(button);
                }
                foreach (AutomationElement prev in previous)
                    foreach (AutomationElement nxt in next)
                    {
                        System.Windows.Rect left = prev.Current.BoundingRectangle;
                        System.Windows.Rect right = nxt.Current.BoundingRectangle;
                        if (right.Left <= left.Right || right.Left - left.Right > 160 || Math.Abs(right.Top - left.Top) > 20) continue;
                        AutomationElement chosen = null;
                        if (action == "spotify_previous") chosen = prev;
                        else if (action == "spotify_next") chosen = nxt;
                        else foreach (AutomationElement candidate in play)
                        {
                            System.Windows.Rect middle = candidate.Current.BoundingRectangle;
                            if (middle.Left > left.Right && middle.Right < right.Left && Math.Abs(middle.Top - left.Top) <= 20)
                            { chosen = candidate; break; }
                        }
                        object pattern;
                        if (chosen != null && chosen.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                        {
                            ((InvokePattern)pattern).Invoke();
                            return true;
                        }
                    }
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
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

        private static IntPtr FindWindow(string process, string fullPath, string title)
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
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (ids.Contains((int)pid) && IsWindowVisible(window) && MatchesTitle(window, title))
                {
                    found = window;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
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
