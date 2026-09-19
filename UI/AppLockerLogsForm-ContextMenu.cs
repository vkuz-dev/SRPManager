using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AWLM.UI
{
    public sealed partial class AppLockerLogsForm
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FILE EVENTS context menu
        // ═══════════════════════════════════════════════════════════════════════

        private ContextMenuStrip _fileEventContextMenu;

        private void AttachFileEventContextMenu()
        {
            _fileEventContextMenu = new ContextMenuStrip();

            var openFolder = new ToolStripMenuItem("Open Containing Folder");
            var sep1 = new ToolStripSeparator();
            var copyPath = new ToolStripMenuItem("Copy File Path");
            var copyFqbn = new ToolStripMenuItem("Copy Publisher (FQBN)");
            var copyFullRow = new ToolStripMenuItem("Copy Full Row");
            var sep2 = new ToolStripSeparator();
            var filterUser = new ToolStripMenuItem("Filter by This User");
            var filterFile = new ToolStripMenuItem("Filter by This File");     // Contains filter chip on filename
            var excludeFile = new ToolStripMenuItem("Exclude This File");   // NotContains filter on filename

            _fileEventContextMenu.Items.AddRange(new ToolStripItem[]
            {
                openFolder,
                sep1,
                copyPath,
                copyFqbn,
                copyFullRow,
                sep2,
                filterUser,
                filterFile,
                excludeFile,

            });

            openFolder.Click += (s, e) => OpenContainingFolder();
            copyPath.Click += (s, e) => CopyAdapterCell(_fileEventGrid, _fileAdapter, col: 4);
            copyFullRow.Click += (s, e) => CopyFullRowFromGrid(_fileEventGrid);
            filterUser.Click += (s, e) => FilterByAdapterCell(_fileEventGrid, _fileAdapter, col: 3, displayColumn: "User");

            copyFqbn.Click += (s, e) =>
            {
                var ev = SelectedFileEvent();
                if (ev == null) return;

                if (string.IsNullOrWhiteSpace(ev.Fqbn) || ev.Fqbn == "N/A")
                {
                    MessageBox.Show(
                        "No publisher (FQBN) available for this event.\n\n" +
                        "This is normal for unsigned binaries.",
                        "Copy Publisher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SetClipboard(ev.Fqbn);
            };

            filterFile.Click += (s, e) =>
            {
                var ev = SelectedFileEvent();
                if (ev == null || ev.FilePath == "N/A") return;

                string filename = Path.GetFileName(ev.FilePath);
                if (string.IsNullOrEmpty(filename)) return;

                _activeFilters.Add(new FilterCondition
                {
                    Column = "File Path",
                    Operator = FilterOperator.Contains,
                    Value = filename
                });
                RefreshFilterChips();
                ApplyAllFilters();
            };

            excludeFile.Click += (s, e) =>
            {
                var ev = SelectedFileEvent();
                if (ev == null || ev.FilePath == "N/A") return;

                string filename = Path.GetFileName(ev.FilePath);
                if (string.IsNullOrEmpty(filename)) return;

                _activeFilters.Add(new FilterCondition
                {
                    Column = "File Path",
                    Operator = FilterOperator.NotContains,
                    Value = filename
                });
                RefreshFilterChips();
                ApplyAllFilters();
            };

            _fileEventContextMenu.Opening += (s, e) =>
            {
                var ev = SelectedFileEvent();
                bool hasRow = ev != null;

                foreach (ToolStripItem item in _fileEventContextMenu.Items)
                    if (item is ToolStripMenuItem mi) mi.Enabled = hasRow;

                if (hasRow)
                {
                    copyFqbn.Enabled = !string.IsNullOrWhiteSpace(ev.Fqbn) && ev.Fqbn != "N/A";
                    filterFile.Enabled = ev.FilePath != "N/A";
                    excludeFile.Enabled = ev.FilePath != "N/A";
                }

                e.Cancel = !hasRow;
            };

            _fileEventGrid.ContextMenuStrip = _fileEventContextMenu;
            _fileEventGrid.MouseDown += Grid_MouseDown_SelectRowOnRightClick;
        }

        // ── Helper: get the backing AppLockerFileEvent for the selected row ────

        private Utilities.AppLockerFileEvent SelectedFileEvent()
        {
            if (_fileEventGrid.SelectedRows.Count == 0) return null;
            int idx = _fileEventGrid.SelectedRows[0].Index;
            return (idx >= 0 && idx < (_fileAdapter?.Count ?? 0))
                ? _fileAdapter.GetItem(idx)
                : null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // APPX EVENTS context menu
        // ═══════════════════════════════════════════════════════════════════════

        private ContextMenuStrip _appxEventContextMenu;

        private void AttachAppxEventContextMenu(DataGridView grid)
        {
            _appxEventContextMenu = new ContextMenuStrip();

            var copyPackage = new ToolStripMenuItem("Copy Package Name");
            var copyFqbn = new ToolStripMenuItem("Copy Publisher (FQBN)");
            var copyFullRow = new ToolStripMenuItem("Copy Full Row");
            var sep1 = new ToolStripSeparator();
            var filterUser = new ToolStripMenuItem("Filter by This User");

            _appxEventContextMenu.Items.AddRange(new ToolStripItem[]
            {
                copyPackage,
                copyFqbn,
                copyFullRow,
                sep1,
                filterUser,
            });

            copyPackage.Click += (s, e) => CopyAdapterCell(_appxEventGrid, _appxAdapter, col: 4);
            copyFullRow.Click += (s, e) => CopyFullRowFromGrid(_appxEventGrid);
            filterUser.Click += (s, e) => FilterByAdapterCell(_appxEventGrid, _appxAdapter, col: 3, displayColumn: "User");

            copyFqbn.Click += (s, e) =>
            {
                var ev = SelectedAppxEvent();
                if (ev == null) return;

                if (string.IsNullOrWhiteSpace(ev.Fqbn) || ev.Fqbn == "N/A")
                {
                    MessageBox.Show(
                        "No publisher (FQBN) available for this event.",
                        "Copy Publisher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SetClipboard(ev.Fqbn);
            };

            _appxEventContextMenu.Opening += (s, e) =>
            {
                var ev = SelectedAppxEvent();
                bool hasRow = ev != null;

                foreach (ToolStripItem item in _appxEventContextMenu.Items)
                    if (item is ToolStripMenuItem mi) mi.Enabled = hasRow;

                if (hasRow)
                    copyFqbn.Enabled = !string.IsNullOrWhiteSpace(ev.Fqbn) && ev.Fqbn != "N/A";

                e.Cancel = !hasRow;
            };

            grid.ContextMenuStrip = _appxEventContextMenu;
            grid.MouseDown += Grid_MouseDown_SelectRowOnRightClick;
        }

        private Utilities.AppLockerAppxEvent SelectedAppxEvent()
        {
            if (_appxEventGrid == null || _appxEventGrid.SelectedRows.Count == 0) return null;
            int idx = _appxEventGrid.SelectedRows[0].Index;
            return (idx >= 0 && idx < (_appxAdapter?.Count ?? 0))
                ? _appxAdapter.GetItem(idx)
                : null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ROW SELECTION ON RIGHT-CLICK
        // ═══════════════════════════════════════════════════════════════════════

        private static void Grid_MouseDown_SelectRowOnRightClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var grid = (DataGridView)sender;
            var hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;

            grid.ClearSelection();
            grid.Rows[hit.RowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[0];
        }

        private static DataGridViewRow SelectedRow(DataGridView grid)
            => grid.SelectedRows.Count > 0 ? grid.SelectedRows[0] : null;

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED ACTIONS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Copies the display value of a specific adapter column for the selected row.</summary>
        private void CopyAdapterCell<T>(
            DataGridView grid,
            VirtualGridAdapter<T> adapter,
            int col)
        {
            if (grid.SelectedRows.Count == 0 || adapter == null) return;
            int idx = grid.SelectedRows[0].Index;
            SetClipboard(adapter.GetDisplayValue(idx, col)?.ToString() ?? string.Empty);
        }

        private void CopyFullRowFromGrid(DataGridView grid)
        {
            var row = SelectedRow(grid);
            if (row == null) return;

            var sb = new StringBuilder();
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!col.Visible) continue;
                if (sb.Length > 0) sb.Append('\t');

                // Virtual grids: cell Value is null; get from the adapter.
                object val = row.Cells[col.Index].Value;
                if (val == null)
                {
                    if (grid == _fileEventGrid)
                        val = _fileAdapter?.GetDisplayValue(row.Index, col.Index);
                    else if (grid == _appxEventGrid)
                        val = _appxAdapter?.GetDisplayValue(row.Index, col.Index);
                }

                sb.Append(val?.ToString() ?? string.Empty);
            }
            SetClipboard(sb.ToString());
        }

        /// <summary>Adds a Contains filter for the selected row's value in the given column.</summary>
        private void FilterByAdapterCell<T>(
            DataGridView grid,
            VirtualGridAdapter<T> adapter,
            int col,
            string displayColumn)
        {
            if (grid.SelectedRows.Count == 0 || adapter == null) return;
            int idx = grid.SelectedRows[0].Index;
            string value = adapter.GetDisplayValue(idx, col)?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(value) || value == "N/A") return;

            _activeFilters.Add(new FilterCondition
            {
                Column = displayColumn,
                Operator = FilterOperator.Contains,
                Value = value
            });

            RefreshFilterChips();
            ApplyAllFilters();
        }

        /// <summary>
        /// Local:  explorer /select highlights the exact file.
        /// Remote: converts C:\path\file.exe to \\HOST\C$\path\file.exe.
        /// </summary>
        private void OpenContainingFolder()
        {
            var ev = SelectedFileEvent();
            if (ev == null) return;

            string filePath = ev.FilePath;
            string targetHost = ev.TargetComputer;

            if (string.IsNullOrWhiteSpace(filePath) || filePath == "N/A")
            {
                MessageBox.Show("No file path available for this event.", "Open Folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (_isLocalhost)
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    string uncPath = ToUncPath(targetHost, filePath);
                    if (uncPath == null)
                    {
                        MessageBox.Show(
                            $"Cannot build a UNC path for:\n{filePath}\n\n" +
                            "Only absolute paths with a drive letter (e.g. C:\\...) are supported.",
                            "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Process.Start("explorer.exe", $"/select,\"{uncPath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open folder:\n\n{ex.Message}\n\n" +
                    (_isLocalhost ? string.Empty :
                        "For remote paths, ensure:\n" +
                        "  • Admin shares (C$, D$, ...) are enabled on the remote machine\n" +
                        "  • Your account has admin rights on that machine\n" +
                        "  • File and Printer Sharing is allowed through the firewall"),
                    "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════════════════════════

        private static string ToUncPath(string host, string localPath)
        {
            if (localPath.Length < 3 ||
                !char.IsLetter(localPath[0]) ||
                localPath[1] != ':' ||
                localPath[2] != '\\')
                return null;

            return $@"\\{host}\{char.ToUpper(localPath[0])}$\{localPath.Substring(3)}";
        }

        private static void SetClipboard(string text)
        {
            try { Clipboard.SetText(string.IsNullOrEmpty(text) ? " " : text); }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy to clipboard:\n{ex.Message}",
                    "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
