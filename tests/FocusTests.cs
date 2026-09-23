using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AtajosLibres;

class FocusTests
{
    static void Main()
    {
        string host = Path.GetFullPath("build\\WindowHost.exe");
        string marker = Path.GetFullPath("build\\window-key.txt");
        if (File.Exists(marker)) File.Delete(marker);
        using (Process process = Process.Start(host))
        {
            try
            {
                Thread.Sleep(700);
                int before = Process.GetProcessesByName("WindowHost").Length;
                AppAutomation.OpenOrActivate(host, "WindowHost");
                Thread.Sleep(200);
                int after = Process.GetProcessesByName("WindowHost").Length;
                if (before != 1 || after != 1) throw new Exception("Se abrió una segunda instancia.");
                AppAutomation.SendShortcutToApp("WindowHost", host, "ShortcutTasker focus test", Modifiers.Ctrl, 0x4A);
                Thread.Sleep(200);
                if (!File.Exists(marker)) throw new Exception("La ventana no recibió el atajo.");
                Console.WriteLine("Existing app window activated without launching a duplicate");
                Console.WriteLine("Application-specific shortcut delivered to its window");
            }
            finally { if (!process.HasExited) process.Kill(); if (File.Exists(marker)) File.Delete(marker); }
        }
    }
}
