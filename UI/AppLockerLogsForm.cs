using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using AWLM.Utilities;

namespace AWLM.UI
{
    public sealed partial class AppLockerLogsForm : Form
    {
        private TabControl _tabControl;
        private DataGridView _fileEventGrid;
        private DataGridView _fileSummaryGrid;
        private DataGridView _appxEventGrid;
        private DataGridView _appxSummaryGrid;
        private Panel _filterPanel;
        private FlowLayoutPanel _filterChipsPanel;
        private TextBox _quickFilterBox;
        private Button _refreshButton;
        private Button _addFilterButton;
        private Button _clearFiltersButton;
        private CheckBox _hideDllCheckBox;
        private CheckBox _hideSystemEventsCheckBox;
        private CheckBox _showCurrentUserCheckBox;
        private Button _exportCsvButton;
        private System.Windows.Forms.Timer _quickFilterDebounce;

        // Backing stores (full unfiltered data from the last load)
        private List<AppLockerFileEvent> _fileEvents = new List<AppLockerFileEvent>();
        private List<AppLockerAppxEvent> _allAppxEvents = new List<AppLockerAppxEvent>();
        private List<FilterCondition> _activeFilters = new List<FilterCondition>();

        // Virtual-mode adapters (created in CreateFileEventGrid / CreateAppxEventGrid)
        internal VirtualGridAdapter<AppLockerFileEvent> _fileAdapter;
        internal VirtualGridAdapter<AppLockerAppxEvent> _appxAdapter;

        // Async load cancellation
        private CancellationTokenSource _loadCts;

        // Shared tooltip for filter chips — one instance services all chips.
        private readonly ToolTip _chipTooltip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };

        private Label _statusFooter;
        private readonly int _timeframeDays;
        private readonly string _machineName;
        private readonly bool _isLocalhost;

        private static readonly Color ColorAllowed = Color.FromArgb(220, 255, 220);
        private static readonly Color ColorBlocked = Color.FromArgb(255, 220, 220);
        private static readonly Color ColorAudit = Color.FromArgb(255, 255, 210);

        // ─── Constructors ──────────────────────────────────────────────────────

        public AppLockerLogsForm(int timeframeDays)
            : this(timeframeDays, null) { }

        public AppLockerLogsForm(int timeframeDays, string machineName)
        {
            _timeframeDays = timeframeDays;
            _isLocalhost = string.IsNullOrWhiteSpace(machineName);
            _machineName = _isLocalhost ? Environment.MachineName : machineName.Trim();
            BuildUI();

            _quickFilterDebounce = new System.Windows.Forms.Timer { Interval = 2000 };
            _quickFilterDebounce.Tick += (s, e) => { _quickFilterDebounce.Stop(); ApplyAllFilters(); };

            // filter out powershell.exe noise by default
            _activeFilters.Add(new FilterCondition
            {
                Column = "File Path",
                Operator = FilterOperator.NotContains,
                Value = "powershell.exe"
            });

            RefreshFilterChips();
            LoadEvents();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _loadCts?.Cancel();
            base.OnFormClosing(e);
        }

        // These are components, not controls, so the base Controls-collection disposal never
        // reaches them. The tray app builds a fresh form on every "Open Logs", so without this
        // they accumulate for the lifetime of the process.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadCts?.Dispose();
                _quickFilterDebounce?.Dispose();
                _chipTooltip?.Dispose();
                _fileEventContextMenu?.Dispose();
                _appxEventContextMenu?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Text = $"AppLocker Events — {_machineName} — Last {_timeframeDays} Day(s)";

            Size = new Size(1400, 750);
            MinimumSize = new Size(1100, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Shield;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            mainLayout.Controls.Add(BuildFilterPanel(), 0, 0);
            mainLayout.Controls.Add(BuildTabControl(), 0, 1);
            mainLayout.Controls.Add(BuildStatusBar(), 0, 2);

            Controls.Add(mainLayout);
        }

