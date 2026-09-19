using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AWLM.UI
{
    /// <summary>
    /// Connects a <see cref="DataGridView"/> running in virtual mode to a
    /// backing <see cref="List{T}"/>. Handles CellValueNeeded, RowPrePaint,
    /// and column-header sorting without materialising physical rows.
    /// </summary>
    public sealed class VirtualGridAdapter<T>
    {
        private readonly DataGridView _grid;
        private readonly Func<T, int, object> _getCellValue;
        private readonly Func<T, Color> _getRowColor;
        private readonly Func<T, int, IComparable> _getSortKey;

        private List<T> _data = new List<T>();
        private int _sortColIdx = -1;
        private bool _sortAsc = true;

        /// <param name="grid">The grid to drive. Must not yet have VirtualMode set.</param>
        /// <param name="getCellValue">Returns the display value for a (row-item, column-index) pair.</param>
        /// <param name="getRowColor">Returns the background colour for a row item.</param>
        /// <param name="getSortKey">
        ///   Optional. Returns an <see cref="IComparable"/> key for sorting by column index.
        ///   Defaults to using <paramref name="getCellValue"/> cast to <see cref="IComparable"/>.
        /// </param>
        public VirtualGridAdapter(
            DataGridView grid,
            Func<T, int, object> getCellValue,
            Func<T, Color> getRowColor,
            Func<T, int, IComparable> getSortKey = null)
        {
            _grid = grid;
            _getCellValue = getCellValue;
            _getRowColor = getRowColor;
            _getSortKey = getSortKey ?? ((item, col) => _getCellValue(item, col) as IComparable ?? string.Empty);

            grid.VirtualMode = true;
            grid.CellValueNeeded += OnCellValueNeeded;
            grid.RowPrePaint += OnRowPrePaint;
            grid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
        }

        // ── Public ──────────────────────────────────────────────────────────────

        public int Count => _data.Count;

        /// <summary>Returns the backing item at the given (post-sort) row index.</summary>
        public T GetItem(int index) => _data[index];

        /// <summary>
        /// Replaces the backing list, re-applies the current sort if any, and
        /// updates <see cref="DataGridView.RowCount"/> so the grid redraws.
        /// </summary>
        public void SetData(List<T> data)
        {
            _data = data ?? new List<T>();
            if (_sortColIdx >= 0)
                ApplySort();
            _grid.RowCount = _data.Count;
            _grid.Invalidate();
        }

        /// <summary>Returns the display value for a specific cell (used by CSV export).</summary>
        public object GetDisplayValue(int rowIndex, int colIndex)
        {
            if (rowIndex < 0 || rowIndex >= _data.Count) return null;
            return _getCellValue(_data[rowIndex], colIndex);
        }

        // ── Event handlers ──────────────────────────────────────────────────────

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _data.Count) return;
            e.Value = _getCellValue(_data[e.RowIndex], e.ColumnIndex);
        }

        private void OnRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _data.Count) return;
            // Accessing Rows[n] unshares the row, but lets us set the colour once per paint.
            _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = _getRowColor(_data[e.RowIndex]);
        }

        private void OnColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            _sortAsc = (e.ColumnIndex == _sortColIdx) ? !_sortAsc : true;
            _sortColIdx = e.ColumnIndex;
            ApplySort();
            _grid.Invalidate();
        }

        // ── Sort ────────────────────────────────────────────────────────────────

        private void ApplySort()
        {
            int col = _sortColIdx;
            bool asc = _sortAsc;

            _data.Sort((a, b) =>
            {
                IComparable ka = _getSortKey(a, col);
                IComparable kb = _getSortKey(b, col);

                if (ka == null && kb == null) return 0;
                if (ka == null) return asc ? -1 : 1;
                if (kb == null) return asc ? 1 : -1;

                int cmp;
                // Prefer typed comparison when both values are the same concrete type
                // (avoids cross-type CompareTo exceptions for mixed int/string columns).
                if (ka.GetType() == kb.GetType())
                    cmp = ka.CompareTo(kb);
                else
                    cmp = string.Compare(ka.ToString(), kb.ToString(), StringComparison.OrdinalIgnoreCase);

                return asc ? cmp : -cmp;
            });
        }
    }
}
