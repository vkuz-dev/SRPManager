using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using AWLM.Core;

namespace AWLM.Utilities
{
    public static class GetAppLockerRules
    {
        // ────────────────────────────────────────────────────────────────────
        // Rule retrieval and parsing
        // ────────────────────────────────────────────────────────────────────
        public static List<AppLockerRuleInfo> GetRules(string machineName = null)
        {
            bool isRemote = !string.IsNullOrWhiteSpace(machineName);
            var rules = new List<AppLockerRuleInfo>();
            try
            {
                string xml = ResolvePolicyState.GetEffectivePolicyXml(machineName);
                var doc = XDocument.Parse(xml);

                foreach (var coll in doc.Descendants("RuleCollection"))
                {
                    string type = coll.Attribute("Type")?.Value ?? "Unknown";
                    string mode = coll.Attribute("EnforcementMode")?.Value ?? "NotConfigured";
                    ParseRuleElements(coll, "FilePathRule", type, mode, rules);
                    ParseRuleElements(coll, "FilePublisherRule", type, mode, rules);
                    ParseRuleElements(coll, "FileHashRule", type, mode, rules);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] GetRules: " + ex.Message);
                // Local failures render as an empty rule set — the grid showing "0 rules" is
                // honest when AppLocker is simply not configured. Remote failures rethrow so
                // AppLockerRulesForm can distinguish them from a machine that is unreachable.
                if (isRemote) throw;
            }
            return rules;
        }

