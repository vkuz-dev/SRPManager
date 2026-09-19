using System.Collections.Generic;

namespace AWLM.Core
{
    public static partial class Strings
    {
        private static Dictionary<string, string> Ru() => new Dictionary<string, string>
        {
            // Tray icon
            { "tray.detecting",         "AppLocker: Определение..." },
            { "tray.status",            "AppLocker: {0}" },

            // Status indicators
            { "menu.adminMode",         "Права администратора" },
            { "menu.adminMode.elevate", "Запустить как администратора" },
            { "menu.appIDSvc",          "AppIDSvc запущена" },
            { "menu.appIDSvc.enable",   "AppIDSvc (включить)" },
            { "menu.appIDSvc.enableTooltip", "Нажмите, чтобы включить и запустить службу Application Identity." },
            { "menu.dllFiltered",       "Фильтрация DLL включена" },

            // Policy items
            { "menu.enable",            "Включить AppLocker" },
            { "menu.disable",           "Отключить AppLocker" },
            { "menu.awlEnabled",        "AppLocker включён" },
            { "menu.awlDisabled",       "AppLocker отключён" },
            { "menu.enforceMode",       "Заморозить состояние AppLocker" },
            { "menu.enforceTooltip",    "Пока активно, политика AppLocker не будет меняться автоматически." },

            // Actions
            { "menu.gpupdate",          "Запустить 'GPUpdate /force'" },
            { "menu.logs",              "Открыть журнал AppLocker" },
            { "menu.logs.1day",         "За 1 день" },
            { "menu.logs.7days",        "За 7 дней" },
            { "menu.logs.30days",       "За 30 дней" },
            { "menu.rules",             "Показать правила AppLocker" },

            // Settings
            { "menu.settings",           "Настройки" },
            { "settings.MenuTitle",      "Настройки SRPManager" },
            { "settings.behaviorGroup",  "Поведение приложения" },
            { "settings.startupToggle",  "Запускать автоматически" },
            { "settings.trayToggle",     "Показывать значок в трее" },
            { "settings.shortcutsGroup", "Ярлыки" },
            { "settings.desktopShortcut", "Создать ярлык на рабочем столе" },
            { "settings.startMenuShortcut", "Создать ярлык в меню «Пуск»" },
            { "settings.shortcutFailed", "Некоторые ярлыки не удалось обновить. Возможно, ими управляет администратор." },
            { "settings.PolicyManaged",  "Управляется Администратором" },
            { "settings.copyPolicyFiles","Скопировать файлы политик в C:\\Windows\\AppLocker\\Policies" },
            { "settings.languageGroup",  "Язык" },
            { "settings.languageLabel",  "Язык:" },
            { "button.ok",               "ОК" },
            { "button.cancel",           "Отмена" },

            { "menu.about",             "О программе" },
            { "menu.exit",              "Выход" },

            // Balloon tips
            { "balloon.policyApplied",  "Политика применена" },
            { "balloon.policyMsg",      "{0} успешно применена." },
            { "balloon.gpupdateDone",   "GPUpdate завершён" },
            { "balloon.gpupdateMsg",    "Групповые политики успешно обновлены." },
            { "msg.frozenTitle",        "AppLocker заморожен" },
            { "balloon.appIdSvcEnabled",    "Служба включена" },
            { "balloon.appIdSvcEnabledMsg", "Служба Application Identity теперь запущена." },

            // Message boxes
            { "msg.policyFail",         "Не удалось применить политику AppLocker:\n\n{0}" },
            { "msg.appIdSvcFail",       "Не удалось включить службу Application Identity:\n\n{0}" },
            { "msg.gpupdateFail",       "GPUpdate завершился с ошибкой:\n\n{0}" },
            { "msg.gpupdateCancel",     "Операция отменена или завершилась с ошибкой:\n\n{0}" },
            { "msg.error",              "Ошибка" },

            // About
            { "about.title",            "О программе" }
        };
    }
}
