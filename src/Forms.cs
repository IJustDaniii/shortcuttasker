using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AtajosLibres
{
    internal static class Ui
    {
        internal static readonly Color Ink = Color.FromArgb(30, 41, 59);
        internal static readonly Color Muted = Color.FromArgb(100, 116, 139);
        internal static readonly Color Blue = Color.FromArgb(37, 99, 235);
        internal static Button Button(string text, int width)
        {
            return new Button { Text = text, Width = width, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, ForeColor = Ink, Margin = new Padding(0, 0, 8, 0) };
        }
        internal static void Primary(Button button)
        {
            button.BackColor = Blue;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
        }
    }

    public class LauncherForm : Form
    {
        private ShortcutConfig config;
        private KeyboardHook hook;
        private ListView list;
        private Label status;
        private NotifyIcon tray;
        private bool quitting;
        private bool paused;
        private bool firstHideNotice;
        private UpdateManager updates;
        public bool StartHidden;

        public LauncherForm()
        {
            Text = "ShortcutTasker";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(248, 250, 252);
            ForeColor = Ui.Ink;
            MinimumSize = new Size(950, 470);
            Size = new Size(1050, 580);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            config = ConfigStore.Load();
            RefreshList();

            tray = new NotifyIcon { Icon = Icon, Text = "ShortcutTasker: activo", Visible = true };
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir configuración", null, delegate { RestoreWindow(); });
            trayMenu.Items.Add("Pausar atajos", null, delegate { TogglePause(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Salir", null, delegate { quitting = true; Close(); });
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { RestoreWindow(); };

            hook = new KeyboardHook();
            hook.SetBindings(config.Shortcuts);
            hook.Triggered += OnTriggered;
            hook.Start();
            updates = new UpdateManager(this);
            Shown += delegate { if (StartHidden) Hide(); updates.CheckOnStartup(); };
            FormClosing += OnClosing;
            FormClosed += delegate { hook.Dispose(); tray.Visible = false; tray.Dispose(); };
        }

        private void BuildUi()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.White, Padding = new Padding(24, 18, 24, 14) };
            Label heading = new Label { Text = "Tus atajos", Font = new Font("Segoe UI Semibold", 19F),
                ForeColor = Ui.Ink, Location = new Point(24, 16), AutoSize = true };
            Label subtitle = new Label { Text = "Abre apps, escribe texto y controla tu equipo desde el teclado.",
                ForeColor = Ui.Muted, Location = new Point(26, 57), AutoSize = true };
            header.Controls.Add(heading);
            header.Controls.Add(subtitle);
            Controls.Add(header);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.White, Padding = new Padding(24, 16, 24, 8) };
            status = new Label { Text = "Activo en segundo plano · Se inicia con Windows", Dock = DockStyle.Fill, ForeColor = Ui.Muted };
            footer.Controls.Add(status);
            Controls.Add(footer);

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 16) };
            FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 47, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            Button add = Ui.Button("+ Nuevo atajo", 132); Ui.Primary(add); add.Click += delegate { EditShortcut(null); };
            Button edit = Ui.Button("Editar", 85); edit.Click += delegate { if (Selected != null) EditShortcut(Selected); };
            Button toggle = Ui.Button("Activar / desactivar", 156); toggle.Click += delegate { ToggleSelected(); };
            Button remove = Ui.Button("Eliminar", 88); remove.Click += delegate { RemoveSelected(); };
            Button pause = Ui.Button("Pausar todos", 106); pause.Click += delegate { TogglePause(); pause.Text = paused ? "Reanudar" : "Pausar todos"; };
            Button update = Ui.Button("Buscar actualizaciones", 158); update.Click += delegate { updates.CheckManually(); };
            toolbar.Controls.AddRange(new Control[] { add, edit, toggle, remove, pause, update });

            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
                GridLines = false, HideSelection = false, MultiSelect = false, BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5F) };
            list.Columns.Add("Atajo", 145);
            list.Columns.Add("Nombre", 160);
            list.Columns.Add("Acción", 120);
            list.Columns.Add("Destino", 300);
            list.Columns.Add("Estado", 90);
            list.Resize += delegate { list.Columns[3].Width = Math.Max(120, list.ClientSize.Width - 530); };
            list.DoubleClick += delegate { if (Selected != null) EditShortcut(Selected); };
            list.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            body.Controls.Add(list);
            body.Controls.Add(toolbar);
            Controls.Add(body);
            body.BringToFront();
        }

        private Shortcut Selected { get { return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as Shortcut; } }

        private void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (Shortcut shortcut in config.Shortcuts)
            {
                ListViewItem item = new ListViewItem(ShortcutNames.Display(shortcut));
                item.SubItems.Add(shortcut.Name);
                item.SubItems.Add(ShortcutNames.ActionDisplay(shortcut.Action));
                item.SubItems.Add(!string.IsNullOrEmpty(shortcut.AppDisplayName)
                    ? shortcut.AppDisplayName : shortcut.Target);
                item.SubItems.Add(shortcut.Enabled ? "Activo" : "Pausado");
                item.Tag = shortcut;
                if (!shortcut.Enabled) item.ForeColor = Ui.Muted;
                list.Items.Add(item);
            }
            list.EndUpdate();
        }

        private void SaveChanges()
        {
            try { ConfigStore.Save(config); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudieron guardar los cambios: " + ex.Message,
                    "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { config = ConfigStore.Load(); } catch { }
                RefreshList();
                return;
            }
            hook.SetBindings(paused ? new List<Shortcut>() : config.Shortcuts);
            RefreshList();
            status.Text = paused ? "Atajos pausados" : "Activo en segundo plano · Se inicia con Windows";
        }

        private void EditShortcut(Shortcut existing)
        {
            if (existing != null && existing.Action == "discord_person")
            {
                MessageBox.Show(this, "Esta acción antigua ya no se puede configurar. Elimina el atajo y crea uno de los dos controles de Discord disponibles.",
                    "Acción retirada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (existing != null && existing.Action == "appkey" &&
                AppActionCatalog.AppId(existing.AppDisplayName, existing.AppProcess, existing.AppLaunchTarget).Length > 0)
            {
                MessageBox.Show(this, "Esta combinación personalizada sigue funcionando, pero las nuevas reglas de Discord y Spotify solo ofrecen sus controles propios. Crea un atajo nuevo para escoger uno de ellos.",
                    "Acción anterior", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (ShortcutEditor editor = new ShortcutEditor(existing))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                Shortcut candidate = editor.Result;
                foreach (Shortcut shortcut in config.Shortcuts)
                {
                    if (shortcut != existing && shortcut.Enabled && candidate.Enabled &&
                        shortcut.Key == candidate.Key && shortcut.Modifiers == candidate.Modifiers)
                    {
                        MessageBox.Show(this, "Ese atajo ya está asignado a «" + shortcut.Name + "».", "Atajo duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                if (existing != null) config.Shortcuts[config.Shortcuts.IndexOf(existing)] = candidate;
                else config.Shortcuts.Add(candidate);
                SaveChanges();
            }
        }

        private void ToggleSelected()
        {
            Shortcut selected = Selected;
            if (selected == null) return;
            if (!selected.Enabled)
            {
                foreach (Shortcut other in config.Shortcuts)
                    if (other != selected && other.Enabled && other.Key == selected.Key && other.Modifiers == selected.Modifiers)
                    {
                        MessageBox.Show(this, "Ese atajo ya está activo en «" + other.Name + "».", "Atajo duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
            }
            selected.Enabled = !selected.Enabled;
            SaveChanges();
        }

        private void RemoveSelected()
        {
            Shortcut selected = Selected;
            if (selected == null) return;
            if (MessageBox.Show(this, "¿Eliminar «" + selected.Name + "»?", "Eliminar atajo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            config.Shortcuts.Remove(selected);
            SaveChanges();
        }

        private void TogglePause()
        {
            paused = !paused;
            hook.SetBindings(paused ? new List<Shortcut>() : config.Shortcuts);
            status.Text = paused ? "Atajos pausados" : "Activo en segundo plano · Se inicia con Windows";
            tray.Text = paused ? "ShortcutTasker: pausado" : "ShortcutTasker: activo";
            if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Items[1].Text = paused ? "Reanudar atajos" : "Pausar atajos";
        }

        private void OnTriggered(Shortcut shortcut)
        {
            try { ActionRunner.Run(shortcut); }
            catch (Exception ex)
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke((MethodInvoker)delegate
                        {
                            status.Text = "Error en «" + shortcut.Name + "»: " + ex.Message;
                            tray.ShowBalloonTip(3500, "Atajo no ejecutado", shortcut.Name + ": " + ex.Message, ToolTipIcon.Warning);
                        });
                }
                catch (InvalidOperationException) { }
            }
        }

        public void RestoreWindow()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            if (updates != null) updates.CheckOnStartup();
        }

        public void ExitForUpdate()
        {
            quitting = true;
            Close();
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (quitting) return;
            e.Cancel = true;
            Hide();
            if (!firstHideNotice)
            {
                firstHideNotice = true;
                tray.ShowBalloonTip(2500, "Atajos Libres sigue activo", "Abre la configuración desde el icono junto al reloj o desde Inicio.", ToolTipIcon.Info);
            }
        }
    }

    internal sealed class KeyOption
    {
        public readonly string Label;
        public readonly int Code;
        public KeyOption(string label, int code) { Label = label; Code = code; }
        public override string ToString() { return Label; }
    }

    internal static class ShortcutNames
    {
        public static string Display(Shortcut shortcut)
        {
            List<string> parts = new List<string>();
            if ((shortcut.Modifiers & Modifiers.Win) != 0) parts.Add("Win");
            if ((shortcut.Modifiers & Modifiers.Ctrl) != 0) parts.Add("Ctrl");
            if ((shortcut.Modifiers & Modifiers.Alt) != 0) parts.Add("Alt");
            if ((shortcut.Modifiers & Modifiers.Shift) != 0) parts.Add("Mayús");
            parts.Add(KeyName(shortcut.Key));
            return string.Join(" + ", parts.ToArray());
        }

        public static string KeyName(int key)
        {
            if (key >= 0x41 && key <= 0x5A) return ((char)key).ToString();
            if (key >= 0x30 && key <= 0x39) return ((char)key).ToString();
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x6F);
            if (key >= 0x60 && key <= 0x69) return "Num " + (key - 0x60);
            Dictionary<int, string> names = new Dictionary<int, string> {
                {0x20,"Espacio"},{0x0D,"Intro"},{0x09,"Tab"},{0x1B,"Esc"},
                {0x25,"←"},{0x26,"↑"},{0x27,"→"},{0x28,"↓"},
                {0x2D,"Insert"},{0x2E,"Supr"},{0x24,"Inicio"},{0x23,"Fin"},
                {0x21,"Re Pág"},{0x22,"Av Pág"}
            };
            return names.ContainsKey(key) ? names[key] : "VK " + key;
        }

        public static string ActionDisplay(string action)
        {
            if (action == "open") return "Abrir";
            if (action == "web") return "Abrir web";
            if (action == "text") return "Escribir texto";
            if (action == "command") return "Comando";
            if (action == "media") return "Multimedia";
            if (action == "appkey") return "Acción en app";
            if (action == "discord_mute") return "Discord: micrófono";
            if (action == "discord_deafen") return "Discord: audio";
            if (action == "spotify_playpause") return "Spotify: reproducir";
            if (action == "spotify_next") return "Spotify: siguiente";
            if (action == "spotify_previous") return "Spotify: anterior";
            if (action == "discord_person") return "Discord: acción antigua";
            return action;
        }
    }

    public class ShortcutEditor : Form
    {
        private TextBox nameBox, targetBox, processBox;
        private CheckBox win, ctrl, alt, shift;
        private CheckBox appWin, appCtrl, appAlt, appShift;
        private ComboBox keyBox, actionBox, mediaBox, appActionBox, appKeyBox;
        private Label targetLabel, processLabel, appActionLabel, appKeyLabel, appModLabel, note;
        private Button browse, installedButton, runningButton, saveButton, cancelButton;
        private string selectedAppLaunchTarget = "", selectedAppName = "", selectedWindowTitle = "";
        private bool changingAppSelection;
        private bool originalEnabled;
        public Shortcut Result { get; private set; }

        public ShortcutEditor(Shortcut existing)
        {
            Text = existing == null ? "Nuevo atajo" : "Editar atajo";
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            ForeColor = Ui.Ink;
            ClientSize = new Size(530, 630);
            MinimumSize = new Size(545, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            originalEnabled = existing == null || existing.Enabled;
            if (existing != null) Fill(existing);
        }

        private Label LabelAt(string text, int y)
        {
            Label label = new Label { Text = text, Left = 24, Top = y, Width = 470, Height = 21,
                Font = new Font("Segoe UI Semibold", 9F) };
            Controls.Add(label);
            return label;
        }

        private void BuildUi()
        {
            LabelAt("Nombre", 20);
            nameBox = new TextBox { Left = 24, Top = 44, Width = 480 };
            Controls.Add(nameBox);

            LabelAt("Combinación de teclas", 86).Width = 275;
            Label keyLabel = new Label { Text = "Tecla", Left = 312, Top = 86, Width = 192,
                Height = 21, Font = new Font("Segoe UI Semibold", 9F) };
            Controls.Add(keyLabel);
            FlowLayoutPanel mods = new FlowLayoutPanel { Left = 24, Top = 112, Width = 275, Height = 35 };
            win = new CheckBox { Text = "Win", Width = 52, Margin = new Padding(0, 0, 4, 0) };
            ctrl = new CheckBox { Text = "Ctrl", Width = 52, Margin = new Padding(0, 0, 4, 0) };
            alt = new CheckBox { Text = "Alt", Width = 50, Margin = new Padding(0, 0, 4, 0) };
            shift = new CheckBox { Text = "Mayús", Width = 71, Margin = new Padding(0, 0, 4, 0) };
            mods.Controls.AddRange(new Control[] { win, ctrl, alt, shift });
            Controls.Add(mods);
            keyBox = new ComboBox { Left = 312, Top = 110, Width = 192, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int key = 0x41; key <= 0x5A; ++key) keyBox.Items.Add(new KeyOption(((char)key).ToString(), key));
            for (int key = 0x30; key <= 0x39; ++key) keyBox.Items.Add(new KeyOption(((char)key).ToString(), key));
            for (int key = 0x70; key <= 0x87; ++key) keyBox.Items.Add(new KeyOption("F" + (key - 0x6F), key));
            for (int key = 0x60; key <= 0x69; ++key) keyBox.Items.Add(new KeyOption("Num " + (key - 0x60), key));
            foreach (int key in new int[] { 0x20, 0x0D, 0x09, 0x1B, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22 })
                keyBox.Items.Add(new KeyOption(ShortcutNames.KeyName(key), key));
            keyBox.SelectedIndex = 2;
            Controls.Add(keyBox);

            LabelAt("Acción", 162);
            actionBox = new ComboBox { Left = 24, Top = 188, Width = 480, DropDownStyle = ComboBoxStyle.DropDownList };
            actionBox.Items.AddRange(new object[] { "Aplicación, archivo o carpeta", "Abrir página web", "Escribir texto", "Ejecutar comando", "Control multimedia" });
            actionBox.SelectedIndex = 0;
            actionBox.SelectedIndexChanged += delegate { UpdateTargetUi(); };
            Controls.Add(actionBox);

            targetLabel = LabelAt("Aplicación, archivo o carpeta", 232);
            targetBox = new TextBox { Left = 24, Top = 258, Width = 280 };
            targetBox.TextChanged += delegate
            {
                if (!changingAppSelection)
                {
                    selectedAppLaunchTarget = ""; selectedAppName = ""; selectedWindowTitle = "";
                    if (processBox != null) processBox.Text = "";
                }
                RefreshAppActions(null);
            };
            Controls.Add(targetBox);
            browse = Ui.Button("Buscar…", 80);
            browse.Left = 312; browse.Top = 256;
            browse.Click += Browse;
            Controls.Add(browse);
            installedButton = Ui.Button("Instaladas…", 104);
            installedButton.Left = 400; installedButton.Top = 256;
            installedButton.Click += SelectInstalledApp;
            Controls.Add(installedButton);
            mediaBox = new ComboBox { Left = 24, Top = 258, Width = 480, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            mediaBox.Items.AddRange(new object[] { "Reproducir / pausar", "Siguiente pista", "Pista anterior", "Subir volumen", "Bajar volumen", "Silenciar" });
            mediaBox.SelectedIndex = 0;
            Controls.Add(mediaBox);

            appActionLabel = LabelAt("Qué hacer con esta aplicación", 310);
            appActionBox = new ComboBox { Left = 24, Top = 334, Width = 480, DropDownStyle = ComboBoxStyle.DropDownList };
            appActionBox.SelectedIndexChanged += delegate { UpdateTargetUi(); };
            Controls.Add(appActionBox);

            processLabel = LabelAt("Proceso de la aplicación", 377);
            processBox = new TextBox { Left = 24, Top = 401, Width = 392 };
            processBox.TextChanged += delegate { if (!changingAppSelection) selectedWindowTitle = ""; };
            Controls.Add(processBox);
            runningButton = Ui.Button("Ventana…", 80);
            runningButton.Left = 424; runningButton.Top = 399;
            runningButton.Click += SelectRunningApp;
            Controls.Add(runningButton);
            appModLabel = LabelAt("Teclas que recibe la aplicación", 444);
            FlowLayoutPanel appMods = new FlowLayoutPanel { Left = 24, Top = 468, Width = 277, Height = 35 };
            appWin = new CheckBox { Text = "Win", Width = 52 }; appCtrl = new CheckBox { Text = "Ctrl", Width = 54 };
            appAlt = new CheckBox { Text = "Alt", Width = 51 }; appShift = new CheckBox { Text = "Mayús", Width = 72 };
            appMods.Controls.AddRange(new Control[] { appWin, appCtrl, appAlt, appShift }); Controls.Add(appMods);
            appKeyLabel = new Label { Text = "Tecla", Left = 312, Top = 444, Width = 192, Height = 21, Font = new Font("Segoe UI Semibold", 9F) };
            Controls.Add(appKeyLabel);
            appKeyBox = new ComboBox { Left = 312, Top = 468, Width = 192, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (KeyOption option in keyBox.Items) appKeyBox.Items.Add(new KeyOption(option.Label, option.Code));
            appKeyBox.SelectedIndex = 0; Controls.Add(appKeyBox);

            note = new Label { Text = "El atajo se ejecuta cuando sueltas las teclas modificadoras.",
                 Left = 24, Top = 530, Width = 480, Height = 34, ForeColor = Ui.Muted };
            Controls.Add(note);

            saveButton = Ui.Button("Guardar atajo", 122); Ui.Primary(saveButton);
            saveButton.Left = 270; saveButton.Top = 575; saveButton.Click += Save;
            cancelButton = Ui.Button("Cancelar", 105);
            cancelButton.Left = 400; cancelButton.Top = 575; cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(saveButton); Controls.Add(cancelButton);
            AcceptButton = saveButton; CancelButton = cancelButton;
            UpdateTargetUi();
        }

        private void UpdateTargetUi()
        {
            int action = actionBox.SelectedIndex;
            targetLabel.Text = action == 0 ? "Aplicación, archivo o carpeta" : action == 1 ? "Dirección web" :
                action == 2 ? "Texto que se escribirá" : action == 3 ? "Comando de Windows" : "Control";
            targetLabel.Visible = action < 4;
            targetBox.Visible = action < 4;
            targetBox.Multiline = action == 2;
            targetBox.Height = action == 2 ? 70 : 27;
            browse.Visible = action == 0;
            installedButton.Visible = action == 0;
            targetBox.Width = action == 0 ? 280 : 480;
            mediaBox.Visible = action == 4;
            bool hasApp = action == 0 && selectedAppName.Length > 0;
            appActionLabel.Visible = appActionBox.Visible = hasApp;
            AppActionOption selected = appActionBox.SelectedItem as AppActionOption;
            bool sendKeys = hasApp && selected != null && selected.Action == "appkey";
            processLabel.Visible = processBox.Visible = runningButton.Visible = sendKeys;
            appModLabel.Visible = appKeyLabel.Visible = appKeyBox.Visible = sendKeys;
            appWin.Visible = appCtrl.Visible = appAlt.Visible = appShift.Visible = sendKeys;
            int noteTop = sendKeys ? 530 : hasApp ? 405 : action == 2 ? 350 : 310;
            note.Top = noteTop;
            saveButton.Top = cancelButton.Top = noteTop + 45;
            int height = noteTop + 100;
            if (ClientSize.Height != height)
            {
                ClientSize = new Size(ClientSize.Width, height);
                if (Visible) CenterToParent();
            }
            if (action == 0) targetBox.PlaceholderTextCompat("C:\\Ruta\\Programa.exe o shell:AppsFolder\\...");
            else if (action == 1) targetBox.PlaceholderTextCompat("https://ejemplo.com");
            else targetBox.PlaceholderTextCompat("");
        }

        private void RefreshAppActions(string preferredAction)
        {
            if (appActionBox == null) return;
            appActionBox.BeginUpdate();
            appActionBox.Items.Clear();
            if (selectedAppName.Length > 0)
            {
                foreach (AppActionOption option in AppActionCatalog.ForApp(selectedAppName, processBox.Text.Trim(), selectedAppLaunchTarget))
                    appActionBox.Items.Add(option);
                int choice = 0;
                for (int i = 0; i < appActionBox.Items.Count; ++i)
                    if (((AppActionOption)appActionBox.Items[i]).Action == preferredAction) { choice = i; break; }
                appActionBox.SelectedIndex = choice;
            }
            appActionBox.EndUpdate();
            UpdateTargetUi();
        }

        private void Browse(object sender, EventArgs e)
        {
            DialogResult choice = MessageBox.Show(this, "¿Quieres elegir una carpeta?\n\nSí: carpeta · No: aplicación o archivo", "Buscar destino", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                    if (dialog.ShowDialog(this) == DialogResult.OK) targetBox.Text = dialog.SelectedPath;
            }
            else if (choice == DialogResult.No)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Todos los archivos|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK) targetBox.Text = dialog.FileName;
                }
            }
        }

        private void SelectInstalledApp(object sender, EventArgs e)
        {
            SelectApp(false);
        }

        private void SelectRunningApp(object sender, EventArgs e)
        {
            SelectApp(true);
        }

        private void SelectApp(bool running)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                using (AppPicker picker = new AppPicker(true, false))
                {
                    Cursor = Cursors.Default;
                    if (running) picker.SelectRunningTab();
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    InstalledApp app = picker.Selection;
                    changingAppSelection = true;
                    try
                    {
                        targetBox.Text = app.Name;
                        processBox.Text = app.ProcessName;
                        selectedAppLaunchTarget = app.LaunchTarget;
                        selectedAppName = app.Name;
                        selectedWindowTitle = app.WindowTitle ?? "";
                    }
                    finally { changingAppSelection = false; }
                    RefreshAppActions("open");
                }
            }
            catch (Exception ex) { Warn("No se pudo leer el catálogo de aplicaciones: " + ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        private void Fill(Shortcut shortcut)
        {
            nameBox.Text = shortcut.Name;
            win.Checked = (shortcut.Modifiers & Modifiers.Win) != 0;
            ctrl.Checked = (shortcut.Modifiers & Modifiers.Ctrl) != 0;
            alt.Checked = (shortcut.Modifiers & Modifiers.Alt) != 0;
            shift.Checked = (shortcut.Modifiers & Modifiers.Shift) != 0;
            for (int i = 0; i < keyBox.Items.Count; ++i)
                if (((KeyOption)keyBox.Items[i]).Code == shortcut.Key) { keyBox.SelectedIndex = i; break; }
            actionBox.SelectedIndex = shortcut.Action == "web" ? 1 : shortcut.Action == "text" ? 2 :
                shortcut.Action == "command" ? 3 : shortcut.Action == "media" ? 4 : 0;
            if (shortcut.Action == "media") mediaBox.SelectedItem = shortcut.Target;
            else
            {
                changingAppSelection = true;
                targetBox.Text = !string.IsNullOrEmpty(shortcut.AppDisplayName)
                    ? shortcut.AppDisplayName : shortcut.Target;
                changingAppSelection = false;
            }
            processBox.Text = shortcut.AppProcess ?? "";
            selectedAppLaunchTarget = shortcut.AppLaunchTarget ?? "";
            selectedAppName = shortcut.AppDisplayName ?? "";
            if (selectedAppName.Length == 0 && (shortcut.Action == "discord_mute" || shortcut.Action == "discord_deafen")) selectedAppName = "Discord";
            if (selectedAppName.Length == 0 && shortcut.Action.StartsWith("spotify_", StringComparison.Ordinal)) selectedAppName = "Spotify";
            selectedWindowTitle = shortcut.AppWindowTitle ?? "";
            appWin.Checked = (shortcut.AppModifiers & Modifiers.Win) != 0;
            appCtrl.Checked = (shortcut.AppModifiers & Modifiers.Ctrl) != 0;
            appAlt.Checked = (shortcut.AppModifiers & Modifiers.Alt) != 0;
            appShift.Checked = (shortcut.AppModifiers & Modifiers.Shift) != 0;
            for (int i = 0; i < appKeyBox.Items.Count; ++i)
                if (((KeyOption)appKeyBox.Items[i]).Code == shortcut.AppKey) { appKeyBox.SelectedIndex = i; break; }
            RefreshAppActions(shortcut.Action);
        }

        private void Save(object sender, EventArgs e)
        {
            Modifiers mods = Modifiers.None;
            if (win.Checked) mods |= Modifiers.Win;
            if (ctrl.Checked) mods |= Modifiers.Ctrl;
            if (alt.Checked) mods |= Modifiers.Alt;
            if (shift.Checked) mods |= Modifiers.Shift;
            if (mods == Modifiers.None) { Warn("Elige al menos una tecla modificadora."); return; }
            int key = ((KeyOption)keyBox.SelectedItem).Code;
            if ((mods & Modifiers.Win) != 0 && key == 0x4C) { Warn("Win+L está reservado por Windows."); return; }
            if ((mods & (Modifiers.Ctrl | Modifiers.Alt)) == (Modifiers.Ctrl | Modifiers.Alt) && key == 0x2E)
            { Warn("Ctrl+Alt+Supr está reservado por Windows."); return; }
            string name = nameBox.Text.Trim();
            if (name.Length == 0) { Warn("Pon un nombre al atajo."); return; }
            string target = actionBox.SelectedIndex == 4 ? Convert.ToString(mediaBox.SelectedItem) :
                actionBox.SelectedIndex == 2 ? targetBox.Text : targetBox.Text.Trim();
            AppActionOption selectedAction = appActionBox.SelectedItem as AppActionOption;
            string appAction = actionBox.SelectedIndex == 0 && selectedAppName.Length > 0 && selectedAction != null
                ? selectedAction.Action : "open";
            if (appAction == "open" && selectedAppLaunchTarget.Length > 0) target = selectedAppLaunchTarget;
            if (string.IsNullOrEmpty(target) && actionBox.SelectedIndex != 0) { Warn("Indica qué debe hacer el atajo."); return; }
            if (actionBox.SelectedIndex == 0 && string.IsNullOrEmpty(target) && appAction != "discord_mute" && appAction != "discord_deafen")
            { Warn("Elige una aplicación o indica su ruta."); return; }
            if (actionBox.SelectedIndex == 1)
            {
                Uri uri;
                if (!Uri.TryCreate(target, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                { Warn("La web debe empezar por https:// o http://."); return; }
            }
            string process;
            string processInput = processBox.Text.Trim();
            if (appAction == "appkey" && processInput.Length == 0 && selectedAppName.Length == 0) processInput = target;
            try { process = AppAutomation.NormalizeProcessName(processInput); }
            catch (ArgumentException ex) { Warn(ex.Message); return; }
            if (appAction == "appkey" && process.Length == 0)
            { Warn("No se detectó el proceso. Abre la aplicación y elige su ventana."); return; }
            Modifiers appMods = Modifiers.None;
            if (appWin.Checked) appMods |= Modifiers.Win;
            if (appCtrl.Checked) appMods |= Modifiers.Ctrl;
            if (appAlt.Checked) appMods |= Modifiers.Alt;
            if (appShift.Checked) appMods |= Modifiers.Shift;
            string[] actions = { appAction, "web", "text", "command", "media" };
            Result = new Shortcut { Name = name, Modifiers = mods, Key = key,
                Action = actions[actionBox.SelectedIndex], Target = target, AppProcess = process,
                AppLaunchTarget = actionBox.SelectedIndex == 0 ? selectedAppLaunchTarget : "",
                AppDisplayName = actionBox.SelectedIndex == 0 ? selectedAppName : "",
                AppWindowTitle = actionBox.SelectedIndex == 0 ? selectedWindowTitle : "",
                AppModifiers = appMods, AppKey = ((KeyOption)appKeyBox.SelectedItem).Code, Enabled = originalEnabled };
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string text) { MessageBox.Show(this, text, "Revisa el atajo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal static class TextBoxHint
    {
        public static void PlaceholderTextCompat(this TextBox box, string text)
        {
            // The .NET Framework WinForms TextBox has no native placeholder property.
            box.AccessibleDescription = text;
        }
    }
}
