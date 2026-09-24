using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AtajosLibres
{
    internal sealed class UpdateInfo
    {
        internal Version Version;
        internal string Url;
        internal string Digest;
        internal long Size;
    }

    internal sealed class UpdateManager
    {
        private const string ApiUrl = "https://api.github.com/repos/IJustDaniii/shortcuttasker/releases/latest";
        private const string DownloadPrefix = "https://github.com/IJustDaniii/shortcuttasker/releases/download/";
        private const string InstallerName = "ShortcutTasker-Setup.exe";
        private readonly LauncherForm owner;
        private bool busy;

        internal UpdateManager(LauncherForm owner) { this.owner = owner; }
        internal void CheckOnStartup() { Check(false); }
        internal void CheckManually() { Check(true); }

        private async void Check(bool manual)
        {
            if (busy) return;
            busy = true;
            try
            {
                UpdateInfo update = await Task.Run(delegate { return FetchLatest(); });
                if (owner.IsDisposed) return;
                Version current = Assembly.GetExecutingAssembly().GetName().Version;
                if (update == null || update.Version <= current)
                {
                    if (manual) MessageBox.Show(owner, "Ya tienes la última versión disponible.", "ShortcutTasker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                owner.RestoreWindow();
                DialogResult answer = MessageBox.Show(owner,
                    "Hay una nueva versión: " + update.Version.ToString(3) + ".\n\n¿Quieres descargarla e instalarla ahora? Si cancelas, volveremos a preguntarte la próxima vez que abras la aplicación.",
                    "Actualización disponible", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                string file = await Task.Run(delegate { return DownloadVerified(update); });
                if (owner.IsDisposed) return;
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                owner.ExitForUpdate();
            }
            catch (WebException ex)
            {
                if (manual && !owner.IsDisposed) MessageBox.Show(owner, "No se pudieron comprobar las actualizaciones: " + ex.Message,
                    "ShortcutTasker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                if (!owner.IsDisposed) MessageBox.Show(owner, "No se pudo completar la actualización: " + ex.Message,
                    "ShortcutTasker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { busy = false; }
        }

        internal static UpdateInfo FetchLatest()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiUrl);
            request.Accept = "application/vnd.github+json";
            request.UserAgent = "ShortcutTasker/1.1.5";
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;
            string json;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream())) json = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound) return null;
                throw;
            }
            return ParseRelease(json);
        }

        internal static UpdateInfo ParseRelease(string json)
        {
            Dictionary<string, object> release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string tag = Convert.ToString(release["tag_name"]);
            Version version;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out version)) throw new InvalidDataException("La versión publicada no es válida.");
            ArrayList assets = release["assets"] as ArrayList;
            if (assets == null) throw new InvalidDataException("La versión no tiene archivos de instalación.");
            foreach (object value in assets)
            {
                Dictionary<string, object> asset = value as Dictionary<string, object>;
                if (asset == null || Convert.ToString(asset["name"]) != InstallerName) continue;
                string url = Convert.ToString(asset["browser_download_url"]);
                string digest = Convert.ToString(asset["digest"]);
                if (!url.StartsWith(DownloadPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
                    throw new InvalidDataException("El instalador publicado no tiene una dirección o huella válida.");
                long size = Convert.ToInt64(asset["size"]);
                if (size <= 0 || size > 100 * 1024 * 1024) throw new InvalidDataException("El tamaño del instalador no es válido.");
                return new UpdateInfo { Version = version, Url = url, Digest = digest.Substring(7), Size = size };
            }
            throw new InvalidDataException("La versión nueva no contiene " + InstallerName + ".");
        }

        private static string DownloadVerified(UpdateInfo info)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ShortcutTasker-Updates");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "ShortcutTasker-Setup-" + info.Version.ToString(3) + ".exe");
            string pending = file + ".download";
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(info.Url);
                request.UserAgent = "ShortcutTasker/1.1.5";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (FileStream output = File.Create(pending))
                {
                    byte[] buffer = new byte[65536];
                    int count;
                    long total = 0;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += count;
                        if (total > info.Size || total > 100 * 1024 * 1024) throw new InvalidDataException("La descarga excede el tamaño esperado.");
                        output.Write(buffer, 0, count);
                    }
                    if (total != info.Size) throw new InvalidDataException("La descarga quedó incompleta.");
                }
                string actual;
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(pending))
                    actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, info.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La descarga no coincide con la huella de GitHub.");
                if (File.Exists(file)) File.Delete(file);
                File.Move(pending, file);
                return file;
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
