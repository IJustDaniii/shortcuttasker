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
                Console.WriteLine("Existing app window activated without launching a duplicate");
            }
            finally { if (!process.HasExited) process.Kill(); }
        }
    }
}