        private Panel BuildFilterPanel()
        {
            _filterPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BackColor = SystemColors.ControlLightLight
            };

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                ColumnCount = 2
            };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var leftPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(5, 3, 5, 3)
            };

            leftPanel.Controls.Add(new Label
            {
                Text = "Quick filter:",
                AutoSize = true,
                Margin = new Padding(0, 6, 6, 0)
            });

            _quickFilterBox = new TextBox { Width = 240, Margin = new Padding(0, 3, 6, 0) };
            _quickFilterBox.TextChanged += (s, e) => RestartQuickFilter();
            leftPanel.Controls.Add(_quickFilterBox);

            var filterHelpBtn = new Button
            {
                Text = "?",
                Width = 20,
                Margin = new Padding(0, 3, 6, 0),
                Font = new Font(Font, FontStyle.Bold)
            };
            filterHelpBtn.Click += (s, e) => ShowQuickFilterHelp();
            leftPanel.Controls.Add(filterHelpBtn);

            _addFilterButton = new Button { Text = "+ Add filter", AutoSize = true, Margin = new Padding(0, 3, 6, 0) };
            _addFilterButton.Click += (s, e) => ShowAddFilterDialog();
            leftPanel.Controls.Add(_addFilterButton);

            _clearFiltersButton = new Button { Text = "🗑 Clear filters", AutoSize = true, Margin = new Padding(0, 3, 6, 0) };
            _clearFiltersButton.Click += (s, e) => ClearAllFilters();
            leftPanel.Controls.Add(_clearFiltersButton);

            _refreshButton = new Button { Text = "⟳ Refresh", AutoSize = true, Margin = new Padding(0, 3, 6, 0) };
            _refreshButton.Click += (s, e) => LoadEvents();
            leftPanel.Controls.Add(_refreshButton);

            _showCurrentUserCheckBox = new CheckBox
            {
                Text = "Current User only",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(8, 6, 6, 0)
            };
            _showCurrentUserCheckBox.CheckedChanged += (s, e) => ApplyAllFilters();
            leftPanel.Controls.Add(_showCurrentUserCheckBox);

            _hideSystemEventsCheckBox = new CheckBox
            {
                Text = "Hide System events",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 6, 6, 0)
            };
            _hideSystemEventsCheckBox.CheckedChanged += (s, e) => ApplyAllFilters();
            leftPanel.Controls.Add(_hideSystemEventsCheckBox);

            _hideDllCheckBox = new CheckBox
            {
                Text = "Hide DLL events",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 6, 6, 0)
            };
            _hideDllCheckBox.CheckedChanged += (s, e) => ApplyAllFilters();
            leftPanel.Controls.Add(_hideDllCheckBox);

            var rightPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(5, 3, 5, 3)
            };

            _exportCsvButton = new Button { Text = "📥 Export to CSV", AutoSize = true };
            _exportCsvButton.Click += (s, e) => ExportToCsv();
            rightPanel.Controls.Add(_exportCsvButton);

            container.Controls.Add(leftPanel, 0, 0);
            container.Controls.Add(rightPanel, 1, 0);

            _filterChipsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(2)
            };

            _filterPanel.Controls.Add(_filterChipsPanel);
            _filterPanel.Controls.Add(container);

            return _filterPanel;
        }

        private TabControl BuildTabControl()
        {
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            var fileEventTab = new TabPage("File Events");
            var fileSummaryTab = new TabPage("File Event Summary");
            var appxEventTab = new TabPage("AppX Events");
            var appxSummaryTab = new TabPage("AppX Event Summary");

            _fileEventGrid = CreateFileEventGrid();
            _fileSummaryGrid = CreateFileSummaryGrid();
            AttachFileEventContextMenu();

            _appxEventGrid = CreateAppxEventGrid();   // also calls AttachAppxEventContextMenu()
            _appxSummaryGrid = CreateAppxSummaryGrid();

            fileEventTab.Controls.Add(_fileEventGrid);
            fileSummaryTab.Controls.Add(_fileSummaryGrid);
            appxEventTab.Controls.Add(_appxEventGrid);
            appxSummaryTab.Controls.Add(_appxSummaryGrid);

            _tabControl.TabPages.Add(fileEventTab);
            _tabControl.TabPages.Add(fileSummaryTab);
            _tabControl.TabPages.Add(appxEventTab);
            _tabControl.TabPages.Add(appxSummaryTab);

            _tabControl.SelectedIndexChanged += (s, e) =>
            {
                bool isAppxTab = _tabControl.SelectedIndex == 2 || _tabControl.SelectedIndex == 3;

                _addFilterButton.Enabled = !isAppxTab;
                _clearFiltersButton.Enabled = !isAppxTab;
                _hideDllCheckBox.Enabled = !isAppxTab;
                _hideSystemEventsCheckBox.Enabled = !isAppxTab;

                if (_tabControl.SelectedIndex == 1 && _pendingFileSummaryResult != null)
                {
                    PopulateFileSummaryGrid(GetAppLockerFileLogs.GetSummary(_pendingFileSummaryResult));
                    _pendingFileSummaryResult = null;
                }
                if (_tabControl.SelectedIndex == 3 && _pendingAppxSummaryResult != null)
                {
                    PopulateAppxSummaryGrid(GetAppLockerAppxLogs.GetSummary(_pendingAppxSummaryResult));
                    _pendingAppxSummaryResult = null;
                }

                UpdateStatusBar();
            };

            return _tabControl;
        }

        private Panel BuildStatusBar()
        {
            var bar = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.ControlLight };
            _statusFooter = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            bar.Controls.Add(_statusFooter);
            return bar;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═══════════════════════════════════════════════════════════════════════

        private void LoadEvents()
        {
            // Cancel any in-flight load before starting a new one.
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            _ = LoadEventsAsync(_loadCts.Token);
        }

        private async Task LoadEventsAsync(CancellationToken ct)
        {
            _refreshButton.Enabled = false;
            _statusFooter.Text = _isLocalhost
                ? "Loading events…"
                : $"Connecting to {_machineName}…";

            string targetMachine = _isLocalhost ? null : _machineName;
            int days = _timeframeDays;

            var progress = new Progress<ParseProgress>(p =>
            {
                string shortChannel = p.Channel.Contains("/") ? p.Channel.Split('/')[1] : p.Channel;
                _statusFooter.Text = p.EventsRead == 0
                    ? $"Reading {shortChannel}…"
                    : $"Reading {shortChannel}… ({p.EventsRead} events read)";
            });

            List<AppLockerFileEvent> fileEvents = null;
            List<AppLockerAppxEvent> appxEvents = null;

            try
            {
                fileEvents = await Task.Run(
                    () => GetAppLockerFileLogs.GetEvents(days, targetMachine, ct, progress), ct);

                ct.ThrowIfCancellationRequested();

                appxEvents = await Task.Run(
                    () => GetAppLockerAppxLogs.GetEvents(days, targetMachine, ct, progress), ct);
            }
            catch (OperationCanceledException)
            {
                _refreshButton.Enabled = true;
                _statusFooter.Text = "Load cancelled.";
                return;
            }
            catch (Exception ex)
            {
                if (IsDisposed || !IsHandleCreated) return;

                _refreshButton.Enabled = true;

                string body =
                    $"Failed to read AppLocker logs from '{targetMachine ?? "localhost"}':\n\n{ex.Message}"
                    + (!_isLocalhost
                        ? "\n\nCheck that:\n"
                          + "  • WinRM (PowerShell Remoting) is enabled on the target\n"
                          + "  • WinRM is allowed through the target's firewall (default port 5985)\n"
                          + "  • Your account has remote management permissions there"
                        : string.Empty);

                MessageBox.Show(body, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (!_isLocalhost && _fileEvents.Count == 0)
                    Close();
                else
                    _statusFooter.Text = $"Error: {ex.Message}";

                return;
            }

            _fileEvents = fileEvents;
            _allAppxEvents = appxEvents;
            _refreshButton.Enabled = true;
            ApplyAllFilters();
        }

        private void UpdateStatusBar()
        {
            int displayed = 0;
            int totalRaw = 0;
            string label = "event(s)";

            switch (_tabControl.SelectedIndex)
            {
                case 0:
                    displayed = _fileAdapter?.Count ?? 0;
                    totalRaw = _fileEvents.Count;
                    label = "event(s)";
                    break;
                case 1:
                    displayed = _fileSummaryGrid.Rows.Count;
                    totalRaw = _fileEvents.Count;
                    label = "unique file(s)";
                    break;
                case 2:
                    displayed = _appxAdapter?.Count ?? 0;
                    totalRaw = _allAppxEvents.Count;
                    label = "event(s)";
                    break;
                case 3:
                    displayed = _appxSummaryGrid.Rows.Count;
                    totalRaw = _allAppxEvents.Count;
                    label = "unique package(s)";
                    break;
            }

            bool isFiltered = _activeFilters.Count > 0 || !string.IsNullOrEmpty(_quickFilterBox.Text);

            if (isFiltered && _tabControl.SelectedIndex != 1 && _tabControl.SelectedIndex != 3)
                _statusFooter.Text = $"{displayed} {label} shown (filtered from {totalRaw} total) — {_machineName} — Last {_timeframeDays} day(s)";
            else
                _statusFooter.Text = $"{displayed} {label} — {_machineName} — Last {_timeframeDays} day(s)";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CELL FORMATTING (summary grids only — virtual grids use RowPrePaint)
        // ═══════════════════════════════════════════════════════════════════════

        private void CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;

            var row = grid.Rows[e.RowIndex];
            if (row.Cells["Action"].Value is string action)
                row.DefaultCellStyle.BackColor = ActionColor(action, grid);
        }

        private Color ActionColor(string action, DataGridView grid)
        {
            if (action.Contains("Allowed")) return ColorAllowed;
            if (action.Contains("Audit")) return ColorAudit;
            if (action.Contains("Blocked")) return ColorBlocked;
            return grid.DefaultCellStyle.BackColor;
        }

        // Sorting state for the non-virtual summary grids
        private int _sortColumn = 0;
        private bool _sortAscending = false;

        private void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var grid = (DataGridView)sender;
            _sortAscending = (e.ColumnIndex == _sortColumn) ? !_sortAscending : true;
            _sortColumn = e.ColumnIndex;
            grid.Sort(grid.Columns[e.ColumnIndex],
                _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CSV EXPORT
        // ═══════════════════════════════════════════════════════════════════════

        private async void ExportToCsv()
        {
            int tabIdx = _tabControl.SelectedIndex;
            DataGridView grid = tabIdx switch
            {
                0 => _fileEventGrid,
                1 => _fileSummaryGrid,
                2 => _appxEventGrid,
                _ => _appxSummaryGrid
            };

            int rowCount = tabIdx == 0 ? (_fileAdapter?.Count ?? 0)
                         : tabIdx == 2 ? (_appxAdapter?.Count ?? 0)
                         : grid.Rows.Count;

            if (rowCount == 0)
            {
                MessageBox.Show("There are no rows to export.", "Export to CSV",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string namePart = tabIdx switch
            {
                0 => "FileEvents",
                1 => "FileSummary",
                2 => "AppxEvents",
                _ => "AppxSummary"
            };
            string defaultFileName =
                $"AppLocker_{namePart}_{_machineName}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            using var dlg = new SaveFileDialog
            {
                Title = "Export to CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = defaultFileName,
                OverwritePrompt = true,
                AutoUpgradeEnabled = false
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _exportCsvButton.Enabled = false;
            _statusFooter.Text = "Exporting CSV...";

            try
            {
                // Capture headers
                var headers = new string[grid.Columns.Count];
                for (int i = 0; i < grid.Columns.Count; i++)
                    headers[i] = CsvEscape(grid.Columns[i].HeaderText);

                // For virtual grids read from the adapter; for regular grids read from cells.
                string[][] rows;
                if (tabIdx == 0 && _fileAdapter != null)
                {
                    rows = BuildRowsFromAdapter(_fileAdapter, grid.Columns.Count, rowCount);
                }
                else if (tabIdx == 2 && _appxAdapter != null)
                {
                    rows = BuildRowsFromAdapter(_appxAdapter, grid.Columns.Count, rowCount);
                }
                else
                {
                    rows = BuildRowsFromGrid(grid);
                }

                await Task.Run(() =>
                {
                    // No space after the comma: every field is already quoted by CsvEscape,
                    // and a space would land outside the quotes and break strict parsers.
                    using var writer = new StreamWriter(dlg.FileName, false, new UTF8Encoding(true));
                    writer.WriteLine(string.Join(",", headers));
                    foreach (var row in rows)
                        writer.WriteLine(string.Join(",", row));
                });

                _statusFooter.Text = $"Exported {rows.Length:N0} row(s) successfully.";
                MessageBox.Show(
                    $"Exported {rows.Length:N0} row(s) to:\n{dlg.FileName}",
                    "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write CSV file:\n\n{ex.Message}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusFooter.Text = "Export failed.";
            }
            finally
            {
                _exportCsvButton.Enabled = true;
                UpdateStatusBar();
            }
        }

        private static string[][] BuildRowsFromAdapter<T>(
            VirtualGridAdapter<T> adapter,
            int colCount,
            int rowCount)
        {
            var rows = new string[rowCount][];
            for (int r = 0; r < rowCount; r++)
            {
                var cells = new string[colCount];
                for (int c = 0; c < colCount; c++)
                    cells[c] = CsvEscape(adapter.GetDisplayValue(r, c)?.ToString() ?? string.Empty);
                rows[r] = cells;
            }
            return rows;
        }

        private static string[][] BuildRowsFromGrid(DataGridView grid)
        {
            var rows = new string[grid.Rows.Count][];
            for (int r = 0; r < grid.Rows.Count; r++)
            {
                var cells = new string[grid.Rows[r].Cells.Count];
                for (int c = 0; c < cells.Length; c++)
                    cells[c] = CsvEscape(grid.Rows[r].Cells[c].Value?.ToString() ?? string.Empty);
                rows[r] = cells;
            }
            return rows;
        }

        private static string CsvEscape(string value) =>
            "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public class FilterDialog : Form
    {
        private ComboBox _columnCombo;
        private ComboBox _operatorCombo;
        private TextBox _valueBox;

        public FilterCondition ResultFilter { get; private set; }

        private static readonly string[] Columns =
        {
            "Time", "Event ID", "Action", "User", "File Path",
            "Policy", "Rule", "Computer", "Log"
        };

        public FilterDialog() { BuildUI(); }

        private void BuildUI()
        {
            Text = "Add Filter";

            Size = new Size(400, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 2,
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _columnCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _columnCombo.Items.AddRange(Columns);
            _columnCombo.SelectedIndex = 4;

            _operatorCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _operatorCombo.Items.AddRange(Enum.GetNames(typeof(FilterOperator)));
            _operatorCombo.SelectedIndex = 0;

            _valueBox = new TextBox { Dock = DockStyle.Fill };

            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            var okBtn = new Button
            {
                Text = "OK",
                Width = 80,
                Height = 26,
                Margin = new Padding(4, 0, 0, 0)
            };
            var cancelBtn = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 26,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(4, 0, 0, 0)
            };

            btnFlow.Controls.Add(okBtn);
            btnFlow.Controls.Add(cancelBtn);

            okBtn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_valueBox.Text))
                {
                    MessageBox.Show("Please enter a filter value.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ResultFilter = new FilterCondition
                {
                    Column = _columnCombo.SelectedItem?.ToString(),
                    Operator = (FilterOperator)_operatorCombo.SelectedIndex,
                    Value = _valueBox.Text.Trim()
                };
                DialogResult = DialogResult.OK;
                Close();
            };

            layout.Controls.Add(MakeLabel("Column:"), 0, 0);
            layout.Controls.Add(_columnCombo, 1, 0);
            layout.Controls.Add(MakeLabel("Operator:"), 0, 1);
            layout.Controls.Add(_operatorCombo, 1, 1);
            layout.Controls.Add(MakeLabel("Value:"), 0, 2);
            layout.Controls.Add(_valueBox, 1, 2);
            layout.Controls.Add(btnFlow, 1, 3);

            outer.Controls.Add(layout);
            Controls.Add(outer);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private static Label MakeLabel(string text) =>
            new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 6, 4, 0)
            };
    }
}
