using System.Collections.Generic;

namespace AWLM.Core
{
    public static partial class Strings
    {
        private static Dictionary<string, string> En() => new Dictionary<string, string>
        {

            // Tray icon
            { "tray.detecting",         "AppLocker: Detecting..." },
            { "tray.status",            "AppLocker: {0}" },
            { "tray.status.remote",     "AppLocker ({0}): {1}" },
            { "tray.status.unreachable","AppLocker: {0} unreachable" },

            // Status indicators
            { "menu.adminMode",         "Administrator permissions" },
            { "menu.adminMode.elevate", "Run as Administrator" },
            { "menu.appIDSvc",          "AppIDSvc running" },
            { "menu.appIDSvc.enable",   "AppIDSvc (enable)" },
            { "menu.appIDSvc.enableTooltip", "Click to enable and start the Application Identity service." },
            { "menu.dllFiltered",       "DLL Filtering enabled" },

            // Host selector
            { "menu.host",              "Host: {0}" },
            { "menu.host.localhost",    "Localhost" },
            { "menu.host.enterNew",     "Enter new host…" },
            { "menu.host.clearHistory", "Clear history" },
            { "menu.localOnlyTooltip",  "Only available when targeting Localhost." },

            // Policy items
            { "menu.enable",            "Enable AppLocker" },
            { "menu.disable",           "Disable AppLocker" },
            { "menu.awlEnabled",        "AppLocker is Enabled" },
            { "menu.awlDisabled",       "AppLocker is Disabled" },
            { "menu.enforceMode",       "Enforce Current Mode While Running" },
            { "menu.enforceTooltip",    "While active, the AppLocker policy will not be changed automatically." },

            // Actions
            { "menu.gpupdate",          "Run 'GPUpdate /force'" },
            { "menu.logs",              "Open AppLocker Logs" },
            { "menu.logs.1day",         "Last 1 Day" },
            { "menu.logs.7days",        "Last 7 Days" },
            { "menu.logs.30days",       "Last 30 Days" },
            { "menu.rules",             "Show AppLocker Rules" },

            // Settings
            { "menu.settings",           "Settings" },
            { "settings.MenuTitle",      "SRPManager Settings" },
            { "settings.behaviorGroup",  "App behavior" },
            { "settings.startupToggle",  "Run at startup" },
            { "settings.trayToggle",     "Always show icon in the tray" },
            { "settings.shortcutsGroup", "Shortcuts" },
            { "settings.desktopShortcut", "Create a Desktop shortcut" },
            { "settings.startMenuShortcut", "Create a Start menu shortcut" },
            { "settings.shortcutFailed", "Some shortcuts could not be updated. They may be managed by an administrator." },
            { "settings.remoteManagementToggle", "Enable remote options" },
            { "settings.PolicyManaged",  "Managed by Administrator" },
            { "settings.copyPolicyFiles","Copy policy files to C:\\Windows\\AppLocker\\Policies" },
            { "settings.languageGroup",  "Language" },
            { "settings.languageLabel",  "Language:" },

            { "button.ok",               "OK" },
            { "button.cancel",           "Cancel" },

            { "menu.about",             "About" },
            { "menu.exit",              "Exit" },

            // Balloon tips
            { "balloon.policyApplied",  "Policy Applied" },
            { "balloon.policyMsg",      "{0} applied successfully." },
            { "balloon.gpupdateStarted","GPUpdate Started" },
            { "balloon.gpupdateStartedMsg", "Applying Group Policy updates.\nThis can take 10-30 seconds..." },
            { "balloon.gpupdateDone",   "GPUpdate Complete" },
            { "balloon.gpupdateMsg",    "Group Policy updated successfully." },
            { "msg.frozenTitle",        "AppLocker Frozen" },
            { "msg.frozenForeign",      "The freeze was set by another SRPManager instance on this machine and can only be lifted there." },
            { "msg.frozenBlocked",      "AppLocker is frozen and was not enabled.\n\nThe freeze may have been set by another administrator on this machine." },
            { "balloon.appIdSvcEnabled",    "Service Enabled" },
            { "balloon.appIdSvcEnabledMsg", "The Application Identity service is now running." },
            { "balloon.hostUnreachable",    "Host Unreachable" },
            { "balloon.hostUnreachableMsg", "'{0}' did not respond over WinRM. The host may be unreachable." },

            // Message boxes
            { "msg.policyFail",         "Failed to apply AppLocker policy:\n\n{0}" },
            { "msg.appIdSvcFail",       "Failed to enable the Application Identity service:\n\n{0}" },
            { "msg.gpupdateFail",       "GPUpdate failed:\n\n{0}" },
            { "msg.gpupdateCancel",     "Operation canceled or failed:\n\n{0}" },
            { "msg.error",              "Error" },
            { "msg.remoteManagementWarningTitle", "Remote Actions" },
            { "msg.remoteManagementWarning",
                "Remote Actions require WinRM (PowerShell Remoting) to be enabled and reachable on target computers:\n\n" +
                "• Run 'winrm quickconfig' or 'Enable-PSRemoting' on each target\n" +
                "• Allow WinRM through the target's firewall (default port 5985)\n" +
                "• Your account needs remote management permissions there\n\n" +
                "These actions use your current Windows credentials." },

            // About
            { "about.title",            "About" },
            { "about.body",
                "SRPManager v{version}\n\n" +
                "Current user: {user}\n\n" +

                "Utility for monitoring and managing AppLocker state.\n\n" +

                "Application whitelisting, when properly configured, can significantly improve system security by reducing the attack surface and restricting execution to trusted software and paths.\n\n" +
                "The AppLocker policies used by this program are located at:\n" +
                "C:\\Windows\\AppLocker\\Policies\n\n" +

                "Command-line usage is supported. Use /? for help.\n\n" +

                "You can read more about the project, report issues or contribute on GitHub" +

                "\n\n──── Developer ─────────────────────────\n" +
                "Vladimir Kuznetsov\n" +
                "Email: vladimir.kuznetsov0706@gmail.com\n" +
                "GitHub: https://github.com/vkuz-dev/SRPManager\n\n" +
                "Copyright © 2026 Vladimir Kuznetsov"
                }
        };
    }
}
