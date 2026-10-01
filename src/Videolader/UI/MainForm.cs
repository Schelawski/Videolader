using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using Videolader.Core;

namespace Videolader.UI;

/// <summary>
/// The only window: input box for links/IDs, settings, the video list and the log.
/// Workflow: paste links → "Liste laden" → check the videos → "Herunterladen".
/// </summary>
internal sealed class MainForm : Form
{
    public const string AppTitle = "Videolader";

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly SettingsStore _store = new(new DpapiSecretProtector());
    private readonly AppSettings _settings;
    private readonly string _toolsDirectory;
    private readonly List<VideoRow> _rows = [];
    private ToolSet _tools;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _closeRequested;

    // Input
    private readonly TextBox _txtInput = new();
    private readonly Button _btnLoad = UiStyle.CreateButton("Liste laden");
    private readonly Button _btnClearInput = UiStyle.CreateButton("Leeren");

    // Settings
    private readonly TextBox _txtFolder = new() { Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 3) };
    private readonly Button _btnBrowse = UiStyle.CreateButton("Durchsuchen…");
    private readonly Button _btnOpenFolder = UiStyle.CreateButton("Öffnen");
    private readonly ComboBox _cmbQuality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ComboBox _cmbCookies = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly CheckBox _chkSubtitles = new() { Text = "herunterladen", AutoSize = true, Margin = new Padding(3, 5, 12, 3) };
    private readonly TextBox _txtLanguages = new() { Width = 110 };
    private readonly CheckBox _chkSkip = new() { Text = "Bereits heruntergeladene überspringen", AutoSize = true, Margin = new Padding(3, 5, 18, 3) };
    private readonly NumericUpDown _numPause = new() { Width = 60, Minimum = 0, Maximum = AppSettings.MaxPauseSeconds };
    private readonly TextBox _txtApiKey = new() { Width = 330, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowKey = new() { Text = "anzeigen", AutoSize = true, Margin = new Padding(6, 5, 12, 3) };
    private readonly Label _lblTools = UiStyle.CreateLabel(string.Empty);
    private readonly Button _btnTools = UiStyle.CreateButton("Einrichten…");

    // List
    private readonly DataGridView _grid = new();
    private readonly DataGridViewCheckBoxColumn _colCheck = new() { HeaderText = string.Empty, Width = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
    private readonly DataGridViewTextBoxColumn _colNr = TextColumn("Nr.", 44);
    private readonly DataGridViewTextBoxColumn _colId = TextColumn("ID", 110);
    private readonly DataGridViewTextBoxColumn _colTitle = TextColumn("Titel", 0);
    private readonly DataGridViewTextBoxColumn _colDuration = TextColumn("Dauer", 70);
    private readonly DataGridViewTextBoxColumn _colPlaylist = TextColumn("Playlist", 170);
    private readonly DataGridViewTextBoxColumn _colStatus = TextColumn("Status", 190);
    private readonly Button _btnAll = UiStyle.CreateButton("Alle");
    private readonly Button _btnNone = UiStyle.CreateButton("Keine");
    private readonly Button _btnOnlyNew = UiStyle.CreateButton("Nur neue");
    private readonly Label _lblCount = UiStyle.CreateHint(string.Empty);
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
    private readonly LogBox _log = new();

    // Actions
    private readonly Button _btnDownload = UiStyle.CreateButton("Herunterladen");
    private readonly Button _btnCancel = UiStyle.CreateButton("Abbrechen");
    private readonly ProgressBar _progress = new() { Width = 260, Height = 20, Margin = new Padding(12, 8, 6, 3) };
    private readonly Label _lblProgress = UiStyle.CreateHint(string.Empty);

    public MainForm()
    {
        _settings = _store.Load();
        _toolsDirectory = ToolLocator.GetToolsDirectory(AppContext.BaseDirectory);
        _tools = ToolLocator.Locate(_toolsDirectory, AppContext.BaseDirectory);

        Text = AppTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 840);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 1000);
        Size = new Size(Math.Min(1180, area.Width), Math.Min(920, area.Height));

        BuildLayout();
        LoadSettingsIntoControls();
        WireEvents();
        UpdateCount();
        SetBusy(false);

        Shown += OnShown;
        FormClosing += OnFormClosing;
    }

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
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        // Fixed to the window width: content wraps instead of widening the column and being cut off.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildInputGroup(), 0, 0);
        root.Controls.Add(BuildSettingsGroup(), 0, 1);
        root.Controls.Add(BuildListArea(), 0, 2);
        root.Controls.Add(BuildActionBar(), 0, 3);

        Controls.Add(root);
    }

    private GroupBox BuildInputGroup()
    {
        var group = NewGroup("Videos und Playlists");
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _txtInput.Multiline = true;
        _txtInput.AcceptsReturn = true;
        _txtInput.ScrollBars = ScrollBars.Vertical;
        _txtInput.Dock = DockStyle.Fill;
        _txtInput.MinimumSize = new Size(0, 72);
        _txtInput.Font = UiStyle.CreateMonospaceFont(9.5f);
        _txtInput.PlaceholderText = "https://www.youtube.com/playlist?list=PL…\r\nhttps://youtu.be/dQw4w9WgXcQ\r\ndQw4w9WgXcQ";

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 0) };
        UiStyle.MakePrimary(_btnLoad);
        _btnLoad.MinimumSize = new Size(120, 32);
        _btnClearInput.MinimumSize = new Size(120, 30);
        buttons.Controls.Add(_btnLoad);
        buttons.Controls.Add(_btnClearInput);

        table.Controls.Add(_txtInput, 0, 0);
        table.Controls.Add(buttons, 1, 0);
        table.Controls.Add(UiStyle.CreateHint(
            "Ein Eintrag pro Zeile: Video-, Playlist- oder Kanal-Links bzw. IDs. Strg+Enter lädt die Liste."), 0, 1);
        table.SetColumnSpan(table.GetControlFromPosition(0, 1)!, 2);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildSettingsGroup()
    {
        var group = NewGroup("Einstellungen");
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

        _cmbCookies.Items.AddRange(
        [
            new Choice<string>(string.Empty, "keine"),
            new Choice<string>("firefox", "Firefox"),
            new Choice<string>("chrome", "Chrome"),
            new Choice<string>("edge", "Edge"),
            new Choice<string>("brave", "Brave"),
        ]);

        AddRow(table, "Zielordner:", folderRow);
        AddRow(table, "Qualität:", WithHint(Flow(
            _cmbQuality,
            UiStyle.CreateLabel("Cookies aus Browser:"), _cmbCookies),
            "Cookies nur nötig, wenn YouTube eine Anmeldung verlangt."));
        AddRow(table, "Untertitel:", WithHint(Flow(
            _chkSubtitles,
            UiStyle.CreateLabel("Sprachen:"), _txtLanguages),
            "z. B. ru,de,en – die erste Sprache gilt auch für die Titel in der Liste."));
        AddRow(table, "Optionen:", Flow(
            _chkSkip,
            UiStyle.CreateLabel("Pause zwischen Downloads:"), _numPause, UiStyle.CreateLabel("s")));
        AddRow(table, "YouTube-API-Key:", WithHint(Flow(
            _txtApiKey, _chkShowKey),
            "Optional – lädt Playlists und Titel schneller, für den Download nicht nötig."));
        AddRow(table, "Werkzeuge:", Flow(_lblTools, _btnTools));

        group.Controls.Add(table);
        return group;
    }

    private Control BuildListArea()
    {
        // Top: toolbar + grid
        var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.Controls.Add(Flow(_btnAll, _btnNone, _btnOnlyNew, _lblCount), 0, 0);
        listPanel.Controls.Add(_grid, 0, 1);

        SetUpGrid();

        var logGroup = NewGroup("Protokoll");
        logGroup.AutoSize = false;
        logGroup.Controls.Add(_log);

        _split.Panel1.Controls.Add(listPanel);
        _split.Panel2.Controls.Add(logGroup);
        _split.Margin = new Padding(0, 6, 0, 0);
        return _split;
    }

    private FlowLayoutPanel BuildActionBar()
    {
        UiStyle.MakePrimary(_btnDownload);
        _btnDownload.MinimumSize = new Size(150, 34);
        _btnCancel.MinimumSize = new Size(110, 34);
        var bar = Flow(_btnDownload, _btnCancel, _progress, _lblProgress);
        bar.Margin = new Padding(0, 8, 0, 0);
        return bar;
    }

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

        _colId.DefaultCellStyle.Font = UiStyle.CreateMonospaceFont();
        _colTitle.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _colTitle.MinimumWidth = 150;
        _colNr.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _colDuration.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.AddRange(_colCheck, _colNr, _colId, _colTitle, _colDuration, _colPlaylist, _colStatus);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Im Browser öffnen", null, (_, _) => OpenSelectedInBrowser());
        menu.Items.Add("IDs kopieren", null, (_, _) => CopySelected(r => r.Entry.Id));
        menu.Items.Add("Links kopieren", null, (_, _) => CopySelected(r => r.Entry.Url));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Markierte auswählen", null, (_, _) => SetCheckedForSelection(true));
        menu.Items.Add("Markierte abwählen", null, (_, _) => SetCheckedForSelection(false));
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
            _log.AppendMessage("Wird abgebrochen…");
        };
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        var maxDistance = _split.Height - _split.Panel2MinSize - _split.SplitterWidth;
        if (maxDistance > _split.Panel1MinSize)
            _split.SplitterDistance = Math.Clamp((int)(_split.Height * 0.62), _split.Panel1MinSize, maxDistance);
        RefreshTools();
        _log.AppendMessage($"Werkzeug-Ordner: {_toolsDirectory}");

        if (!_tools.IsComplete)
        {
            var missing = string.Join(", ", MissingTools());
            _log.AppendMessage($"Es fehlen: {missing}. Klicke bei „Werkzeuge“ auf „Einrichten…“ – sie werden automatisch heruntergeladen.");
            return;
        }

        // yt-dlp must stay current, YouTube changes often. Once a day is enough.
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_settings.LastToolUpdate != today)
        {
            await RunBusyAsync("yt-dlp wird aktualisiert…", async ct =>
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

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            var answer = MessageBox.Show(this, "Es läuft noch ein Vorgang. Abbrechen und beenden?", AppTitle,
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
            _log.AppendMessage($"Nicht erkannt: {invalid}");

        if (parsed.Items.Count == 0)
        {
            MessageBox.Show(this, "Bitte mindestens einen YouTube-Link oder eine Video-/Playlist-ID eingeben.", AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ReadSettingsFromControls();
        SaveSettings();
        RefreshTools();

        await RunBusyAsync("Liste wird geladen…", async ct =>
        {
            var playlists = parsed.Items.Count(i => i.Kind != InputKind.Video);
            var videos = parsed.Items.Count - playlists;
            _log.AppendMessage($"Lade Liste: {videos} Einzelvideo(s), {playlists} Playlist(s)/Kanäle…");

            var ytDlp = _tools.YtDlp is null ? null : new YtDlpClient(_tools);
            var log = new Progress<string>(_log.AppendMessage);
            var resolver = new VideoResolver(Http, ytDlp, _settings.ApiKey, _settings.CookiesBrowser, log,
                SubtitleLanguageList.Split(_settings.SubtitleLanguages).FirstOrDefault());
            var entries = await resolver.ResolveAsync(parsed.Items, ct);

            FillGrid(entries);
            var existing = _rows.Count(r => r.Status == RowStatus.Existing);
            var unavailable = _rows.Count(r => r.Status == RowStatus.Unavailable);
            _log.AppendMessage($"{entries.Count} Videos in der Liste, davon {existing} bereits vorhanden" +
                               (unavailable > 0 ? $" und {unavailable} nicht verfügbar." : "."));
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
                SetStatus(row, RowStatus.Unavailable, "Nicht verfügbar");
                SetChecked(row, false);
            }
            else if (library?.IsDownloaded(row.Entry.Id) == true)
            {
                SetStatus(row, RowStatus.Existing, "Bereits vorhanden");
                SetChecked(row, !_chkSkip.Checked);
            }
            else
            {
                SetStatus(row, RowStatus.New, "Neu");
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
            MessageBox.Show(this, "yt-dlp und ffmpeg werden benötigt. Klicke bei „Werkzeuge“ auf „Einrichten…“.", AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_tools.Deno is null)
            _log.AppendMessage("Warnung: Deno fehlt – YouTube liefert dann oft nur eingeschränkte Formate.");

        var targets = _rows.Where(IsChecked).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Keine Videos ausgewählt. Lade zuerst eine Liste und setze Häkchen.", AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveSettings();
        foreach (var row in targets)
            SetStatus(row, RowStatus.Waiting, "Wartet");

        int done = 0, failed = 0, skipped = 0;
        await RunBusyAsync("Download läuft…", async ct =>
        {
            var library = VideoLibrary.Open(_settings.OutputFolder);
            var job = new DownloadJob(new YtDlpClient(_tools));
            var needsPause = false;

            for (var i = 0; i < targets.Count; i++)
            {
                var row = targets[i];
                if (_settings.SkipExisting && library.IsDownloaded(row.Entry.Id))
                {
                    SetStatus(row, RowStatus.Skipped, "Übersprungen (vorhanden)");
                    SetChecked(row, false);
                    skipped++;
                    continue;
                }

                if (needsPause && _settings.PauseSeconds > 0)
                {
                    _lblProgress.Text = $"Pause {_settings.PauseSeconds} s…";
                    await Task.Delay(TimeSpan.FromSeconds(_settings.PauseSeconds), ct);
                }

                needsPause = true;
                _lblProgress.Text = $"{i + 1} von {targets.Count}: {row.Entry.Title ?? row.Entry.Id}";
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
                SetStatus(row, RowStatus.Running, "Startet…");
                ScrollIntoView(row);
                _log.AppendMessage($"▶ {row.Entry.Id} – {row.Entry.Title ?? "(Titel unbekannt)"}");

                var progress = new Progress<DownloadUpdate>(update => OnDownloadUpdate(row, update));
                DownloadResult result;
                try
                {
                    result = await job.RunAsync(row.Entry, _settings, library, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    SetStatus(row, RowStatus.Canceled, "Abgebrochen");
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    result = DownloadResult.Failed(ex.Message);
                }

                if (result.Success)
                {
                    done++;
                    var subtitles = result.Subtitles.Count > 0
                        ? " + Untertitel " + string.Join(", ", result.Subtitles.Select(s => s.Language))
                        : string.Empty;
                    SetStatus(row, RowStatus.Done, "Fertig" + subtitles);
                    SetChecked(row, false);
                    row.Row.Cells[_colTitle.Index].Value = TitleText(row.Entry);
                    _log.AppendMessage($"✔ {Path.GetFileName(result.MediaFile)} gespeichert.");
                }
                else
                {
                    failed++;
                    SetStatus(row, RowStatus.Failed, "Fehler: " + result.Error);
                    _log.AppendMessage($"✖ {row.Entry.Id}: {result.Error}");
                    if (result.Error is not null && YtDlpOutput.GetHint(result.Error) is { } hint)
                        _log.AppendMessage("  Tipp: " + hint);
                }
            }
        });

        // Rows that were still waiting when the user cancelled.
        foreach (var row in targets.Where(r => r.Status == RowStatus.Waiting))
            SetStatus(row, RowStatus.New, "Neu");

        _log.AppendMessage($"Fertig: {done} heruntergeladen, {failed} Fehler, {skipped} übersprungen.");
        UpdateCount();
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
            SetStatus(row, RowStatus.Running, $"{update.Phase ?? "Lädt"} {percent:0} %");
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
        static string Mark(string? path) => path is null ? "✗ fehlt" : "✓";
        _lblTools.Text = $"yt-dlp {Mark(_tools.YtDlp)}    ffmpeg {Mark(_tools.Ffmpeg)}    Deno {Mark(_tools.Deno)}";
        _lblTools.ForeColor = _tools.IsComplete ? UiStyle.SuccessText : UiStyle.DangerText;
        _btnTools.Text = _tools.IsComplete ? "Aktualisieren" : "Einrichten…";
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
                $"Folgende Werkzeuge werden von GitHub heruntergeladen: {string.Join(", ", missing)}.\n\n" +
                $"Zielordner: {_toolsDirectory}\n" +
                (_tools.Ffmpeg is null ? "ffmpeg ist ca. 190 MB groß.\n" : string.Empty) +
                "\nFortfahren?",
                AppTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK)
                return;
        }

        await RunBusyAsync("Werkzeuge…", async ct =>
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
                _lblProgress.Text = $"{p.Tool}: {p.Received / 1048576.0:0.0} MB" +
                                    (p.Total is > 0 ? $" von {p.Total.Value / 1048576.0:0.0} MB" : string.Empty);
            });

            if (_tools.YtDlp is null)
            {
                _log.AppendMessage("yt-dlp wird heruntergeladen…");
                await installer.InstallYtDlpAsync(_toolsDirectory, progress, ct);
            }

            if (_tools.Ffmpeg is null)
            {
                _log.AppendMessage("ffmpeg wird heruntergeladen…");
                await installer.InstallFfmpegAsync(_toolsDirectory, progress, ct);
            }

            if (_tools.Deno is null)
            {
                _log.AppendMessage("Deno wird heruntergeladen…");
                await installer.InstallDenoAsync(_toolsDirectory, progress, ct);
            }

            RefreshTools();
            _log.AppendMessage(_tools.IsComplete ? "Alle Werkzeuge sind eingerichtet." : "Es fehlen noch Werkzeuge.");
            await LogVersionAsync();
        });
    }

    private async Task UpdateYtDlpAsync(CancellationToken ct)
    {
        if (_tools.YtDlp is null)
            return;

        _log.AppendMessage("Prüfe auf yt-dlp-Updates…");
        var log = new Progress<string>(line => _log.AppendLine("    " + line));
        await new YtDlpClient(_tools).UpdateAsync(line => ((IProgress<string>)log).Report(line), ct);
        await LogVersionAsync();
    }

    private async Task UpdateDenoAsync(CancellationToken ct)
    {
        // Only update the copy Videolader installed; a Deno installed by the user is left alone.
        if (_tools.Deno is null || !IsInside(_tools.Deno, _toolsDirectory))
            return;

        _log.AppendMessage("Prüfe auf Deno-Updates…");
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
            _log.AppendMessage($"yt-dlp Version {version}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _log.AppendMessage("yt-dlp lässt sich nicht starten: " + ex.Message);
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
            _log.AppendMessage("Abgebrochen.");
        }
        catch (Exception ex)
        {
            _log.AppendMessage("Fehler: " + ex.Message);
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
                     _chkSubtitles, _chkSkip, _numPause, _txtApiKey, _btnTools, _btnAll, _btnNone, _btnOnlyNew, _btnDownload,
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
        _txtApiKey.Text = _settings.ApiKey;
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
            _log.AppendMessage("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ Folder

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Ordner für die Videos wählen",
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
            MessageBox.Show(this, "Der Zielordner kann nicht angelegt werden:\n" + ex.Message, AppTitle,
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
            ? "Noch keine Liste geladen."
            : $"{_rows.Count} Videos · {selected} ausgewählt · {existing} vorhanden";
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

    private static string TitleText(VideoEntry entry) => entry.Title ?? "(Titel unbekannt)";

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
    private static TableLayoutPanel WithHint(Control line, string hint)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        panel.Controls.Add(line, 0, 0);
        panel.Controls.Add(UiStyle.CreateHint(hint), 0, 1);
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
