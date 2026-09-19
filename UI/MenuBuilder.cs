using System;
using System.Windows.Forms;

using AWLM.Core;
using AWLM.Utilities;

namespace AWLM.UI
{
    /// <summary>
    /// Holds the three display-only status items so they can be refreshed on every
    /// timer tick without rebuilding the whole menu.
    /// </summary>
    public class StatusMenuItems
    {
        public ToolStripMenuItem AdminItem { get; set; }
        public ToolStripMenuItem AppIdItem { get; set; }
        public ToolStripMenuItem DllItem { get; set; }

        public void UpdateStatus(bool isAdmin, bool isUacActive, bool appIdRunning, bool dllActive, bool isRemoteTarget)
        {
            bool canElevate = !isRemoteTarget && !isAdmin && isUacActive;
            AdminItem.Text = canElevate ? Strings.Get("menu.adminMode.elevate") : Strings.Get("menu.adminMode");
            AdminItem.Enabled = canElevate;
            AdminItem.Checked = !isRemoteTarget && isAdmin;
            AdminItem.ToolTipText = isRemoteTarget ? Strings.Get("menu.localOnlyTooltip") : null;

            bool canEnableAppId = !isRemoteTarget && isAdmin && !appIdRunning; // clickable only when local, elevated, and the service is down
            AppIdItem.Text = canEnableAppId ? Strings.Get("menu.appIDSvc.enable") : Strings.Get("menu.appIDSvc");
            AppIdItem.Enabled = canEnableAppId;
            AppIdItem.Checked = appIdRunning;
            AppIdItem.ToolTipText = canEnableAppId ? Strings.Get("menu.appIDSvc.enableTooltip") : null;

            DllItem.Text = Strings.Get("menu.dllFiltered");
            DllItem.Checked = dllActive;
        }
    }

    public class PolicyMenuItems
    {
        public ToolStripMenuItem EnableItem { get; set; }
        public ToolStripMenuItem DisableItem { get; set; }

        // OLD APPROACH: hide the active state button, show available options

        //     public void UpdateForState(PolicyState state)
        //     {
        //         EnableItem.Text     = Strings.Get("menu.enable");
        //         EnableItem.Visible  = true;
        //         DisableItem.Text    = Strings.Get("menu.disable");
        //         DisableItem.Visible = true;

        //         switch (state)
        //         {
        //             case PolicyState.On:  EnableItem.Visible  = false; break;
        //             case PolicyState.Off: DisableItem.Visible = false; break;
        //         }
        //     }
        public void UpdateForState(PolicyState state, bool isRemoteTarget)
        {
            switch (state)
            {
                case PolicyState.On:
                    EnableItem.Text = Strings.Get("menu.awlEnabled");   // "AppLocker is Enabled"
                    EnableItem.Enabled = false;
                    EnableItem.Checked = true;
                    DisableItem.Text = Strings.Get("menu.disable");       // "Disable AppLocker"
                    DisableItem.Enabled = true;
                    DisableItem.Checked = false;
                    break;

                case PolicyState.Off:
                    EnableItem.Text = Strings.Get("menu.enable");        // "Enable AppLocker"
                    EnableItem.Enabled = true;
                    EnableItem.Checked = false;
                    DisableItem.Text = Strings.Get("menu.awlDisabled");   // "AppLocker is Disabled"
                    DisableItem.Enabled = false;
                    DisableItem.Checked = true;
                    break;

                default: // Custom, Unknown — both active, no checkmark
                    EnableItem.Text = Strings.Get("menu.enable");
                    EnableItem.Enabled = true;
                    EnableItem.Checked = false;
                    DisableItem.Text = Strings.Get("menu.disable");
                    DisableItem.Enabled = true;
                    DisableItem.Checked = false;
                    break;
            }


            if (!isRemoteTarget && !StatusChecker.IsProcessElevated())
            {
                EnableItem.Enabled = false;
                DisableItem.Enabled = false;
            }
        }
    }


