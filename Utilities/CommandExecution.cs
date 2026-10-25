using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AWLM.Utilities
{
    public class CommandResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }
    public static class CommandExecution
    {

        private const int DefaultTimeoutMs = 60000;

        /// <summary>
        /// Wraps <paramref name="value"/> so the child process parses it back as exactly one
        /// argument, however many quotes and backslashes it contains.
        /// </summary>
        /// <remarks>
        /// Windows hands a child a single command-line string, not an argv array, and the child
        /// splits it again with CommandLineToArgvW rules: a run of backslashes is literal unless
        /// it precedes a quote, in which case each backslash must be doubled and the quote
        /// escaped. Encoding that correctly is what lets a whole PowerShell script travel as one
        /// argument.
        /// </remarks>
        public static string QuoteArgument(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Double each backslash run that precedes a quote, and escape the quote itself.
            string escaped = Regex.Replace(value, @"(\\*)""", @"$1$1\""");

            // Trailing backslashes would otherwise escape our own closing quote.
            escaped = Regex.Replace(escaped, @"(\\+)$", "$1$1");

            return "\"" + escaped + "\"";
        }

        public static async Task<CommandResult> ExecuteCommandAsync(
            string fileName,
            string arguments,
            int timeoutMs = DefaultTimeoutMs,
            bool createNoWindow = true,
            string standardInput = null,
            int[] validExitCodes = null)
        {
            var result = new CommandResult();
            Process process = null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = createNoWindow,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = !string.IsNullOrEmpty(standardInput),
                };

                process = Process.Start(psi);
                if (process == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start process.";
                    return result;
                }

                if (!string.IsNullOrEmpty(standardInput))
                {
                    try
                    {
                        process.StandardInput.WriteLine(standardInput);
                        process.StandardInput.Close();
                    }
                    catch (IOException) { /* process exited before we could write */ }
                }

                // Read both streams concurrently to avoid deadlock on full buffers
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                // WaitForExit on a thread-pool thread since WaitForExitAsync isn't available
                bool exited = await Task.Run(() => process.WaitForExit(timeoutMs));

                if (!exited)
                {
                    try { process.Kill(); } catch { /* already exited */ }
                    result.Success = false;
                    result.ErrorMessage = $"Command timed out after {timeoutMs}ms.";
                    return result;
                }

                await Task.WhenAll(stdoutTask, stderrTask);

                result.ExitCode = process.ExitCode;
                result.StandardOutput = stdoutTask.Result;
                result.StandardError = stderrTask.Result;

                validExitCodes ??= new[] { 0 };
                result.Success = validExitCodes.Contains(process.ExitCode);

                if (!result.Success)
                    result.ErrorMessage = $"Command failed with exit code {process.ExitCode}.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                process?.Dispose();
            }

            return result;
        }
    }
}
