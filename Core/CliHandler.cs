using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using AWLM.UI;
using AWLM.Utilities;

namespace AWLM.Core
{
    static class CliHandler
    {
        public static async Task HandleArgs(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            string cmd = args[0].ToLowerInvariant();

            switch (cmd)
            {
                case "-help":
                case "-h":
                case "/?":
                case "-?":
                    ShowHelp();
                    return;

                case "-status":
                    HandleStatus();
                    return;

                case "-enable":
                case "-e":
                    await HandleEnable(args);
                    return;

                case "-enable -force":
                case "-ef":
                    await HandleEnable(new[] { "-enable", "-force" });
                    return;

                case "-disable":
                case "-d":
                    await HandleDisable(args);
                    return;

                case "-logs":
                    HandleLogs(args);
                    return;

                case "-rules":
                    HandleRules(args);
                    return;

                default:
                    Console.WriteLine("Unknown command: " + args[0]);
                    Console.WriteLine("Run 'SRPManager.exe -help' for usage.");
                    break;
            }
        }

        private static void HandleStatus()
        {
            PolicyState state = ResolvePolicyState.DetectCurrentPolicy();
            bool dllActive = StatusChecker.IsDllFilteringActive();
            bool appIdSvc = StatusChecker.IsAppIDSvcRunning();
            bool isAdmin = StatusChecker.IsProcessElevated();
            bool frozen = MutexManager.IsMutexActive();

            Console.WriteLine("AppLocker Status");
            Console.WriteLine("================");
            Console.WriteLine("Policy state     : " + state);
            Console.WriteLine("AppIDSvc         : " + (appIdSvc ? "Running" : "Stopped"));
            Console.WriteLine("DLL filtering    : " + (dllActive ? "Yes" : "No"));
            Console.WriteLine("Administrator    : " + (isAdmin ? "Yes" : "No"));
            Console.WriteLine("AppLocker Frozen : " + (frozen ? "Yes" : "No"));
        }

        private static async Task HandleEnable(string[] args)
        {
            bool force = args.Length >= 2 &&
                        args[1].Equals("-force", StringComparison.OrdinalIgnoreCase);

            string policyFile = AppLockerManager.ResolveEnablePolicyFileName();
            Console.WriteLine("Enabling AppLocker...");

            AppLockerManager.ApplyPolicyResult result = await AppLockerManager.ApplyPolicyAsync(policyFile, force);

            if (result.Success)
                Console.WriteLine("Done. New state: " + ResolvePolicyState.DetectCurrentPolicy());
            else if (result.Frozen)
                Console.WriteLine("AppLocker is frozen. Use -force to override.");
            else
                Console.WriteLine("Failed: " + result.Error);
        }

        private static async Task HandleDisable(string[] args)
        {
            Console.WriteLine("Disabling AppLocker...");

            AppLockerManager.ApplyPolicyResult result = await AppLockerManager.ApplyPolicyAsync("AppLocker-Disable.xml");

            if (result.Success)
                Console.WriteLine("Done. New state: " + ResolvePolicyState.DetectCurrentPolicy());
            else
                Console.WriteLine("Failed: " + result.Error);
        }

        private static void HandleLogs(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SRPManager.exe -logs <days> [-gui]");
                return;
            }

            if (!int.TryParse(args[1], out int days) || days < 1)
            {
                Console.WriteLine("Invalid number of days: " + args[1]);
                return;
            }

            bool gui = args.Length >= 3 &&
                       args[2].Equals("-gui", StringComparison.OrdinalIgnoreCase);

