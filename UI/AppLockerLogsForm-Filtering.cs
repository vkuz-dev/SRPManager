using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using AWLM.Utilities;

namespace AWLM.UI
{
    public sealed partial class AppLockerLogsForm
    {
        private void ApplyAllFilters()
        {
            string quickFilter = _quickFilterBox.Text.Trim();
            List<QuickFilterTerm> terms = null;
            if (!string.IsNullOrEmpty(quickFilter))
            {
                terms = new List<QuickFilterTerm>();
                foreach (var t in quickFilter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    bool neg = t.StartsWith("!", StringComparison.Ordinal);
                    string txt = neg ? t.Substring(1) : t;
                    if (txt.Length > 0) terms.Add(new QuickFilterTerm(neg, txt));
                }
            }

            // ── Filter Standard Events ──────────────────────────────────────────────
            IEnumerable<AppLockerFileEvent> filtered = _fileEvents;
            if (terms != null) filtered = filtered.Where(e => QuickFilterMatch(e, terms));
            foreach (var fc in _activeFilters) filtered = filtered.Where(e => fc.Matches(e));

            // ── Apply checkbox exclusions ─────────────────────────────────────────
            if (_hideDllCheckBox.Checked)
                filtered = filtered.Where(e => !e.FilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

            if (_hideSystemEventsCheckBox.Checked)
            {
                filtered = filtered.Where(e =>
                    !(e.FilePath.EndsWith("conhost.exe", StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.EndsWith("Pester.psd1", StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.EndsWith("Pester.psm1", StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.EndsWith("PowerShellGet.psd1", StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.StartsWith(@"C:\Windows\System32", StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.IndexOf("PSScriptPolicyTest", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            if (_showCurrentUserCheckBox.Checked)
            {
                string currentUser = Environment.UserName;
                filtered = filtered.Where(e => e.User.IndexOf(currentUser, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var result = filtered.ToList();
            PopulateFileEventGrid(result);

            // ── Filter AppX Events ──────────────────────────────────────────────────
            IEnumerable<AppLockerAppxEvent> appxFiltered = _allAppxEvents;
            if (terms != null) appxFiltered = appxFiltered.Where(e => QuickFilterMatchAppx(e, terms));
            // AppX events don't have FilePath, so DLL/Noisy checkboxes don't apply.
            if (_showCurrentUserCheckBox.Checked)
            {
                string currentUser = Environment.UserName;
                appxFiltered = appxFiltered.Where(e => e.User.IndexOf(currentUser, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            var appxResult = appxFiltered.ToList();
            PopulateAppxEventGrid(appxResult);

            // ── Summary ────────────────────────────────────────────────────

            // File summary
            if (_tabControl.SelectedIndex == 1)
            {
                PopulateFileSummaryGrid(GetAppLockerFileLogs.GetSummary(result));
            }
            else
            {
                _pendingFileSummaryResult = result;
            }

            // AppX summary
            if (_tabControl.SelectedIndex == 3)
            {
                PopulateAppxSummaryGrid(GetAppLockerAppxLogs.GetSummary(appxResult));
            }
            else
            {
                _pendingAppxSummaryResult = appxResult;
            }
            UpdateStatusBar();
        }

        // Holds the last filtered result so summary can be computed lazily on tab switch
        private List<AppLockerFileEvent> _pendingFileSummaryResult;
        private List<AppLockerAppxEvent> _pendingAppxSummaryResult;


        private void RestartQuickFilter()
        {
            // Restart the timer so ApplyAllFilters only fires once the user has
            // stopped typing, instead of on every single keystroke.
            _quickFilterDebounce.Stop();
            _quickFilterDebounce.Start();
        }

        private static bool QuickFilterMatch(AppLockerFileEvent e, List<QuickFilterTerm> terms)
        {
            // ToString() is called once per event per filter run, not per term.
            // OrdinalIgnoreCase avoids creating any lowercase copies.
            string timeStr = e.TimeCreated.ToString();
            string eventIdStr = e.EventId.ToString();

            foreach (var term in terms)
            {
                bool match =
                    timeStr.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventIdStr.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Action.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.User.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.FilePath.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.PolicyName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.RuleName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.TargetComputer.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.LogName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0;

                if (term.Negated && match) return false;
                if (!term.Negated && !match) return false;
            }

            return true;
        }

        private static bool QuickFilterMatchAppx(AppLockerAppxEvent e, List<QuickFilterTerm> terms)
        {
            string timeStr = e.TimeCreated.ToString();
            string eventIdStr = e.EventId.ToString();

            foreach (var term in terms)
            {
                bool match =
                    timeStr.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    eventIdStr.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Action.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Channel.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.User.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.PackageName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Publisher.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.PackageVersion.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.PolicyName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.RuleName.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.TargetComputer.IndexOf(term.Text, StringComparison.OrdinalIgnoreCase) >= 0;

                if (term.Negated && match) return false;
                if (!term.Negated && !match) return false;
            }
            return true;
        }

        private void ShowAddFilterDialog()
        {
            using (var dlg = new FilterDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultFilter != null)
                {
                    _activeFilters.Add(dlg.ResultFilter);
                    RefreshFilterChips();
                    ApplyAllFilters();
                }
            }
        }

        private void RefreshFilterChips()
        {
            _filterChipsPanel.Controls.Clear();
            foreach (var filter in _activeFilters)
                _filterChipsPanel.Controls.Add(CreateFilterChip(filter));
        }

        /// <summary>
        /// Builds one "chip" for an active filter.
        /// The chip width is capped at 380 px; text that overflows gets an ellipsis
        /// and the full filter description is shown in a tooltip on hover.
        /// </summary>
        private Control CreateFilterChip(FilterCondition filter)
        {
            const int MaxChipWidth = 380;
            const int RemoveBtnWidth = 24;
            const int ChipHeight = 26;

            string fullText = filter.ToString();

            var chip = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Height = ChipHeight,
                Margin = new Padding(2),
                BackColor = Color.FromArgb(225, 235, 245),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = false
            };
            chip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            chip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RemoveBtnWidth));
            chip.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Size the chip to exactly fit the text, but never wider than MaxChipWidth.
            int measuredWidth = TextRenderer.MeasureText(fullText, Font).Width + 10;
            chip.Width = Math.Max(180, Math.Min(MaxChipWidth, measuredWidth + RemoveBtnWidth + 4));

            bool isTruncated = measuredWidth + RemoveBtnWidth + 4 > MaxChipWidth;

            var label = new Label
            {
                Text = fullText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
                AutoSize = false,
                // Show "..." when the chip is at max width so the user knows text is cut.
                AutoEllipsis = isTruncated
            };

            // Tooltip shows the full text whenever the chip is at max width.
            if (isTruncated)
            {
                _chipTooltip.SetToolTip(label, fullText);
                _chipTooltip.SetToolTip(chip, fullText);
            }

            var removeBtn = new Button
            {
                Text = "🞪",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(1)
            };
            removeBtn.FlatAppearance.BorderSize = 0;
            removeBtn.Click += (s, e) =>
            {
                _activeFilters.Remove(filter);
                RefreshFilterChips();
                ApplyAllFilters();
            };

            chip.Controls.Add(label, 0, 0);
            chip.Controls.Add(removeBtn, 1, 0);
            return chip;
        }

        private void ClearAllFilters()
        {
            _activeFilters.Clear();
            _quickFilterBox.Text = "";
            RefreshFilterChips();
            ApplyAllFilters();
        }

        private void ShowQuickFilterHelp()
        {
            MessageBox.Show(
                "The Quick Filter searches across ALL columns at once.\n" +
                "Separate terms with spaces to apply multiple AND filters.\n" +
                "Prefix any term with ! to apply IF NOT logic.\n\n" +
                "──── Examples ────────────────────────────────────\n\n" +
                "  chrome\n" +
                "      Rows that contain \"chrome\" anywhere.\n\n" +
                "  !chrome\n" +
                "      Exclude rows containing \"chrome\".\n\n" +
                "  blocked chrome\n" +
                "      Blocked events that also involve \"chrome\".\n\n" +
                "  !.dll blocked\n" +
                "      Blocked events, with DLL files excluded.\n\n" +
                "  DOMAIN\\username\n" +
                "      Events for a specific domain user.\n\n" +
                "  system32 !conhost\n" +
                "      Events from System32, excluding conhost.exe.\n\n" +
                "──── Tips ────────────────────────────────────────\n\n" +
                "  • Matching is case-insensitive.\n" +
                "  • For column-specific filters, use [+ Add Filter].",
                "Quick Filter — Usage Examples",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
    // Filter condition

    public class FilterCondition
    {
        public string Column { get; set; }
        public FilterOperator Operator { get; set; }
        public string Value { get; set; }

        public bool Matches(AppLockerFileEvent e)
        {
            string columnValue = GetColumnValue(e);
            if (columnValue == null) return false;

            string cv = columnValue.ToLowerInvariant();
            string fv = Value.ToLowerInvariant();

            switch (Operator)
            {
                case FilterOperator.Contains: return cv.Contains(fv);
                case FilterOperator.NotContains: return !cv.Contains(fv);
                case FilterOperator.Equals: return cv == fv;
                case FilterOperator.NotEquals: return cv != fv;
                case FilterOperator.StartsWith: return cv.StartsWith(fv);
                case FilterOperator.EndsWith: return cv.EndsWith(fv);
                case FilterOperator.Regex:
                    try
                    {
                        return System.Text.RegularExpressions.Regex.IsMatch(
                            columnValue, Value,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    catch { return false; }
                default: return false;
            }
        }

        private string GetColumnValue(AppLockerFileEvent e)
        {
            switch (Column)
            {
                case "Time": return e.TimeCreated.ToString();
                case "Event ID": return e.EventId.ToString();
                case "Action": return e.Action;
                case "User": return e.User;
                case "File Path": return e.FilePath;
                case "Policy": return e.PolicyName;
                case "Rule": return e.RuleName;
                case "Computer": return e.TargetComputer;
                case "Log": return e.LogName;
                default: return null;
            }
        }

        public override string ToString()
        {
            string op;
            switch (Operator)
            {
                case FilterOperator.Contains: op = "contains"; break;
                case FilterOperator.NotContains: op = "does not contain"; break;
                case FilterOperator.Equals: op = "="; break;
                case FilterOperator.NotEquals: op = "≠"; break;
                case FilterOperator.StartsWith: op = "starts with"; break;
                case FilterOperator.EndsWith: op = "ends with"; break;
                case FilterOperator.Regex: op = "matches regex"; break;
                default: op = Operator.ToString(); break;
            }
            return $"{Column} {op} \"{Value}\"";
        }
    }

    public enum FilterOperator
    {
        Contains,
        NotContains,
        Equals,
        NotEquals,
        StartsWith,
        EndsWith,
        Regex
    }

    internal struct QuickFilterTerm
    {
        public readonly bool Negated;
        public readonly string Text;

        public QuickFilterTerm(bool negated, string text)
        {
            Negated = negated;
            Text = text;
        }
    }
}
