using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Win32;

using AWLM.Utilities;
namespace AWLM.Core
{

    /// <summary>
    /// Detects the current AppLocker enforcement state by fetching the effective
    /// policy XML and analysing the rule collections within it.
    ///
    /// Decision table
    /// ┌─────────────┬───────────────────────┬───────────────┬──────────┐
    /// │ appIdSvc    │ Enforcement           │ Wildcards     │ Result   │
    /// ├─────────────┼───────────────────────┼───────────────┼──────────┤
    /// │ Not running │ —                     │ —             │ Off      │
    /// │ Running     │ No rule collections   │ —             │ Off      │
    /// │ Running     │ None enforced         │ —             │ Off      │
    /// │ Running     │ AuditOnly             │ —             │ AuditOnly│
    /// │ Running     │ All enforced          │ All wildcards │ Off      │
    /// │ Running     │ All enforced          │ —             │ On       │
    /// │ Running     │ Any other combination │ —             │ Custom   │
    /// └─────────────┴───────────────────────┴───────────────┴──────────┘
    /// </summary>
    public static class ResolvePolicyState
    {
        // Only these three collection types are considered when determining
        // overall enforcement state. Appx and Dll are intentionally ignored.
        private static readonly HashSet<string> RelevantCollections =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Exe", "Msi", "Script" };

        public static PolicyState DetectCurrentPolicy()
        {
            try
            {
                if (!StatusChecker.IsAppIDSvcRunning())
                    return PolicyState.Off;

                string hash = GetSrpV2PolicyHash();
                if (TryGetCachedState(hash, out PolicyState cached))
                    return cached;

                PolicyState state = CheckXml(GetEffectivePolicyXml());
                UpdateCache(hash, state);
                return state;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerManager] DetectCurrentPolicy: " + ex.Message);
                return PolicyState.Unknown;
            }
        }