        private static readonly Dictionary<string, string> RuleTypeNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "FilePathRule",      "FilePath"      },
                { "FilePublisherRule", "FilePublisher" },
                { "FileHashRule",      "FileHash"      },
            };

        private static void ParseRuleElements(XElement coll, string elementName,
            string type, string mode, List<AppLockerRuleInfo> target)
        {
            if (!RuleTypeNames.TryGetValue(elementName, out string ruleType))
                ruleType = elementName; // safe fallback for unknown future element names

            foreach (var rule in coll.Elements(elementName))
                target.Add(ParseSingleRule(rule, type, mode, ruleType, elementName));
        }

        private static AppLockerRuleInfo ParseSingleRule(
            XElement rule, string collection, string mode, string ruleType, string elementName)
        {
            var info = new AppLockerRuleInfo
            {
                RuleCollection = collection,
                EnforcementMode = mode,
                RuleType = ruleType,
                RuleName = rule.Attribute("Name")?.Value ?? "",
                Action = rule.Attribute("Action")?.Value ?? "",
                UserOrGroupSid = rule.Attribute("UserOrGroupSid")?.Value ?? "",
                Description = rule.Attribute("Description")?.Value ?? "",
            };

            // Get the <Conditions> element (conditions are only inside it)
            XElement conditionsEl = rule.Element("Conditions");

            switch (elementName)
            {
                case "FilePathRule":
                    // ONLY inside <Conditions>
                    info.Paths = conditionsEl?.Elements("FilePathCondition")
                        .Select(c => c.Attribute("Path")?.Value)
                        .Where(v => v != null)
                        .ToList() ?? new List<string>();
                    break;

                case "FilePublisherRule":
                    // ONLY inside <Conditions>
                    var pub = conditionsEl?.Elements("FilePublisherCondition").FirstOrDefault();
                    if (pub != null)
                    {
                        info.PublisherName = pub.Attribute("PublisherName")?.Value;
                        info.ProductName = pub.Attribute("ProductName")?.Value;
                        info.BinaryName = pub.Attribute("BinaryName")?.Value;

                        var vr = pub.Element("BinaryVersionRange");
                        if (vr != null)
                        {
                            info.MinVersion = vr.Attribute("LowSection")?.Value;
                            info.MaxVersion = vr.Attribute("HighSection")?.Value;
                        }
                    }
                    break;

                case "FileHashRule":
                    if (conditionsEl != null)
                    {
                        var hashConditions = conditionsEl
                            .Elements("FileHashCondition")
                            .Elements("FileHash")
                            .ToList();

                        info.Hashes = hashConditions
                            .Select(h =>
                                $"{h.Attribute("Type")?.Value}:{h.Attribute("Data")?.Value}" +
                                $" ({h.Attribute("SourceFileName")?.Value})")
                            .ToList();

                        info.HashSourceFiles = hashConditions
                            .Select(h => h.Attribute("SourceFileName")?.Value)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToList();
                    }
                    break;
            }

            // Exceptions column
            var exceptionsEl = rule.Descendants("Exceptions").FirstOrDefault();
            if (exceptionsEl != null)
            {
                info.PathExceptions = exceptionsEl
                    .Elements("FilePathCondition")
                    .Select(ex => ex.Attribute("Path")?.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();

                info.PublisherExceptions = exceptionsEl
                .Elements("FilePublisherCondition")
                .Select(ex =>
                {
                    string pub = ex.Attribute("PublisherName")?.Value;
                    string prod = ex.Attribute("ProductName")?.Value;
                    string bin = ex.Attribute("BinaryName")?.Value;

                    // 1. Choose the most specific identifier
                    string display = null;
                    if (!string.IsNullOrEmpty(bin) && bin != "*")
                        display = bin;
                    else if (!string.IsNullOrEmpty(prod) && prod != "*")
                        display = prod;
                    else if (!string.IsNullOrEmpty(pub))
                        display = pub;
                    else
                        return null; // will be filtered out

                    // 2. Append version range if it adds useful information
                    var vr = ex.Element("BinaryVersionRange");
                    if (vr != null)
                    {
                        string low = vr.Attribute("LowSection")?.Value;
                        string high = vr.Attribute("HighSection")?.Value;

                        if (!string.IsNullOrEmpty(low) || !string.IsNullOrEmpty(high))
                        {
                            // Include version only if at least one bound is not "*"
                            if (low != "*" || high != "*")
                                display += $" ({low ?? "*"} - {high ?? "*"})";
                        }
                    }

                    return display;
                })
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

                // FileHash exceptions – only the SourceFileName is stored
                info.HashExceptions = exceptionsEl
                    .Elements("FileHashCondition")
                    .Elements("FileHash")
                    .Select(h => h.Attribute("SourceFileName")?.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();
            }

            return info;
        }
    }
    // ────────────────────────────────────────────────────────────────────────
    // Data transfer object
    // ────────────────────────────────────────────────────────────────────────

    public class AppLockerRuleInfo
    {
        public string RuleCollection { get; set; }
        public string EnforcementMode { get; set; }
        public string RuleType { get; set; }
        public string RuleName { get; set; }
        public string Action { get; set; }
        public string UserOrGroupSid { get; set; }
        public string Description { get; set; }

        public List<string> Paths { get; set; } = new List<string>();

        public string PublisherName { get; set; }
        public string ProductName { get; set; }
        public string BinaryName { get; set; }
        public string MinVersion { get; set; }
        public string MaxVersion { get; set; }


        public List<string> PathExceptions { get; set; } = new List<string>();
        public List<string> PublisherExceptions { get; set; } = new List<string>();

        /// <summary>Full "TYPE:digest (SourceFileName)" strings, as shown in the rules grid.</summary>
        public List<string> Hashes { get; set; } = new List<string>();

        /// <summary>Just the source file names behind <see cref="Hashes"/>, for compact displays.</summary>
        public List<string> HashSourceFiles { get; set; } = new List<string>();

        public List<string> HashExceptions { get; set; } = new List<string>();

        // Helpers

        public IEnumerable<string> AllExceptions =>
            ((IEnumerable<string>)PathExceptions)
                .Concat(PublisherExceptions)
                .Concat(HashExceptions);

        public string PrimaryConditionDisplay()
        {
            if (!string.IsNullOrEmpty(PublisherName))
                return $"{PublisherName} / {ProductName} / {BinaryName}";

            if (Paths?.Count > 0)
                return string.Join("; ", Paths);

            if (Hashes?.Count > 0)
                return string.Join("; ", Hashes);

            return "";
        }
    }

}
