using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AtajosLibres
{
    internal static class Program
    {
        internal static string InstallFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ShortcutTasker");
        internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string RunName = "ShortcutTasker";
        internal const string ShowEventName = @"Local\AtajosLibres.Mostrar.v1";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool portable = args.Length > 0 && args[0] == "--portable";
            if (!portable && !Path.GetDirectoryName(Application.ExecutablePath).Equals(InstallFolder, StringComparison.OrdinalIgnoreCase))
            {
                try { InstallAndLaunch(); }
                catch (Exception ex) { MessageBox.Show("No se pudo instalar ShortcutTasker:\n" + ex.Message, "ShortcutTasker", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }

            bool first;
            using (Mutex mutex = new Mutex(true, @"Local\AtajosLibres.Instancia.v1", out first))
            {
                if (!first)
                {
                    try { EventWaitHandle.OpenExisting(ShowEventName).Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return;
                }
                try
                {
                    bool created;
                    using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName, out created))
                    using (ManualResetEvent stop = new ManualResetEvent(false))
                    using (LauncherForm form = new LauncherForm())
                    {
                        Thread listener = new Thread(delegate()
                        {
                            WaitHandle[] signals = { stop, showEvent };
                            while (true)
                            {
                                int signal = WaitHandle.WaitAny(signals);
                                if (signal == 0) break;
                                if (form.IsHandleCreated && !form.IsDisposed)
                                {
                                    try { form.BeginInvoke((MethodInvoker)delegate { form.RestoreWindow(); }); }
                                    catch (InvalidOperationException) { }
                                }
                            }
                        });
                        listener.IsBackground = true;
                        listener.Start();
                        if (args.Length > 0 && args[0] == "--background")
                        {
                            form.StartHidden = true;
                            form.WindowState = FormWindowState.Minimized;
                            form.ShowInTaskbar = false;
                        }
                        try { Application.Run(form); }
                        finally { stop.Set(); listener.Join(1000); }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("ShortcutTasker no pudo iniciarse:\n" + ex.Message,
                        "ShortcutTasker", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static void InstallAndLaunch()
        {
            Directory.CreateDirectory(InstallFolder);
            string source = Application.ExecutablePath;
            string hash;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(source))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Substring(0, 12);
            string target = Path.Combine(InstallFolder, "ShortcutTasker-" + hash + ".exe");
            if (!File.Exists(target)) File.Copy(source, target);

            string previous = null;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                previous = key.GetValue(RunName) as string;
                key.SetValue(RunName, "\"" + target + "\" --background", RegistryValueKind.String);
            }
            try
            {
                CreateStartShortcut(target);
                Process process = Process.Start(new ProcessStartInfo(target, "--show") { UseShellExecute = true });
                if (process == null) throw new Exception("Windows no inició la copia instalada.");
                Thread.Sleep(400);
                if (process.HasExited && process.ExitCode != 0) throw new Exception("La aplicación instalada terminó inesperadamente.");
            }
            catch
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (previous == null) key.DeleteValue(RunName, false);
                    else key.SetValue(RunName, previous, RegistryValueKind.String);
                }
                throw;
            }
        }

        private static void CreateStartShortcut(string target)
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string link = Path.Combine(programs, "ShortcutTasker.lnk");
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new Exception("Windows no pudo crear el acceso del menú Inicio.");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallFolder });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Configurar atajos de teclado" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
