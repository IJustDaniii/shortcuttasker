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
            var action = (ComboBox)typeof(ShortcutEditor).GetField("actionBox", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(editor);
            action.SelectedIndex = 5;
            Application.DoEvents();
            editor.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save("build\\editor-app.png", ImageFormat.Png);
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
