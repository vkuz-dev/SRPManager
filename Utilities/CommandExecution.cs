using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

        /// <summary>
        /// Runs a command to completion and captures its output.
        /// </summary>
        /// <param name="fileName">The executable to run</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="output">Captured standard output on success</param>
        /// <param name="errorMessage">Failure reason, or null on success</param>
        /// <param name="timeoutMs">Wall-clock budget for the whole call</param>
        /// <returns>True if the command exited with code 0 within the timeout</returns>
        public static bool ExecuteCommand(
            string fileName,
            string arguments,
            out string output,
            out string errorMessage,
            int timeoutMs = DefaultTimeoutMs)
        {
            output = null;
            errorMessage = null;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var stdOut = new StringBuilder();
                var stdErr = new StringBuilder();

                using (Process process = new Process { StartInfo = psi })
                using (var stdOutClosed = new ManualResetEventSlim(false))
                using (var stdErrClosed = new ManualResetEventSlim(false))
                {
                    // Pump both pipes on background threads. Reading one to completion before
                    // touching the other deadlocks as soon as the child fills the other buffer.
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data == null) stdOutClosed.Set();
                        else stdOut.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data == null) stdErrClosed.Set();
                        else stdErr.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // The timeout has to cover the pipe drain too, not just process exit —
                    // otherwise it can never fire, because a blocking read gets there first.
                    var deadline = new Stopwatch();
                    deadline.Start();

                    bool completed =
                        process.WaitForExit(timeoutMs) &&
                        stdOutClosed.Wait(Remaining(deadline, timeoutMs)) &&
                        stdErrClosed.Wait(Remaining(deadline, timeoutMs));

                    if (!completed)
                    {
                        try { process.Kill(); } catch { /* raced with exit */ }
                        errorMessage = $"Command timed out after {timeoutMs}ms and was terminated.";
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        errorMessage = $"Command failed with exit code {process.ExitCode}. {stdErr}";
                        return false;
                    }

                    output = stdOut.ToString();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static int Remaining(Stopwatch elapsed, int timeoutMs) =>
            (int)Math.Max(0, timeoutMs - elapsed.ElapsedMilliseconds);

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
