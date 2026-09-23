using System;
using System.Collections.Generic;
using AtajosLibres;

class CoreTests
{
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }

    [STAThread]
    static void Main()
    {
        ShortcutMatcher matcher = new ShortcutMatcher();
        matcher.SetBindings(new List<Shortcut> {
            new Shortcut { Name = "ChatGPT", Modifiers = Modifiers.Win, Key = 0x43, Enabled = true },
            new Shortcut { Name = "Texto", Modifiers = Modifiers.Ctrl | Modifiers.Alt, Key = 0x54, Enabled = true }
        });

        Check(!matcher.Process(0x5B, true).Suppress, "Win down must pass");
        Check(!matcher.Process(0x45, true).Suppress, "Win+E must pass");
        Check(!matcher.Process(0x45, false).Suppress, "Win+E up must pass");
        Check(!matcher.Process(0x5B, false).MaskMenu, "Unmatched Win should open Start normally");

        matcher.Process(0x5B, true);
        MatchResult trigger = matcher.Process(0x43, true);
        Check(trigger.Suppress && trigger.MaskMenu, "Win+C must suppress and mask");
        Check(matcher.Process(0x43, true).Suppress, "Repeat C must be suppressed");
        Check(matcher.Process(0x43, false).Suppress, "C up must be suppressed");
        MatchResult release = matcher.Process(0x5B, false);
        Check(release.MaskMenu && release.Run.Count == 1 && release.Run[0].Name == "ChatGPT", "Launch once after Win release");

        matcher.Process(0x11, true);
        matcher.Process(0x12, true);
        Check(matcher.Process(0x54, true).Suppress, "Ctrl+Alt+T must match");
        matcher.Process(0x54, false);
        Check(matcher.Process(0x11, false).Run.Count == 0, "Wait for every modifier");
        Check(matcher.Process(0x12, false).Run.Count == 1, "Run after final modifier");

        matcher.Process(0xA2, true);
        matcher.Process(0xA4, true);
        matcher.ReconcileModifiers(delegate(int key) { return key == 0xA2 || key == 0xA4; });
        Check(matcher.Process(0x54, true).Suppress, "Left Ctrl+Alt must match");
        matcher.Process(0x54, false);
        matcher.Process(0xA2, false);
        Check(matcher.Process(0xA4, false).Run.Count == 1, "Left modifiers release action");

        Check(AppAutomation.NormalizeProcessName("Discord.exe") == "Discord", "Normalize process name");
        Check(AppAutomation.NormalizeProcessName("  spotify  ") == "spotify", "Trim process name");
        try
        {
            AppAutomation.NormalizeProcessName(@"C:\Discord.exe");
            throw new Exception("Process path should be rejected");
        }
        catch (ArgumentException) { }

        string releaseJson = "{\"tag_name\":\"v1.1.0\",\"assets\":[{\"name\":\"ShortcutTasker-Setup.exe\",\"browser_download_url\":\"https://github.com/IJustDaniii/shortcuttasker/releases/download/v1.1.0/ShortcutTasker-Setup.exe\",\"digest\":\"sha256:" + new string('a', 64) + "\",\"size\":1234}]}";
        UpdateInfo parsed = UpdateManager.ParseRelease(releaseJson);
        Check(parsed.Version == new Version(1, 1, 0) && parsed.Size == 1234, "Parse update release");
        try
        {
            UpdateManager.ParseRelease(releaseJson.Replace("github.com/IJustDaniii/shortcuttasker", "example.com/fake"));
            throw new Exception("Untrusted update origin should be rejected");
        }
        catch (System.IO.InvalidDataException) { }

        InstalledApp desktop = InstalledApps.CreateEntry("Prueba", "Prueba.App", @"C:\Apps\Prueba.exe");
        Check(desktop.LaunchTarget == @"shell:AppsFolder\Prueba.App" && desktop.ProcessName == "Prueba", "Catalog desktop app");
        InstalledApp packaged = InstalledApps.CreateEntry("Paquete", "Ejemplo.Paquete_123!App", "");
        Check(packaged.LaunchTarget == @"shell:AppsFolder\Ejemplo.Paquete_123!App" && packaged.ProcessName == "", "Catalog packaged app");
        List<InstalledApp> detected = InstalledApps.Discover();
        Check(detected.Count > 0, "Windows installed-app catalog");
        InstalledApp knownPackage = detected.Find(delegate(InstalledApp app) { return app.LaunchTarget.StartsWith(@"shell:AppsFolder\OpenAI.Codex_"); });
        if (knownPackage != null) Check(InstalledApps.ResolvePackagedProcess(knownPackage.LaunchTarget) == "ChatGPT", "Packaged app process");

        Console.WriteLine("13 behavioral checks passed");
    }
}
