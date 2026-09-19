using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using AWLM.Core;
using AWLM.Utilities;

namespace AWLM.UI
{
    public sealed class AppLockerRulesForm : Form
    {
        private DataGridView _grid;
        private TextBox _filterBox;
        private Label _statusLabel;
        private Button _refreshButton;

        private readonly List<CheckBox> _categoryCheckBoxes = new List<CheckBox>();

        // Tracks the rules currently shown (post-filter), used for CSV export.
        private List<AppLockerRuleInfo> _displayedRules = new List<AppLockerRuleInfo>();

        private readonly string _machineName;
        private readonly bool _isLocalhost;

        public AppLockerRulesForm() : this(null) { }

        public AppLockerRulesForm(string machineName)
        {
            _isLocalhost = string.IsNullOrWhiteSpace(machineName);
            _machineName = _isLocalhost ? Environment.MachineName : machineName.Trim();
            BuildUI();
            LoadRules();
        }

        private void BuildUI()
        {
            Text = $"AppLocker Rules — {_machineName} — Effective Policy";

            Size = new Size(1600, 620);
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Shield;

            // ── Toolbar ─────────────────────────────────────────────────────
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            toolbar.Controls.Add(new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            });

            _filterBox = new TextBox
            {
                Width = 280,
                Margin = new Padding(8, 3, 15, 0)
            };
            _filterBox.ForeColor = SystemColors.GrayText;
            _filterBox.Text = "Type to filter…";
            _filterBox.GotFocus += (s, _) => { if (_filterBox.ForeColor == SystemColors.GrayText) { _filterBox.Text = ""; _filterBox.ForeColor = SystemColors.WindowText; } };
            _filterBox.LostFocus += (s, _) => { if (string.IsNullOrEmpty(_filterBox.Text)) { _filterBox.ForeColor = SystemColors.GrayText; _filterBox.Text = "Type to filter…"; } };
            _filterBox.TextChanged += (s, e) => { if (_filterBox.ForeColor != SystemColors.GrayText) ApplyFilter(); };
            toolbar.Controls.Add(_filterBox);

            // Refresh button
            _refreshButton = new Button { Text = "⟳ Refresh", AutoSize = true, Margin = new Padding(0, 3, 6, 0) };
            _refreshButton.Click += (s, e) => LoadRules();
            toolbar.Controls.Add(_refreshButton);

            // Export button
            var exportBtn = new Button
            {
                Text = "📥 Export to CSV",
                AutoSize = true,
                Margin = new Padding(0, 3, 6, 0)
            };
            exportBtn.Click += ExportToCsv_Click;
            toolbar.Controls.Add(exportBtn);

            // Edit Rules button
            bool isAdmin = StatusChecker.IsProcessElevated();
            bool isDomainMode = StatusChecker.IsDomainMode();
            if (isAdmin && !isDomainMode && _isLocalhost)
            {
                var editRulesBtn = new Button
                {
                    Text = "✏️ Edit AppLocker Rules",
                    AutoSize = true,
                    Margin = new Padding(0, 3, 6, 0)
                };
                editRulesBtn.Click += EditAppLockerRules_Click;
                toolbar.Controls.Add(editRulesBtn);
            }

            // Category visibility checkboxes
            toolbar.Controls.Add(new Label
            {
                Text = "Show:",
                AutoSize = true,
                Margin = new Padding(10, 6, 0, 0)
            });

            foreach (var category in new[] { "Exe", "Dll", "Script", "Msi", "Appx" })
            {
                var cb = new CheckBox
                {
                    Text = category,
                    AutoSize = true,
                    Checked = true,
                    Tag = category,
                    Margin = new Padding(6, 6, 0, 0)
                };
                cb.CheckedChanged += (s, e) => ApplyFilter();
                toolbar.Controls.Add(cb);
                _categoryCheckBoxes.Add(cb);
            }

            // ── Grid ─────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                ColumnHeadersDefaultCellStyle = { Font = new Font(Font, FontStyle.Bold) },
                AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(245, 245, 245) }
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleCollection", HeaderText = "Rule", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EnforcementMode", HeaderText = "Status", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleType", HeaderText = "Type", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "Rule Name", Width = 220 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserOrGroupSid", HeaderText = "User/Group", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PathOrPublisher", HeaderText = "Path / Publisher / Hash" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Exceptions", HeaderText = "Exceptions" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", Width = 220 });

