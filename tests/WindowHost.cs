using System;
using System.Drawing;
using System.Windows.Forms;

class WindowHost
{
    [STAThread]
    static void Main()
    {
        using (Form form = new Form { Text = "ShortcutTasker focus test", StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(200, 100) })
        {
            Application.Run(form);
        }
    }
}
