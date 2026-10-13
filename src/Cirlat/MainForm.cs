using System.Text;
using Cirlat.Core;

namespace Cirlat;

internal sealed class MainForm : Form
{
    private const string Title = "CIRLAT";

    private readonly ProtectedPaths _prot;

    // izbor
    private readonly ListBox _folders = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended, HorizontalScrollbar = true };
    private readonly Button _add = new() { Text = "Dodaj folder…", AutoSize = true };
    private readonly Button _remove = new() { Text = "Ukloni", AutoSize = true };
    private readonly CheckBox _recursive = new() { Text = "Uključi poddirektorijume", Checked = true, AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly Button _analyze = new() { Text = "Analiziraj", AutoSize = true };
    private readonly CheckBox _suffix = new() { Text = "Konflikte reši sufiksom (2), (3)…", AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };

    // pregled
    private readonly DataGridView _grid = new();
    private readonly ContextMenuStrip _gridMenu = new();

    // dno
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 16 };
    private readonly Label _stats = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
    private readonly Button _execute = new() { Text = "Izvrši preimenovanje", AutoSize = true };
    private readonly Button _undo = new() { Text = "Poništi poslednju operaciju", AutoSize = true };
    private readonly Button _export = new() { Text = "Izvoz izveštaja…", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Prekini", AutoSize = true, Enabled = false };

    private RenamePlan? _plan;
    private List<RenameItem> _view = [];
    private bool _executed;
    private bool _busy;
    private CancellationTokenSource? _cts;

    private const int ColInclude = 0, ColOriginal = 1, ColProposed = 2, ColStatus = 3, ColMessage = 4, ColFolder = 5;

    public MainForm(ProtectedPaths prot)
    {
        _prot = prot;

        Text = "CIRLAT — normalizacija naziva fajlova";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1150, 720);
        MinimumSize = new Size(800, 500);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath); } catch { }

        _kind.Items.AddRange(new object[] { "Fajlovi i folderi", "Samo fajlovi", "Samo folderi" });
        _kind.SelectedIndex = 0;
        _filter.Items.AddRange(new object[] { "Prikaz: promene i problemi", "Prikaz: sve stavke", "Prikaz: samo problemi" });
        _filter.SelectedIndex = 0;

        BuildGrid();
        BuildLayout();
        WireEvents();
        UpdateButtons();
        _stats.Text = "Dodaj jedan ili više foldera i klikni Analiziraj.";
    }

    // ───────────────────────────── raspored ─────────────────────────────

    private void BuildLayout()
    {
        var folderButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        folderButtons.Controls.AddRange(new Control[] { _add, _remove });

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 110, ColumnCount = 2, Padding = new Padding(8, 8, 8, 0) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(_folders, 0, 0);
        top.Controls.Add(folderButtons, 1, 0);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 4, 8, 4), WrapContents = true };
        _filter.Margin = new Padding(24, 3, 3, 3);
        options.Controls.AddRange(new Control[]
        {
            _recursive,
            new Label { Text = "Obraditi:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            _kind,
            _analyze,
            _suffix,
            _filter,
        });

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        buttons.Controls.AddRange(new Control[] { _execute, _undo, _export, _cancel });

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(8, 4, 8, 8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_progress, 0, 0);
        bottom.SetColumnSpan(_progress, 2);
        bottom.Controls.Add(_stats, 0, 1);
        bottom.Controls.Add(buttons, 1, 1);

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
        gridHost.Controls.Add(_grid);

        // redosled dodavanja određuje docking: Fill prvi, zatim donji, pa gornji (poslednji je najviše)
        Controls.Add(gridHost);
        Controls.Add(bottom);
        Controls.Add(options);
        Controls.Add(top);
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.VirtualMode = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.ContextMenuStrip = _gridMenu;

        var include = new DataGridViewCheckBoxColumn { HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 32 };
        _grid.Columns.Add(include);
        _grid.Columns.Add(Text("Originalni naziv", 22));
        _grid.Columns.Add(Text("Predloženi naziv", 22));
        _grid.Columns.Add(Text("Status", 10));
        _grid.Columns.Add(Text("Rezultat provere", 22));
        _grid.Columns.Add(Text("Folder", 24));

        _gridMenu.Items.Add("Uključi označene", null, (_, _) => SetIncludeOnSelection(true));
        _gridMenu.Items.Add("Isključi označene", null, (_, _) => SetIncludeOnSelection(false));
        _gridMenu.Items.Add(new ToolStripSeparator());
        _gridMenu.Items.Add("Otvori folder u Exploreru", null, (_, _) => OpenSelectedFolder());

        static DataGridViewTextBoxColumn Text(string header, float weight) => new()
        {
            HeaderText = header, ReadOnly = true, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable,
        };
    }

    private void WireEvents()
    {
        _add.Click += (_, _) => AddFoldersDialog();
        _remove.Click += (_, _) => RemoveSelectedFolders();
        _analyze.Click += async (_, _) => await AnalyzeAsync();
        _execute.Click += async (_, _) => await ExecuteAsync();
        _undo.Click += async (_, _) => await UndoAsync();
        _export.Click += (_, _) => ExportCsv();
        _cancel.Click += (_, _) => _cts?.Cancel();
        _filter.SelectedIndexChanged += (_, _) => RefreshView();
        _recursive.CheckedChanged += (_, _) => InvalidatePlan();
        _kind.SelectedIndexChanged += (_, _) => InvalidatePlan();
        _suffix.CheckedChanged += (_, _) =>
        {
            if (_plan == null || _executed) return;
            _plan.SuffixConflicts = _suffix.Checked;
            _plan.Recompute();
            _grid.Invalidate();
            UpdateStats();
        };

        _folders.AllowDrop = true;
        _folders.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
        };
        _folders.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                foreach (var p in paths.Where(Directory.Exists)) AddFolder(p);
        };
        _folders.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedFolders(); };
        _folders.SelectedIndexChanged += (_, _) => UpdateButtons();

        _grid.CellValueNeeded += Grid_CellValueNeeded;
        _grid.CellValuePushed += Grid_CellValuePushed;
        _grid.CellBeginEdit += (_, e) =>
        {
            if (e.ColumnIndex != ColInclude || e.RowIndex < 0 || e.RowIndex >= _view.Count || !CanToggle(_view[e.RowIndex]))
                e.Cancel = true;
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell?.ColumnIndex == ColInclude)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == ColFolder) OpenSelectedFolder(); };

        FormClosing += (_, e) =>
        {
            if (!_busy) return;
            e.Cancel = true;
            _cts?.Cancel();
            MessageBox.Show(this, "Operacija je u toku. Prekinuta je; zatvori prozor kad se završi.", Title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        Shown += (_, _) => UpdateButtons();
    }

    // ───────────────────────────── izbor foldera ─────────────────────────────

    private void AddFoldersDialog()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Izaberi folder(e) za obradu",
            UseDescriptionForTitle = true,
            Multiselect = true,
            ShowNewFolderButton = false,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        foreach (var p in dlg.SelectedPaths) AddFolder(p);
    }

    private void AddFolder(string raw)
    {
        string path;
        try { path = PathResolver.Canonical(raw); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Putanja nije ispravna:\n{raw}\n\n{ex.Message}", Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var p = _prot.Classify(path);
        if (p.Kind == ProtectionKind.Blocked)
        {
            MessageBox.Show(this,
                $"Ova lokacija je zaštićena ({p.Reason}).\nCIRLAT nad njom i nad njenim sadržajem ne radi ništa.\n\n{path}",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_folders.Items.Cast<string>().Any(f => PathResolver.Equal(f, path))) return;
        _folders.Items.Add(path);
        _folders.ClearSelected();
        _folders.SelectedIndex = _folders.Items.Count - 1;
        InvalidatePlan();
        UpdateButtons();
        UpdateStats();
    }

    private void RemoveSelectedFolders()
    {
        if (_busy) return;
        var selected = _folders.SelectedItems.Cast<string>().ToList();
        if (selected.Count == 0 && _folders.Items.Count == 1)
            selected.Add((string)_folders.Items[0]);
        foreach (var s in selected) _folders.Items.Remove(s);
        InvalidatePlan();
        UpdateButtons();
        UpdateStats();
    }

    // ───────────────────────────── analiza ─────────────────────────────

    private async Task AnalyzeAsync()
    {
        if (_folders.Items.Count == 0)
        {
            MessageBox.Show(this, "Dodaj bar jedan folder.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var opt = new ScanOptions
        {
            Roots = _folders.Items.Cast<string>().ToList(),
            Recursive = _recursive.Checked,
            Kind = (ItemKind)_kind.SelectedIndex,
        };
        bool suffix = _suffix.Checked;

        InvalidatePlan();
        SetBusy(true, marquee: true);
        _stats.Text = "Analiza…";
        var progress = new Progress<int>(n => _stats.Text = $"Analiza… pregledano {n:N0} stavki");

        try
        {
            var token = _cts!.Token;
            var plan = await Task.Run(() => new RenamePlan(Scanner.Scan(opt, _prot, progress, token), suffix), token);
            _plan = plan;
            _executed = false;
        }
        catch (OperationCanceledException)
        {
            _stats.Text = "Analiza je prekinuta.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Analiza nije uspela:\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }

        RefreshView();
        UpdateStats();
    }

    // ───────────────────────────── izvršavanje ─────────────────────────────

    private async Task ExecuteAsync()
    {
        if (_plan == null || _executed) return;
        int count = _plan.WillRename.Count();
        if (count == 0)
        {
            MessageBox.Show(this, "Nema uključenih stavki koje mogu da se preimenuju.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Biće preimenovano {count:N0} stavki.\n\n" +
            "Sadržaj fajlova se ne menja, ali preimenovanje može da pokvari:\n" +
            " • prečice (.lnk) koje pokazuju na ove fajlove,\n" +
            " • linkove između Excel/Word dokumenata,\n" +
            " • listu nedavno otvorenih fajlova u Office-u.\n\n" +
            "Operacija se beleži i može se poništiti. Nastaviti?",
            Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        SetBusy(true, marquee: false);
        _progress.Maximum = count;
        var progress = new Progress<(int Done, int Total)>(p =>
        {
            _progress.Value = Math.Min(p.Done, _progress.Maximum);
            _stats.Text = $"Preimenovanje… {p.Done:N0} / {p.Total:N0}";
        });

        try
        {
            var token = _cts!.Token;
            await Task.Run(() => Executor.Execute(_plan, progress, token));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Greška pri izvršavanju:\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _executed = true;
            SetBusy(false);
        }

        RefreshView();
        UpdateStats();

        int done = _plan.Items.Count(i => i.Status == ItemStatus.Done);
        int failed = _plan.Items.Count(i => i.Status == ItemStatus.Failed);
        MessageBox.Show(this,
            $"Preimenovano: {done:N0}\nNeuspešno: {failed:N0}" +
            (count - done - failed > 0 ? $"\nNije obrađeno (prekinuto): {count - done - failed:N0}" : "") +
            (failed > 0 ? "\n\nNeuspešne stavke su označene u tabeli (Prikaz: samo problemi)." : ""),
            Title, MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private async Task UndoAsync()
    {
        var journal = Journal.LatestUndoable();
        if (journal == null)
        {
            MessageBox.Show(this, "Nema operacije koja može da se poništi.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateButtons();
            return;
        }

        int n = journal.Read().Count;
        var when = File.GetLastWriteTime(journal.FilePath);
        if (MessageBox.Show(this,
                $"Vratiti originalne nazive za {n:N0} stavki iz operacije od {when:dd.MM.yyyy HH:mm}?",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        SetBusy(true, marquee: false);
        _progress.Maximum = Math.Max(n, 1);
        var progress = new Progress<(int Done, int Total)>(p =>
        {
            _progress.Value = Math.Min(p.Done, _progress.Maximum);
            _stats.Text = $"Poništavanje… {p.Done:N0} / {p.Total:N0}";
        });

        UndoResult? result = null;
        try
        {
            var token = _cts!.Token;
            result = await Task.Run(() => Executor.Undo(journal, progress, token));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Greška pri poništavanju:\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }

        InvalidatePlan();
        if (result == null) return;

        var sb = new StringBuilder($"Vraćeno: {result.Restored:N0}\nNije vraćeno: {result.Failures.Count:N0}");
        if (result.Failures.Count > 0)
        {
            sb.Append("\n\n");
            foreach (var f in result.Failures.Take(15)) sb.Append(f).Append('\n');
            if (result.Failures.Count > 15) sb.Append($"… i još {result.Failures.Count - 15}\n");
            sb.Append("\nNevraćene stavke ostaju u dnevniku; poništavanje se može ponoviti.");
        }
        _stats.Text = "Poništavanje završeno. Pokreni ponovo analizu za novi pregled.";
        MessageBox.Show(this, sb.ToString(), Title, MessageBoxButtons.OK,
            result.Failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    // ───────────────────────────── izveštaj ─────────────────────────────

    private void ExportCsv()
    {
        if (_plan == null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"cirlat-izvestaj-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("Folder;Originalni naziv;Predloženi naziv;Status;Rezultat provere;Uključeno");
        foreach (var i in _plan.Items.OrderBy(i => i.ParentDir, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine(string.Join(";", Q(i.ParentDir), Q(i.Name), Q(i.Proposed), Q(NameRules.Label(i.Status)), Q(i.Message), i.Include ? "da" : "ne"));

        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); // BOM: Excel ispravno čita ćirilicu
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Izveštaj nije sačuvan:\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ───────────────────────────── tabela ─────────────────────────────

    private void Grid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _view.Count) return;
        var it = _view[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            ColInclude => it.Include && it.CanRename && !_executed,
            ColOriginal => it.Name,
            ColProposed => it.IsListingError ? "" : it.Proposed,
            ColStatus => NameRules.Label(it.Status),
            ColMessage => it.Message,
            ColFolder => it.ParentDir,
            _ => null,
        };
    }

    private void Grid_CellValuePushed(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (_plan == null || e.ColumnIndex != ColInclude || e.RowIndex < 0 || e.RowIndex >= _view.Count) return;
        var it = _view[e.RowIndex];
        if (!CanToggle(it)) return;
        it.Include = e.Value is true;
        _plan.Recompute();
        _grid.Invalidate();
        UpdateStats();
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _view.Count || e.CellStyle == null) return;
        var it = _view[e.RowIndex];
        e.CellStyle.BackColor = it.Status switch
        {
            ItemStatus.Conflict => Color.FromArgb(255, 222, 222),
            ItemStatus.Invalid => Color.FromArgb(255, 244, 206),
            ItemStatus.AccessError or ItemStatus.Failed => Color.FromArgb(255, 205, 205),
            ItemStatus.Done => Color.FromArgb(222, 245, 222),
            _ => SystemColors.Window,
        };
        if (it.Status == ItemStatus.Unchanged || (!it.Include && it.Status is ItemStatus.Ready))
            e.CellStyle.ForeColor = SystemColors.GrayText;
    }

    private bool CanToggle(RenameItem it) => _plan != null && !_executed && !_busy && it.CanRename;

    private void SetIncludeOnSelection(bool include)
    {
        if (_plan == null || _executed || _busy) return;
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.Index < 0 || row.Index >= _view.Count) continue;
            var it = _view[row.Index];
            if (it.CanRename) it.Include = include;
        }
        _plan.Recompute();
        _grid.Invalidate();
        UpdateStats();
    }

    private void OpenSelectedFolder()
    {
        var row = _grid.CurrentRow;
        if (row == null || row.Index < 0 || row.Index >= _view.Count) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + _view[row.Index].ParentDir + "\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void RefreshView()
    {
        IEnumerable<RenameItem> items = _plan?.Items ?? [];
        items = _filter.SelectedIndex switch
        {
            1 => items,
            2 => items.Where(i => i.Status is ItemStatus.Conflict or ItemStatus.Invalid or ItemStatus.AccessError or ItemStatus.Failed),
            _ => items.Where(i => i.Status != ItemStatus.Unchanged),
        };
        _view = items
            .OrderBy(i => i.ParentDir, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.IsDirectory ? 0 : 1)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _grid.RowCount = 0;
        _grid.RowCount = _view.Count;
        _grid.Invalidate();
        UpdateButtons();
    }

    private void InvalidatePlan()
    {
        if (_plan == null) return;
        _plan = null;
        _executed = false;
        RefreshView();
        UpdateStats();
    }

    // ───────────────────────────── stanje ─────────────────────────────

    private void UpdateStats()
    {
        if (_busy) return;
        if (_plan == null)
        {
            if (!_stats.Text.StartsWith("Poništavanje", StringComparison.Ordinal) && !_stats.Text.StartsWith("Analiza je", StringComparison.Ordinal))
                _stats.Text = _folders.Items.Count == 0 ? "Dodaj jedan ili više foldera i klikni Analiziraj." : "Klikni Analiziraj.";
            return;
        }

        var c = _plan.Counts();
        var parts = new List<string> { $"Stavki: {_plan.Items.Count(i => !i.IsListingError):N0}" };
        if (!_executed) parts.Add($"za preimenovanje: {_plan.WillRename.Count():N0}");
        foreach (var s in Enum.GetValues<ItemStatus>())
            if (c.TryGetValue(s, out int n) && n > 0)
                parts.Add($"{NameRules.Label(s)}: {n:N0}");

        var sc = _plan.Scan;
        if (sc.SkippedProtected + sc.SkippedSystemHidden + sc.SkippedLinks > 0)
            parts.Add($"preskočeno — zaštićeno: {sc.SkippedProtected:N0}, sistemsko: {sc.SkippedSystemHidden:N0}, linkovi: {sc.SkippedLinks:N0}");

        _stats.Text = string.Join("   |   ", parts);
        UpdateButtons();
    }

    private void SetBusy(bool busy, bool marquee = false)
    {
        _busy = busy;
        if (busy)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _progress.Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            _progress.Value = 0;
            UseWaitCursor = true;
        }
        else
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            UseWaitCursor = false;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool idle = !_busy;
        _add.Enabled = idle;
        _remove.Enabled = idle && (_folders.SelectedItems.Count > 0 || _folders.Items.Count == 1);
        _recursive.Enabled = idle;
        _kind.Enabled = idle;
        _suffix.Enabled = idle && !_executed;
        _analyze.Enabled = idle && _folders.Items.Count > 0;
        _execute.Enabled = idle && _plan != null && !_executed && _plan.WillRename.Any();
        _export.Enabled = idle && _plan != null;
        _undo.Enabled = idle && Journal.LatestUndoable() != null;
        _cancel.Enabled = _busy;
    }
}
