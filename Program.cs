using System;
using System.Reflection;
using System.Windows.Forms;

using AWLM.Core;

namespace AWLM
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                bool attached = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                bool allocated = false;

                if (!attached)
                {
                    NativeMethods.AllocConsole();
                    allocated = true;
                }

                Console.WriteLine();

                // Run async CLI work synchronously — fine here since we're not pumping a UI message loop
                CliHandler.HandleArgs(args).GetAwaiter().GetResult();

                Console.WriteLine();
                Console.Out.Flush();

                if (allocated)
                    NativeMethods.FreeConsole();

                return;
            }

            // Create Mutex
            // This mutex ensures that there is only one instance of the app running for the user
            string mutexName = "SRPManager_SingleInstance_" + Environment.UserName.ToUpperInvariant();
            using (var mutex = new System.Threading.Mutex(true, mutexName, out bool isNewInstance))
            {
                if (!isNewInstance)
                {
                    MessageBox.Show(
                        "SRPManager is already running.",
                        "SRPManager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AWLM_App());
            }
        }
    }

    public static class AppBuildVersion
    {
        public static string Version =>
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
    }

    internal static class NativeMethods
    {
        internal const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AllocConsole();

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FreeConsole();
    }
}