        /// <summary>
        /// Async counterpart of <see cref="DetectCurrentPolicy"/>. Use this from the UI —
        /// the sync version spawns powershell.exe on the calling thread, which freezes the
        /// tray for the duration of the round-trip.
        /// </summary>
        public static async Task<PolicyState> DetectCurrentPolicyAsync()
        {
            try
            {
                if (!StatusChecker.IsAppIDSvcRunning())
                    return PolicyState.Off;

                string hash = GetSrpV2PolicyHash();
                if (TryGetCachedState(hash, out PolicyState cached))
                    return cached;

                PolicyState state = CheckXml(await GetEffectivePolicyXmlAsync().ConfigureAwait(false));
                UpdateCache(hash, state);
                return state;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerManager] DetectCurrentPolicyAsync: " + ex.Message);
                return PolicyState.Unknown;
            }
        }

        // Hash and cache AppLocker policy key to save CPU cycles on Get-AppLockerPolicy -Effective
        private static readonly object CacheLock = new object();
        private static string _lastPolicyHash;
        private static PolicyState? _lastPolicyState;

        private static bool TryGetCachedState(string hash, out PolicyState state)
        {
            lock (CacheLock)
            {
                if (hash != null && hash == _lastPolicyHash && _lastPolicyState.HasValue)
                {
                    state = _lastPolicyState.Value;
                    return true;
                }
            }

            state = PolicyState.Unknown;
            return false;
        }

        private static void UpdateCache(string hash, PolicyState state)
        {
            // A null hash means the registry read itself failed - don't cache on top of that,
            // so the next call falls back to a real poll instead of trusting a stale match.
            if (hash == null)
                return;

            lock (CacheLock)
            {
                _lastPolicyHash = hash;
                _lastPolicyState = state;
            }
        }

        private const string SrpV2PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\SrpV2";

        private static string GetSrpV2PolicyHash()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SrpV2PolicyKeyPath))
                using (SHA256 sha = SHA256.Create())
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                    {
                        if (key == null)
                            writer.Write("MISSING");
                        else
                            HashRegistryTree(key, writer);
                    }

                    ms.Position = 0;
                    return Convert.ToBase64String(sha.ComputeHash(ms));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ResolvePolicyState] GetSrpV2PolicyHash: " + ex.Message);
                return null;
            }
        }

        // Walks values and subkeys in a stable (sorted) order so the hash only depends on
        // content, not on registry enumeration order.
        private static void HashRegistryTree(RegistryKey key, BinaryWriter writer)
        {
            foreach (string valueName in key.GetValueNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                writer.Write(valueName);
                writer.Write(FormatRegistryValue(key.GetValue(valueName)));
            }

            foreach (string subKeyName in key.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                {
                    if (subKey == null)
                        continue;

                    writer.Write(subKeyName);
                    HashRegistryTree(subKey, writer);
                }
            }
        }

        private static string FormatRegistryValue(object value)
        {
            switch (value)
            {
                case null:
                    return "";
                case byte[] bytes:
                    return Convert.ToBase64String(bytes);
                case string[] arr:
                    // REG_MULTI_SZ: join on a byte that cannot occur inside a registry string, so
                    // ["ab","c"] and ["a","bc"] hash differently instead of both collapsing to
                    // "abc". That collision would make the cache miss a real policy change.
                    // Do not replace with Concat or an empty separator.
                    return string.Join("\u0001", arr);
                default:
                    return value.ToString();
            }
        }

        private const string EffectivePolicyArgs =
            "-NoProfile -Command \"Get-AppLockerPolicy -Effective -Xml\"";

        public static async Task<string> GetEffectivePolicyXmlAsync()
        {
            try
            {
                CommandResult result = await CommandExecution
                    .ExecuteCommandAsync("powershell.exe", EffectivePolicyArgs)
                    .ConfigureAwait(false);

                if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    Debug.WriteLine("[AppLockerPolicy] GetEffectivePolicyXmlAsync: " +
                                    (result.ErrorMessage ?? "empty output"));
                    return EmptyPolicyXml;
                }

                return result.StandardOutput.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] GetEffectivePolicyXmlAsync: " + ex.Message);
                return EmptyPolicyXml;
            }
        }

        public static string GetEffectivePolicyXml(string machineName = null)
        {
            if (!string.IsNullOrWhiteSpace(machineName))
                return GetEffectivePolicyXmlRemote(machineName);

            try
            {
                string output;
                string error;

                bool success = CommandExecution.ExecuteCommand(
                    "powershell.exe",
                    EffectivePolicyArgs,
                    out output,
                    out error);

                if (!success || string.IsNullOrWhiteSpace(output))
                {
                    Debug.WriteLine("[AppLockerPolicy] GetEffectivePolicyXml: " +
                                    (error ?? "empty output"));
                    return EmptyPolicyXml;
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] GetEffectivePolicyXml: " + ex.Message);
                return EmptyPolicyXml;
            }
        }

        private static string GetEffectivePolicyXmlRemote(string machineName)
        {
            var result = RemoteManagement.GetEffectivePolicyXmlRemoteAsync(machineName).GetAwaiter().GetResult();

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                throw new InvalidOperationException(
                    $"Failed to read the effective AppLocker policy from '{machineName}':\n{result.ErrorMessage}" +
                    (string.IsNullOrWhiteSpace(result.StandardError) ? "" : $"\n{result.StandardError}"));

            return result.StandardOutput.Trim();
        }

        // XML analysis 
        internal static PolicyState CheckXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return PolicyState.Unknown;

            try
            {
                var doc = XDocument.Parse(xml);

                var collectionsByType = doc
                    .Descendants("RuleCollection")
                    .Where(c => RelevantCollections.Contains(c.Attribute("Type")?.Value ?? ""))
                    .GroupBy(c => (c.Attribute("Type")?.Value ?? "").ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                // No relevant collections → Off
                if (collectionsByType.Count == 0)
                    return PolicyState.Off;

                var enforcedCollections = collectionsByType.Values.Where(IsEnforced).ToList();
                var auditCollections = collectionsByType.Values.Where(IsAuditOnly).ToList();

                // Nothing enforced or audited → Off
                if (enforcedCollections.Count == 0 && auditCollections.Count == 0)
                    return PolicyState.Off;

                // Collections present but none enforced, at least one audited → AuditOnly
                if (enforcedCollections.Count == 0)
                    return PolicyState.AuditOnly;

                // Not all three relevant types enforced → Custom
                if (enforcedCollections.Count != RelevantCollections.Count)
                    return PolicyState.Custom;

                // All three enforced — Off only if every collection has a wildcard
                // Allow-All for Everyone (Path="*"), meaning nothing is actually blocked.
                if (enforcedCollections.All(HasEffectiveAllowAll))
                    return PolicyState.Off;

                // At least one collection has Allow-All but not all three → Custom
                if (enforcedCollections.Any(HasEffectiveAllowAll))
                    return PolicyState.Custom;

                return PolicyState.On;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ResolvePolicyState] CheckXml: " + ex.Message);
                return PolicyState.Unknown;
            }
        }

        // Collection-level helpers
        private static bool IsEnforced(XElement collection) =>
            string.Equals(
                collection.Attribute("EnforcementMode")?.Value,
                "Enabled",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsAuditOnly(XElement collection) =>
            string.Equals(
                collection.Attribute("EnforcementMode")?.Value,
                "AuditOnly",
                StringComparison.OrdinalIgnoreCase);

        private static bool HasEffectiveAllowAll(XElement collection) =>
            collection
                .Elements()
                .Where(e => e.Name.LocalName.EndsWith("Rule", StringComparison.Ordinal))
                .Any(r =>
                    string.Equals(r.Attribute("Action")?.Value, "Allow", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Attribute("UserOrGroupSid")?.Value, "S-1-1-0", StringComparison.Ordinal) &&
                    r.Descendants("FilePathCondition")
                     .Any(c => string.Equals(c.Attribute("Path")?.Value, "*", StringComparison.Ordinal)));

        private const string EmptyPolicyXml = "<AppLockerPolicy Version=\"1\"/>";
    }

    public enum PolicyState
    {
        On,
        Off,
        Custom,
        AuditOnly,
        Unknown
    }

    // Handles loading and caching of embedded icon resources
    public static class IconLoader
    {
        private static readonly System.Reflection.Assembly _asm =
            System.Reflection.Assembly.GetExecutingAssembly();

        private static readonly Dictionary<string, Icon> _iconCache = new Dictionary<string, Icon>();

        // Loads an icon from embedded resources with caching
        public static Icon GetIcon(string name)
        {
            if (_iconCache.TryGetValue(name, out var cachedIcon))
                return cachedIcon;

            var resourceName = $"AWLM.Resources.Icons.{name}";
            var stream = _asm.GetManifestResourceStream(resourceName);

            if (stream == null)
                return SystemIcons.Question;

            using (stream)
            {
                var icon = new Icon(stream);
                _iconCache[name] = icon;
                return icon;
            }
        }

        // Gets a tray icon based on policy state
        private static readonly Dictionary<PolicyState, string> IconMap = new()
        {
            { PolicyState.On, "applocker_on_mode.ico" },
            { PolicyState.Off, "applocker_off_mode.ico" },
            { PolicyState.Custom, "applocker_custom_mode.ico" },
            { PolicyState.AuditOnly, "applocker_audit_mode.ico" },
            { PolicyState.Unknown, "tray_app_small.ico" }
        };

        private const string EnforcedOffIcon = "applocker_off_enf_mode.ico";

        public static Icon GetStateIcon(PolicyState state, bool frozen = false)
        {
            var iconName = frozen && state == PolicyState.Off
                ? EnforcedOffIcon
                : IconMap.TryGetValue(state, out var name)
                    ? name
                    : "tray_app_small.ico";

            try
            {
                return GetIcon(iconName);
            }
            catch
            {
                return GetIcon("tray_app_small.ico");
            }
        }
    }
}
