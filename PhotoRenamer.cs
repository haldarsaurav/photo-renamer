using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FolderPhotoRenamer
{
    internal static class Program
    {
        [STAThread] private static void Main(string[] args)
        {
            var folders = new List<string>();
            for (int i = 0; i < args.Length; i++) if (args[i] == "--state-dir" && i + 1 < args.Length) Renamer.StateRoot = Path.GetFullPath(args[++i]); else folders.Add(args[i]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new RenamerForm(folders.ToArray())); }
            catch (Exception error) { MessageBox.Show(error.Message, "Photo Renamer could not open", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }
    internal sealed class QueueEntry
    {
        public string Folder, ObjectId, Error, PhotoError;
        public RenamePlan Plan;
        public override string ToString() { return Path.GetFileName(Folder) + (Error != null ? "  ·  needs attention" : ""); }
    }
    public sealed class RenamerForm : Form
    {
        private readonly List<QueueEntry> queue = new List<QueueEntry>();
        private readonly List<Tuple<RenamePlan, RenameItem>> photoRows = new List<Tuple<RenamePlan, RenameItem>>();
        private TextBox path, baseName;
        private ComboBox numberPosition;
        private NumericUpDown startNumber;
        private ListBox folders;
        private DataGridView photoGrid, folderGrid;
        private Label status, photoCount, folderCount, folderExample, queueCount;
        private DarkButton add, children, addPath, refresh, remove, clear, rename, undo, cancel;
        private DarkButton[] tabs;
        private Panel[] pages;
        private TableLayoutPanel workspace;
        private Panel selection;
        private ProgressBar progress;
        private bool busy;
        private int activePage;
        private CancellationTokenSource cancellation;
        private FolderRenamePlan folderPlan;
        private string folderError;

        public RenamerForm(string[] initial)
        {
            Text = "Photo Renamer"; Font = new Font(Theme.FontName, 10F); ForeColor = Theme.Text; BackColor = Theme.Background;
            ClientSize = new Size(1260, 890); MinimumSize = new Size(1120, 790); StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); DoubleBuffered = true;
            var root = Table(1, new float[] { 88, 54, -1, 8, 76, 22 }); root.Padding = new Padding(26, 20, 26, 16); Controls.Add(root);
            var header = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var logo = new PictureBox { Size = new Size(58, 58), Location = new Point(0, 8), SizeMode = PictureBoxSizeMode.Zoom };
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("PhotoRenamer.Icon.png")) if (stream != null) using (var source = new Bitmap(stream)) logo.Image = new Bitmap(source);
            header.Controls.Add(logo);
            var brand = Label("PHOTO RENAMER", 9, Theme.Accent, true); brand.Dock = DockStyle.None; brand.SetBounds(76, 3, 500, 21); header.Controls.Add(brand);
            var title = Label("Give every memory a name.", 24, Theme.Text, true); title.Dock = DockStyle.None; title.SetBounds(72, 23, 850, 46); header.Controls.Add(title);
            var offline = Label("PRIVATE  /  OFFLINE", 9, Theme.Mint, true); offline.Dock = DockStyle.Right; offline.Width = 175; offline.TextAlign = ContentAlignment.MiddleRight; header.Controls.Add(offline); root.Controls.Add(header, 0, 0);
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            tabs = new[] { Button("Photos", false, 150), Button("Folder names", false, 166), Button("Help & safety", false, 166) };
            for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].AccessibleRole = AccessibleRole.PageTab; tabs[i].Click += delegate { ShowPage(index); }; navigation.Controls.Add(tabs[i]); }
            root.Controls.Add(navigation, 0, 1);
            workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 302)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.Controls.Add(workspace, 0, 2);
            selection = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(15), Margin = new Padding(0) };
            var select = Table(1, new float[] { 31, 26, 45, 44, 42, -1, 44 }); selection.Controls.Add(select);
            select.Controls.Add(Label("YOUR FOLDERS", 9, Theme.Muted, true), 0, 0); queueCount = Label("Drop folders here, or add them below.", 9, Theme.Muted, false); select.Controls.Add(queueCount, 0, 1);
            add = Button("+  Add folders", true, 260); add.Dock = DockStyle.Fill; add.Click += delegate { PickFolders(false); }; select.Controls.Add(add, 0, 2);
            children = Button("Add immediate subfolders", false, 260); children.Dock = DockStyle.Fill; children.Click += delegate { PickFolders(true); }; select.Controls.Add(children, 0, 3);
            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) }; pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            path = Input("Folder path to add"); path.Margin = new Padding(0, 5, 6, 0); path.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AddPaths(new[] { path.Text }); } };
            addPath = Button("+", false, 42); addPath.Dock = DockStyle.Fill; addPath.AccessibleName = "Add path"; addPath.Click += async delegate { await AddPaths(new[] { path.Text }); }; pathRow.Controls.Add(path, 0, 0); pathRow.Controls.Add(addPath, 1, 0); select.Controls.Add(pathRow, 0, 4);
            folders = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 37, IntegralHeight = false, Margin = new Padding(0, 8, 0, 8), AccessibleName = "Folders in this batch" };
            folders.DrawItem += delegate(object sender, DrawItemEventArgs e) { if (e.Index < 0) return; using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Theme.Raised : Theme.Surface)) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, folders.Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), queue[e.Index].Error == null ? Theme.Text : Theme.Error, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix); };
            Theme.DarkControl(folders); select.Controls.Add(folders, 0, 5);
            var manage = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) }; refresh = Button("Refresh", false, 80); remove = Button("Remove", false, 80); clear = Button("Clear", false, 80);
            refresh.Click += async delegate { await RefreshQueue(); };
            remove.Click += async delegate { if (!busy && folders.SelectedIndex >= 0) { int selected = folders.SelectedIndex; await RefreshQueue(queue.Where((x, i) => i != selected).Select(x => x.Folder).ToArray()); } };
            clear.Click += async delegate { if (!busy) await RefreshQueue(new string[0]); }; manage.Controls.Add(refresh); manage.Controls.Add(remove); manage.Controls.Add(clear); select.Controls.Add(manage, 0, 6); workspace.Controls.Add(selection, 0, 0);
            var host = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; workspace.Controls.Add(host, 2, 0);
            pages = new[] { new Panel(), new Panel(), new Panel() }; foreach (var page in pages) { page.Dock = DockStyle.Fill; page.AccessibleRole = AccessibleRole.PropertyPage; host.Controls.Add(page); }
            BuildPhotoPage(); BuildFolderPage(); BuildHelpPage();
            progress = new ProgressBar { Dock = DockStyle.Fill, Visible = false, Margin = new Padding(0), Style = ProgressBarStyle.Continuous }; root.Controls.Add(progress, 0, 3);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 14, 0, 0) }; footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); foreach (float width in new float[] { 168, 164, 192 }) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            status = Label("Ready when you are. Add folders to begin.\nYour originals stay untouched until you confirm.", 9, Theme.Muted, false);
            cancel = Button("Cancel & restore", false, 160); cancel.Dock = DockStyle.Fill; cancel.Enabled = false; cancel.Click += delegate { if (cancellation != null) { cancellation.Cancel(); cancel.Enabled = false; status.Text = "Stopping and restoring the batch..."; } };
            undo = Button("Undo last batch", false, 156); undo.Dock = DockStyle.Fill; undo.Click += async delegate { await UndoOrRecover(); };
            rename = Button("Rename photos", true, 184); rename.Dock = DockStyle.Fill; rename.Click += async delegate { await Apply(); };
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(cancel, 1, 0); footer.Controls.Add(undo, 2, 0); footer.Controls.Add(rename, 3, 0); root.Controls.Add(footer, 0, 4);
            root.Controls.Add(Label("LOCAL FILES ONLY     •     NO UPLOADS     •     NO LOG FILES IN YOUR PHOTO FOLDERS", 8, Theme.Muted, false), 0, 5);
            WireDrop(this); ShowPage(0);
            Shown += async delegate { Theme.DarkWindow(this); if (initial.Length > 0) await AddPaths(initial); if (Renamer.HasPending) status.Text = "An interrupted batch needs recovery. Click Recover batch."; };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; if (cancellation != null) { cancellation.Cancel(); status.Text = "Restoring names before closing. Close again when finished."; } } };
        }
        private void BuildPhotoPage()
        {
            var layout = Table(1, new float[] { 45, 36, 61, 31, -1, 37 }); pages[0].Controls.Add(layout);
            layout.Controls.Add(Label("Make every photo belong.", 19, Theme.Text, true), 0, 0); layout.Controls.Add(Label("Use each folder’s name, with a number at the start or end.", 10, Theme.Muted, false), 0, 1);
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12), Margin = new Padding(0, 4, 0, 4), WrapContents = false };
            var label = Label("Number position", 9, Theme.Muted, true); label.Dock = DockStyle.None; label.Size = new Size(133, 27); settings.Controls.Add(label);
            numberPosition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 268, BackColor = Theme.Raised, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, AccessibleName = "Incremental number position" };
            numberPosition.Items.AddRange(new object[] { "End:  FolderName_1.jpg", "Start:  1_FolderName.jpg" }); numberPosition.SelectedIndex = 0;
            numberPosition.SelectedIndexChanged += delegate { foreach (var e in queue) if (e.Plan != null) Renamer.SetNumberPosition(e.Plan, SelectedPosition); RebuildPreview(); status.Text = "Photo preview updated for every folder."; status.ForeColor = Theme.Muted; }; settings.Controls.Add(numberPosition); layout.Controls.Add(settings, 0, 2);
            var previewHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; previewHeader.Controls.Add(Label("PHOTO PREVIEW", 9, Theme.Muted, true));
            layout.Controls.Add(previewHeader, 0, 3);
            photoGrid = Grid("Photo rename preview", new[] { "FOLDER", "CURRENT NAME", "NEW NAME" }, new[] { 26F, 30F, 44F });
            photoGrid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) { if (e.RowIndex < photoRows.Count) { var row = photoRows[e.RowIndex]; e.Value = e.ColumnIndex == 0 ? row.Item1.Prefix : e.ColumnIndex == 1 ? row.Item2.OriginalName : row.Item2.NewName; } }; layout.Controls.Add(photoGrid, 0, 4);
            photoCount = Label("No photos queued", 9, Theme.Muted, false); layout.Controls.Add(photoCount, 0, 5);
        }
        private void BuildFolderPage()
        {
            var layout = Table(1, new float[] { 45, 36, 112, 33, -1, 42 }); pages[1].Controls.Add(layout);
            layout.Controls.Add(Label("Name the whole collection.", 19, Theme.Text, true), 0, 0); layout.Controls.Add(Label("A shared name for your folders. Files inside keep their names.", 10, Theme.Muted, false), 0, 1);
            var settings = Table(2, new float[] { 28, 38, 35 }); settings.BackColor = Theme.Surface; settings.Padding = new Padding(12, 5, 12, 5); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            settings.Controls.Add(Label("SHARED FOLDER NAME", 8.5F, Theme.Muted, true), 0, 0); settings.Controls.Add(Label("START AT", 8.5F, Theme.Muted, true), 1, 0);
            baseName = Input("Shared folder name"); baseName.Text = "Photo collection"; baseName.Margin = new Padding(0, 3, 12, 0);
            startNumber = new NumericUpDown { Minimum = 1, Maximum = 1000000000, Value = 1, Dock = DockStyle.Top, BackColor = Theme.Raised, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Folder numbering starts at", Margin = new Padding(0, 3, 0, 0) };
            baseName.TextChanged += delegate { ReformatFolders(); }; startNumber.ValueChanged += delegate { ReformatFolders(); }; settings.Controls.Add(baseName, 0, 1); settings.Controls.Add(startNumber, 1, 1);
            folderExample = Label("_01, _02, _03 … _110, _1119   /   Numbered in displayed order", 8.5F, Theme.Mint, false); settings.Controls.Add(folderExample, 0, 2); settings.SetColumnSpan(folderExample, 2); layout.Controls.Add(settings, 0, 2);
            layout.Controls.Add(Label("FOLDER PREVIEW  /  Sorted by current full path", 9, Theme.Muted, true), 0, 3);
            folderGrid = Grid("Folder name preview", new[] { "CURRENT FOLDER", "NEW FOLDER NAME" }, new[] { 40F, 60F });
            folderGrid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) { if (folderPlan != null && e.RowIndex < folderPlan.Items.Count) { var item = folderPlan.Items[e.RowIndex]; e.Value = Path.GetFileName(e.ColumnIndex == 0 ? item.OriginalPath : item.NewPath); } };
            folderGrid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e) { if (folderPlan != null && e.RowIndex >= 0 && e.RowIndex < folderPlan.Items.Count) e.ToolTipText = folderPlan.Items[e.RowIndex].OriginalPath; }; layout.Controls.Add(folderGrid, 0, 4);
            folderCount = Label("Add folders to preview their new names", 9, Theme.Muted, false); layout.Controls.Add(folderCount, 0, 5);
        }
        private void BuildHelpPage()
        {
            var layout = Table(1, new float[] { 48, 37, -1 }); pages[2].Controls.Add(layout);
            layout.Controls.Add(Label("A little guidance. A lot of control.", 22, Theme.Text, true), 0, 0); layout.Controls.Add(Label("Everything you need to name your collection with confidence.", 11, Theme.Muted, false), 0, 1);
            var help = new RichTextBox { Dock = DockStyle.Fill, BackColor = Theme.Surface, ForeColor = Theme.Text, ReadOnly = true, BorderStyle = BorderStyle.None, Font = new Font(Theme.FontName, 11), DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical, Margin = new Padding(0, 8, 0, 0), AccessibleName = "Photo Renamer help and safety guide" };
            HelpSection(help, "01  Add your folders", "Use Add folders to select several folders with Ctrl or Shift. Drag folders into the app, paste a path, or use Add immediate subfolders to add a parent’s direct children. The same selection is shared by Photos and Folder names.");
            HelpSection(help, "02  Rename photos", "Each folder supplies its full name, including emoji. Choose End for FolderName_1.jpg or Start for 1_FolderName.jpg. Each folder restarts at 1. The preview updates instantly. Photos are ordered by current filename (IMG_2 before IMG_10), not capture date.");
            HelpSection(help, "03  Rename folder names", "Choose Folder names, enter one shared name, and review the sequence: Trip_01, Trip_02, … Trip_110, Trip_1119. Numbers have at least two digits and grow automatically. Start at lets you continue an existing sequence. Folders are numbered in the displayed order, sorted by their current full paths. Only the selected folder names change; their files and nested folders keep their names.");
            HelpSection(help, "04  Check, then apply", "Review the complete preview and click Rename photos or Rename folders. The final check identifies what will change. Existing files and folders are never overwritten or merged. Selecting both a parent and its nested folders is blocked in folder-name mode; choose one level at a time. Drive roots and folder links cannot be renamed.");
            HelpSection(help, "05  Undo, cancel and recovery", "Undo last batch restores the most recent successful batch, whether it renamed photos or folders, even after restarting the app. A new successful batch replaces the previous Undo. Cancel & restore rolls back the current batch. Closing while work is running requests cancellation; close again after restoration finishes. If a drive is disconnected or files are externally changed, restore access and click Recover batch. If original folder locations changed, restore those locations first.");
            HelpSection(help, "06  Formats and privacy", "JPG, HEIC, PNG, WebP, TIFF, DNG and common camera RAW extensions are supported. Photo mode skips nested folders, videos, documents, sidecars and photo symlinks. RAW/JPG/sidecar stems are not grouped. File contents and modification times are preserved. Nothing is uploaded. No JSON, history or lock files are placed in your photo folders. Undo and recovery live in %LOCALAPPDATA%\\Photo Renamer. Temporary names may appear while a batch runs; keep them if recovery is pending.");
            HelpSection(help, "07  Practical limits", "Undo uses the recorded paths and checks file size, modification time and directory identity. Keep backups of valuable collections: Undo is a filename history, not a backup of photo contents. Close applications that lock your selected folders. Keep the .exe.config beside the executable for long-path support; the drive and Windows must also support the path.");
            help.SelectionStart = 0; help.ScrollToCaret(); layout.Controls.Add(help, 0, 2); Theme.DarkControl(help);
        }
        private void HelpSection(RichTextBox box, string heading, string text)
        {
            box.SelectionFont = new Font(Theme.FontName, 13, FontStyle.Bold); box.SelectionColor = Theme.Mint; box.AppendText("\n  " + heading + "\n");
            box.SelectionFont = new Font(Theme.FontName, 10.5F); box.SelectionColor = Theme.Text; box.AppendText("  " + text + "\n\n");
        }
        private static TableLayoutPanel Table(int columns, float[] heights)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = heights.Length, Margin = new Padding(0) };
            foreach (float height in heights) table.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, height < 0 ? 100 : height)); return table;
        }
        private Label Label(string text, float size, Color color, bool bold) { return new Label { Text = text, Font = new Font(Theme.FontName, size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0), UseMnemonic = false, AutoEllipsis = true }; }
        private DarkButton Button(string text, bool primary, int width) { return new DarkButton { Text = text, Primary = primary, Width = width, Height = 41, Margin = new Padding(0, 0, 8, 4) }; }
        private TextBox Input(string name) { return new TextBox { Dock = DockStyle.Top, BackColor = Theme.Raised, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = name, Font = new Font(Theme.FontName, 10.5F) }; }
        private DataGridView Grid(string name, string[] headers, float[] weights)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Theme.Surface, BorderStyle = BorderStyle.None, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, VirtualMode = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Margin = new Padding(0), AccessibleName = name, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Theme.Border };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Raised, ForeColor = Theme.Muted, Font = new Font(Theme.FontName, 8.5F, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0), SelectionBackColor = Theme.Raised };
            g.ColumnHeadersHeight = 41; g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font(Theme.FontName, 9.5F), ForeColor = Theme.Text, BackColor = Theme.Surface, SelectionBackColor = Color.FromArgb(49, 43, 79), SelectionForeColor = Theme.Text, Padding = new Padding(10, 0, 0, 0) }; g.RowTemplate.Height = 38;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 33, 49);
            for (int i = 0; i < headers.Length; i++) g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = headers[i], FillWeight = weights[i], SortMode = DataGridViewColumnSortMode.NotSortable });
            g.Paint += delegate(object sender, PaintEventArgs e) { if (g.RowCount == 0) using (var emptyFont = new Font(Theme.FontName, 11)) TextRenderer.DrawText(e.Graphics, "Your new names will appear here.\nAdd folders to get started.", emptyFont, new Rectangle(15, 60, Math.Max(1, g.Width - 30), Math.Max(60, g.Height - 90)), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak); };
            Theme.DarkControl(g); return g;
        }
        private NumberPosition SelectedPosition { get { return numberPosition.SelectedIndex == 1 ? NumberPosition.Start : NumberPosition.End; } }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (!busy && (keys == Keys.F1 || keys == (Keys.Control | Keys.D3))) { ShowPage(2); return true; }
            if (!busy && keys == (Keys.Control | Keys.D1)) { ShowPage(0); return true; }
            if (!busy && keys == (Keys.Control | Keys.D2)) { ShowPage(1); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }
        private void ShowPage(int page)
        {
            if (busy) return; activePage = page;
            for (int i = 0; i < pages.Length; i++) { pages[i].Visible = i == page; tabs[i].Selected = i == page; tabs[i].Invalidate(); }
            selection.Visible = page != 2; workspace.ColumnStyles[0].Width = page == 2 ? 0 : 302; workspace.ColumnStyles[1].Width = page == 2 ? 0 : 20;
            UpdateButtons();
        }
        private async void PickFolders(bool childFolders)
        {
            if (busy) return;
            try { var picked = FolderPicker.Pick(this, childFolders ? "Choose parents: add their immediate subfolders" : "Select folders (Ctrl or Shift selects several)"); if (childFolders) picked = picked.SelectMany(Directory.GetDirectories).ToArray(); await AddPaths(picked); }
            catch (Exception error) { ShowError(error); }
        }
        private void WireDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += delegate(object sender, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) { var paths = (string[])e.Data.GetData(DataFormats.FileDrop); if (paths.Length > 0 && paths.All(Directory.Exists)) e.Effect = DragDropEffects.Copy; } };
            control.DragDrop += async delegate(object sender, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) await AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); };
            foreach (Control child in control.Controls) WireDrop(child);
        }
        private static List<QueueEntry> ReadEntries(IEnumerable<string> paths, NumberPosition position)
        {
            var result = new List<QueueEntry>(); var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in paths)
            {
                var e = new QueueEntry { Folder = value };
                try { var d = new DirectoryInfo(Path.GetFullPath(value.Trim().Trim('"'))); if (!d.Exists) throw new IOException("Folder not found."); e.Folder = d.FullName; e.ObjectId = Renamer.DirectoryObjectId(e.Folder); if (!identities.Add(Renamer.FolderIdentity(e.Folder))) continue; }
                catch (Exception error) { e.Error = error.Message; }
                if (e.Error == null) try { e.Plan = Renamer.Preview(e.Folder, position); } catch (Exception error) { e.PhotoError = error.Message; }
                result.Add(e);
            }
            return result;
        }
        private async Task AddPaths(IEnumerable<string> paths)
        {
            if (busy) return; var additions = paths.Where(x => !String.IsNullOrWhiteSpace(x)).ToArray(); if (additions.Length == 0) return;
            await RefreshQueue(queue.Select(x => x.Folder).Concat(additions).ToArray()); path.Clear();
        }
        private async Task RefreshQueue(string[] paths = null)
        {
            if (busy) return; var input = paths ?? queue.Select(x => x.Folder).ToArray(); var position = SelectedPosition; string common = baseName.Text; int start = (int)startNumber.Value;
            SetBusy(true, "Reading folders and preparing your previews...", false);
            try
            {
                var entries = await Task.Run(() => ReadEntries(input, position));
                FolderRenamePlan captured = null; string error = null;
                try { captured = await Task.Run(() => FolderRenamer.Preview(entries.Select(x => x.Folder), "Preview", 1)); FolderRenamer.SetNames(captured, common, start); } catch (Exception ex) { error = ex.Message; }
                queue.Clear(); queue.AddRange(entries); folderPlan = captured; folderError = error; RebuildPreview(); status.Text = queue.Count == 0 ? "Add folders to begin." : "Preview ready. Review the names, then apply."; status.ForeColor = Theme.Muted;
            }
            catch (Exception error) { ShowError(error); }
            finally { SetBusy(false, null, false); }
        }
        private void ReformatFolders()
        {
            if (folderGrid == null) return;
            if (folderPlan != null) try { FolderRenamer.SetNames(folderPlan, baseName.Text, (int)startNumber.Value); folderError = null; } catch (Exception error) { folderError = error.Message; }
            RenderFolders(); UpdateButtons();
        }
        private void RebuildPreview()
        {
            photoGrid.RowCount = 0; photoRows.Clear(); folders.Items.Clear();
            foreach (var e in queue) { folders.Items.Add(e); if (e.Plan != null) foreach (var item in e.Plan.Items) photoRows.Add(Tuple.Create(e.Plan, item)); }
            photoGrid.RowCount = photoRows.Count; photoGrid.Invalidate(); photoGrid.ClearSelection();
            queueCount.Text = queue.Count + " folders selected  ·  drag to add more";
            string error = queue.Where(x => x.Error != null || x.PhotoError != null).Select(x => x.Error ?? x.PhotoError).FirstOrDefault();
            photoCount.Text = error ?? photoRows.Count.ToString("N0") + " photos  ·  " + photoRows.Count(x => x.Item2.Changed).ToString("N0") + " to rename  ·  Direct photos only"; photoCount.ForeColor = error == null ? Theme.Muted : Theme.Error;
            RenderFolders(); UpdateButtons();
        }
        private void RenderFolders()
        {
            if (folderGrid == null) return; folderGrid.RowCount = 0; folderGrid.RowCount = folderPlan == null ? 0 : folderPlan.Items.Count; folderGrid.Invalidate(); folderGrid.ClearSelection();
            folderCount.Text = folderError ?? (folderPlan == null ? "Add folders to preview their new names" : folderPlan.ChangeCount + " folders to rename  ·  Files inside keep their names"); folderCount.ForeColor = folderError == null ? Theme.Muted : Theme.Error;
        }
        private async Task Apply()
        {
            if (busy || activePage == 2) return;
            bool isFolders = activePage == 1;
            int total = isFolders ? folderPlan == null || folderError != null ? 0 : folderPlan.ChangeCount : photoRows.Count(x => x.Item2.Changed);
            if (total == 0) return;
            string summary = isFolders ? "Rename " + total + " folder names?\n\nShared name: " + baseName.Text + "\nSequence: _" + ((int)startNumber.Value).ToString("D2") + ", _" + ((int)startNumber.Value + 1).ToString("D2") + ", …\n\nFiles inside will keep their names." : "Rename " + total.ToString("N0") + " photos across " + queue.Count + " folders?\n\nNumber at the " + (SelectedPosition == NumberPosition.Start ? "start: 1_FolderName.jpg" : "end: FolderName_1.jpg");
            if (!Confirm(summary + "\n\nUndo last batch restores these changes.\nCancel & restore rolls back a running batch.", "Review your batch")) return;
            if (isFolders)
            {
                var selected = folderPlan; var affected = selected.Items.ToList();
                await RunOperation(() => FolderRenamer.Rename(selected, ReportProgress, cancellation.Token), "Renaming folders...", "folder names updated.", true, affected);
            }
            else { var plans = queue.Select(x => x.Plan).ToList(); await RunOperation(() => Renamer.RenameBatch(plans, ReportProgress, cancellation.Token), "Renaming photos...", "photos renamed.", true, null); }
        }
        private async Task UndoOrRecover()
        {
            if (busy) return;
            try
            {
                bool recovery = Renamer.HasPending; var b = Renamer.ReadJournal(recovery ? Renamer.PendingPath : Renamer.UndoPath); bool directoryBatch = b.Kind == "folders";
                string description = directoryBatch ? b.ItemCount + " folder names" : b.PhotoCount.ToString("N0") + " photo names";
                if (!Confirm(recovery ? "Recover the interrupted batch?\n\nUnfinished work is restored. Completed work finishes saving its Undo." : "Restore the previous names for " + description + "?\n\nThis affects the most recent successful batch.", recovery ? "Recover batch" : "Undo last batch")) return;
                await RunOperation(() => recovery ? Renamer.RecoverBatch() : Renamer.UndoBatch(ReportProgress, cancellation.Token), recovery ? "Recovering batch..." : "Restoring previous names...", recovery ? "items recovered." : "previous names restored.", !recovery, directoryBatch ? b.FolderRenames : null);
            }
            catch (Exception error) { ShowError(error); }
        }
        private bool Confirm(string text, string title) { return MessageBox.Show(this, text, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK; }
        private async Task RunOperation(Func<int> operation, string message, string success, bool canCancel, List<FolderRename> affected)
        {
            cancellation = new CancellationTokenSource(); SetBusy(true, message, canCancel); string result; bool failed = false;
            try { int count = await Task.Run(operation); result = count.ToString("N0") + " " + success; }
            catch (OperationCanceledException error) { result = error.Message; }
            catch (Exception error) { result = error.Message; failed = true; ShowError(error); }
            try
            {
                if (Renamer.HasPending) { var pending = Renamer.ReadJournal(Renamer.PendingPath); if (pending.Kind == "folders") affected = pending.FolderRenames; }
                if (affected != null) foreach (var item in affected) { string current = FolderRenamer.Locate(item); if (current != null) foreach (var entry in queue.Where(x => x.ObjectId == item.ObjectId)) entry.Folder = current; }
            }
            catch (Exception error) { result += "\n" + error.Message; failed = true; }
            finally { cancellation.Dispose(); cancellation = null; SetBusy(false, null, false); }
            await RefreshQueue(); status.Text = result; status.ForeColor = failed ? Theme.Error : Theme.Mint;
        }
        private void ReportProgress(int done, int total) { if (done % Math.Max(1, total / 100) != 0 && done != total) return; BeginInvoke((Action)delegate { progress.Value = Math.Min(100, (int)((long)done * 100 / total)); }); }
        private void SetBusy(bool value, string message, bool canCancel)
        {
            busy = value; UpdateButtons(); cancel.Enabled = value && canCancel; progress.Visible = value;
            if (value) { progress.Value = 0; status.ForeColor = Theme.Muted; status.Text = message; }
        }
        private void UpdateButtons()
        {
            if (rename == null) return;
            foreach (var b in new[] { add, children, addPath, refresh, remove, clear }) b.Enabled = !busy;
            foreach (var t in tabs) t.Enabled = !busy;
            path.Enabled = folders.Enabled = numberPosition.Enabled = baseName.Enabled = startNumber.Enabled = !busy;
            undo.Text = Renamer.HasPending ? "Recover batch" : "Undo last batch"; undo.Enabled = !busy && (Renamer.HasPending || Renamer.HasUndo);
            rename.Text = activePage == 1 ? "Rename folders" : "Rename photos";
            rename.Enabled = !busy && !Renamer.HasPending && activePage != 2 && (activePage == 1 ? folderPlan != null && folderError == null && folderPlan.ChangeCount > 0 : queue.Count > 0 && queue.All(x => x.Error == null && x.PhotoError == null) && photoRows.Any(x => x.Item2.Changed));
        }
        private void ShowError(Exception error) { status.ForeColor = Theme.Error; status.Text = error.Message; MessageBox.Show(this, error.Message, "Photo Renamer", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
