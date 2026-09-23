using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AtajosLibres
{
    public sealed class InstalledApp
    {
        public string Name { get; set; }
        public string LaunchTarget { get; set; }
        public string ProcessName { get; set; }
        public string Kind { get; set; }
        public string WindowTitle { get; set; }
    }

    public static class RunningApps
    {
        private delegate bool EnumWindowProc(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        public static List<InstalledApp> Discover()
        {
            List<InstalledApp> result = new List<InstalledApp>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true;
                int length = GetWindowTextLength(window);
                if (length == 0 || length > 500) return true;
                StringBuilder title = new StringBuilder(length + 1);
                GetWindowText(window, title, title.Capacity);
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                try
                {
                    using (Process process = Process.GetProcessById((int)pid))
                    {
                        string name = process.ProcessName;
                        string identity = name + "|" + title;
                        if (name.Equals("ShortcutTasker", StringComparison.OrdinalIgnoreCase) || !seen.Add(identity)) return true;
                        result.Add(new InstalledApp { Name = title.ToString(), WindowTitle = title.ToString(),
                            ProcessName = name, Kind = "Abierta", LaunchTarget = "" });
                    }
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                return true;
            }, IntPtr.Zero);
            result.Sort(delegate(InstalledApp first, InstalledApp second) { return StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name); });
            return result;
        }
    }

    public static class InstalledApps
    {
        public static List<InstalledApp> Discover()
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) throw new InvalidOperationException("Windows no ofrece el catálogo de aplicaciones.");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic folder = null;
            dynamic items = null;
            List<InstalledApp> result = new List<InstalledApp>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                folder = shell.NameSpace("shell:AppsFolder");
                if (folder == null) throw new InvalidOperationException("No se pudo abrir la carpeta de aplicaciones de Windows.");
                items = folder.Items();
                int count = (int)items.Count;
                for (int i = 0; i < count; ++i)
                {
                    dynamic item = null;
                    try
                    {
                        item = items.Item(i);
                        if (item == null) continue;
                        object rawName = item.Name, rawPath = item.Path;
                        object rawExecutable = item.ExtendedProperty("System.Link.TargetParsingPath");
                        string name = rawName == null ? "" : Convert.ToString(rawName).Trim();
                        string path = rawPath == null ? "" : Convert.ToString(rawPath).Trim();
                        string executable = rawExecutable == null ? "" : Convert.ToString(rawExecutable).Trim();
                        InstalledApp app = CreateEntry(name, path, executable);
                        if (app != null && seen.Add(app.LaunchTarget)) result.Add(app);
                    }
                    catch (COMException) { }
                    finally { Release(item); }
                }
            }
            finally { Release(items); Release(folder); Release(shell); }
            result.Sort(delegate(InstalledApp first, InstalledApp second) { return StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name); });
            return result;
        }

        public static InstalledApp CreateEntry(string name, string path, string executable)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith("::{", StringComparison.Ordinal) || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return null;
            string target;
            string kind;
            string process = "";
            if (Path.IsPathRooted(path))
            {
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
                target = path;
                kind = "Escritorio";
                process = Path.GetFileNameWithoutExtension(path);
            }
            else if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                target = path;
                kind = "Protocolo";
            }
            else if (path.IndexOfAny(new char[] { '\\', '/' }) < 0)
            {
                target = "shell:AppsFolder\\" + path;
                kind = path.IndexOf('!') >= 0 ? "Tienda" : "Escritorio";
            }
            else return null;
            if (process.Length == 0 && executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                process = Path.GetFileNameWithoutExtension(executable.Replace('/', '\\'));
            return new InstalledApp { Name = name, LaunchTarget = target, ProcessName = process, Kind = kind };
        }

        public static string ResolvePackagedProcess(string launchTarget)
        {
            const string prefix = "shell:AppsFolder\\";
            if (string.IsNullOrEmpty(launchTarget) || !launchTarget.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
            string aumid = launchTarget.Substring(prefix.Length);
            int separator = aumid.IndexOf('!');
            if (separator <= 0 || separator == aumid.Length - 1 || !Regex.IsMatch(aumid, @"^[A-Za-z0-9_.!\-]+$")) return "";
            string family = aumid.Substring(0, separator);
            string appId = aumid.Substring(separator + 1);
            string script = "$p=Get-AppxPackage | Where-Object PackageFamilyName -eq '" + family + "' | Select-Object -First 1; " +
                "if($p){$m=Get-AppxPackageManifest $p; $a=$m.Package.Applications.Application | Where-Object Id -eq '" + appId + "' | Select-Object -First 1; " +
                "if($a){[Console]::Write([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$a.Executable)))}}";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encoded)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (Process process = Process.Start(info))
            {
                if (!process.WaitForExit(8000)) { process.Kill(); return ""; }
                if (process.ExitCode != 0) return "";
                string output = process.StandardOutput.ReadToEnd().Trim();
                try
                {
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(output));
                    return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(path.Replace('/', '\\')) : "";
                }
                catch (FormatException) { return ""; }
            }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
    }
}
