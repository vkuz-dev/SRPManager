using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

using AWLM.Core;
using AWLM.UI;
using AWLM.Utilities;

namespace AWLM
{
    public class AWLM_App : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _contextMenu;
        private Timer _updateTimer;
        private ToolStripMenuItem _freezeMenuItem;
        private PolicyMenuItems _policyMenuItems;
        private StatusMenuItems _statusMenuItems;
        private string _currentHost; // null = localhost
        private bool _statusRefreshInFlight;

        // Construction / disposal
        public AWLM_App()
        {
            InitializeComponents();
            SettingsTrayPin.ApplyTrayPin();
            RefreshStatus();

            // Set up timer to refresh tray icon every 5 seconds
            _updateTimer = new Timer { Interval = 5_000 };
            _updateTimer.Tick += (s, e) => RefreshStatus();
            _updateTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Strings.LanguageChanged -= OnLanguageChanged;
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _trayIcon?.Dispose();
                _contextMenu?.Dispose();
                MutexManager.ReleaseFreezeMutex();
            }
            base.Dispose(disposing);
        }

        // Initialisation
        private void InitializeComponents()
        {
            Strings.Lang = SettingsLanguage.Load();
            Strings.LanguageChanged += OnLanguageChanged;

            _contextMenu = BuildMenu();

            _trayIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Icon = IconLoader.GetStateIcon(PolicyState.Unknown),
                Text = Strings.Get("tray.detecting"),
                Visible = true
            };

        }

        private ContextMenuStrip BuildMenu()
        {
            return MenuBuilder.BuildMainMenu(
                onPolicySelect: OnPolicySelected,
                onGPUpdate: OnGPUpdate,
                onViewLogs: OnViewLogs,
                onViewRules: OnViewRules,
                onAbout: OnAbout,
                onExit: OnExit,
                onFreezeToggle: OnFreezeToggle,
                onSettings: OnSettings,
                onRequestElevation: OnRequestElevation,
                onEnableAppIDSvc: OnEnableAppIDSvc,
                currentHost: _currentHost,
                onHostSelect: OnHostSelect,
                onEnterNewHost: OnEnterNewHost,
                onClearHostHistory: OnClearHostHistory,
                freezeMenuItem: out _freezeMenuItem,
                policyMenuItems: out _policyMenuItems,
                statusMenuItems: out _statusMenuItems);
        }

        // Host selection
        private void OnHostSelect(string machineName)
        {
            _currentHost = string.IsNullOrWhiteSpace(machineName) ? null : machineName;
            RebuildMenu();

            if (_currentHost != null)
                CheckReachability(_currentHost);
        }

        private void OnEnterNewHost()
        {
            string host = HostInputDialog.PromptForHost(null);
            if (host == null) return;

            _currentHost = host;
            RebuildMenu();
            CheckReachability(_currentHost);
        }

        private void OnClearHostHistory()
        {
            RemoteHistoryStore.Save(new List<string>());
            RebuildMenu();
        }

        // Test connection to the selected host
        private async void CheckReachability(string machineName)
        {
            bool reachable = await RemoteManagement.TestReachabilityAsync(machineName);

            // The user may have switched hosts again while this was in flight.
            if (!string.Equals(_currentHost, machineName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!reachable)
            {
                ShowBalloon(
                    Strings.Get("balloon.hostUnreachable"),
                    string.Format(Strings.Get("balloon.hostUnreachableMsg"), machineName),
                    ToolTipIcon.Warning);
            }
        }

        private void OnRequestElevation(object sender, EventArgs e)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                System.Diagnostics.Process.Start(psi);
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled the login dialog - do nothing
            }
        }

        // Language change
        private void OnLanguageChanged(object sender, EventArgs e) => RebuildMenu();

        // Rebuilds the tray context menu in place, preserving the Freeze checkbox state.
        // Used both on language change and after Settings closes (e.g. Remote Actions toggle).
        private void RebuildMenu()
        {
            bool wasFrozen = _freezeMenuItem?.Checked ?? false;
            var oldMenu = _contextMenu;

            _contextMenu = BuildMenu();
            _trayIcon.ContextMenuStrip = _contextMenu;

            if (wasFrozen && _freezeMenuItem != null)
                _freezeMenuItem.Checked = true;

            oldMenu?.Dispose();

            RefreshStatus();
        }

        private async void RefreshStatus()
        {
            if (_statusRefreshInFlight) return;
            _statusRefreshInFlight = true;
            try
            {
                if (string.IsNullOrEmpty(_currentHost))
                    await UpdateTrayIconAsync();
                else
                    await UpdateTrayIconRemoteAsync(_currentHost);
            }
            catch (Exception ex)
            {
                // async void: an escaping exception would tear the process down.
                Debug.WriteLine("[AWLM_App] RefreshStatus: " + ex.Message);
            }
            finally
            {
                _statusRefreshInFlight = false;
            }
        }

        // Tray icon / menu state refresh for localhost (called on the 5 s timer and after actions)
        private async Task UpdateTrayIconAsync()
        {
            if (_trayIcon == null) return;

            PolicyState state = await ResolvePolicyState.DetectCurrentPolicyAsync();

            // Awaited on the UI thread, so the tray icon may be gone by the time we resume.
            if (_trayIcon == null) return;

            bool frozen = MutexManager.IsMutexActive();

            // Tray icon + tooltip
            var newIcon = IconLoader.GetStateIcon(state, frozen);
            if (_trayIcon.Icon != newIcon)
                _trayIcon.Icon = newIcon;

            string newText = string.Format(Strings.Get("tray.status"), state);
            if (_trayIcon.Text != newText)
                _trayIcon.Text = newText;

            if (_freezeMenuItem != null)
                _freezeMenuItem.Checked = frozen;

            // Live menu items
            _policyMenuItems?.UpdateForState(state, isRemoteTarget: false);
            _statusMenuItems?.UpdateStatus(
                StatusChecker.IsProcessElevated(),
                StatusChecker.IsUACActive(),
                StatusChecker.IsAppIDSvcRunning(),
                StatusChecker.IsDllFilteringActive(),
                isRemoteTarget: false);
        }

        // Tray icon / menu state refresh for a remote target. On failure, leaves the last-known
        // menu values alone (no flicker off a transient network hiccup) and just flags the tooltip.
        private async Task UpdateTrayIconRemoteAsync(string machineName)
        {
            if (_trayIcon == null) return;

            RemoteStatusSnapshot snapshot = await RemoteStatusChecker.GetSnapshotAsync(machineName);

            // The selected host may have changed while this call was in flight.
            if (!string.Equals(_currentHost, machineName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!snapshot.Success)
            {
                _trayIcon.Text = string.Format(Strings.Get("tray.status.unreachable"), machineName);
                return;
            }

            var newIcon = IconLoader.GetStateIcon(snapshot.State);
            if (_trayIcon.Icon != newIcon)
                _trayIcon.Icon = newIcon;

            string newText = string.Format(Strings.Get("tray.status.remote"), machineName, snapshot.State);
            if (_trayIcon.Text != newText)
                _trayIcon.Text = newText;

            _policyMenuItems?.UpdateForState(snapshot.State, isRemoteTarget: true);
            _statusMenuItems?.UpdateStatus(
                isAdmin: false,
                isUacActive: false,
                appIdRunning: snapshot.AppIdSvcRunning,
                dllActive: snapshot.DllFilteringActive,
                isRemoteTarget: true);
        }

        // Policy selection

        private async void OnPolicySelected(string policyFileName, string policyName)
        {
            AppLockerManager.ApplyPolicyResult result = string.IsNullOrEmpty(_currentHost)
                ? await AppLockerManager.ApplyPolicyAsync(policyFileName)
                : await AppLockerManager.ApplyPolicyRemoteAsync(policyFileName, _currentHost);

            if (result.Success)
            {
                // ApplyPolicy* already polled for the state change before returning,
                // so the new state is live and the tray can refresh immediately.
                RefreshStatus();
                ShowBalloon(
                    Strings.Get("balloon.policyApplied"),
                    string.Format(Strings.Get("balloon.policyMsg"), policyName),
                    ToolTipIcon.Info);
            }
            else if (result.Frozen)
            {
                MessageBox.Show(
                    Strings.Get("msg.frozenBlocked"),
                    Strings.Get("msg.frozenTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                MessageBox.Show(
                    string.Format(Strings.Get("msg.policyFail"), result.Error),
                    Strings.Get("msg.error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Enable AppIDSvc
        private async void OnEnableAppIDSvc(object sender, EventArgs e)
        {
            AppLockerManager.ApplyPolicyResult result = await AppLockerManager.EnableAppIDSvcAsync();

            if (result.Success)
            {
                RefreshStatus();
                ShowBalloon(
                    Strings.Get("balloon.appIdSvcEnabled"),
                    Strings.Get("balloon.appIdSvcEnabledMsg"),
                    ToolTipIcon.Info);
            }
            else
            {
                MessageBox.Show(
                    string.Format(Strings.Get("msg.appIdSvcFail"), result.Error),
                    Strings.Get("msg.error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Freeze toggle
        private void OnFreezeToggle(object sender, EventArgs e)
        {
            if (!MutexManager.IsMutexActive())
            {
                MutexManager.AcquireFreezeMutex();
            }
            else if (!MutexManager.IsOwnedByThisProcess)
            {
                // Held elsewhere on this machine — only that owner can lift it.
                MessageBox.Show(
                    Strings.Get("msg.frozenForeign"),
                    Strings.Get("msg.frozenTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MutexManager.ReleaseFreezeMutex();
            }

            _freezeMenuItem.Checked = MutexManager.IsMutexActive();
            RefreshStatus();
        }

        // View Logs
        private void OnViewLogs(int timeframeDays)
        {
            var form = new AppLockerLogsForm(timeframeDays, _currentHost);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        // View Rules
        private void OnViewRules(object sender, EventArgs e)
        {
            var form = new AppLockerRulesForm(_currentHost);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show();
        }

        // Run GPUpdate
        private async void OnGPUpdate(object sender, EventArgs e)
        {
            bool isRemote = !string.IsNullOrEmpty(_currentHost);
            try
            {
                ShowBalloon(
                    Strings.Get("balloon.gpupdateStarted"),
                    Strings.Get("balloon.gpupdateStartedMsg"),
                    ToolTipIcon.Info);

                var result = isRemote
                    ? await RemoteManagement.RunGPUpdateRemoteAsync(_currentHost)
                    : await CommandExecution.ExecuteCommandAsync(
                        "gpupdate.exe",
                        "/force",
                        createNoWindow: true,
                        standardInput: "n",
                        validExitCodes: new[] { 0, 1, 2, 3 } // treat all these as success
                    );

                if (result.Success)
                {
                    ShowBalloon(
                        Strings.Get("balloon.gpupdateDone"),
                        Strings.Get("balloon.gpupdateMsg"),
                        ToolTipIcon.Info);
                }
                else
                {
                    MessageBox.Show(
                        string.Format(Strings.Get("msg.gpupdateFail"), result.ErrorMessage),
                        Strings.Get("msg.error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Get("msg.gpupdateCancel"), ex.Message),
                    Strings.Get("msg.error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // Settings
        private void OnSettings()
        {
            using (var settingsForm = new SettingsForm())
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                    RebuildMenu();
            }
        }

        // About
        private void OnAbout(object sender, EventArgs e)
        {

            string body = Strings.Get("about.body")
                .Replace("{version}", AppBuildVersion.Version)
                .Replace("{user}", StatusChecker.GetCurrentUserName());

            MessageBox.Show(
                body,
                Strings.Get("about.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Exit
        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _updateTimer?.Stop();
            Application.Exit();
        }

        // Helpers
        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            if (_trayIcon == null) return;
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(1000);
        }
    }
}
