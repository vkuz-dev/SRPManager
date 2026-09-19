using System;
using System.Drawing;
using System.Windows.Forms;

using AWLM.Core;
using AWLM.Utilities;

namespace AWLM.UI
{
    public class SettingsForm : Form
    {
        // Controls
        private CheckBox _chkStartup;
        private CheckBox _chkPinTray;
        private CheckBox _chkRemoteManagement;
        private CheckBox _chkCopyPolicyFiles;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private ComboBox _cmbLanguage;
        private Button _btnOk;
        private Button _btnCancel;

        // State
        private readonly bool _startupEnabled;
        private readonly bool _startupManaged;
        private readonly bool _pinEnabled;
        private readonly bool _remoteManagementEnabled;
        private readonly bool _desktopShortcutPresent;
        private readonly bool _desktopShortcutManaged;
        private readonly bool _startMenuShortcutPresent;
        private readonly bool _startMenuShortcutManaged;
        private readonly string _currentLang;
        private readonly bool _isDomainMode;
        private readonly bool _isLocalFilesDeployed;
        private readonly bool _isRunningAsAdmin;

        public SettingsForm()
        {
            // Read current state
            _startupEnabled = SettingsStartup.IsStartupEnabled();
            _startupManaged = SettingsStartup.IsGlobalStartupEnabled();
            _pinEnabled = SettingsTrayPin.IsPinEnabled();
            _remoteManagementEnabled = SettingsRemoteManagement.IsRemoteManagementEnabled();
            _desktopShortcutPresent = SettingsShortcuts.IsDesktopShortcutPresent();
            _desktopShortcutManaged = SettingsShortcuts.IsDesktopShortcutManaged();
            _startMenuShortcutPresent = SettingsShortcuts.IsStartMenuShortcutPresent();
            _startMenuShortcutManaged = SettingsShortcuts.IsStartMenuShortcutManaged();
            _currentLang = Strings.Lang;

            _isDomainMode = StatusChecker.IsDomainMode();
            _isLocalFilesDeployed = StatusChecker.IsLocalFilesDeployed();
            _isRunningAsAdmin = StatusChecker.IsProcessElevated();

            InitializeComponent();
            LoadCurrentValues();

            // Wired after LoadCurrentValues so restoring the saved state does not fire the warning.
            _chkRemoteManagement.CheckedChanged += OnRemoteManagementCheckedChanged;
        }

        private void InitializeComponent()
        {
            this.Text = Strings.Get("settings.MenuTitle");


            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Padding = new Padding(12);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // Root layout: stacks groups vertically, then the button row
            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0, // grown dynamically below
                Padding = new Padding(0)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // --- Behavior group ---
            var grpSettings = new GroupBox
            {
                Text = Strings.Get("settings.behaviorGroup"),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 4, 6, 6),
                Margin = new Padding(0, 0, 0, 8)
            };

