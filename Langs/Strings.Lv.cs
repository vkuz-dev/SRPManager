using System.Collections.Generic;

namespace AWLM.Core
{
    public static partial class Strings
    {
        private static Dictionary<string, string> Lv() => new Dictionary<string, string>
        {
            // Tray icon
            { "tray.detecting",         "AppLocker: Notiek noteikšana..." },
            { "tray.status",            "AppLocker: {0}" },

            // Status indicators
            { "menu.adminMode",         "Administratora tiesības" },
            { "menu.adminMode.elevate", "Palaist kā Administratoru" },
            { "menu.appIDSvc",          "AppIDSvc darbojas" },
            { "menu.appIDSvc.enable",   "AppIDSvc (iespējot)" },
            { "menu.appIDSvc.enableTooltip", "Noklikšķiniet, lai iespējotu un palaistu Application Identity pakalpojumu." },
            { "menu.dllFiltered",       "DLL filtrēšana ieslēgta" },

            // Policy items
            { "menu.enable",            "Iespējot AppLocker" },
            { "menu.disable",           "Atspējot AppLocker" },
            { "menu.awlEnabled",        "AppLocker ir ieslēgts" },
            { "menu.awlDisabled",       "AppLocker ir izslēgts" },
            { "menu.enforceMode",       "Saglabāt pašreizējo režīmu darbības laikā" },
            { "menu.enforceTooltip",
                "Kamēr šī opcija ir aktīva, AppLocker politika netiks automātiski mainīta." },

            // Actions
            { "menu.gpupdate",          "Izpildīt 'GPUpdate /force'" },
            { "menu.logs",              "Atvērt AppLocker žurnālu" },
            { "menu.logs.1day",         "Pēdējā diena" },
            { "menu.logs.7days",        "Pēdējās 7 dienas" },
            { "menu.logs.30days",       "Pēdējās 30 dienas" },
            { "menu.rules",             "Rādīt AppLocker noteikumus" },

            // Settings
            { "menu.settings",           "Iestatījumi" },
            { "settings.MenuTitle",      "SRPManager iestatījumi" },
            { "settings.behaviorGroup",  "Lietotnes uzvedība" },
            { "settings.startupToggle",  "Palaist automātiski" },
            { "settings.trayToggle",     "Vienmēr rādīt ikonu sistēmas panelī" },
            { "settings.shortcutsGroup", "Saīsnes" },
            { "settings.desktopShortcut", "Izveidot saīsni darbvirsmā" },
            { "settings.startMenuShortcut", "Izveidot saīsni izvēlnē Sākt" },
            { "settings.shortcutFailed", "Dažas saīsnes neizdevās atjaunināt. Iespējams, tās pārvalda administrators." },
            { "settings.PolicyManaged",  "Pārvalda Administrators" },
            { "settings.copyPolicyFiles","Kopēt politikas failus uz C:\\Windows\\AppLocker\\Policies" },
            { "settings.languageGroup",  "Valoda" },
            { "settings.languageLabel",  "Valoda:" },
            { "button.ok",               "OK" },
            { "button.cancel",           "Atcelt" },

            { "menu.about",             "Par programmu" },
            { "menu.exit",              "Iziet" },

            // Balloon tips
            { "balloon.policyApplied",  "Politika piemērota" },
            { "balloon.policyMsg",      "{0} veiksmīgi piemērota." },
            { "balloon.gpupdateDone",   "GPUpdate pabeigts" },
            { "balloon.gpupdateMsg",    "Grupas politika veiksmīgi atjaunināta." },
            { "msg.frozenTitle",        "AppLocker stāvoklis nofiksēts" },
            { "balloon.appIdSvcEnabledMsg", "Application Identity pakalpojums tagad darbojas." },

            // Message boxes
            { "msg.policyFail",         "Neizdevās piemērot AppLocker politiku:\n\n{0}" },
            { "msg.appIdSvcFail",       "Neizdevās iespējot Application Identity pakalpojumu:\n\n{0}" },
            { "msg.gpupdateFail",       "GPUpdate neizdevās:\n\n{0}" },
            { "msg.gpupdateCancel",     "Darbība atcelta vai neizdevās:\n\n{0}" },
            { "msg.error",              "Kļūda" },

            // About
            { "about.title",            "Par programmu" }
        };
    }
}