            if (gui)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppLockerLogsForm(days));
                return;
            }

            Console.WriteLine("Fetching AppLocker events for the last " + days + " day(s)...");
            List<AppLockerFileEvent> events = GetAppLockerFileLogs.GetEvents(days);

            if (events.Count == 0)
            {
                Console.WriteLine("No AppLocker events found.");
                return;
            }

            Console.WriteLine(
                PadRight("Time", 22) +
                PadRight("EventID", 8) +
                PadRight("Action", 22) +
                PadRight("User", 30) +
                PadRight("FilePath", 55) +
                PadRight("Rule", 25) +
                "Log");
            Console.WriteLine(new string('-', 170));

            foreach (AppLockerFileEvent ev in events)
            {
                Console.WriteLine(
                    PadRight(ev.TimeCreated.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), 22) +
                    PadRight(ev.EventId.ToString(), 8) +
                    PadRight(ev.Action, 22) +
                    PadRight(ev.User, 30) +
                    PadRight(ev.FilePath, 55) +
                    PadRight(ev.RuleName, 25) +
                    ev.LogName);
            }

            Console.WriteLine();
            Console.WriteLine(events.Count + " event(s) found.");
        }

        private static void HandleRules(string[] args)
        {
            bool gui = args.Length >= 2 &&
                       args[1].Equals("-gui", StringComparison.OrdinalIgnoreCase);

            if (gui)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppLockerRulesForm());
                return;
            }

            Console.WriteLine("Reading the effective AppLocker policy...");
            List<AppLockerRuleInfo> rules = GetAppLockerRules.GetRules();

            if (rules.Count == 0)
            {
                Console.WriteLine("No AppLocker rules found in the effective policy.");
                return;
            }

            string header =
                PadRight("Collection", 12) +
                PadRight("Enforcement", 15) +
                PadRight("Type", 15) +
                PadRight("Action", 8) +
                PadRight("User/Group", 28) +
                PadRight("Rule Name", 45) +
                "Condition";

            Console.WriteLine();
            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));

            // Condition is deliberately left unpadded and untruncated: it is the last column,
            // and a publisher condition carries information worth keeping when piped to a file.
            foreach (AppLockerRuleInfo rule in rules)
            {
                Console.WriteLine(
                    PadRight(rule.RuleCollection, 12) +
                    PadRight(rule.EnforcementMode, 15) +
                    PadRight(rule.RuleType, 15) +
                    PadRight(rule.Action, 8) +
                    PadRight(SidResolver.ResolveDisplayName(rule.UserOrGroupSid), 28) +
                    PadRight(rule.RuleName, 45) +
                    FormatCondition(rule));
            }

            Console.WriteLine();
            Console.WriteLine(rules.Count + " rule(s) found.");

            // Enforcement is a per-collection attribute, so one rule per group speaks for it.
            foreach (var group in rules
                         .GroupBy(r => r.RuleCollection)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {group.Key,-8} {group.Count(),4} rule(s)   {group.First().EnforcementMode}");
            }
        }

        /// <summary>
        /// Condition text for the console table. Matches the GUI's Path/Publisher/Hash column,
        /// except that hash rules print their source file names rather than the raw digests,
        /// which would otherwise run to hundreds of characters per row.
        /// </summary>
        private static string FormatCondition(AppLockerRuleInfo rule)
        {
            string condition = rule.HashSourceFiles.Count > 0
                ? "Hash: " + string.Join("; ", rule.HashSourceFiles)
                : rule.PrimaryConditionDisplay();

            int exceptions = rule.AllExceptions.Count();
            return exceptions > 0 ? $"{condition}  (+{exceptions} exception(s))" : condition;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("SRPManager - AppLocker Management Utility");
            Console.WriteLine("Developed by: Vladimirs Kuznecovs");
            Console.WriteLine("Version: v" + AppBuildVersion.Version);
            Console.WriteLine("==============================");
            Console.WriteLine();
            Console.WriteLine("Usage: SRPManager.exe [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  (no args)                 Launch the system tray GUI");
            Console.WriteLine("  -help|-h|-?|/?            Show this help message");
            Console.WriteLine("  -status                   Show current AppLocker state");
            Console.WriteLine("  -enable|-e                Enable AppLocker(soft), checks if frozen flag is set");
            Console.WriteLine("  -enable -force|-ef        Enable even if frozen flag is set");
            Console.WriteLine("  -disable|-d               Disable AppLocker");
            Console.WriteLine("  -logs <days>              Print events to console");
            Console.WriteLine("  -logs <days> -gui         Open the log viewer GUI");
            Console.WriteLine("  -rules                    Print effective policy rules to console");
            Console.WriteLine("  -rules -gui               Open the rules viewer GUI");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  SRPManager.exe -enable");
            Console.WriteLine("  SRPManager.exe -status");
            Console.WriteLine("  SRPManager.exe -logs 1");
            Console.WriteLine("  SRPManager.exe -rules");
            Console.WriteLine("  SRPManager.exe -rules > rules.txt");
        }

        private static string PadRight(string s, int width)
        {
            if (s == null) s = "";
            if (s.Length >= width) return s.Substring(0, width - 1) + " ";
            return s.PadRight(width);
        }
    }
}