    public static class MenuBuilder
    {
        public static ContextMenuStrip BuildMainMenu(
            Action<string, string> onPolicySelect,
            EventHandler onGPUpdate,
            Action<int> onViewLogs,
            EventHandler onViewRules,
            EventHandler onAbout,
            EventHandler onExit,
            EventHandler onFreezeToggle,
            Action onSettings,
            EventHandler onRequestElevation,
            EventHandler onEnableAppIDSvc,
            string currentHost,
            Action<string> onHostSelect,
            Action onEnterNewHost,
            Action onClearHostHistory,
            out ToolStripMenuItem freezeMenuItem,
            out PolicyMenuItems policyMenuItems,
            out StatusMenuItems statusMenuItems)
        {
            bool isAdmin = StatusChecker.IsProcessElevated();
            bool isUacActive = StatusChecker.IsUACActive();
            bool isRemoteTarget = !string.IsNullOrWhiteSpace(currentHost);

            // appIdSvc/dllActive: real local values on localhost. On a remote target these start
            // as neutral placeholders — BuildMainMenu never performs network I/O; the caller
            // corrects them immediately after build via a status refresh, same as it already does
            // for localhost's initial "Detecting..." state.
            bool appIdSvc = !isRemoteTarget && StatusChecker.IsAppIDSvcRunning();
            bool dllActive = !isRemoteTarget && StatusChecker.IsDllFilteringActive();

            bool canElevate = !isRemoteTarget && !isAdmin && isUacActive;
            bool canEnableAppId = !isRemoteTarget && isAdmin && !appIdSvc;

            var menu = new ContextMenuStrip();

            // ── Host selector ────────────────────────────────────────────────────
            if (SettingsRemoteManagement.IsRemoteManagementEnabled())
            {
                menu.Items.Add(BuildHostMenu(currentHost, isRemoteTarget, onHostSelect, onEnterNewHost, onClearHostHistory));
                menu.Items.Add(new ToolStripSeparator());
            }

            // ── Status indicators (display-only, except Admin/AppIDSvc when actionable) ──
            statusMenuItems = new StatusMenuItems
            {
                AdminItem = AddStatusItem(menu, canElevate ? Strings.Get("menu.adminMode.elevate") : Strings.Get("menu.adminMode"), !isRemoteTarget && isAdmin, canElevate ? onRequestElevation : null),
                AppIdItem = AddStatusItem(menu, canEnableAppId ? Strings.Get("menu.appIDSvc.enable") : Strings.Get("menu.appIDSvc"), appIdSvc, onEnableAppIDSvc),
                DllItem = AddStatusItem(menu, Strings.Get("menu.dllFiltered"), dllActive),
            };
            statusMenuItems.AdminItem.ToolTipText = isRemoteTarget ? Strings.Get("menu.localOnlyTooltip") : null;
            statusMenuItems.AppIdItem.Enabled = canEnableAppId;
            statusMenuItems.AppIdItem.ToolTipText = canEnableAppId ? Strings.Get("menu.appIDSvc.enableTooltip") : null;



            menu.Items.Add(new ToolStripSeparator());

            // ── Policy items ──────────────────────────────────────────────────────
            policyMenuItems = AddPolicyMenuItems(menu, onPolicySelect, isAdmin, isRemoteTarget);

            // ── Freeze toggle ─────────────────────────────────────────────────────
            freezeMenuItem = new ToolStripMenuItem(
                Strings.Get("menu.enforceMode"), null, onFreezeToggle)
            {
                CheckOnClick = false,
                Enabled = isAdmin && !isRemoteTarget,
                Checked = false,
                ToolTipText = isRemoteTarget ? Strings.Get("menu.localOnlyTooltip") : Strings.Get("menu.enforceTooltip")
            };
            menu.Items.Add(freezeMenuItem);

            menu.Items.Add(new ToolStripSeparator());

            // ── Logs ──────────────────────────────────────────────────────────────
            menu.Items.Add(BuildLogsMenu(onViewLogs));

            // ── Rules ─────────────────────────────────────────────────────────────
            menu.Items.Add(new ToolStripMenuItem(Strings.Get("menu.rules"), null, onViewRules));

            // ── GPUpdate ──────────────────────────────────────────────────────────
            menu.Items.Add(new ToolStripMenuItem(Strings.Get("menu.gpupdate"), null, onGPUpdate));

            menu.Items.Add(new ToolStripSeparator());

            // ── Settings ──────────────────────────────────────────────────────────
            menu.Items.Add(new ToolStripMenuItem(Strings.Get("menu.settings"), null, (s, e) => onSettings()));


            // ── About / Exit ──────────────────────────────────────────────────────
            menu.Items.Add(new ToolStripMenuItem(Strings.Get("menu.about"), null, onAbout));
            menu.Items.Add(new ToolStripMenuItem(Strings.Get("menu.exit"), null, onExit));

            return menu;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ToolStripMenuItem BuildHostMenu(
            string currentHost, bool isRemoteTarget,
            Action<string> onHostSelect, Action onEnterNewHost, Action onClearHostHistory)
        {
            string headerLabel = string.Format(
                Strings.Get("menu.host"),
                isRemoteTarget ? currentHost : Strings.Get("menu.host.localhost"));

            var header = new ToolStripMenuItem(headerLabel);

            header.DropDownItems.Add(new ToolStripMenuItem(
                Strings.Get("menu.host.localhost"), null, (s, e) => onHostSelect(null))
            {
                Checked = !isRemoteTarget,
                CheckOnClick = false
            });

            var history = RemoteHistoryStore.Load();
            if (history.Count > 0)
            {
                header.DropDownItems.Add(new ToolStripSeparator());
                foreach (string entry in history)
                {
                    string machine = entry;
                    header.DropDownItems.Add(new ToolStripMenuItem(machine, null, (s, e) => onHostSelect(machine))
                    {
                        Checked = isRemoteTarget && string.Equals(machine, currentHost, StringComparison.OrdinalIgnoreCase),
                        CheckOnClick = false
                    });
                }
            }

            header.DropDownItems.Add(new ToolStripSeparator());
            header.DropDownItems.Add(new ToolStripMenuItem(
                Strings.Get("menu.host.enterNew"), null, (s, e) => onEnterNewHost()));

            if (history.Count > 0)
            {
                header.DropDownItems.Add(new ToolStripMenuItem(
                    Strings.Get("menu.host.clearHistory"), null, (s, e) => onClearHostHistory()));
            }

            return header;
        }

        private static ToolStripMenuItem AddStatusItem(
            ContextMenuStrip menu, string text, bool isChecked,
            EventHandler onClick = null)
        {
            var item = new ToolStripMenuItem(text)
            {
                Enabled = onClick != null,  // display-only items get no handler, so they gray out
                Checked = isChecked,
                CheckOnClick = false
            };
            if (onClick != null)
                item.Click += onClick;
            menu.Items.Add(item);
            return item;
        }

        private static PolicyMenuItems AddPolicyMenuItems(
            ContextMenuStrip menu, Action<string, string> onPolicySelect, bool isAdmin, bool isRemoteTarget)
        {
            var items = new PolicyMenuItems();

            foreach (var policy in AppLockerManager.Policies)
            {
                string fileName = policy.Value;
                string displayName = policy.Key;
                bool isEnable = fileName.Equals("AppLocker-Enable.xml", StringComparison.OrdinalIgnoreCase);

                var item = new ToolStripMenuItem(
                    displayName,
                    null,
                    (s, e) =>
                    {
                        // Resolve enable policy at click time so DomainMode is always current
                        string resolvedFile = isEnable ? AppLockerManager.ResolveEnablePolicyFileName(isRemoteTarget) : fileName;
                        onPolicySelect(resolvedFile, displayName);
                    })
                {
                    Enabled = isRemoteTarget || isAdmin
                };

                menu.Items.Add(item);

                if (isEnable)
                    items.EnableItem = item;
                else if (fileName.Equals("AppLocker-Disable.xml", StringComparison.OrdinalIgnoreCase))
                    items.DisableItem = item;
            }

            return items;
        }


        private static ToolStripMenuItem BuildLogsMenu(Action<int> onViewLogs)
        {
            var sub = new ToolStripMenuItem(Strings.Get("menu.logs"));

            // ── Local time-range items ────────────────────────────────────────
            sub.DropDownItems.Add(new ToolStripMenuItem(
                Strings.Get("menu.logs.1day"), null, (s, e) => onViewLogs(1)));
            sub.DropDownItems.Add(new ToolStripMenuItem(
                Strings.Get("menu.logs.7days"), null, (s, e) => onViewLogs(7)));
            sub.DropDownItems.Add(new ToolStripMenuItem(
                Strings.Get("menu.logs.30days"), null, (s, e) => onViewLogs(30)));

            return sub;
        }
    }
}
