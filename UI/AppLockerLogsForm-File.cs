using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using AWLM.Utilities;

namespace AWLM.UI
{
    public sealed partial class AppLockerLogsForm
    {
        // Adapter is created inside CreateFileEventGrid so it can capture the grid reference.
        // The field is declared in AppLockerLogsForm.cs alongside the other adapter field.

        private DataGridView CreateFileEventGrid()
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
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FilePath", HeaderText = "File Path", MinimumWidth = 200 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PolicyName", HeaderText = "Policy", MinimumWidth = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "Rule", MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TargetComputer", HeaderText = "Computer", Width = 110, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LogName", HeaderText = "Log", Width = 80, MinimumWidth = 40 });

            grid.Columns["FilePath"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["PolicyName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["RuleName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns["FilePath"].FillWeight = 70;
            grid.Columns["PolicyName"].FillWeight = 10;
            grid.Columns["RuleName"].FillWeight = 20;

            _fileAdapter = new VirtualGridAdapter<AppLockerFileEvent>(
                grid,
                getCellValue: (e, col) => col switch
                {
                    0 => (object)e.TimeCreated.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    1 => e.EventId,
                    2 => e.Action,
                    3 => e.User,
                    4 => e.FilePath,
                    5 => e.PolicyName,
                    6 => e.RuleName,
                    7 => e.TargetComputer,
                    8 => e.LogName,
                    _ => null
                },
                getRowColor: e => ActionColor(e.Action, grid),
                getSortKey: (e, col) => col switch
                {
                    0 => (IComparable)e.TimeCreated,
                    1 => e.EventId,
                    2 => e.Action,
                    3 => e.User,
                    4 => e.FilePath,
                    5 => e.PolicyName,
                    6 => e.RuleName,
                    7 => e.TargetComputer,
                    8 => e.LogName,
                    _ => string.Empty
                });

            return grid;
        }

        private DataGridView CreateFileSummaryGrid()
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

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FilePath", HeaderText = "File Path", MinimumWidth = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", Width = 150, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Count", Width = 80, MinimumWidth = 40 });

            grid.Columns["FilePath"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.CellFormatting += CellFormatting;
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            return grid;
        }

        private void PopulateFileEventGrid(List<AppLockerFileEvent> events)
        {
            _fileAdapter.SetData(events);
        }

        private void PopulateFileSummaryGrid(List<AppLockerFileEventSummary> summary)
        {
            _fileSummaryGrid.Rows.Clear();

            foreach (var s in summary)
            {
                var rowIndex = _fileSummaryGrid.Rows.Add(s.FilePath, s.Action, s.Count);
                var row = _fileSummaryGrid.Rows[rowIndex];
                row.DefaultCellStyle.BackColor = ActionColor(s.Action, _fileSummaryGrid);
            }
        }
    }
}
