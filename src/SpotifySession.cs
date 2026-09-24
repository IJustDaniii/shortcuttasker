using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;

namespace AtajosLibres
{
    internal static class SpotifySession
    {
        internal static void Control(string action)
        {
            GlobalSystemMediaTransportControlsSessionManager manager;
            try { manager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception ex) { throw new InvalidOperationException("Windows no pudo acceder a las sesiones multimedia: " + ex.Message, ex); }

            foreach (GlobalSystemMediaTransportControlsSession session in manager.GetSessions())
            {
                string source = session.SourceAppUserModelId ?? "";
                if (source.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool accepted;
                if (action == "spotify_playpause") accepted = session.TryTogglePlayPauseAsync().AsTask().GetAwaiter().GetResult();
                else if (action == "spotify_next") accepted = session.TrySkipNextAsync().AsTask().GetAwaiter().GetResult();
                else if (action == "spotify_previous") accepted = session.TrySkipPreviousAsync().AsTask().GetAwaiter().GetResult();
                else throw new ArgumentException("Control de Spotify desconocido.");
                if (!accepted) throw new InvalidOperationException("Spotify no aceptó el control multimedia. Comprueba que tiene una reproducción activa.");
                return;
            }
            throw new InvalidOperationException("No hay una sesión multimedia de Spotify. Abre Spotify y reproduce una canción al menos una vez.");
        }
    }
}
