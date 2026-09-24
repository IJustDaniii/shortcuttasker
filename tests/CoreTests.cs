using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using AtajosLibres;
using Shortcut = AtajosLibres.Shortcut;

class CoreTests
{
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }

    static Shortcut SaveAppAction(string appName, string process, string launchTarget, string action)
    {
        using (ShortcutEditor editor = new ShortcutEditor(null))
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(ShortcutEditor);
            ((TextBox)type.GetField("nameBox", flags).GetValue(editor)).Text = "Control de prueba";
            type.GetField("keyCode", flags).SetValue(editor, 0x43);
            ((CheckBox)type.GetField("win", flags).GetValue(editor)).Checked = true;
            ((TextBox)type.GetField("targetBox", flags).GetValue(editor)).Text = appName;
            ((TextBox)type.GetField("processBox", flags).GetValue(editor)).Text = process;
            type.GetField("selectedAppName", flags).SetValue(editor, appName);
            type.GetField("selectedAppLaunchTarget", flags).SetValue(editor, launchTarget);
            type.GetMethod("RefreshAppActions", flags).Invoke(editor, new object[] { action });
            type.GetMethod("Save", flags).Invoke(editor, new object[] { null, EventArgs.Empty });
            return editor.Result;
        }
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
        Check(trigger.Suppress && trigger.MaskMenu && trigger.Run.Count == 1 && trigger.Run[0].Name == "ChatGPT", "Win+C must run on key down");
        MatchResult repeat = matcher.Process(0x43, true);
        Check(repeat.Suppress && repeat.Run.Count == 0, "Repeat C must be suppressed without retriggering");
        Check(matcher.Process(0x43, false).Suppress, "C up must be suppressed");
        MatchResult release = matcher.Process(0x5B, false);
        Check(release.MaskMenu && release.Run.Count == 0, "Win release masks Start without rerunning");

        matcher.Process(0x11, true);
        matcher.Process(0x12, true);
        MatchResult textTrigger = matcher.Process(0x54, true);
        Check(textTrigger.Suppress && textTrigger.Run.Count == 1, "Ctrl+Alt+T must run on key down");
        matcher.Process(0x54, false);
        Check(matcher.Process(0x11, false).Run.Count == 0, "No rerun on first modifier release");
        Check(matcher.Process(0x12, false).Run.Count == 0, "No rerun on last modifier release");

        matcher.Process(0xA2, true);
        matcher.Process(0xA4, true);
        matcher.ReconcileModifiers(delegate(int key) { return key == 0xA2 || key == 0xA4; });
        MatchResult leftTrigger = matcher.Process(0x54, true);
        Check(leftTrigger.Suppress && leftTrigger.Run.Count == 1, "Left Ctrl+Alt must match and run immediately");
        matcher.Process(0x54, false);
        matcher.Process(0xA2, false);
        Check(matcher.Process(0xA4, false).Run.Count == 0, "Left modifiers release does not rerun");

        int mouseCode; bool mouseDown, mousePulse;
        Check(InputCode.TryDecodeMouse(Native.WM_XBUTTONDOWN, 2u << 16, out mouseCode, out mouseDown, out mousePulse) &&
            mouseCode == InputCode.MouseX2 && mouseDown && !mousePulse, "Second side button decoded");
        Check(InputCode.TryDecodeMouse(Native.WM_MOUSEWHEEL, 120u << 16, out mouseCode, out mouseDown, out mousePulse) &&
            mouseCode == InputCode.WheelUp && mousePulse, "Vertical wheel up decoded");
        Check(InputCode.TryDecodeMouse(Native.WM_MOUSEHWHEEL, unchecked((uint)(-120 << 16)), out mouseCode, out mouseDown, out mousePulse) &&
            mouseCode == InputCode.WheelLeft && mousePulse, "Horizontal wheel left decoded");
        Check(ShortcutNames.KeyName(InputCode.MouseX2) == "Botón lateral 2" &&
            ShortcutNames.KeyName(InputCode.WheelUp) == "Rueda arriba", "Mouse inputs display in shortcut list");
        WheelAccumulator smoothWheel = new WheelAccumulator();
        Check(smoothWheel.Add(InputCode.WheelUp, 40) == 0 && smoothWheel.Add(InputCode.WheelUp, 40) == 0 &&
            smoothWheel.Add(InputCode.WheelUp, 40) == 1, "Three small wheel deltas make one shortcut");
        Check(smoothWheel.Add(InputCode.WheelDown, -60) == 0 && smoothWheel.Add(InputCode.WheelUp, 120) == 1,
            "Changing wheel direction discards the previous partial step");
        matcher.SetBindings(new List<Shortcut> { new Shortcut { Name = "Rueda", Modifiers = Modifiers.Ctrl, Key = InputCode.WheelUp, Enabled = true } });
        matcher.Process(0x11, true);
        MatchResult wheel = matcher.Process(InputCode.WheelUp, true);
        Check(wheel.Suppress && wheel.Run.Count == 1, "Ctrl+wheel runs on wheel event");
        matcher.Process(InputCode.WheelUp, false);
        Check(matcher.Process(InputCode.WheelUp, true).Run.Count == 1, "Next wheel notch runs again");
        matcher.Process(InputCode.WheelUp, false);
        matcher.Process(0x11, false);
        matcher.SetBindings(new List<Shortcut> { new Shortcut { Name = "Rueda Win", Modifiers = Modifiers.Win, Key = InputCode.WheelUp, Enabled = true } });
        matcher.Process(0x5B, true);
        Check(matcher.HasBinding(InputCode.WheelUp) && matcher.MarkHandledInput(), "Partial Win+wheel is handled and masks Start");
        Check(matcher.Process(0x5B, false).MaskMenu, "Win release stays masked after a partial wheel movement");

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
        List<AppActionOption> discord = AppActionCatalog.ForApp("Discord", "Discord", "");
        Check(discord.Count == 3 && discord[0].Action == "open" && discord[1].Action == "discord_mute" &&
            discord[2].Action == "discord_deafen", "Discord offers only its two voice controls");
        List<AppActionOption> spotify = AppActionCatalog.ForApp("Spotify", "Spotify", "");
        Check(spotify.Count == 4 && spotify[1].Action == "spotify_playpause" && spotify[2].Action == "spotify_next" &&
            spotify[3].Action == "spotify_previous", "Spotify offers three playback controls");
        List<AppActionOption> other = AppActionCatalog.ForApp("Editor", "editor", @"C:\Apps\editor.exe");
        Check(other.Count == 2 && other[1].Action == "appkey", "Other apps offer a custom shortcut");
        Shortcut discordRule = SaveAppAction("Discord", "Discord", @"shell:AppsFolder\com.squirrel.Discord.Discord", "discord_mute");
        Check(discordRule != null && discordRule.Action == "discord_mute" && discordRule.AppDisplayName == "Discord", "Editor saves Discord voice control");
        Shortcut spotifyRule = SaveAppAction("Spotify", "Spotify", @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "spotify_next");
        Check(spotifyRule != null && spotifyRule.Action == "spotify_next" && spotifyRule.AppProcess == "Spotify", "Editor saves Spotify playback control");
        List<InstalledApp> detected = InstalledApps.Discover();
        Check(detected.Count > 0, "Windows installed-app catalog");
        InstalledApp knownPackage = detected.Find(delegate(InstalledApp app) { return app.LaunchTarget.StartsWith(@"shell:AppsFolder\OpenAI.Codex_"); });
        if (knownPackage != null) Check(InstalledApps.ResolvePackagedProcess(knownPackage.LaunchTarget) == "ChatGPT", "Packaged app process");

        Console.WriteLine("Behavioral checks passed");
    }
}
