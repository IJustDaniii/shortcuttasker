using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AtajosLibres
{
    public sealed class AppPicker : Form
    {
        private List<InstalledApp> installed;
        private readonly List<InstalledApp> running;
        private readonly TextBox search;
        private readonly TabControl tabs;
        private readonly ListView installedList;
        private readonly ListView runningList;
        private readonly bool requireProcess;
        private readonly Label info;
        public InstalledApp Selection { get; private set; }

        public void SelectRunningTab()
        {
            if (tabs.TabPages.Count > 1) tabs.SelectedIndex = 1;
        }

        public AppPicker(bool includeRunning, bool requireProcess)
        {
            this.requireProcess = requireProcess;
            installed = new List<InstalledApp>();
            running = includeRunning ? RunningApps.Discover() : new List<InstalledApp>();
            Text = "Elegir aplicación";
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            ForeColor = Ui.Ink;
            ClientSize = new Size(760, 525);
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;

            Label heading = new Label { Text = "Busca una aplicación", Left = 24, Top = 20, AutoSize = true,
                Font = new Font("Segoe UI Semibold", 15F) };
            Controls.Add(heading);
            search = new TextBox { Left = 24, Top = 63, Width = 712, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            search.TextChanged += delegate { RefreshLists(); };
            Controls.Add(search);

            tabs = new TabControl { Left = 24, Top = 106, Width = 712, Height = 344,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            TabPage installedTab = new TabPage("Instaladas");
            installedList = MakeList();
            installedTab.Controls.Add(installedList);
            tabs.TabPages.Add(installedTab);
            if (includeRunning)
            {
                TabPage runningTab = new TabPage("Ventanas abiertas");
                runningList = MakeList();
                runningTab.Controls.Add(runningList);
                tabs.TabPages.Add(runningTab);
            }
            Controls.Add(tabs);

            info = new Label { Text = "Detectando aplicaciones instaladas...",
                Left = 24, Top = 459, Width = 500, Height = 36, ForeColor = Ui.Muted,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            Controls.Add(info);
            Button choose = Ui.Button("Elegir", 105); Ui.Primary(choose);
            choose.Left = 514; choose.Top = 468; choose.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            choose.Click += Choose;
            Button cancel = Ui.Button("Cancelar", 105);
            cancel.Left = 631; cancel.Top = 468; cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(choose); Controls.Add(cancel);
            AcceptButton = choose; CancelButton = cancel;
            RefreshLists();
            Shown += delegate { LoadInstalledAsync(); };
        }

        private void LoadInstalledAsync()
        {
            Thread worker = new Thread(delegate()
            {
                try
                {
                    List<InstalledApp> found = InstalledApps.Discover();
                    if (!IsDisposed && IsHandleCreated)
                        try { BeginInvoke((MethodInvoker)delegate
                        {
                            installed = found;
                            info.Text = requireProcess
                                ? "Para enviar teclas, la app debe tener un proceso reconocible."
                                : "Se guardará la aplicación seleccionada en el atajo.";
                            RefreshLists();
                        }); }
                        catch (InvalidOperationException) { }
                }
                catch (Exception ex)
                {
                    if (!IsDisposed && IsHandleCreated)
                        try { BeginInvoke((MethodInvoker)delegate
                        {
                            info.Text = "No se pudo leer el catálogo de aplicaciones.";
                            MessageBox.Show(this, ex.Message, "Aplicaciones instaladas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }); }
                        catch (InvalidOperationException) { }
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private ListView MakeList()
        {
            ListView view = new ListView { Dock = DockStyle.Fill, View = View.Details,
                FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None };
            view.Columns.Add("Aplicación", 220);
            view.Columns.Add("Tipo", 80);
            view.Columns.Add("Proceso", 130);
            view.Columns.Add("Destino / ventana", 260);
            view.DoubleClick += Choose;
            return view;
        }

        private void RefreshLists()
        {
            Populate(installedList, installed);
            if (runningList != null) Populate(runningList, running);
        }

        private void Populate(ListView view, List<InstalledApp> source)
        {
            string query = search.Text.Trim();
            view.BeginUpdate();
            view.Items.Clear();
            foreach (InstalledApp app in source)
            {
                if (query.Length > 0 && app.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    app.ProcessName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                ListViewItem row = new ListViewItem(app.Name);
                row.SubItems.Add(app.Kind);
                row.SubItems.Add(app.ProcessName.Length == 0 ? "Por detectar" : app.ProcessName);
                row.SubItems.Add(app.WindowTitle ?? app.LaunchTarget);
                row.Tag = app;
                view.Items.Add(row);
            }
            view.EndUpdate();
        }

        private void Choose(object sender, EventArgs e)
        {
            ListView view = tabs.SelectedIndex == 0 ? installedList : runningList;
            if (view == null || view.SelectedItems.Count == 0) return;
            InstalledApp app = (InstalledApp)view.SelectedItems[0].Tag;
            if (requireProcess && app.ProcessName.Length == 0)
            {
                try
                {
                    Cursor = Cursors.WaitCursor;
                    app.ProcessName = InstalledApps.ResolvePackagedProcess(app.LaunchTarget);
                }
                finally { Cursor = Cursors.Default; }
                if (app.ProcessName.Length == 0)
                {
                    MessageBox.Show(this, "Windows no indicó el proceso de esta aplicación. Ábrela y elige su ventana en «Ventanas abiertas».",
                        "Proceso no detectado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            Selection = app;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
