using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using AWLM.Utilities;

namespace AWLM.UI
{
    public sealed partial class AppLockerLogsForm
    {
        private DataGridView CreateAppxEventGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(248, 248, 248) },
                ColumnHeadersDefaultCellStyle = { Font = new Font(Font, FontStyle.Bold) }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TimeCreated", HeaderText = "Time", Width = 155, MinimumWidth = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EventId", HeaderText = "Event ID", Width = 70, MinimumWidth = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", Width = 145, MinimumWidth = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "User", HeaderText = "User", Width = 160, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PackageName", HeaderText = "Package", MinimumWidth = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Publisher", HeaderText = "Publisher", MinimumWidth = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PackageVersion", HeaderText = "Version", Width = 120, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "Rule", MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TargetComputer", HeaderText = "Computer", Width = 110, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LogName", HeaderText = "Log", Width = 100, MinimumWidth = 80 });

            grid.Columns["PackageName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["Publisher"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["RuleName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns["PackageName"].FillWeight = 40;
            grid.Columns["Publisher"].FillWeight = 35;
            grid.Columns["RuleName"].FillWeight = 25;

            _appxAdapter = new VirtualGridAdapter<AppLockerAppxEvent>(
                grid,
                getCellValue: (e, col) => col switch
                {
                    0 => (object)e.TimeCreated.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    1 => e.EventId,
                    2 => e.Action,
                    3 => e.User,
                    4 => e.PackageName,
                    5 => e.Publisher,
                    6 => e.PackageVersion,
                    7 => e.RuleName,
                    8 => e.TargetComputer,
                    9 => AppxLogLabel(e),
                    _ => null
                },
                getRowColor: e => ActionColor(e.Action, grid),
                getSortKey: (e, col) => col switch
                {
                    0 => (IComparable)e.TimeCreated,
                    1 => e.EventId,
                    2 => e.Action,
                    3 => e.User,
                    4 => e.PackageName,
                    5 => e.Publisher,
                    6 => e.PackageVersion,
                    7 => e.RuleName,
                    8 => e.TargetComputer,
                    9 => AppxLogLabel(e),
                    _ => string.Empty
                });

            AttachAppxEventContextMenu(grid);
            return grid;
        }

        /// <summary>Label for the Log column: "AppX-Deployment" or "AppX-Execution".</summary>
        private static string AppxLogLabel(AppLockerAppxEvent e) => $"AppX-{e.Channel}";

        private DataGridView CreateAppxSummaryGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeColumns = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(248, 248, 248) },
                ColumnHeadersDefaultCellStyle = { Font = new Font(Font, FontStyle.Bold) }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PackageName", HeaderText = "Package", MinimumWidth = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", Width = 150, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Count", Width = 80, MinimumWidth = 40 });

            grid.Columns["PackageName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.CellFormatting += CellFormatting;
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            return grid;
        }

        private void PopulateAppxEventGrid(List<AppLockerAppxEvent> events)
        {
            _appxAdapter.SetData(events);
        }

        private void PopulateAppxSummaryGrid(List<AppLockerAppxEventSummary> summary)
        {
            _appxSummaryGrid.Rows.Clear();

            foreach (var s in summary)
            {
                var rowIndex = _appxSummaryGrid.Rows.Add(s.PackageName, s.Action, s.Count);
                var row = _appxSummaryGrid.Rows[rowIndex];
                row.DefaultCellStyle.BackColor = ActionColor(s.Action, _appxSummaryGrid);
            }
        }
    }
}
