using System;
using System.Collections.Generic;

namespace AtajosLibres
{
    public sealed class AppActionOption
    {
        public readonly string Action;
        public readonly string Label;
        public AppActionOption(string action, string label) { Action = action; Label = label; }
        public override string ToString() { return Label; }
    }

    public static class AppActionCatalog
    {
        public static string AppId(string name, string process, string launchTarget)
        {
            if (Same(process, "Discord") || Starts(launchTarget, @"shell:AppsFolder\com.squirrel.Discord.")) return "discord";
            if (Same(process, "Spotify") || Starts(launchTarget, @"shell:AppsFolder\SpotifyAB.SpotifyMusic_")) return "spotify";
            if (Same(name, "Discord")) return "discord";
            if (Same(name, "Spotify")) return "spotify";
            return "";
        }

        public static List<AppActionOption> ForApp(string name, string process, string launchTarget)
        {
            List<AppActionOption> options = new List<AppActionOption>();
            options.Add(new AppActionOption("open", "Abrir o mostrar la aplicación"));
            string id = AppId(name, process, launchTarget);
            if (id == "discord")
            {
                options.Add(new AppActionOption("discord_mute", "Silenciar / activar mi micrófono"));
                options.Add(new AppActionOption("discord_deafen", "Ensordecer / volver a oír"));
            }
            else if (id == "spotify")
            {
                options.Add(new AppActionOption("spotify_playpause", "Reproducir / pausar canción"));
                options.Add(new AppActionOption("spotify_next", "Siguiente canción"));
                options.Add(new AppActionOption("spotify_previous", "Canción anterior"));
            }
            else options.Add(new AppActionOption("appkey", "Usar un atajo propio de la aplicación"));
            return options;
        }

        private static bool Same(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
        private static bool Starts(string value, string prefix)
        {
            return value != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
