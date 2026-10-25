using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using Videolader.Core;

namespace Videolader.UI;

/// <summary>
/// The only window. Tab "Download": input box for links/IDs and the common options side by side, below them the
/// video list and the log. Tab "Settings": rarely used settings (language, cookies, API key, tools).
/// Workflow: paste links → "Load list" → check the videos → "Download". All texts come from <see cref="UiText"/>.
/// </summary>
internal sealed class MainForm : Form
{
    public const string AppTitle = "Videolader";

    /// <summary>Free space above and below the window at start, as a share of the screen height.</summary>
    private const double ScreenMargin = 0.05;

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly string _toolsDirectory;
    private readonly List<VideoRow> _rows = [];
    private ToolSet _tools;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _closeRequested;

    // Input
    private readonly TextBox _txtInput = new();
    private readonly Button _btnLoad = UiStyle.CreateButton(UiText.LoadList);
    private readonly Button _btnClearInput = UiStyle.CreateButton(UiText.Clear);

    // Settings
    private readonly TextBox _txtFolder = new() { Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 3) };
    private readonly Button _btnBrowse = UiStyle.CreateButton(UiText.Browse);
    private readonly Button _btnOpenFolder = UiStyle.CreateButton(UiText.Open);
    private readonly ComboBox _cmbQuality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, DropDownWidth = 210 };
    private readonly ComboBox _cmbCookies = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly CheckBox _chkSubtitles = new() { Text = UiText.DownloadSubtitles, AutoSize = true, Margin = new Padding(3, 5, 12, 3) };
    private readonly TextBox _txtLanguages = new() { Width = 110 };
    private readonly CheckBox _chkSkip = new() { Text = UiText.SkipExisting, AutoSize = true, Margin = new Padding(3, 5, 3, 3) };
    private readonly NumericUpDown _numPause = new() { Width = 60, Minimum = 0, Maximum = AppSettings.MaxPauseSeconds };
    private readonly CheckBox _chkRandomPause = new() { Text = UiText.RandomPause, AutoSize = true, Margin = new Padding(6, 5, 3, 3) };
    private readonly NumericUpDown _numMaxVideos = new() { Width = 70, Minimum = 0, Maximum = AppSettings.MaxVideosPerRunLimit };
    private readonly TextBox _txtApiKey = new() { Width = 330, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowKey = new() { Text = UiText.ShowKey, AutoSize = true, Margin = new Padding(6, 5, 12, 3) };
    private readonly Label _lblTools = UiStyle.CreateLabel(string.Empty);
    private readonly Button _btnTools = UiStyle.CreateButton(UiText.ToolsSetUp);
    private readonly ComboBox _cmbUiLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _tabSettings = new(UiText.TabSettings) { Padding = new Padding(8), UseVisualStyleBackColor = true };
    private readonly ToolTip _tips = new() { AutoPopDelay = 15000 };

    // List
    private readonly DataGridView _grid = new();
    private readonly DataGridViewCheckBoxColumn _colCheck = new() { HeaderText = string.Empty, Width = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
    private readonly DataGridViewTextBoxColumn _colNr = TextColumn(UiText.ColumnNumber, 44);
    private readonly DataGridViewTextBoxColumn _colId = TextColumn("ID", 110);
    private readonly DataGridViewTextBoxColumn _colTitle = TextColumn(UiText.ColumnTitle, 0);
    private readonly DataGridViewTextBoxColumn _colDuration = TextColumn(UiText.ColumnDuration, 80);
    private readonly DataGridViewTextBoxColumn _colPlaylist = TextColumn(UiText.ColumnPlaylist, 170);
    private readonly DataGridViewTextBoxColumn _colStatus = TextColumn(UiText.ColumnStatus, 190);
    private readonly Button _btnAll = UiStyle.CreateButton(UiText.SelectAll);
    private readonly Button _btnNone = UiStyle.CreateButton(UiText.SelectNone);
    private readonly Button _btnOnlyNew = UiStyle.CreateButton(UiText.SelectOnlyNew);
    private readonly Label _lblCount = UiStyle.CreateHint(string.Empty);
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
    private readonly LogBox _log = new();
    private readonly Button _btnToggleLog = UiStyle.CreateButton(UiText.HideLog);

    // Actions
    private readonly Button _btnDownload = UiStyle.CreateButton(UiText.Download);
    private readonly Button _btnCancel = UiStyle.CreateButton(UiText.Cancel);
    private readonly Button _btnHelp = UiStyle.CreateButton(UiText.HelpButton);

    /// <summary>Help topic of the last tip in the log; F1 in the log or list opens it.</summary>
    private string? _lastHelpTopic;
    private readonly ProgressBar _progress = new() { Width = 260, Height = 20, Margin = new Padding(12, 8, 6, 3) };
    private readonly Label _lblProgress = UiStyle.CreateHint(string.Empty);

    /// <param name="store">Where the settings are saved.</param>
    /// <param name="settings">The loaded settings; <see cref="UiText.Language"/> must already match them.</param>
    /// <param name="bounds">Position and size of the previous window after a language change, otherwise null.</param>
    public MainForm(SettingsStore store, AppSettings settings, Rectangle? bounds = null)
    {
        _store = store;
        _settings = settings;
        _toolsDirectory = ToolLocator.GetToolsDirectory(AppContext.BaseDirectory);
        _tools = ToolLocator.Locate(_toolsDirectory, AppContext.BaseDirectory);

        Text = $"{AppTitle} {AppVersion.Current}";
        Icon = UiStyle.AppIcon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 640);
        // Height from the screen the window opens on (CenterScreen uses the one under the mouse), with a margin above and below.
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var height = (int)(area.Height * (1 - 2 * ScreenMargin));
        Size = new Size(Math.Min(1180, area.Width), Math.Min(Math.Max(height, MinimumSize.Height), area.Height));
        if (bounds is { } previous)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = previous;
        }

        BuildLayout();
        ApplyIcons();
        LoadSettingsIntoControls();
        WireEvents();
        UpdateCount();
        SetBusy(false);

        Shown += OnShown;
        FormClosing += OnFormClosing;
    }

    /// <summary>True when the window was closed to switch the language; Program then opens it again.</summary>
    public bool LanguageChangeRequested { get; private set; }

    private enum RowStatus
    {
        New,
        Existing,
        Unavailable,
        Waiting,
        Running,
        Done,
        Failed,
        Skipped,
        Canceled,
    }

    // ------------------------------------------------------------------ Layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        // Fixed to the window width: content wraps instead of widening the column and being cut off.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var downloadPage = new TabPage(UiText.TabDownload) { Padding = new Padding(8), UseVisualStyleBackColor = true, ImageKey = "download" };
        downloadPage.Controls.Add(BuildDownloadPage());
        _tabSettings.Controls.Add(BuildSettingsPage());
        _tabs.TabPages.Add(downloadPage);
        _tabs.TabPages.Add(_tabSettings);

        root.Controls.Add(_tabs, 0, 0);
        root.Controls.Add(BuildActionBar(), 0, 1);

        Controls.Add(root);
    }

    /// <summary>Input and options side by side (so they need little height), the list below gets the rest.</summary>
    private TableLayoutPanel BuildDownloadPage()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var input = BuildInputGroup();
        input.Margin = new Padding(0, 0, 8, 6);
        top.Controls.Add(input, 0, 0);
        top.Controls.Add(BuildOptionsGroup(), 1, 0);

        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(top, 0, 0);
        page.Controls.Add(BuildListArea(), 0, 1);
        return page;
    }

    private void ApplyIcons()
    {
        UiStyle.SetIcon(_btnLoad, Glyph.List, DeviceDpi);
        UiStyle.SetIcon(_btnClearInput, Glyph.Delete, DeviceDpi);
        UiStyle.SetIcon(_btnBrowse, Glyph.Folder, DeviceDpi);
        UiStyle.SetIcon(_btnOpenFolder, Glyph.OpenFolder, DeviceDpi);
        UiStyle.SetIcon(_btnAll, Glyph.CheckboxChecked, DeviceDpi);
        UiStyle.SetIcon(_btnNone, Glyph.Checkbox, DeviceDpi);
        UiStyle.SetIcon(_btnOnlyNew, Glyph.Filter, DeviceDpi);
        UiStyle.SetIcon(_btnDownload, Glyph.Download, DeviceDpi);
        UiStyle.SetIcon(_btnCancel, Glyph.Cancel, DeviceDpi);
        UiStyle.SetIcon(_btnHelp, Glyph.Help, DeviceDpi);

        var size = (int)Math.Round(16 * DeviceDpi / 96f);
        var images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(size, size) };
        foreach (var (key, glyph, color) in new[]
                 {
                     ("download", Glyph.Download, SystemColors.ControlText),
                     ("settings", Glyph.Settings, SystemColors.ControlText),
                     ("warning", Glyph.Warning, UiStyle.DangerText),
                 })
        {
            if (UiStyle.CreateIcon(glyph, color, DeviceDpi) is { } icon)
                images.Images.Add(key, icon);
        }

        _tabs.ImageList = images;
        _tabSettings.ImageKey = "settings";
    }

    private GroupBox BuildInputGroup()
    {
        var group = NewGroup(UiText.GroupInput);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // The text box takes whatever height the options next to it need.
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _txtInput.Multiline = true;
        _txtInput.AcceptsReturn = true;
        _txtInput.ScrollBars = ScrollBars.Vertical;
        _txtInput.Dock = DockStyle.Fill;
        _txtInput.MinimumSize = new Size(0, 90);
        _txtInput.Font = UiStyle.CreateMonospaceFont(9.5f);
        _txtInput.PlaceholderText = "https://www.youtube.com/playlist?list=PL…\r\nhttps://youtu.be/aqz-KE-bpKQ\r\naqz-KE-bpKQ";
        _tips.SetToolTip(_txtInput, UiText.InputTip);

        UiStyle.MakePrimary(_btnLoad);
        _btnLoad.MinimumSize = new Size(120, 32);
        _btnClearInput.MinimumSize = new Size(0, 32);
        _tips.SetToolTip(_btnLoad, $"{UiText.LoadList} {UiText.CtrlEnterHint}");
        var buttons = Flow(_btnLoad, _btnClearInput);
        buttons.Margin = new Padding(0, 4, 0, 0);

        table.Controls.Add(_txtInput, 0, 0);
        table.Controls.Add(buttons, 0, 1);

        group.Controls.Add(table);
        return group;
    }

    /// <summary>The options needed for almost every download.</summary>
    private GroupBox BuildOptionsGroup()
    {
        var group = NewGroup(UiText.GroupOptions);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Folder row
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Margin = Padding.Empty };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.Controls.Add(_txtFolder, 0, 0);
        folderRow.Controls.Add(_btnBrowse, 1, 0);
        folderRow.Controls.Add(_btnOpenFolder, 2, 0);

        foreach (var quality in Enum.GetValues<VideoQuality>())
            _cmbQuality.Items.Add(new Choice<VideoQuality>(quality, quality.GetDisplayName()));

        AddRow(table, UiText.OutputFolderLabel, folderRow);
        AddRow(table, UiText.QualityLabel, Flow(_cmbQuality, Badge(HelpTopics.Quality, UiText.HelpTipQuality)));
        AddRow(table, UiText.SubtitlesLabel, Flow(
            _chkSubtitles, UiStyle.CreateLabel(UiText.LanguagesLabel), _txtLanguages, Badge(HelpTopics.Quality, UiText.HelpTipQuality)));
        AddRow(table, UiText.PauseLabel, Flow(
            _numPause, UiStyle.CreateLabel(UiText.SecondsUnit), _chkRandomPause, Badge(HelpTopics.Pause, UiText.HelpTipPause)));
        AddRow(table, UiText.LimitLabel, Flow(_numMaxVideos, UiStyle.CreateLabel(UiText.VideosPerRun), UiStyle.CreateHint(UiText.LimitHint)));
        AddRow(table, string.Empty, Flow(_chkSkip, Badge(HelpTopics.Existing, UiText.HelpTipExisting)));

        _tips.SetToolTip(_chkSkip, UiText.TipSkipExisting);
        _tips.SetToolTip(_txtLanguages, UiText.TipLanguages);
        _tips.SetToolTip(_numPause, UiText.TipPause);
        _tips.SetToolTip(_chkRandomPause, UiText.TipRandomPause);

        group.Controls.Add(table);
        return group;
    }

    /// <summary>Settings that are rarely needed.</summary>
    private TableLayoutPanel BuildSettingsPage()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _cmbCookies.Items.AddRange(
        [
            new Choice<string>(string.Empty, UiText.CookiesNone),
            new Choice<string>("firefox", "Firefox"),
            new Choice<string>("chrome", UiText.BrowserUsuallyLocked("Chrome")),
            new Choice<string>("edge", UiText.BrowserUsuallyLocked("Edge")),
            new Choice<string>("brave", UiText.BrowserUsuallyLocked("Brave")),
        ]);

        foreach (var language in UiLanguages.All)
            _cmbUiLanguage.Items.Add(new Choice<UiLanguage>(language, UiLanguages.NativeName(language)));

        AddRow(table, UiText.UiLanguageLabel, WithHint(Flow(_cmbUiLanguage), UiText.UiLanguageHint));
        AddRow(table, UiText.CookiesLabel, WithHint(Flow(_cmbCookies, Badge(HelpTopics.Cookies, UiText.HelpTipCookies)), UiText.CookiesHint));
        AddRow(table, UiText.ApiKeyLabel, WithHint(Flow(_txtApiKey, _chkShowKey, Badge(HelpTopics.ApiKey, UiText.HelpTipApiKey)), UiText.ApiKeyHint));
        AddRow(table, UiText.ToolsLabel, WithHint(Flow(_lblTools, _btnTools, Badge(HelpTopics.Tools, UiText.HelpTipTools)), UiText.ToolsHint(_toolsDirectory)));
        return table;
    }

    private Control BuildListArea()
    {
        // Top: toolbar + grid
        var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.Controls.Add(Flow(_btnAll, _btnNone, _btnOnlyNew, _lblCount), 0, 0);
        toolbar.Controls.Add(_btnToggleLog, 1, 0);
        listPanel.Controls.Add(toolbar, 0, 0);
        listPanel.Controls.Add(_grid, 0, 1);

        SetUpGrid();

        var logGroup = NewGroup(UiText.GroupLog);
        logGroup.AutoSize = false;
        logGroup.Controls.Add(_log);

        _split.Panel1.Controls.Add(listPanel);
        _split.Panel2.Controls.Add(logGroup);
        _split.Panel1MinSize = 80;
        _split.Panel2MinSize = 40;
        _split.Margin = Padding.Empty;
        return _split;
    }

    private TableLayoutPanel BuildActionBar()
    {
        UiStyle.MakePrimary(_btnDownload);
        _btnDownload.MinimumSize = new Size(150, 34);
        _btnCancel.MinimumSize = new Size(110, 34);
        _btnHelp.MinimumSize = new Size(0, 34);
        _tips.SetToolTip(_btnHelp, UiText.HelpButtonTooltip);

        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(Flow(_btnDownload, _btnCancel, _progress, _lblProgress), 0, 0);
        bar.Controls.Add(_btnHelp, 1, 0);
        return bar;
    }

    /// <summary>Small "?" next to a setting: tooltip with a short explanation, click opens the help topic.</summary>
    private HelpBadge Badge(string topicId, string tip) => new(topicId, tip, _tips)
    {
        Size = new Size(LogicalToDeviceUnits(16), LogicalToDeviceUnits(16)),
        Margin = new Padding(4, LogicalToDeviceUnits(7), 4, 0),
    };

    private void SetUpGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = UiStyle.GridLine;
        _grid.AutoGenerateColumns = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.EnableHeadersVisualStyles = true;
        _grid.RowTemplate.Height = 24;
        _grid.DefaultCellStyle.SelectionBackColor = UiStyle.Selection;
        _grid.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;

        _colId.DefaultCellStyle.Font = UiStyle.CreateMonospaceFont();
        _colTitle.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _colTitle.MinimumWidth = 150;
        _colNr.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _colDuration.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.AddRange(_colCheck, _colNr, _colId, _colTitle, _colDuration, _colPlaylist, _colStatus);

        var menu = new ContextMenuStrip();
        menu.Items.Add(UiText.MenuOpenInBrowser, null, (_, _) => OpenSelectedInBrowser());
        menu.Items.Add(UiText.MenuCopyIds, null, (_, _) => CopySelected(r => r.Entry.Id));
        menu.Items.Add(UiText.MenuCopyLinks, null, (_, _) => CopySelected(r => r.Entry.Url));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(UiText.MenuCheckSelected, null, (_, _) => SetCheckedForSelection(true));
        menu.Items.Add(UiText.MenuUncheckSelected, null, (_, _) => SetCheckedForSelection(false));
        _grid.ContextMenuStrip = menu;
    }

    // ------------------------------------------------------------------ Events

    private void WireEvents()
    {
        _btnLoad.Click += async (_, _) => await LoadListAsync();
        _btnClearInput.Click += (_, _) => _txtInput.Clear();
        _txtInput.KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadListAsync();
            }
        };

        _btnBrowse.Click += (_, _) => BrowseFolder();
        _btnOpenFolder.Click += (_, _) => OpenFolder();
        _txtFolder.Leave += (_, _) => OnFolderChanged();
        _chkSkip.CheckedChanged += (_, _) => { if (!_busy) ApplyLibraryStatus(); };
        _chkSubtitles.CheckedChanged += (_, _) => _txtLanguages.Enabled = _chkSubtitles.Checked && !_busy;
        _chkShowKey.CheckedChanged += (_, _) => _txtApiKey.UseSystemPasswordChar = !_chkShowKey.Checked;
        _btnTools.Click += async (_, _) => await SetUpToolsAsync();
        _btnToggleLog.Click += (_, _) => SetLogVisible(_split.Panel2Collapsed);
        _btnHelp.Click += (_, _) => HelpForm.Open(this, HelpTopicForFocus());
        _cmbUiLanguage.SelectionChangeCommitted += (_, _) => SwitchUiLanguage();

        _btnAll.Click += (_, _) => SetChecked(_ => true);
        _btnNone.Click += (_, _) => SetChecked(_ => false);
        _btnOnlyNew.Click += (_, _) => SetChecked(r => r.Status is RowStatus.New or RowStatus.Failed or RowStatus.Canceled);

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.ColumnIndex == _colCheck.Index)
                UpdateCount();
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex != _colCheck.Index)
                OpenSelectedInBrowser();
        };
        _grid.KeyDown += (_, e) =>
        {
            // In the check box column the grid toggles the cell itself.
            if (e.KeyCode == Keys.Space && !_busy && _grid.CurrentCell?.ColumnIndex != _colCheck.Index)
            {
                e.Handled = true;
                var anyUnchecked = SelectedRows().Any(r => !IsChecked(r));
                SetCheckedForSelection(anyUnchecked);
            }
        };

        _btnDownload.Click += async (_, _) => await DownloadAsync();
        _btnCancel.Click += (_, _) =>
        {
            _cts?.Cancel();
            _btnCancel.Enabled = false;
            _log.AppendMessage(UiText.Cancelling);
        };
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        var maxDistance = _split.Height - _split.Panel2MinSize - _split.SplitterWidth;
        if (maxDistance > _split.Panel1MinSize)
            _split.SplitterDistance = Math.Clamp((int)(_split.Height * 0.75), _split.Panel1MinSize, maxDistance);
        RefreshTools();
        _log.AppendMessage(UiText.ToolsFolderLog(_toolsDirectory));

        if (!_tools.IsComplete)
        {
            var missing = string.Join(", ", MissingTools());
            _log.AppendMessage(UiText.ToolsMissingLog(missing));
            _tabs.SelectedTab = _tabSettings;
            return;
        }

        // yt-dlp must stay current, YouTube changes often. Once a day is enough.
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_settings.LastToolUpdate != today)
        {
            await RunBusyAsync(UiText.UpdatingYtDlp, async ct =>
            {
                await UpdateYtDlpAsync(ct);
                _settings.LastToolUpdate = today;
                SaveSettings();
            });
        }
        else
        {
            await LogVersionAsync();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1)
        {
            HelpForm.Open(this, HelpTopicForFocus());
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>The help topic for the control that has the focus (F1), e.g. "Pause" in the pause box.</summary>
    private string HelpTopicForFocus()
    {
        for (Control? control = ActiveControl; control is not null; control = (control as ContainerControl)?.ActiveControl)
        {
            if (control == _numPause || control == _chkRandomPause || control == _numMaxVideos)
                return HelpTopics.Pause;
            if (control == _cmbQuality || control == _chkSubtitles || control == _txtLanguages)
                return HelpTopics.Quality;
            if (control == _chkSkip || control == _txtFolder)
                return HelpTopics.Existing;
            if (control == _cmbCookies)
                return HelpTopics.Cookies;
            if (control == _txtApiKey || control == _chkShowKey)
                return HelpTopics.ApiKey;
            if (control == _btnTools)
                return HelpTopics.Tools;
            if ((control == _log || control == _grid) && _lastHelpTopic is not null)
                return _lastHelpTopic;
        }

        return _tabs.SelectedTab == _tabSettings ? HelpTopics.Tools : HelpTopics.FirstSteps;
    }

    /// <summary>" → Help: “Cookies from browser” (F1)" after a tip, and F1 then opens that topic.</summary>
    private string HelpReference(string error)
    {
        if (YtDlpOutput.GetHelpTopic(error) is not { } topicId)
            return string.Empty;

        _lastHelpTopic = topicId;
        var title = HelpDocument.Load(UiLanguages.Code(UiText.Language)).Find(topicId)?.Title;
        return title is null ? string.Empty : " " + UiText.SeeHelp(title);
    }

    /// <summary>Saves the choice and closes the window; Program reopens it in the new language.</summary>
    private void SwitchUiLanguage()
    {
        if (_busy || _cmbUiLanguage.SelectedItem is not Choice<UiLanguage> { Value: var language } || language == UiText.Language)
            return;

        _settings.UiLanguage = UiLanguages.Code(language);
        LanguageChangeRequested = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            var answer = MessageBox.Show(this, UiText.ConfirmCloseWhileBusy, AppTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            // Close as soon as the running operation has stopped (see RunBusyAsync).
            e.Cancel = true;
            if (answer == DialogResult.Yes)
            {
                _closeRequested = true;
                _cts?.Cancel();
            }

            return;
        }

        ReadSettingsFromControls();
        SaveSettings();
    }

    // ------------------------------------------------------------------ Load list

    private async Task LoadListAsync()
    {
        if (_busy)
            return;

        var parsed = InputParser.Parse(_txtInput.Text);
        foreach (var invalid in parsed.Invalid)
            _log.AppendMessage(UiText.NotRecognized(invalid));

        if (parsed.Items.Count == 0)
        {
            MessageBox.Show(this, UiText.EnterAtLeastOneLink, AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ReadSettingsFromControls();
        SaveSettings();
        RefreshTools();

        await RunBusyAsync(UiText.LoadingList, async ct =>
        {
            var playlists = parsed.Items.Count(i => i.Kind != InputKind.Video);
            var videos = parsed.Items.Count - playlists;
            _log.AppendMessage(UiText.LoadingListLog(videos, playlists));

            var ytDlp = _tools.YtDlp is null ? null : new YtDlpClient(_tools);
            var log = new Progress<string>(_log.AppendMessage);
            var resolver = new VideoResolver(Http, ytDlp, _settings.ApiKey, _settings.CookiesBrowser, log,
                SubtitleLanguageList.Split(_settings.SubtitleLanguages).FirstOrDefault());
            var entries = await resolver.ResolveAsync(parsed.Items, ct);

            FillGrid(entries);
            var existing = _rows.Count(r => r.Status == RowStatus.Existing);
            var unavailable = _rows.Count(r => r.Status == RowStatus.Unavailable);
            _log.AppendMessage(UiText.ListLoadedLog(entries.Count, existing, unavailable));
        });
    }

    private void FillGrid(IReadOnlyList<VideoEntry> entries)
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        _rows.Clear();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var index = _grid.Rows.Add(false, i + 1, entry.Id, TitleText(entry), FormatDuration(entry.Duration),
                entry.PlaylistTitle ?? string.Empty, string.Empty);
            var row = new VideoRow(entry, _grid.Rows[index]);
            _grid.Rows[index].Tag = row;
            _rows.Add(row);
        }

        _grid.ResumeLayout();
        ApplyLibraryStatus();
    }

    /// <summary>Marks videos that already exist in the output folder. Keeps results of this session.</summary>
    private void ApplyLibraryStatus()
    {
        var library = TryOpenLibrary(_txtFolder.Text);
        foreach (var row in _rows)
        {
            if (row.Status is RowStatus.Done or RowStatus.Failed or RowStatus.Running)
                continue;

            if (row.Entry.Unavailable)
            {
                SetStatus(row, RowStatus.Unavailable, UiText.StatusUnavailable);
                SetChecked(row, false);
            }
            else if (library?.IsDownloaded(row.Entry.Id) == true)
            {
                SetStatus(row, RowStatus.Existing, UiText.StatusExisting);
                SetChecked(row, !_chkSkip.Checked);
            }
            else
            {
                SetStatus(row, RowStatus.New, UiText.StatusNew);
                SetChecked(row, true);
            }
        }

        UpdateCount();
    }

    // ------------------------------------------------------------------ Download

    private async Task DownloadAsync()
    {
        if (_busy)
            return;

        ReadSettingsFromControls();
        if (!EnsureOutputFolder())
            return;

        RefreshTools();
        if (_tools.YtDlp is null || _tools.Ffmpeg is null)
        {
            MessageBox.Show(this, UiText.NeedYtDlpAndFfmpeg, AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_tools.Deno is null)
            _log.AppendMessage(UiText.DenoMissingWarning);

        var targets = _rows.Where(IsChecked).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, UiText.NoVideosSelected, AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveSettings();
        foreach (var row in targets)
            SetStatus(row, RowStatus.Waiting, UiText.StatusWaiting);

        int done = 0, failed = 0, skipped = 0;
        var limitReached = false;
        var cookieError = false;
        await RunBusyAsync(UiText.DownloadRunning, async ct =>
        {
            var library = VideoLibrary.Open(_settings.OutputFolder);
            var job = new DownloadJob(new YtDlpClient(_tools));
            var needsPause = false;

            for (var i = 0; i < targets.Count; i++)
            {
                var row = targets[i];
                if (_settings.SkipExisting && library.IsDownloaded(row.Entry.Id))
                {
                    SetStatus(row, RowStatus.Skipped, UiText.StatusSkipped);
                    SetChecked(row, false);
                    skipped++;
                    continue;
                }

                if (_settings.MaxVideosPerRun > 0 && done + failed >= _settings.MaxVideosPerRun)
                {
                    limitReached = true;
                    break;
                }

                if (needsPause && _settings.PauseSeconds > 0)
                {
                    var pause = PauseTiming.Next(_settings.PauseSeconds, _settings.RandomPause);
                    _lblProgress.Text = UiText.PauseProgress(pause);
                    await Task.Delay(pause, ct);
                }

                needsPause = true;
                _lblProgress.Text = UiText.ProgressOf(i + 1, targets.Count, row.Entry.Title ?? row.Entry.Id);
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
                SetStatus(row, RowStatus.Running, UiText.StatusStarting);
                ScrollIntoView(row);
                _log.AppendMessage($"▶ {row.Entry.Id} – {TitleText(row.Entry)}");

                var progress = new Progress<DownloadUpdate>(update => OnDownloadUpdate(row, update));
                DownloadResult result;
                try
                {
                    result = await job.RunAsync(row.Entry, _settings, library, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    SetStatus(row, RowStatus.Canceled, UiText.StatusCanceled);
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    result = DownloadResult.Failed(ex.Message);
                }

                if (result.Success)
                {
                    done++;
                    SetStatus(row, RowStatus.Done, UiText.StatusDone([.. result.Subtitles.Select(s => s.Language)]));
                    SetChecked(row, false);
                    row.Row.Cells[_colTitle.Index].Value = TitleText(row.Entry);
                    _log.AppendMessage(UiText.SavedLog(Path.GetFileName(result.MediaFile)!));
                }
                else
                {
                    failed++;
                    SetStatus(row, RowStatus.Failed, UiText.StatusFailed(result.Error));
                    _log.AppendMessage($"✖ {row.Entry.Id}: {result.Error}");
                    if (result.Error is not null && YtDlpOutput.GetHint(result.Error) is { } hint)
                        _log.AppendMessage("  " + UiText.Tip(hint) + HelpReference(result.Error));

                    // Every further video would fail the same way.
                    if (result.Error is not null && YtDlpOutput.IsCookieError(result.Error))
                    {
                        cookieError = true;
                        break;
                    }
                }
            }
        });

        // Rows that were still waiting when the user cancelled.
        foreach (var row in targets.Where(r => r.Status == RowStatus.Waiting))
            SetStatus(row, RowStatus.New, UiText.StatusNew);

        _log.AppendMessage(UiText.FinishedLog(done, failed, skipped));
        if (limitReached)
        {
            var remaining = targets.Count(r => r.Status == RowStatus.New);
            _log.AppendMessage(UiText.LimitReachedLog(_settings.MaxVideosPerRun, remaining));
        }
        UpdateCount();

        if (cookieError)
            OfferToTurnOffCookies();
    }

    private void OfferToTurnOffCookies()
    {
        _log.AppendMessage(UiText.CookieStopLog);
        var browser = (_cmbCookies.SelectedItem as Choice<string>)?.Value ?? _cmbCookies.Text;
        var answer = MessageBox.Show(this, UiText.CookieStopQuestion(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(browser)),
            AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;

        _cmbCookies.SelectedIndex = 0;
        ReadSettingsFromControls();
        SaveSettings();
        _log.AppendMessage(UiText.CookiesTurnedOff);
    }

    private void OnDownloadUpdate(VideoRow row, DownloadUpdate update)
    {
        if (update.LogLine is not null)
            _log.AppendLine("    " + update.LogLine);

        if (row.Status != RowStatus.Running)
            return;

        if (update.Percent is { } percent)
        {
            _progress.Value = Math.Clamp((int)Math.Round(percent), 0, 100);
            SetStatus(row, RowStatus.Running, $"{update.Phase ?? UiText.PhaseDownloading} {percent:0} %");
        }
        else if (update.Phase is not null)
        {
            SetStatus(row, RowStatus.Running, update.Phase);
        }
    }

    // ------------------------------------------------------------------ Tools

    private void RefreshTools()
    {
        _tools = ToolLocator.Locate(_toolsDirectory, AppContext.BaseDirectory);
        static string Mark(string? path) => path is null ? UiText.ToolMissing : "✓";
        _lblTools.Text = $"yt-dlp {Mark(_tools.YtDlp)}    ffmpeg {Mark(_tools.Ffmpeg)}    Deno {Mark(_tools.Deno)}";
        _lblTools.ForeColor = _tools.IsComplete ? UiStyle.SuccessText : UiStyle.DangerText;
        _btnTools.Text = _tools.IsComplete ? UiText.ToolsUpdate : UiText.ToolsSetUp;
        UiStyle.SetIcon(_btnTools, _tools.IsComplete ? Glyph.Refresh : Glyph.Download, DeviceDpi);
        _tabSettings.Text = _tools.IsComplete ? UiText.TabSettings : UiText.TabSettingsToolsMissing;
        _tabSettings.ImageKey = _tools.IsComplete ? "settings" : "warning";
    }

    private IEnumerable<string> MissingTools()
    {
        if (_tools.YtDlp is null)
            yield return "yt-dlp";
        if (_tools.Ffmpeg is null)
            yield return "ffmpeg";
        if (_tools.Deno is null)
            yield return "Deno";
    }

    private async Task SetUpToolsAsync()
    {
        RefreshTools();
        var missing = MissingTools().ToList();

        if (missing.Count > 0)
        {
            var answer = MessageBox.Show(this,
                UiText.ToolsDownloadQuestion(string.Join(", ", missing), _toolsDirectory, _tools.Ffmpeg is null),
                AppTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK)
                return;
        }

        await RunBusyAsync(UiText.ToolsBusy, async ct =>
        {
            if (missing.Count == 0)
            {
                await UpdateYtDlpAsync(ct);
                await UpdateDenoAsync(ct);
                _settings.LastToolUpdate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                SaveSettings();
                return;
            }

            var installer = new ToolInstaller(Http);
            var progress = new Progress<ToolDownloadProgress>(p =>
            {
                _progress.Style = p.Percent is null ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
                if (p.Percent is { } percent)
                    _progress.Value = Math.Clamp((int)percent, 0, 100);
                _lblProgress.Text = UiText.ToolProgress(p.Tool, p.Received, p.Total);
            });

            if (_tools.YtDlp is null)
            {
                _log.AppendMessage(UiText.DownloadingTool("yt-dlp"));
                await installer.InstallYtDlpAsync(_toolsDirectory, progress, ct);
            }

            if (_tools.Ffmpeg is null)
            {
                _log.AppendMessage(UiText.DownloadingTool("ffmpeg"));
                await installer.InstallFfmpegAsync(_toolsDirectory, progress, ct);
            }

            if (_tools.Deno is null)
            {
                _log.AppendMessage(UiText.DownloadingTool("Deno"));
                await installer.InstallDenoAsync(_toolsDirectory, progress, ct);
            }

            RefreshTools();
            _log.AppendMessage(_tools.IsComplete ? UiText.AllToolsReady : UiText.ToolsStillMissing);
            await LogVersionAsync();
        });
    }

    private async Task UpdateYtDlpAsync(CancellationToken ct)
    {
        if (_tools.YtDlp is null)
            return;

        _log.AppendMessage(UiText.CheckingForUpdates("yt-dlp"));
        var log = new Progress<string>(line => _log.AppendLine("    " + line));
        await new YtDlpClient(_tools).UpdateAsync(line => ((IProgress<string>)log).Report(line), ct);
        await LogVersionAsync();
    }

    private async Task UpdateDenoAsync(CancellationToken ct)
    {
        // Only update the copy Videolader installed; a Deno installed by the user is left alone.
        if (_tools.Deno is null || !IsInside(_tools.Deno, _toolsDirectory))
            return;

        _log.AppendMessage(UiText.CheckingForUpdates("Deno"));
        var log = new Progress<string>(line => _log.AppendLine("    " + line));
        await ProcessRunner.RunAsync(_tools.Deno, ["upgrade"], line => ((IProgress<string>)log).Report(line), _tools.CreateEnvironment(), ct);
    }

    private async Task LogVersionAsync()
    {
        if (_tools.YtDlp is null)
            return;
        try
        {
            var version = await new YtDlpClient(_tools).GetVersionAsync();
            _log.AppendMessage(UiText.YtDlpVersion(version));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _log.AppendMessage(UiText.YtDlpCannotStart(ex.Message));
        }
    }

    // ------------------------------------------------------------------ Busy state

    private async Task RunBusyAsync(string label, Func<CancellationToken, Task> work)
    {
        if (_busy)
            return;

        _cts = new CancellationTokenSource();
        SetBusy(true);
        _lblProgress.Text = label;
        _progress.Style = ProgressBarStyle.Marquee;
        try
        {
            await work(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.AppendMessage(UiText.Cancelled);
        }
        catch (Exception ex)
        {
            _log.AppendMessage(UiText.Error(ex.Message));
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            _lblProgress.Text = string.Empty;
            if (_closeRequested)
                BeginInvoke(Close);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (var control in new Control[]
                 {
                     _txtInput, _btnLoad, _btnClearInput, _txtFolder, _btnBrowse, _cmbQuality, _cmbCookies,
                     _chkSubtitles, _chkSkip, _numPause, _chkRandomPause, _numMaxVideos, _txtApiKey, _btnTools, _cmbUiLanguage, _btnAll, _btnNone, _btnOnlyNew, _btnDownload,
                 })
        {
            control.Enabled = !busy;
        }

        _txtLanguages.Enabled = !busy && _chkSubtitles.Checked;
        _colCheck.ReadOnly = busy;
        _btnCancel.Enabled = busy;
        UseWaitCursor = false;
    }

    // ------------------------------------------------------------------ Settings

    private void LoadSettingsIntoControls()
    {
        _txtInput.Text = _settings.LastInput;
        _txtFolder.Text = _settings.OutputFolder.Length > 0
            ? _settings.OutputFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videolader");
        _cmbQuality.SelectedItem = _cmbQuality.Items.Cast<Choice<VideoQuality>>().First(c => c.Value == _settings.Quality);
        _cmbCookies.SelectedItem = _cmbCookies.Items.Cast<Choice<string>>().FirstOrDefault(c => c.Value == _settings.CookiesBrowser)
                                   ?? _cmbCookies.Items[0];
        _chkSubtitles.Checked = _settings.DownloadSubtitles;
        _txtLanguages.Text = _settings.SubtitleLanguages;
        _chkSkip.Checked = _settings.SkipExisting;
        _numPause.Value = _settings.PauseSeconds;
        _chkRandomPause.Checked = _settings.RandomPause;
        _numMaxVideos.Value = _settings.MaxVideosPerRun;
        _txtApiKey.Text = _settings.ApiKey;
        _cmbUiLanguage.SelectedItem = _cmbUiLanguage.Items.Cast<Choice<UiLanguage>>().First(c => c.Value == UiText.Language);
        SetLogVisible(_settings.ShowLog);
    }

    private void SetLogVisible(bool visible)
    {
        _split.Panel2Collapsed = !visible;
        _btnToggleLog.Text = visible ? UiText.HideLog : UiText.ShowLog;
        UiStyle.SetIcon(_btnToggleLog, visible ? Glyph.ChevronDown : Glyph.ChevronUp, DeviceDpi);
        _settings.ShowLog = visible;
    }

    private void ReadSettingsFromControls()
    {
        _settings.LastInput = _txtInput.Text;
        _settings.OutputFolder = _txtFolder.Text.Trim().Trim('"');
        _settings.Quality = (_cmbQuality.SelectedItem as Choice<VideoQuality>)?.Value ?? VideoQuality.Max1080;
        _settings.CookiesBrowser = (_cmbCookies.SelectedItem as Choice<string>)?.Value ?? string.Empty;
        _settings.DownloadSubtitles = _chkSubtitles.Checked;
        _settings.SubtitleLanguages = _txtLanguages.Text;
        _settings.SkipExisting = _chkSkip.Checked;
        _settings.PauseSeconds = (int)_numPause.Value;
        _settings.RandomPause = _chkRandomPause.Checked;
        _settings.MaxVideosPerRun = (int)_numMaxVideos.Value;
        _settings.ApiKey = _txtApiKey.Text;
        _settings.Normalize();
        _txtLanguages.Text = _settings.SubtitleLanguages;
    }

    private void SaveSettings()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (IOException ex)
        {
            _log.AppendMessage(UiText.SettingsNotSaved(ex.Message));
        }
    }

    // ------------------------------------------------------------------ Folder

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = UiText.ChooseFolderTitle,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        if (Directory.Exists(_txtFolder.Text))
            dialog.InitialDirectory = _txtFolder.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtFolder.Text = dialog.SelectedPath;
            OnFolderChanged();
        }
    }

    private void OnFolderChanged()
    {
        var folder = _txtFolder.Text.Trim().Trim('"');
        if (folder == _settings.OutputFolder)
            return;

        ReadSettingsFromControls();
        SaveSettings();
        if (!_busy)
            ApplyLibraryStatus();
    }

    private bool EnsureOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            BrowseFolder();
            ReadSettingsFromControls();
            if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
                return false;
        }

        try
        {
            Directory.CreateDirectory(_settings.OutputFolder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(this, UiText.FolderCannotBeCreated(ex.Message), AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void OpenFolder()
    {
        var folder = _txtFolder.Text.Trim().Trim('"');
        if (Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private static VideoLibrary? TryOpenLibrary(string folder)
    {
        folder = folder.Trim().Trim('"');
        if (folder.Length == 0)
            return null;
        try
        {
            return VideoLibrary.Open(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ Grid helpers

    private IEnumerable<VideoRow> SelectedRows() =>
        _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<VideoRow>().OrderBy(r => r.Row.Index);

    private bool IsChecked(VideoRow row) => row.Row.Cells[_colCheck.Index].Value is true;

    private void SetChecked(VideoRow row, bool value) => row.Row.Cells[_colCheck.Index].Value = value;

    private void SetChecked(Func<VideoRow, bool> predicate)
    {
        foreach (var row in _rows)
            SetChecked(row, row.Status != RowStatus.Unavailable && predicate(row));
        UpdateCount();
    }

    private void SetCheckedForSelection(bool value)
    {
        if (_busy)
            return;
        foreach (var row in SelectedRows())
            SetChecked(row, value && row.Status != RowStatus.Unavailable);
        UpdateCount();
    }

    private void SetStatus(VideoRow row, RowStatus status, string text)
    {
        row.Status = status;
        var cell = row.Row.Cells[_colStatus.Index];
        cell.Value = text;
        cell.ToolTipText = text;
        cell.Style.ForeColor = status switch
        {
            RowStatus.Done => UiStyle.SuccessText,
            RowStatus.Running or RowStatus.Waiting => UiStyle.InfoText,
            RowStatus.Existing or RowStatus.Skipped => UiStyle.MutedText,
            RowStatus.Failed => UiStyle.DangerText,
            RowStatus.Canceled => UiStyle.WarningText,
            RowStatus.Unavailable => UiStyle.DisabledText,
            _ => Color.Empty,
        };
        row.Row.DefaultCellStyle.ForeColor = status == RowStatus.Unavailable ? UiStyle.DisabledText : Color.Empty;
    }

    private void ScrollIntoView(VideoRow row)
    {
        try
        {
            if (!row.Row.Displayed)
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Row.Index - 3);
        }
        catch (InvalidOperationException)
        {
            // Grid too small to scroll; not important.
        }
    }

    private void UpdateCount()
    {
        var selected = _rows.Count(IsChecked);
        var existing = _rows.Count(r => r.Status is RowStatus.Existing or RowStatus.Done or RowStatus.Skipped);
        _lblCount.Text = _rows.Count == 0
            ? UiText.NoListLoaded
            : UiText.ListSummary(_rows.Count, selected, existing);
    }

    private void OpenSelectedInBrowser()
    {
        foreach (var row in SelectedRows().Take(10))
            Process.Start(new ProcessStartInfo(row.Entry.Url) { UseShellExecute = true });
    }

    private void CopySelected(Func<VideoRow, string> text)
    {
        var lines = SelectedRows().Select(text).ToList();
        if (lines.Count > 0)
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    // ------------------------------------------------------------------ Small helpers

    private static string TitleText(VideoEntry entry) => entry.Title ?? UiText.TitleUnknown;

    internal static string FormatDuration(TimeSpan? duration) => duration is not { } d
        ? string.Empty
        : d.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{d.Minutes}:{d.Seconds:00}");

    private static bool IsInside(string file, string folder) =>
        Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateHttpClient()
    {
        // No overall timeout: ffmpeg is ~190 MB; every call can be cancelled instead.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Videolader", "1.0"));
        return client;
    }

    private static DataGridViewTextBoxColumn TextColumn(string header, int width)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (width > 0)
            column.Width = width;
        return column;
    }

    private static GroupBox NewGroup(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(8, 6, 8, 8),
        Margin = new Padding(0, 0, 0, 6),
    };

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        panel.Controls.AddRange(controls);
        return panel;
    }

    /// <summary>The control line with a hint below it (instead of beside it, where it would be cut off in narrow windows).</summary>
    private TableLayoutPanel WithHint(Control line, string hint)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        panel.Controls.Add(line, 0, 0);
        var label = UiStyle.CreateHint(hint);
        label.MaximumSize = new Size(LogicalToDeviceUnits(620), 0); // wraps long hints
        panel.Controls.Add(label, 0, 1);
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control content)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = UiStyle.CreateLabel(label);
        caption.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        table.Controls.Add(caption, 0, row);
        table.Controls.Add(content, 1, row);
    }

    private sealed class VideoRow(VideoEntry entry, DataGridViewRow row)
    {
        public VideoEntry Entry { get; } = entry;

        public DataGridViewRow Row { get; } = row;

        public RowStatus Status { get; set; } = RowStatus.New;
    }

    /// <summary>Combo box item with a display text.</summary>
    private sealed record Choice<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }
}