            var behaviorLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 2, 4, 2)
            };

            _chkStartup = new CheckBox
            {
                Text = Strings.Get("settings.startupToggle"),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            };

            _chkPinTray = new CheckBox
            {
                Text = Strings.Get("settings.trayToggle"),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };

            _chkRemoteManagement = new CheckBox
            {
                Text = Strings.Get("settings.remoteManagementToggle"),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };

            behaviorLayout.Controls.Add(_chkStartup);
            behaviorLayout.Controls.Add(_chkPinTray);
            behaviorLayout.Controls.Add(_chkRemoteManagement);

            // Policy files checkbox — only added to the layout when relevant
            if (!_isDomainMode)
            {
                string baseText = Strings.Get("settings.copyPolicyFiles");

                if (_isLocalFilesDeployed)
                {
                    baseText += " [Already copied]";
                }
                else if (!_isRunningAsAdmin)
                {
                    baseText += " [Run as Administrator]";
                }

                _chkCopyPolicyFiles = new CheckBox
                {
                    Text = baseText,
                    AutoSize = true,
                    Margin = new Padding(0, 2, 0, 2),
                    Enabled = !(_isLocalFilesDeployed || !_isRunningAsAdmin),
                    Checked = _isLocalFilesDeployed
                };

                if (_chkCopyPolicyFiles.Enabled == false)
                    _chkCopyPolicyFiles.ForeColor = SystemColors.GrayText;

                behaviorLayout.Controls.Add(_chkCopyPolicyFiles);
            }

            grpSettings.Controls.Add(behaviorLayout);
            rootLayout.RowCount++;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.Controls.Add(grpSettings);

            // --- Shortcuts group ---
            var grpShortcuts = new GroupBox
            {
                Text = Strings.Get("settings.shortcutsGroup"),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 4, 6, 6),
                Margin = new Padding(0, 0, 0, 8)
            };

            var shortcutsLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 2, 4, 2)
            };

            _chkDesktopShortcut = new CheckBox
            {
                Text = Strings.Get("settings.desktopShortcut"),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            };

            _chkStartMenuShortcut = new CheckBox
            {
                Text = Strings.Get("settings.startMenuShortcut"),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };

            shortcutsLayout.Controls.Add(_chkDesktopShortcut);
            shortcutsLayout.Controls.Add(_chkStartMenuShortcut);
            grpShortcuts.Controls.Add(shortcutsLayout);

            rootLayout.RowCount++;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.Controls.Add(grpShortcuts);

            // --- Language group ---
            var grpLang = new GroupBox
            {
                Text = Strings.Get("settings.languageGroup"),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 4, 6, 6),
                Margin = new Padding(0, 0, 0, 8)
            };

            var langLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 4, 4, 4)
            };

            var lblLang = new Label
            {
                Text = Strings.Get("settings.languageLabel"),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 6, 0)
            };

            _cmbLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180,
                Margin = new Padding(0, 2, 0, 2)
            };
            _cmbLanguage.Items.Add(new LanguageItem("en", "English"));
            _cmbLanguage.Items.Add(new LanguageItem("lv", "Latviešu"));
            _cmbLanguage.Items.Add(new LanguageItem("ru", "Русский"));
            _cmbLanguage.DisplayMember = "Name";
            _cmbLanguage.ValueMember = "Code";

            langLayout.Controls.Add(lblLang);
            langLayout.Controls.Add(_cmbLanguage);
            grpLang.Controls.Add(langLayout);

            rootLayout.RowCount++;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.Controls.Add(grpLang);

            // --- Button row ---
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            _btnCancel = new Button
            {
                Text = Strings.Get("button.cancel"),
                DialogResult = DialogResult.Cancel,
                Size = new Size(80, 26),
                Margin = new Padding(0)
            };
            _btnOk = new Button
            {
                Text = Strings.Get("button.ok"),
                DialogResult = DialogResult.OK,
                Size = new Size(80, 26),
                Margin = new Padding(0, 0, 6, 0)
            };

            buttonPanel.Controls.Add(_btnCancel);
            buttonPanel.Controls.Add(_btnOk);

            rootLayout.RowCount++;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.Controls.Add(buttonPanel);

            this.Controls.Add(rootLayout);
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            _btnOk.Click += (s, e) => ApplyAndClose();
            _btnCancel.Click += (s, e) => this.Close();

            this.MinimumSize = new Size(380, 0);
        }

        private void LoadCurrentValues()
        {
            _chkStartup.Checked = _startupEnabled;
            _chkStartup.Enabled = !_startupManaged;

            if (_startupManaged)
                _chkStartup.Text += "  [" + Strings.Get("settings.PolicyManaged") + "]";

            _chkPinTray.Checked = _pinEnabled;
            _chkRemoteManagement.Checked = _remoteManagementEnabled;

            // An all-users shortcut is administrator-deployed: show it as present, but the
            // toggle stays read-only because removing it needs rights we may not have.
            _chkDesktopShortcut.Checked = _desktopShortcutPresent;
            _chkDesktopShortcut.Enabled = !_desktopShortcutManaged;

            _chkStartMenuShortcut.Checked = _startMenuShortcutPresent;
            _chkStartMenuShortcut.Enabled = !_startMenuShortcutManaged;

            if (_desktopShortcutManaged)
            {
                _chkDesktopShortcut.Text += "  [" + Strings.Get("settings.PolicyManaged") + "]";
                _chkDesktopShortcut.ForeColor = SystemColors.GrayText;
            }

            if (_startMenuShortcutManaged)
            {
                _chkStartMenuShortcut.Text += "  [" + Strings.Get("settings.PolicyManaged") + "]";
                _chkStartMenuShortcut.ForeColor = SystemColors.GrayText;
            }

            foreach (LanguageItem item in _cmbLanguage.Items)
            {
                if (item.Code == _currentLang)
                {
                    _cmbLanguage.SelectedItem = item;
                    break;
                }
            }
        }

        private void ApplyAndClose()
        {
            // Startup
            if (!_startupManaged)
                SettingsStartup.SetStartup(_chkStartup.Checked);

            // Tray pin
            SettingsTrayPin.SaveTrayPin(_chkPinTray.Checked);

            // Remote actions
            SettingsRemoteManagement.SaveRemoteManagementEnabled(_chkRemoteManagement.Checked);

            // Shortcuts — only touched when the user actually changed them, so an unrelated
            // OK never rewrites a shortcut the user placed by hand.
            bool shortcutFailed = false;

            if (!_desktopShortcutManaged && _chkDesktopShortcut.Checked != _desktopShortcutPresent)
                shortcutFailed |= !SettingsShortcuts.SetDesktopShortcut(_chkDesktopShortcut.Checked);

            if (!_startMenuShortcutManaged && _chkStartMenuShortcut.Checked != _startMenuShortcutPresent)
                shortcutFailed |= !SettingsShortcuts.SetStartMenuShortcut(_chkStartMenuShortcut.Checked);

            if (shortcutFailed)
            {
                MessageBox.Show(
                    Strings.Get("settings.shortcutFailed"),
                    Strings.Get("settings.MenuTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Copy policy
            if (_chkCopyPolicyFiles?.Checked == true)
                PolicyStore.CopyLocalPolicies();

            // Language
            if (_cmbLanguage.SelectedItem is LanguageItem selectedLang)
            {
                string newLang = selectedLang.Code;
                if (!string.Equals(newLang, _currentLang, StringComparison.OrdinalIgnoreCase))
                {
                    SettingsLanguage.Save(newLang);
                    Strings.Lang = newLang;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnRemoteManagementCheckedChanged(object sender, EventArgs e)
        {
            if (!_chkRemoteManagement.Checked) return;

            MessageBox.Show(
                Strings.Get("msg.remoteManagementWarning"),
                Strings.Get("msg.remoteManagementWarningTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        internal class LanguageItem
        {
            public string Code { get; }
            public string Name { get; }

            public LanguageItem(string code, string name)
            {
                Code = code;
                Name = name;
            }
        }
    }
}
