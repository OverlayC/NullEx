using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
using SharpMonoInjector;
using Panel = System.Windows.Forms.Panel;

namespace NullEx
{
    internal sealed class DllRecord
    {
        public string Path { get; set; }
        public string Folder { get; set; } = "";
        public string Hash { get; set; } = "";
        public string SelectedEntry { get; set; } = "";
        public List<EntryPoint> EntryPoints { get; set; } = new List<EntryPoint>();
        public bool Changed { get; set; }
    }

    internal sealed class LibraryDto
    {
        public List<LibraryItemDto> Dlls { get; set; } = new List<LibraryItemDto>();
    }

    internal sealed class LibraryItemDto
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public string Folder { get; set; }
        public string Hash { get; set; }
        public string SelectedEntry { get; set; }
    }

    public class MainForm : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        private Panel _titleBar;
        private Panel _sidebar;
        private Panel _contentHost;
        private SidebarButton _gamesBtn, _dllsBtn, _modsBtn, _pluginsBtn;

        private Panel _gamesPage;
        private Panel _dllsPage;
        private Panel _modsPage;
        private Panel _pluginsPage;

        private List<Process> _games = new List<Process>();
        private Process _selectedGame = null;
        private string _expandedGameKey = null;

        private readonly Dictionary<string, DllRecord> _dllLibrary =
            new Dictionary<string, DllRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _injectedThisSession = new HashSet<string>();
        private readonly HashSet<string> _checkedDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _selectedDllPath;

        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullEx");
        private static readonly string LibraryFilePath = Path.Combine(AppDataDir, "dll_library.json");
        private static readonly string LegacyLibraryFilePath = Path.Combine(AppDataDir, "dll_library.txt");
        private static readonly string ImportDir = Path.Combine(AppDataDir, "imported");

        private DateTime _lastGamesScan = DateTime.MinValue;
        private static readonly TimeSpan GamesCacheTtl = TimeSpan.FromSeconds(3);

        private Panel _dllsListHost;
        private Label _dllsStatusLabel;
        private Panel _dllDetailHost;
        private Panel _modsListHost;
        private Panel _gamesListHost;
        private Label _gamesStatusLabel;
        private bool _refreshingGames = false;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public MainForm()
        {
            InitializeComponents();
            SwitchTab(0);
            Shown += (s, e) =>
            {
                LoadDllLibrary();
                RefreshGames(forceScan: true);
            };
        }

        private void InitializeComponents()
        {
            SuspendLayout();

            Text = "NullEx";
            Size = new Size(1180, 760);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.WindowBg;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Body;
            DoubleBuffered = true;
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            _titleBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.SurfaceBg };
            _titleBar.MouseDown += TitleBar_MouseDown;

            var appIcon = new Label
            {
                Text = "◆",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Theme.Accent,
                AutoSize = true,
                Location = new Point(10, 8),
                BackColor = Color.Transparent
            };
            appIcon.MouseDown += TitleBar_MouseDown;

            var appTitle = new Label
            {
                Text = "NullEx",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(32, 9),
                BackColor = Color.Transparent
            };
            appTitle.MouseDown += TitleBar_MouseDown;

            var closeBtn = MakeWindowButton("✕", true);
            var maxBtn = MakeWindowButton("☐", false);
            var minBtn = MakeWindowButton("—", false);
            closeBtn.Click += (s, e) => Close();
            maxBtn.Click += (s, e) =>
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _titleBar.Controls.Add(appIcon);
            _titleBar.Controls.Add(appTitle);
            _titleBar.Controls.Add(minBtn);
            _titleBar.Controls.Add(maxBtn);
            _titleBar.Controls.Add(closeBtn);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg };
            _sidebar = new Panel { Dock = DockStyle.Left, Width = 72, BackColor = Theme.SidebarBg };

            _gamesBtn = new SidebarButton { Glyph = "\uE7FC", Dock = DockStyle.Top };
            _dllsBtn = new SidebarButton { Glyph = "\uE7B8", Dock = DockStyle.Top };
            _modsBtn = new SidebarButton { Glyph = "\uE734", Dock = DockStyle.Top };
            _pluginsBtn = new SidebarButton { Glyph = "\uF156", Dock = DockStyle.Top }; // SemiCircleQuestionMark

            _gamesBtn.Click += (s, e) => SwitchTab(0);
            _dllsBtn.Click += (s, e) => SwitchTab(1);
            _modsBtn.Click += (s, e) => SwitchTab(2);
            _pluginsBtn.Click += (s, e) => SwitchTab(3);

            _sidebar.Controls.Add(_pluginsBtn);
            _sidebar.Controls.Add(_modsBtn);
            _sidebar.Controls.Add(_dllsBtn);
            _sidebar.Controls.Add(_gamesBtn);

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.WindowBg,
                Padding = new Padding(28, 20, 28, 20),
                AllowDrop = true
            };
            _contentHost.DragEnter += MainForm_DragEnter;
            _contentHost.DragDrop += MainForm_DragDrop;

            BuildGamesPage();
            BuildDllsPage();
            BuildModsPage();
            BuildPluginsPage();

            _contentHost.Controls.Add(_gamesPage);
            _contentHost.Controls.Add(_dllsPage);
            _contentHost.Controls.Add(_modsPage);
            _contentHost.Controls.Add(_pluginsPage);

            body.Controls.Add(_contentHost);
            body.Controls.Add(_sidebar);
            Controls.Add(body);
            Controls.Add(_titleBar);
            ResumeLayout(false);
        }

        private Button MakeWindowButton(string glyph, bool close)
        {
            var btn = new Button
            {
                Text = glyph,
                Size = new Size(46, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceBg,
                ForeColor = Theme.TextSecondary,
                Font = new Font("Segoe UI", 9F),
                TabStop = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = close ? Color.FromArgb(196, 43, 28) : Theme.CardHover;
            btn.FlatAppearance.MouseDownBackColor = close ? Color.FromArgb(160, 30, 20) : Theme.Border;
            return btn;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;

            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;
                string ext = Path.GetExtension(file);
                if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    AddDllToLibrary(file);
                else if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    ImportLibrary(file);
            }
        }

        private void BuildGamesPage()
        {
            _gamesPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg, AllowDrop = true };
            _gamesPage.DragEnter += MainForm_DragEnter;
            _gamesPage.DragDrop += MainForm_DragDrop;

            var header = new Label
            {
                Text = "Games",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            var sub = new Label
            {
                Text = "Click a game to reveal DLLs. Check rows then Inject selected, or click a row to inject one.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(2, 32),
                BackColor = Color.Transparent
            };

            _gamesStatusLabel = new Label
            {
                Text = "Scanning...",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(0, 60),
                BackColor = Color.Transparent
            };

            var refreshBtn = new RoundedButton
            {
                Text = "Refresh",
                Size = new Size(110, 34),
                Location = new Point(0, 88),
                CornerRadius = 8,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPressed,
                Font = new Font("Segoe UI Semibold", 9F)
            };
            refreshBtn.Click += (s, e) => RefreshGames(forceScan: true);

            var addDllBtn = new RoundedButton
            {
                Text = "Add DLL to library...",
                Size = new Size(180, 34),
                Location = new Point(122, 88),
                CornerRadius = 8,
                NormalColor = Theme.InputBg,
                HoverColor = Theme.CardHover,
                PressedColor = Theme.Border,
                BorderColor = Theme.BorderLight,
                BorderThickness = 1,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body
            };
            addDllBtn.Click += (s, e) =>
            {
                BrowseForDll();
                RefreshGames(forceScan: false);
            };

            _gamesListHost = new Panel
            {
                Location = new Point(0, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.WindowBg,
                AutoScroll = true,
                Size = new Size(_contentHost.Width - 56, _contentHost.Height - 160)
            };

            _gamesPage.Controls.Add(header);
            _gamesPage.Controls.Add(sub);
            _gamesPage.Controls.Add(_gamesStatusLabel);
            _gamesPage.Controls.Add(refreshBtn);
            _gamesPage.Controls.Add(addDllBtn);
            _gamesPage.Controls.Add(_gamesListHost);
        }

        private string GameKey(Process p) => $"{p.ProcessName}:{p.Id}";

        private void RefreshGames(bool forceScan = false)
        {
            if (_refreshingGames) return;
            _refreshingGames = true;

            try
            {
                int selectedPid = _selectedGame?.Id ?? -1;
                bool needScan = forceScan ||
                    (DateTime.UtcNow - _lastGamesScan) > GamesCacheTtl ||
                    _games.Count == 0;

                if (needScan)
                {
                    _games = FindUnityMonoGames();
                    _lastGamesScan = DateTime.UtcNow;
                }

                _gamesListHost.SuspendLayout();
                _gamesListHost.Controls.Clear();

                if (_games.Count == 0)
                {
                    _gamesStatusLabel.Text = "No Unity Mono games detected.";
                    var empty = new IconCard
                    {
                        Title = "Nothing running",
                        Subtitle = "Launch a Unity game that uses the Mono backend",
                        IconGlyph = "\uE7BA",
                        IconColor = Theme.Border,
                        Dock = DockStyle.Top,
                        Height = 72,
                        Margin = new Padding(0, 4, 0, 8)
                    };
                    _gamesListHost.Controls.Add(empty);
                    _selectedGame = null;
                    _expandedGameKey = null;
                    return;
                }

                int changed = _dllLibrary.Values.Count(r => r.Changed);
                _gamesStatusLabel.Text =
                    $"{_games.Count} game(s)  •  {_dllLibrary.Count} DLL(s)" +
                    (changed > 0 ? $"  •  {changed} changed" : "");

                _selectedGame = _games.FirstOrDefault(g => g.Id == selectedPid);
                if (_selectedGame == null) _expandedGameKey = null;

                for (int i = _games.Count - 1; i >= 0; i--)
                {
                    var g = _games[i];
                    string key = GameKey(g);
                    bool isExpanded = _expandedGameKey == key;
                    bool isSelected = _selectedGame != null && _selectedGame.Id == g.Id;

                    var card = new IconCard
                    {
                        Title = g.ProcessName,
                        Subtitle = $"PID {g.Id}",
                        IconGlyph = "\uE7FC",
                        IconColor = Color.FromArgb(80, 120, 200),
                        Badge = isExpanded ? "" : (_dllLibrary.Count > 0 ? $"{_dllLibrary.Count} DLLs" : ""),
                        Selected = isSelected,
                        Expanded = isExpanded,
                        ShowChevron = true,
                        Dock = DockStyle.Top,
                        Height = 72,
                        Margin = new Padding(0, 4, 0, 8),
                        Tag2 = g.Id
                    };

                    int pid = g.Id;
                    string processName = g.ProcessName;

                    card.Click += (s, e) =>
                    {
                        _selectedGame = _games.FirstOrDefault(p => p.Id == pid);
                        if (_expandedGameKey == key) _expandedGameKey = null;
                        else _expandedGameKey = key;
                        RefreshGames(forceScan: false);
                    };

                    if (isExpanded)
                    {
                        var expansionHost = BuildExpansionPanel(processName, pid);
                        _gamesListHost.Controls.Add(expansionHost);
                    }

                    _gamesListHost.Controls.Add(card);
                }
            }
            finally
            {
                _gamesListHost.ResumeLayout(true);
                _refreshingGames = false;
            }
        }

        private Panel BuildExpansionPanel(string processName, int pid)
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                Height = 10,
                BackColor = Theme.WindowBg,
                Padding = new Padding(28, 4, 4, 8)
            };

            if (_dllLibrary.Count == 0)
            {
                var empty = new Label
                {
                    Text = "No DLLs in library — add one above, or drag a .dll onto the window.",
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = Theme.TextMuted,
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };
                host.Controls.Add(empty);
                host.Height = 42;
                return host;
            }

            var groups = _dllLibrary.Values
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Folder) ? "" : r.Folder.Trim())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int gi = groups.Count - 1; gi >= 0; gi--)
            {
                var g = groups[gi];
                var recs = g.OrderBy(r => Path.GetFileName(r.Path), StringComparer.OrdinalIgnoreCase).ToList();

                for (int i = recs.Count - 1; i >= 0; i--)
                {
                    var rec = recs[i];
                    string dllPath = rec.Path;
                    string injKey = $"{pid}|{dllPath}";
                    bool wasInjected = _injectedThisSession.Contains(injKey);
                    var selected = GetSelectedEntry(rec);

                    var row = new DllRow
                    {
                        FileName = Path.GetFileName(dllPath),
                        FullPath = dllPath,
                        EntryPointCount = rec.EntryPoints.Count,
                        IsInjected = wasInjected,
                        IsChanged = rec.Changed,
                        SelectedEntryLabel = selected != null ? selected.MethodName : "",
                        Folder = rec.Folder,
                        Checked = _checkedDlls.Contains(dllPath),
                        Dock = DockStyle.Top,
                        Height = 40,
                        Margin = new Padding(0, 0, 0, 4)
                    };

                    string capturedPath = dllPath;
                    row.CheckedChanged += (s, e) =>
                    {
                        if (row.Checked) _checkedDlls.Add(capturedPath);
                        else _checkedDlls.Remove(capturedPath);
                    };
                    row.Click += (s, e) => InjectDllIntoGame(processName, pid, capturedPath);

                    host.Controls.Add(row);
                    host.Height += 44;
                }

                if (!string.IsNullOrEmpty(g.Key))
                {
                    var hdr = new Label
                    {
                        Text = g.Key.ToUpperInvariant(),
                        Font = new Font("Segoe UI", 8F),
                        ForeColor = Theme.TextMuted,
                        AutoSize = false,
                        Dock = DockStyle.Top,
                        Height = 22,
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.Transparent
                    };
                    host.Controls.Add(hdr);
                    host.Height += 22;
                }
            }

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };

            var injectSel = new RoundedButton
            {
                Text = "Inject selected",
                Size = new Size(140, 28),
                Location = new Point(0, 2),
                CornerRadius = 6,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPressed,
                Font = new Font("Segoe UI Semibold", 8.5F)
            };
            injectSel.Click += (s, e) => InjectSelected(processName, pid);

            var selectAll = new RoundedButton
            {
                Text = "Select all",
                Size = new Size(100, 28),
                Location = new Point(148, 2),
                CornerRadius = 6,
                NormalColor = Theme.InputBg,
                HoverColor = Theme.CardHover,
                PressedColor = Theme.Border,
                BorderColor = Theme.BorderLight,
                BorderThickness = 1,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body
            };
            selectAll.Click += (s, e) =>
            {
                bool allOn = _dllLibrary.Keys.All(p => _checkedDlls.Contains(p));
                _checkedDlls.Clear();
                if (!allOn)
                {
                    foreach (var p in _dllLibrary.Keys)
                        _checkedDlls.Add(p);
                }
                RefreshGames(forceScan: false);
            };

            toolbar.Controls.Add(injectSel);
            toolbar.Controls.Add(selectAll);
            host.Controls.Add(toolbar);
            host.Height += 38;
            host.Height += 4;
            return host;
        }

        private void InjectSelected(string processName, int pid)
        {
            var paths = _dllLibrary.Keys.Where(p => _checkedDlls.Contains(p)).ToList();
            if (paths.Count == 0)
            {
                MessageBox.Show(this, "Check one or more DLLs first, or click a row to inject a single DLL.",
                    "Inject selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int ok = 0;
            var errors = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    if (InjectDllIntoGame(processName, pid, path, refresh: false))
                        ok++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }

            _gamesStatusLabel.Text = $"Injected {ok}/{paths.Count} DLL(s) into {processName}";
            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n", errors),
                    "Some injections failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshGames(forceScan: false);
        }

        private bool InjectDllIntoGame(string processName, int pid, string dllPath, bool refresh = true)
        {
            if (!_dllLibrary.TryGetValue(dllPath, out var rec) || rec.EntryPoints.Count == 0)
            {
                MessageBox.Show(this, "No valid entry points in this DLL.\nRescan it from the DLLs tab.",
                    "Injection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var entry = GetSelectedEntry(rec);
            if (entry == null)
            {
                MessageBox.Show(this, "No valid entry points in this DLL.",
                    "Injection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(dllPath);
                using (var injector = new Injector(processName))
                {
                    injector.Inject(bytes, entry.Namespace, entry.ClassName, entry.MethodName);
                }

                _injectedThisSession.Add($"{pid}|{dllPath}");

                if (refresh)
                {
                    _gamesStatusLabel.Text =
                        $"Injected {Path.GetFileName(dllPath)} into {processName} ({entry})";
                    RefreshGames(forceScan: false);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (refresh)
                {
                    MessageBox.Show(this, $"Injection failed:\n{ex.Message}",
                        "Injection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                throw;
            }
        }

        private static EntryPoint GetSelectedEntry(DllRecord rec)
        {
            if (rec.EntryPoints == null || rec.EntryPoints.Count == 0) return null;
            if (!string.IsNullOrEmpty(rec.SelectedEntry))
            {
                var match = rec.EntryPoints.FirstOrDefault(e => e.ToString() == rec.SelectedEntry);
                if (match != null) return match;
            }
            return rec.EntryPoints.OrderByDescending(x => x.Score).First();
        }

        private void BuildDllsPage()
        {
            _dllsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg, AllowDrop = true };
            _dllsPage.DragEnter += MainForm_DragEnter;
            _dllsPage.DragDrop += MainForm_DragDrop;

            var header = new Label
            {
                Text = "DLLs",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            var sub = new Label
            {
                Text = "Library of mod assemblies — drag .dll files here, group into folders, pick an entry point",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(2, 32),
                BackColor = Color.Transparent
            };

            _dllsStatusLabel = new Label
            {
                Text = "No DLLs in library",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(0, 60),
                BackColor = Color.Transparent
            };

            var addBtn = new RoundedButton
            {
                Text = "Add DLL...",
                Size = new Size(120, 34),
                Location = new Point(0, 88),
                CornerRadius = 8,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPressed,
                Font = new Font("Segoe UI Semibold", 9F)
            };
            addBtn.Click += (s, e) => BrowseForDll();

            var exportBtn = new RoundedButton
            {
                Text = "Export",
                Size = new Size(90, 34),
                Location = new Point(128, 88),
                CornerRadius = 8,
                NormalColor = Theme.InputBg,
                HoverColor = Theme.CardHover,
                PressedColor = Theme.Border,
                BorderColor = Theme.BorderLight,
                BorderThickness = 1,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body
            };
            exportBtn.Click += (s, e) => ExportLibrary();

            var importBtn = new RoundedButton
            {
                Text = "Import",
                Size = new Size(90, 34),
                Location = new Point(226, 88),
                CornerRadius = 8,
                NormalColor = Theme.InputBg,
                HoverColor = Theme.CardHover,
                PressedColor = Theme.Border,
                BorderColor = Theme.BorderLight,
                BorderThickness = 1,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body
            };
            importBtn.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "NullEx pack (*.zip;*.json)|*.zip;*.json|Zip (*.zip)|*.zip|JSON (*.json)|*.json";
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        ImportLibrary(dialog.FileName);
                }
            };

            _dllsListHost = new Panel
            {
                Location = new Point(0, 140),
                BackColor = Theme.WindowBg,
                AutoScroll = true,
                AllowDrop = true
            };
            _dllsListHost.DragEnter += MainForm_DragEnter;
            _dllsListHost.DragDrop += MainForm_DragDrop;

            _dllDetailHost = new Panel
            {
                BackColor = Theme.WindowBg,
                AutoScroll = true
            };

            _dllsPage.Controls.Add(header);
            _dllsPage.Controls.Add(sub);
            _dllsPage.Controls.Add(_dllsStatusLabel);
            _dllsPage.Controls.Add(addBtn);
            _dllsPage.Controls.Add(exportBtn);
            _dllsPage.Controls.Add(importBtn);
            _dllsPage.Controls.Add(_dllsListHost);
            _dllsPage.Controls.Add(_dllDetailHost);
            LayoutDllsPage();
        }

        private void LayoutDllsPage()
        {
            int w = _contentHost.Width - 56;
            int h = _contentHost.Height - 160;
            if (w < 200 || h < 100) return;
            int detailWidth = Math.Min(400, w / 2);
            int listWidth = w - detailWidth - 12;
            _dllsListHost.Size = new Size(listWidth, h);
            _dllDetailHost.Location = new Point(listWidth + 12, 140);
            _dllDetailHost.Size = new Size(detailWidth, h);
        }

        private void BrowseForDll()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var file in dialog.FileNames)
                        AddDllToLibrary(file);
                }
            }
        }

        private void AddDllToLibrary(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return;

            dllPath = Path.GetFullPath(dllPath);

            if (_dllLibrary.ContainsKey(dllPath))
            {
                _selectedDllPath = dllPath;
                RebuildDllsList();
                ShowDllDetails(dllPath);
                return;
            }

            List<EntryPoint> eps;
            try
            {
                eps = AssemblyScanner.Scan(dllPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to scan DLL:\n{ex.Message}",
                    "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var rec = new DllRecord
            {
                Path = dllPath,
                Folder = "",
                Hash = ComputeFileHash(dllPath),
                EntryPoints = eps,
                Changed = false,
                SelectedEntry = eps.Count > 0
                    ? eps.OrderByDescending(x => x.Score).First().ToString()
                    : ""
            };

            _dllLibrary[dllPath] = rec;
            _selectedDllPath = dllPath;
            UpdateDllsStatus();
            RebuildDllsList();
            ShowDllDetails(dllPath);
            SaveDllLibrary();
            RefreshGames(forceScan: false);
        }

        private void RemoveDllFromLibrary(string dllPath)
        {
            if (!_dllLibrary.ContainsKey(dllPath)) return;

            var name = Path.GetFileName(dllPath);
            var result = MessageBox.Show(this,
                $"Remove {name} from the library?\n\nThe file itself is not deleted.",
                "Remove DLL", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            _dllLibrary.Remove(dllPath);
            _checkedDlls.Remove(dllPath);
            if (string.Equals(_selectedDllPath, dllPath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedDllPath = null;
                _dllDetailHost.Controls.Clear();
            }

            UpdateDllsStatus();
            RebuildDllsList();
            SaveDllLibrary();
            RefreshGames(forceScan: false);
        }

        private void RescanDll(string dllPath)
        {
            if (!_dllLibrary.TryGetValue(dllPath, out var rec)) return;
            if (!File.Exists(dllPath))
            {
                MessageBox.Show(this, "File no longer exists on disk.",
                    "Rescan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                rec.EntryPoints = AssemblyScanner.Scan(dllPath);
                rec.Hash = ComputeFileHash(dllPath);
                rec.Changed = false;
                if (!rec.EntryPoints.Any(e => e.ToString() == rec.SelectedEntry))
                {
                    rec.SelectedEntry = rec.EntryPoints.Count > 0
                        ? rec.EntryPoints.OrderByDescending(x => x.Score).First().ToString()
                        : "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to rescan DLL:\n{ex.Message}",
                    "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UpdateDllsStatus();
            RebuildDllsList();
            ShowDllDetails(dllPath);
            SaveDllLibrary();
            RefreshGames(forceScan: false);
        }

        private IconCard MakeDllCard(DllRecord rec)
        {
            string badge = rec.Changed ? "changed" : $"{rec.EntryPoints.Count} methods";
            var card = new IconCard
            {
                Title = Path.GetFileNameWithoutExtension(rec.Path),
                Subtitle = string.IsNullOrWhiteSpace(rec.Folder) ? rec.Path : $"{rec.Folder}  •  {rec.Path}",
                IconGlyph = "\uE7B8",
                IconColor = rec.Changed ? Theme.Warning : Color.FromArgb(120, 80, 180),
                Badge = badge,
                Selected = string.Equals(_selectedDllPath, rec.Path, StringComparison.OrdinalIgnoreCase),
                Dock = DockStyle.Top,
                Height = 72,
                Margin = new Padding(0, 4, 0, 8)
            };
            string capturedPath = rec.Path;
            card.Click += (s, e) =>
            {
                _selectedDllPath = capturedPath;
                RebuildDllsList();
                ShowDllDetails(capturedPath);
            };
            return card;
        }

        private void RebuildDllsList()
        {
            if (_dllsListHost == null) return;

            _dllsListHost.SuspendLayout();
            _dllsListHost.Controls.Clear();

            var groups = _dllLibrary.Values
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Folder) ? "" : r.Folder.Trim())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var controls = new List<Control>();
            foreach (var g in groups)
            {
                if (!string.IsNullOrEmpty(g.Key))
                {
                    controls.Add(new Label
                    {
                        Text = g.Key.ToUpperInvariant(),
                        Font = new Font("Segoe UI", 8F),
                        ForeColor = Theme.TextMuted,
                        AutoSize = false,
                        Dock = DockStyle.Top,
                        Height = 22,
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.Transparent,
                        Padding = new Padding(4, 0, 0, 0)
                    });
                }

                foreach (var rec in g.OrderBy(r => Path.GetFileName(r.Path), StringComparer.OrdinalIgnoreCase))
                    controls.Add(MakeDllCard(rec));
            }

            for (int i = controls.Count - 1; i >= 0; i--)
                _dllsListHost.Controls.Add(controls[i]);

            _dllsListHost.ResumeLayout(true);
            UpdateDllsStatus();
        }

        private void UpdateDllsStatus()
        {
            if (_dllsStatusLabel == null) return;
            if (_dllLibrary.Count == 0)
            {
                _dllsStatusLabel.Text = "No DLLs in library — add or drag files here";
                return;
            }
            int changed = _dllLibrary.Values.Count(r => r.Changed);
            int folders = _dllLibrary.Values
                .Select(r => string.IsNullOrWhiteSpace(r.Folder) ? "" : r.Folder.Trim())
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            _dllsStatusLabel.Text =
                $"{_dllLibrary.Count} DLL(s)" +
                (folders > 0 ? $"  •  {folders} folder(s)" : "") +
                (changed > 0 ? $"  •  {changed} changed on disk" : "");
        }

        private void ShowDllDetails(string dllPath)
        {
            if (!_dllLibrary.TryGetValue(dllPath, out var rec)) return;

            _dllDetailHost.Controls.Clear();
            int w = Math.Max(200, _dllDetailHost.Width - 20);

            var info = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 210,
                CornerRadius = 10,
                BackColor = Theme.CardBg
            };

            info.Controls.Add(new Label
            {
                Text = Path.GetFileName(dllPath),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(w - 20, 22),
                Location = new Point(16, 12),
                BackColor = Color.Transparent
            });

            info.Controls.Add(new Label
            {
                Text = dllPath,
                Font = Theme.Small,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Size = new Size(w - 20, 28),
                Location = new Point(16, 34),
                BackColor = Color.Transparent
            });

            string hashShort = string.IsNullOrEmpty(rec.Hash) ? "(no hash)"
                : rec.Hash.Length > 16 ? rec.Hash.Substring(0, 16) + "…" : rec.Hash;
            info.Controls.Add(new Label
            {
                Text = rec.Changed
                    ? $"SHA-256 {hashShort}  •  file changed since last scan"
                    : $"SHA-256 {hashShort}  •  {rec.EntryPoints.Count} entry point(s)",
                Font = Theme.Small,
                ForeColor = rec.Changed ? Theme.Warning : Theme.Success,
                AutoSize = false,
                Size = new Size(w - 20, 18),
                Location = new Point(16, 64),
                BackColor = Color.Transparent
            });

            info.Controls.Add(new Label
            {
                Text = "FOLDER",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(16, 88),
                BackColor = Color.Transparent
            });

            var folderBox = new RoundedTextBox
            {
                Location = new Point(16, 104),
                Size = new Size(Math.Min(260, w - 36), 32),
                PlaceholderText = "e.g. Visual, Combat, Utility"
            };
            folderBox.Text = rec.Folder ?? "";
            folderBox.Inner.LostFocus += (s, e) =>
            {
                rec.Folder = (folderBox.Text ?? "").Trim();
                SaveDllLibrary();
                RebuildDllsList();
                RefreshGames(forceScan: false);
            };
            info.Controls.Add(folderBox);

            var rescanBtn = new RoundedButton
            {
                Text = "Rescan",
                Size = new Size(90, 28),
                Location = new Point(16, 148),
                CornerRadius = 6,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPressed,
                Font = new Font("Segoe UI Semibold", 8.5F)
            };
            rescanBtn.Click += (s, e) => RescanDll(dllPath);

            var removeBtn = new RoundedButton
            {
                Text = "Remove",
                Size = new Size(90, 28),
                Location = new Point(114, 148),
                CornerRadius = 6,
                NormalColor = Theme.InputBg,
                HoverColor = Color.FromArgb(90, 40, 40),
                PressedColor = Theme.Error,
                BorderColor = Theme.BorderLight,
                BorderThickness = 1,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body
            };
            removeBtn.Click += (s, e) => RemoveDllFromLibrary(dllPath);

            info.Controls.Add(rescanBtn);
            info.Controls.Add(removeBtn);
            _dllDetailHost.Controls.Add(info);

            _dllDetailHost.Controls.Add(new Label
            {
                Text = "ENTRY POINTS  —  click to select the method used on inject",
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(0, 222),
                BackColor = Color.Transparent
            });

            string selectedKey = rec.SelectedEntry;
            int y = 246;
            foreach (var ep in rec.EntryPoints.Take(40))
            {
                string key = ep.ToString();
                bool isSel = key == selectedKey;
                var lbl = new Label
                {
                    Text = (isSel ? "●  " : "○  ") + key,
                    Font = new Font("Consolas", 8.5F),
                    ForeColor = isSel ? Theme.Accent : Theme.TextSecondary,
                    AutoSize = false,
                    Size = new Size(_dllDetailHost.Width - 20, 20),
                    Location = new Point(4, y),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                string capturedKey = key;
                lbl.Click += (s, e) =>
                {
                    rec.SelectedEntry = capturedKey;
                    SaveDllLibrary();
                    ShowDllDetails(dllPath);
                    RefreshGames(forceScan: false);
                };
                _dllDetailHost.Controls.Add(lbl);
                y += 22;
            }
        }

        private void SaveDllLibrary()
        {
            try
            {
                if (!Directory.Exists(AppDataDir))
                    Directory.CreateDirectory(AppDataDir);

                var dto = new LibraryDto
                {
                    Dlls = _dllLibrary.Values.Select(r => new LibraryItemDto
                    {
                        Path = r.Path,
                        FileName = Path.GetFileName(r.Path),
                        Folder = r.Folder,
                        Hash = r.Hash,
                        SelectedEntry = r.SelectedEntry
                    }).ToList()
                };

                File.WriteAllText(LibraryFilePath, JsonSerializer.Serialize(dto, JsonOpts));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save DLL library: {ex.Message}");
            }
        }

        private void LoadDllLibrary()
        {
            try
            {
                if (File.Exists(LibraryFilePath))
                {
                    var json = File.ReadAllText(LibraryFilePath);
                    var dto = JsonSerializer.Deserialize<LibraryDto>(json, JsonOpts);
                    if (dto?.Dlls != null)
                    {
                        foreach (var item in dto.Dlls)
                            TryLoadRecord(item.Path, item.Folder, item.Hash, item.SelectedEntry);
                    }
                }
                else if (File.Exists(LegacyLibraryFilePath))
                {
                    foreach (var line in File.ReadAllLines(LegacyLibraryFilePath))
                    {
                        var p = line.Trim();
                        if (!string.IsNullOrEmpty(p))
                            TryLoadRecord(p, "", null, null);
                    }
                    SaveDllLibrary();
                }

                RebuildDllsList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load DLL library: {ex.Message}");
            }
        }

        private void TryLoadRecord(string dllPath, string folder, string savedHash, string selectedEntry)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath)) return;
            if (_dllLibrary.ContainsKey(dllPath)) return;

            try
            {
                var eps = AssemblyScanner.Scan(dllPath);
                string hash = ComputeFileHash(dllPath);
                bool changed = !string.IsNullOrEmpty(savedHash) &&
                               !string.Equals(savedHash, hash, StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(selectedEntry) || !eps.Any(e => e.ToString() == selectedEntry))
                {
                    selectedEntry = eps.Count > 0
                        ? eps.OrderByDescending(x => x.Score).First().ToString()
                        : "";
                }

                _dllLibrary[dllPath] = new DllRecord
                {
                    Path = dllPath,
                    Folder = folder ?? "",
                    Hash = hash,
                    SelectedEntry = selectedEntry,
                    EntryPoints = eps,
                    Changed = changed
                };
            }
            catch { }
        }

        private static string ComputeFileHash(string path)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(fs);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }

        private void ExportLibrary()
        {
            if (_dllLibrary.Count == 0)
            {
                MessageBox.Show(this, "Library is empty.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "NullEx pack (*.zip)|*.zip|JSON only (*.json)|*.json";
                dialog.FileName = "nullex-library.zip";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var dto = new LibraryDto
                    {
                        Dlls = _dllLibrary.Values.Select(r => new LibraryItemDto
                        {
                            Path = r.Path,
                            FileName = Path.GetFileName(r.Path),
                            Folder = r.Folder,
                            Hash = r.Hash,
                            SelectedEntry = r.SelectedEntry
                        }).ToList()
                    };
                    string json = JsonSerializer.Serialize(dto, JsonOpts);

                    if (Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                        using (var zip = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create))
                        {
                            var jsonEntry = zip.CreateEntry("library.json");
                            using (var sw = new StreamWriter(jsonEntry.Open()))
                                sw.Write(json);

                            foreach (var rec in _dllLibrary.Values)
                            {
                                if (!File.Exists(rec.Path)) continue;
                                string name = Path.GetFileName(rec.Path);
                                zip.CreateEntryFromFile(rec.Path, "dlls/" + name, CompressionLevel.Fastest);
                            }
                        }
                    }

                    MessageBox.Show(this, "Library exported.", "Export",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Export failed:\n{ex.Message}",
                        "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportLibrary(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                string ext = Path.GetExtension(filePath);
                if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = JsonSerializer.Deserialize<LibraryDto>(File.ReadAllText(filePath), JsonOpts);
                    ImportDto(dto, null);
                }
                else
                {
                    string dest = Path.Combine(ImportDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    Directory.CreateDirectory(dest);
                    ZipFile.ExtractToDirectory(filePath, dest);

                    string jsonPath = Path.Combine(dest, "library.json");
                    LibraryDto dto = null;
                    if (File.Exists(jsonPath))
                        dto = JsonSerializer.Deserialize<LibraryDto>(File.ReadAllText(jsonPath), JsonOpts);

                    string dllsDir = Path.Combine(dest, "dlls");
                    ImportDto(dto, Directory.Exists(dllsDir) ? dllsDir : dest);
                }

                SaveDllLibrary();
                RebuildDllsList();
                RefreshGames(forceScan: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed:\n{ex.Message}",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportDto(LibraryDto dto, string extractedDllsDir)
        {
            int added = 0;

            if (dto?.Dlls != null)
            {
                foreach (var item in dto.Dlls)
                {
                    string path = item.Path;
                    if (extractedDllsDir != null && !string.IsNullOrEmpty(item.FileName))
                    {
                        string packed = Path.Combine(extractedDllsDir, item.FileName);
                        if (File.Exists(packed)) path = packed;
                    }

                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    path = Path.GetFullPath(path);
                    if (_dllLibrary.ContainsKey(path)) continue;

                    TryLoadRecord(path, item.Folder, item.Hash, item.SelectedEntry);
                    if (_dllLibrary.ContainsKey(path)) added++;
                }
            }
            else if (extractedDllsDir != null)
            {
                foreach (var dll in Directory.GetFiles(extractedDllsDir, "*.dll"))
                {
                    if (_dllLibrary.ContainsKey(dll)) continue;
                    TryLoadRecord(dll, "", null, null);
                    if (_dllLibrary.ContainsKey(dll)) added++;
                }
            }

            MessageBox.Show(this, added > 0
                    ? $"Imported {added} DLL(s)."
                    : "Nothing new to import (files missing or already in library).",
                "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildModsPage()
        {
            _modsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg };

            var header = new Label
            {
                Text = "Known Mods",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            var sub = new Label
            {
                Text = "Popular Unity mod frameworks and tools that work with NullEx",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(2, 32),
                BackColor = Color.Transparent
            };

            _modsListHost = new Panel
            {
                Location = new Point(0, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.WindowBg,
                AutoScroll = true,
                Size = new Size(_contentHost.Width - 56, _contentHost.Height - 90)
            };

            _modsPage.Controls.Add(header);
            _modsPage.Controls.Add(sub);
            _modsPage.Controls.Add(_modsListHost);

            var knownMods = new (string name, string desc, string glyph, Color color, string badge)[]
            {
                ("BepInEx", "Plugin framework for Unity Mono and IL2CPP games", "\uE7B8", Color.FromArgb(90, 140, 220), "Framework"),
                ("Harmony", "Runtime method patching library used by most mods", "\uE90F", Color.FromArgb(180, 120, 60), "Library"),
                ("UnityExplorer", "In-game inspector for GameObjects, scenes, and C# REPL", "\uE773", Color.FromArgb(120, 90, 200), "Tool"),
                ("dnSpy", "External .NET debugger and assembly editor", "\uE7C3", Color.FromArgb(80, 160, 120), "Tool"),
                ("MelonLoader", "Universal mod loader for Unity Mono and IL2CPP", "\uE7FC", Color.FromArgb(200, 100, 100), "Framework"),
                ("AssetRipper", "Extract assets, assemblies, and shaders from Unity games", "\uE8B7", Color.FromArgb(150, 150, 90), "Tool"),
            };

            for (int i = knownMods.Length - 1; i >= 0; i--)
            {
                var (name, desc, glyph, color, badge) = knownMods[i];
                var card = new IconCard
                {
                    Title = name,
                    Subtitle = desc,
                    IconGlyph = glyph,
                    IconColor = color,
                    Badge = badge,
                    Dock = DockStyle.Top,
                    Height = 72,
                    Margin = new Padding(0, 4, 0, 8)
                };
                card.Click += (s, e) =>
                {
                    MessageBox.Show(this,
                        $"{name}\n\n{desc}\n\nThis is an external tool or library. " +
                        $"Install it manually or use it alongside NullEx.",
                        name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                _modsListHost.Controls.Add(card);
            }
        }

        private void BuildPluginsPage()
        {
            _pluginsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg };

            var icon = new Label
            {
                Text = "\uF156",
                Font = new Font("Segoe MDL2 Assets", 48F),
                ForeColor = Theme.Accent,
                AutoSize = false,
                Size = new Size(80, 80),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var title = new Label
            {
                Text = "Plugins",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            var msg = new Label
            {
                Text = "Feature Being Worked On So Be Patient",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _pluginsPage.Controls.Add(icon);
            _pluginsPage.Controls.Add(title);
            _pluginsPage.Controls.Add(msg);
            _pluginsPage.Resize += (s, e) => LayoutPlaceholder();
            LayoutPlaceholder();

            void LayoutPlaceholder()
            {
                icon.Location = new Point(Math.Max(0, (_pluginsPage.ClientSize.Width - icon.Width) / 2),
                    Math.Max(80, _pluginsPage.ClientSize.Height / 2 - 90));
                title.Location = new Point(Math.Max(0, (_pluginsPage.ClientSize.Width - title.Width) / 2), icon.Bottom + 12);
                msg.Location = new Point(Math.Max(0, (_pluginsPage.ClientSize.Width - msg.Width) / 2), title.Bottom + 8);
            }
        }

        private void SwitchTab(int index)
        {
            _gamesPage.Visible = index == 0;
            _dllsPage.Visible = index == 1;
            _modsPage.Visible = index == 2;
            _pluginsPage.Visible = index == 3;

            _gamesBtn.IsActive = index == 0;
            _dllsBtn.IsActive = index == 1;
            _modsBtn.IsActive = index == 2;
            _pluginsBtn.IsActive = index == 3;

            _gamesBtn.Invalidate();
            _dllsBtn.Invalidate();
            _modsBtn.Invalidate();
            _pluginsBtn.Invalidate();

            if (index == 1) LayoutDllsPage();
        }

        private static readonly HashSet<string> MonoModuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mono.dll", "mono-2.0-bdwgc.dll", "mono-2.0-sgen.dll", "mono-2.0.dll"
        };

        private List<Process> FindUnityMonoGames()
        {
            var results = new List<Process>();
            int currentPid = Process.GetCurrentProcess().Id;
            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { return results; }

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == currentPid) continue;
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    foreach (ProcessModule module in process.Modules)
                    {
                        if (module.ModuleName != null && MonoModuleNames.Contains(module.ModuleName))
                        {
                            results.Add(process);
                            break;
                        }
                    }
                }
                catch { }
            }

            results.Sort((a, b) => string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    var pos = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                    int edge = 6;
                    if (pos.X <= edge && pos.Y <= edge) m.Result = (IntPtr)HTTOPLEFT;
                    else if (pos.X >= ClientSize.Width - edge && pos.Y <= edge) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (pos.X <= edge && pos.Y >= ClientSize.Height - edge) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (pos.X >= ClientSize.Width - edge && pos.Y >= ClientSize.Height - edge) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (pos.X <= edge) m.Result = (IntPtr)HTLEFT;
                    else if (pos.X >= ClientSize.Width - edge) m.Result = (IntPtr)HTRIGHT;
                    else if (pos.Y <= edge) m.Result = (IntPtr)HTTOP;
                    else if (pos.Y >= ClientSize.Height - edge) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_gamesListHost != null && _gamesPage != null)
            {
                _gamesListHost.Size = new Size(_contentHost.Width - 56, _contentHost.Height - 160);
                _modsListHost.Size = new Size(_contentHost.Width - 56, _contentHost.Height - 90);
                LayoutDllsPage();
            }
        }
    }
}