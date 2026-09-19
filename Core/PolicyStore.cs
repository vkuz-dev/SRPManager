using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace AWLM.Core
{
    public static class PolicyStore
    {
        public const string PoliciesFolder = @"C:\Windows\AppLocker\Policies";

        public static void CopyLocalPolicies()
        {
            try
            {
                if (!StatusChecker.IsProcessElevated())
                {
                    Debug.WriteLine("[PolicyStore] Skipping initial setup — not running as administrator.");
                    return;
                }

                Directory.CreateDirectory(PoliciesFolder);
                ExtractEmbeddedPolicies();

                Debug.WriteLine("[PolicyStore] Setup complete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PolicyStore] CopyLocalPolicies failed: " + ex.Message);
            }
        }

        public static string ResolvePolicy(string fileName, out string error)
        {
            error = null;
            string policyPath = Path.Combine(PoliciesFolder, fileName);

            try
            {
                if (!StatusChecker.IsDomainMode() && File.Exists(policyPath))
                {
                    Debug.WriteLine("[PolicyStore] Loading policy from disk: " + policyPath);
                    return File.ReadAllText(policyPath, Encoding.UTF8);
                }

                return ReadEmbeddedPolicy(fileName, out error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PolicyStore] Policy load failed: " + ex.Message);

                // Last-resort fallback
                return ReadEmbeddedPolicy(fileName, out error);
            }
        }

        // ── Extraction helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Writes any embedded policy that is not already on disk. Existing files are never
        /// overwritten — an administrator may have customised them.
        /// </summary>
        private static void ExtractEmbeddedPolicies()
        {
            const string prefix = "AWLM.Resources.Policies.";
            var asm = Assembly.GetExecutingAssembly();

            foreach (string resourceName in asm.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!resourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = resourceName.Substring(prefix.Length);
                string destPath = Path.Combine(PoliciesFolder, fileName);

                if (File.Exists(destPath))
                {
                    Debug.WriteLine("[PolicyStore] Skipping existing file: " + destPath);
                    continue;
                }

                try
                {
                    using (var stream = asm.GetManifestResourceStream(resourceName))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string xml = reader.ReadToEnd();
                        File.WriteAllText(destPath, xml, Encoding.UTF8);
                        Debug.WriteLine("[PolicyStore] Created: " + destPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PolicyStore] Failed to extract {fileName}: " + ex.Message);
                }
            }
        }

        // Embedded resource fallback

        public static string ReadEmbeddedPolicy(string fileName, out string error)
        {
            error = null;
            string resourceName = "AWLM.Resources.Policies." + fileName;
            try
            {
                using (var stream = Assembly.GetExecutingAssembly()
                                            .GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        error = $"Embedded resource not found: {resourceName}";
                        return null;
                    }
                    return new StreamReader(stream, Encoding.UTF8).ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                error = "Failed to read embedded policy: " + ex.Message;
                return null;
            }
        }
    }
}
