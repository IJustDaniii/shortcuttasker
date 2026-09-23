using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AtajosLibres;

class UiProbe
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        using (ShortcutEditor editor = new ShortcutEditor(null))
        using (Bitmap image = new Bitmap(editor.Width, editor.Height))
        {
            editor.StartPosition = FormStartPosition.Manual;
            editor.Location = new Point(-10000, -10000);
            editor.Show();
            Application.DoEvents();
            editor.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save("build\\editor.png", ImageFormat.Png);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(ShortcutEditor);
            ((TextBox)type.GetField("targetBox", flags).GetValue(editor)).Text = "Discord";
            type.GetField("selectedAppName", flags).SetValue(editor, "Discord");
            type.GetField("selectedAppLaunchTarget", flags).SetValue(editor, @"shell:AppsFolder\com.squirrel.Discord.Discord");
            ((TextBox)type.GetField("processBox", flags).GetValue(editor)).Text = "Discord";
            type.GetMethod("RefreshAppActions", flags).Invoke(editor, new object[] { "discord_mute" });
            Application.DoEvents();
            using (Bitmap discordImage = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(discordImage, new Rectangle(0, 0, discordImage.Width, discordImage.Height));
                discordImage.Save("build\\editor-discord.png", ImageFormat.Png);
            }
            ((TextBox)type.GetField("targetBox", flags).GetValue(editor)).Text = "Spotify";
            type.GetField("selectedAppName", flags).SetValue(editor, "Spotify");
            type.GetField("selectedAppLaunchTarget", flags).SetValue(editor, @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");
            ((TextBox)type.GetField("processBox", flags).GetValue(editor)).Text = "Spotify";
            type.GetMethod("RefreshAppActions", flags).Invoke(editor, new object[] { "spotify_next" });
            Application.DoEvents();
            using (Bitmap spotifyImage = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(spotifyImage, new Rectangle(0, 0, spotifyImage.Width, spotifyImage.Height));
                spotifyImage.Save("build\\editor-spotify.png", ImageFormat.Png);
            }
        }
        using (AppPicker picker = new AppPicker(true, true))
        using (Bitmap image = new Bitmap(picker.Width, picker.Height))
        {
            picker.StartPosition = FormStartPosition.Manual;
            picker.Location = new Point(-10000, -10000);
            picker.Show();
            for (int i = 0; i < 80; ++i) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
            picker.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save("build\\picker.png", ImageFormat.Png);
        }
    }
}
