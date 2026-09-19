using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AWLM.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Host name validation — used by HostInputDialog
    // ═══════════════════════════════════════════════════════════════════════════

    internal static class RemoteInputParser
    {
        private static readonly Regex MachineRx = new Regex(
            @"^[A-Za-z0-9]([A-Za-z0-9\-\.]{0,252}[A-Za-z0-9])?$",
            RegexOptions.Compiled);

        /// <summary>
        /// True if <paramref name="machine"/> is a syntactically valid NetBIOS name or FQDN.
        /// Also the guard that keeps quote characters out of interpolated PowerShell.
        /// </summary>
        public static bool IsValidMachineName(string machine) =>
            !string.IsNullOrWhiteSpace(machine) && MachineRx.IsMatch(machine.Trim());

        /// <summary>
        /// Validates operator input and returns the trimmed computer name, or null with
        /// <paramref name="error"/> set to a message fit for a dialog.
        /// </summary>
        public static string TryParse(string raw, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Please enter a computer name.";
                return null;
            }

            string machine = raw.Trim();

            if (!MachineRx.IsMatch(machine))
            {
                error = $"'{machine}' is not a valid NetBIOS name or FQDN.\n" +
                         "Use letters, digits, hyphens and dots only.";
                return null;
            }

            return machine;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Recent-computers history
    // ═══════════════════════════════════════════════════════════════════════════

    internal static class RemoteHistoryStore
    {
        private static readonly string HistoryFile =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SRPManager", "remote_history.txt");

        public const int MaxHistory = 10;

        public static List<string> Load()
        {
            try
            {
                // Re-validate on load: this file is plain text in the user's profile, so it is
                // not a trusted source even though everything written via Add() was validated.
                if (File.Exists(HistoryFile))
                    return File.ReadAllLines(HistoryFile)
                               // Older versions appended a ";7d" timeframe — keep only the host part,
                               // so a stale file can never feed "PC01;7d" back in as a machine name.
                               .Select(l => l.Split(';')[0].Trim())
                               .Where(RemoteInputParser.IsValidMachineName)
                               .Take(MaxHistory)
                               .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RemoteHistory] Load failed: " + ex.Message);
            }
            return new List<string>();
        }

        public static void Add(List<string> history, string entry)
        {
            // Extract just the machine name portion to avoid duplicates with different timeframes.
            string machinePart = entry.Split(';')[0].Trim();
            history.RemoveAll(h =>
                string.Equals(h.Split(';')[0].Trim(), machinePart, StringComparison.OrdinalIgnoreCase));
            history.Insert(0, entry);
            if (history.Count > MaxHistory)
                history.RemoveRange(MaxHistory, history.Count - MaxHistory);
            Save(history);
        }

        public static void Save(List<string> history)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile));
                File.WriteAllLines(HistoryFile, history);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RemoteHistory] Save failed: " + ex.Message);
            }
        }
    }
}