            _grid.Columns["PathOrPublisher"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["PathOrPublisher"].FillWeight = 40;
            _grid.Columns["Exceptions"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["Exceptions"].FillWeight = 30;
            _grid.Columns["Description"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["Description"].FillWeight = 30;

            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.RowTemplate.Height = 22;
            _grid.ColumnHeaderMouseClick += (s, e) =>
            {
                _grid.Sort(_grid.Columns[e.ColumnIndex],
                    _grid.SortOrder == SortOrder.Ascending
                        ? System.ComponentModel.ListSortDirection.Descending
                        : System.ComponentModel.ListSortDirection.Ascending);
            };

            _grid.ShowCellToolTips = true;
            _grid.CellFormatting += Grid_CellFormatting;

            // ── Status bar ───────────────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = SystemColors.ControlLight };
            _statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
            statusBar.Controls.Add(_statusLabel);

            // Order of adding controls matters for Docking! 
            // Add Fill last or use BringToFront/SendToBack.
            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(statusBar);
        }

        // -----------------------------------------------------------------------
        // Data
        // -----------------------------------------------------------------------

        private List<AppLockerRuleInfo> _allRules = new List<AppLockerRuleInfo>();

        private void LoadRules()
        {
            _statusLabel.Text = _isLocalhost ? "Loading rules…" : $"Connecting to {_machineName}…";
            _filterBox.Enabled = false;
            _refreshButton.Enabled = false;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                List<AppLockerRuleInfo> rules = new List<AppLockerRuleInfo>();
                Exception loadError = null;

                try { rules = GetAppLockerRules.GetRules(_isLocalhost ? null : _machineName); }
                catch (Exception ex) { loadError = ex; }

                // Guard against the user closing the form before the load completes.
                if (IsDisposed || !IsHandleCreated) return;

                Invoke((Action)(() =>
                {
                    _filterBox.Enabled = true;
                    _refreshButton.Enabled = true;

                    if (loadError != null)
                    {
                        _statusLabel.Text = "Failed to load rules.";
                        string body = _isLocalhost
                            ? $"Failed to load AppLocker rules:\n\n{loadError.Message}"
                            : $"Failed to read AppLocker rules from '{_machineName}':\n\n{loadError.Message}\n\n"
                              + "Check that:\n"
                              + "  • WinRM (PowerShell Remoting) is enabled on the target\n"
                              + "  • WinRM is allowed through the target's firewall (default port 5985)\n"
                              + "  • Your account has remote management permissions there";

                        MessageBox.Show(body, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        if (!_isLocalhost && _allRules.Count == 0)
                            Close();
                        return;
                    }

                    _allRules = rules;
                    ApplyFilter();
                }));
            });
        }

        private void ApplyFilter()
        {
            string term = _filterBox.ForeColor == SystemColors.GrayText
                ? "" : _filterBox.Text.Trim().ToLowerInvariant();
            bool textFiltering = !string.IsNullOrEmpty(term);

            var visibleCategories = new HashSet<string>(
                _categoryCheckBoxes.Where(c => c.Checked).Select(c => (string)c.Tag),
                StringComparer.OrdinalIgnoreCase);
            bool categoryFiltering = visibleCategories.Count < _categoryCheckBoxes.Count;

            IEnumerable<AppLockerRuleInfo> query = _allRules.Where(r => visibleCategories.Contains(r.RuleCollection ?? ""));
            if (textFiltering)
                query = query.Where(r => RuleMatchesFilter(r, term));

            var toShow = query.ToList();
            PopulateGrid(toShow);

            _statusLabel.Text = (textFiltering || categoryFiltering)
                ? $"Showing {toShow.Count} of {_allRules.Count} rules" + (textFiltering ? $"  (filter: \"{_filterBox.Text.Trim()}\")" : "")
                : $"{_allRules.Count} rule(s) configured";
        }

        private static bool RuleMatchesFilter(AppLockerRuleInfo r, string t) =>
            (r.RuleCollection ?? "").ToLowerInvariant().Contains(t) ||
            (r.EnforcementMode ?? "").ToLowerInvariant().Contains(t) ||
            (r.RuleType ?? "").ToLowerInvariant().Contains(t) ||
            (r.Action ?? "").ToLowerInvariant().Contains(t) ||
            (r.RuleName ?? "").ToLowerInvariant().Contains(t) ||
            (r.UserOrGroupSid ?? "").ToLowerInvariant().Contains(t) ||
            (r.PublisherName ?? "").ToLowerInvariant().Contains(t) ||
            (r.Description ?? "").ToLowerInvariant().Contains(t) ||
            r.Paths.Any(p => p.ToLowerInvariant().Contains(t)) ||
            r.AllExceptions.Any(e => e.ToLowerInvariant().Contains(t));

        /// <summary>
        /// Returns a compact one-line summary for the Exceptions cell.
        /// e.g. "Path: %WINDIR%\*\bash.exe  (+5 more)"
        /// </summary>
        private static string FormatExceptionsCell(AppLockerRuleInfo r)
        {
            var all = r.AllExceptions.ToList();
            if (all.Count == 0) return "";

            string first;
            if (r.PathExceptions.Count > 0)
                first = $"Path: {r.PathExceptions[0]}";
            else if (r.PublisherExceptions.Count > 0)
                first = $"Publisher: {r.PublisherExceptions[0]}";
            else if (r.HashExceptions.Count > 0)
                first = $"Hash: {r.HashExceptions[0]}";
            else
                first = "";

            return all.Count == 1 ? first : $"{first}  (+{all.Count - 1} more)";
        }

        /// <summary>Returns the full newline-separated list used in the tooltip.</summary>
        private static string FormatExceptionsTooltip(AppLockerRuleInfo r)
        {
            var parts = new List<string>();
            foreach (var p in r.PathExceptions)
                parts.Add($"Path: {p}");
            foreach (var p in r.PublisherExceptions)
                parts.Add($"Publisher: {p}");
            foreach (var p in r.HashExceptions)
                parts.Add($"Hash: {p}");
            return string.Join(Environment.NewLine, parts);
        }

        private void PopulateGrid(List<AppLockerRuleInfo> rules)
        {
            _displayedRules = rules;
            _grid.Rows.Clear();
            foreach (var r in rules)
            {
                string pathOrPub = r.PrimaryConditionDisplay();
                string exceptionsCell = FormatExceptionsCell(r);
                string exceptionsTooltip = FormatExceptionsTooltip(r);

                // Publisher tooltip includes version range when present.
                string pathOrPubTooltip = pathOrPub;
                if (!string.IsNullOrEmpty(r.PublisherName) &&
                    (!string.IsNullOrEmpty(r.MinVersion) || !string.IsNullOrEmpty(r.MaxVersion)))
                {
                    pathOrPubTooltip += $"{Environment.NewLine}Version: {r.MinVersion ?? "*"} – {r.MaxVersion ?? "*"}";
                }

                int idx = _grid.Rows.Add(
                    r.RuleCollection,
                    r.EnforcementMode,
                    r.RuleType,
                    r.Action,
                    r.RuleName,
                    SidResolver.ResolveDisplayName(r.UserOrGroupSid),
                    pathOrPub,
                    exceptionsCell,
                    r.Description);

                var row = _grid.Rows[idx];
                row.Cells["PathOrPublisher"].ToolTipText = pathOrPubTooltip;
                row.Cells["Exceptions"].ToolTipText = exceptionsTooltip;
                row.Cells["RuleName"].ToolTipText = r.RuleName ?? "";
                row.Cells["Description"].ToolTipText = r.Description ?? "";

                SetRowColor(row, r.Action);
            }
        }

        // -----------------------------------------------------------------------
        // Row colouring: Allow = green , Deny = red 
        // -----------------------------------------------------------------------

        /// <summary>
        /// Applied once per row during PopulateGrid rather than inside CellFormatting,
        /// which fires O(columns) times per row and would redundantly reapply the same colour.
        /// The CellFormatting handler is still wired up to handle post-sort redraws.
        /// </summary>
        private static void SetRowColor(DataGridViewRow row, string action)
        {
            row.DefaultCellStyle.BackColor =
                string.Equals(action, "Allow", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(220, 255, 220) :
                string.Equals(action, "Deny", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(255, 220, 220) :
                Color.Empty;
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Only re-colour when the grid redraws after a sort; SetRowColor handles the
            // initial population so we only need to act on the first cell of each row.
            if (e.ColumnIndex != 0) return;

            if (_grid.Rows[e.RowIndex].Cells["Action"].Value is string action)
                SetRowColor(_grid.Rows[e.RowIndex], action);
        }

        // -----------------------------------------------------------------------
        // CSV Export
        // -----------------------------------------------------------------------

        private void ExportToCsv_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export AppLocker Rules to CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"AppLockerRules_{_machineName}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var sb = new StringBuilder();

                    // Header
                    sb.AppendLine("Collection,Enforcement,Type,Action,Rule Name,User/Group,Path / Publisher / Hash,Exceptions,Description");

                    // Export from the source data, not the grid cells, so the
                    // Exceptions column contains the full list rather than the
                    // truncated "X (+N more)" cell summary.
                    foreach (var r in _displayedRules)
                    {
                        sb.AppendLine(string.Join(",", new[]
                        {
                            CsvEscape(r.RuleCollection  ?? ""),
                            CsvEscape(r.EnforcementMode ?? ""),
                            CsvEscape(r.RuleType        ?? ""),
                            CsvEscape(r.Action          ?? ""),
                            CsvEscape(r.RuleName        ?? ""),
                            CsvEscape(SidResolver.ResolveDisplayName(r.UserOrGroupSid)),
                            CsvEscape(r.PrimaryConditionDisplay()),
                            CsvEscape(FormatExceptionsTooltip(r)),  // full list
                            CsvEscape(r.Description     ?? "")
                        }));
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);

                    _statusLabel.Text = $"Exported {_displayedRules.Count} rule(s) to {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n\n{ex.Message}",
                        "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>Wraps a value in double-quotes and escapes any embedded quotes.</summary>
        private static string CsvEscape(string value)
        {
            // Normalise the newlines produced by FormatExceptionsTooltip so each
            // rule stays on a single CSV row.
            value = value.Replace("\r\n", " | ").Replace("\r", " | ").Replace("\n", " | ");
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // -----------------------------------------------------------------------
        // Edit AppLocker Rules — launch embedded MMC console
        // -----------------------------------------------------------------------

        private void EditAppLockerRules_Click(object sender, EventArgs e)
        {
            // Show warning first; only proceed if the user acknowledges it.
            using (var warning = new AppLockerEditWarningDialog())
            {
                if (warning.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            LaunchAppLockerMmc();
        }

        private void LaunchAppLockerMmc()
        {
            const string ResourceName = "AWLM.Resources.Addons.AppLockerLocalRules.msc";

            string tempPath = Path.Combine(Path.GetTempPath(), "AppLockerLocalRules.msc");

            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly()
                                        .GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                        throw new InvalidOperationException(
                            $"Embedded console file is missing from this build: {ResourceName}");

                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                        stream.CopyTo(fs);
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mmc.exe",
                    Arguments = $"\"{tempPath}\"",
                    Verb = "runas",          // triggers UAC elevation
                    UseShellExecute = true              // required for Verb = "runas"
                };

                System.Diagnostics.Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 1223)      // ERROR_CANCELLED — user clicked "No" in UAC
            {
                // User cancelled the UAC prompt — silently ignore.
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch the AppLocker MMC console:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
	    /// Simple modal warning shown before the AppLocker MMC console is launched.
	    /// The user must click OK to proceed; closing the window cancels the action.
	    /// </summary>
	    internal sealed class AppLockerEditWarningDialog : Form
        {
            public AppLockerEditWarningDialog()
            {
                Text = "Warning - Editing AppLocker Rules";

                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                Icon = SystemIcons.Warning;

                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(12);

                var layout = new TableLayoutPanel
                {
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 2,
                    Dock = DockStyle.Fill
                };

                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                // Icon
                var icon = new PictureBox
                {
                    Image = SystemIcons.Warning.ToBitmap(),
                    SizeMode = PictureBoxSizeMode.AutoSize,
                    Margin = new Padding(0, 4, 12, 0)
                };

                // Message
                var msg = new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(400, 0), // wrap text
                    Text =
                        "You are about to open the AppLocker MMC console to edit local rules.\r\n\r\n" +
                        "After making your changes, you must export the updated policy and save it to:\r\n\r\n" +
                        "C:\\Windows\\AppLocker\\Policies\\AppLocker-Enable.xml\r\n\r\n" +
                        "Otherwise, SRPManager will overwrite any changes on the next Enable action."
                };

                // Button panel (bottom row)
                var buttonPanel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.RightToLeft,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 12, 0, 0)
                };

                var okBtn = new Button
                {
                    Text = "OK, I understand",
                    DialogResult = DialogResult.OK,
                    AutoSize = true
                };

                buttonPanel.Controls.Add(okBtn);

                layout.Controls.Add(icon, 0, 0);
                layout.Controls.Add(msg, 1, 0);
                layout.SetColumnSpan(buttonPanel, 2);
                layout.Controls.Add(buttonPanel, 0, 1);

                Controls.Add(layout);

                AcceptButton = okBtn;
                CancelButton = okBtn;
            }
        }
    }
}
