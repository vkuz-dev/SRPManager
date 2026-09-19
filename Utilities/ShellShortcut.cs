using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AWLM.Utilities
{
    /// <summary>
    /// Creates and removes Windows .lnk shortcuts through the WScript.Shell COM object.
    /// Shared by every feature that plants a shortcut (startup, desktop, Start menu) so the
    /// COM plumbing lives in exactly one place.
    /// </summary>
    public static class ShellShortcut
    {
        /// <summary>Full path of the running executable.</summary>
        public static string AppProcessPath =>
            System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

        /// <summary>File name of the running executable, without extension.</summary>
        public static string AppBaseName =>
            Path.GetFileNameWithoutExtension(AppProcessPath);

        /// <summary>
        /// Returns the path of this app's shortcut in <paramref name="folder"/>, or null.
        /// Tolerates the legacy "SRPManager.exe.lnk" spelling as well as "SRPManager.lnk".
        /// </summary>
        public static string FindIn(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;

            foreach (string file in Directory.EnumerateFiles(folder, "*.lnk"))
            {
                string baseName = Path.GetFileNameWithoutExtension(file);

                if (baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    baseName = baseName.Substring(0, baseName.Length - ".exe".Length);

                if (string.Equals(baseName, AppBaseName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }

            return null;
        }

        public static bool ExistsIn(string folder) => FindIn(folder) != null;

        /// <summary>Removes this app's shortcut from <paramref name="folder"/> if present.</summary>
        public static bool RemoveFrom(string folder)
        {
            string found = FindIn(folder);
            if (found == null || !File.Exists(found))
                return false;

            File.Delete(found);
            return true;
        }

        /// <summary>
        /// Creates (or overwrites) this app's shortcut in <paramref name="folder"/>.
        /// </summary>
        public static void CreateIn(string folder, string description = null)
        {
            Directory.CreateDirectory(folder);
            Create(Path.Combine(folder, AppBaseName + ".lnk"), AppProcessPath, description);
        }

        /// <summary>Creates a .lnk pointing at <paramref name="targetPath"/>.</summary>
        public static void Create(string shortcutPath, string targetPath, string description = null)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell is unavailable on this system.");

            object shell = Activator.CreateInstance(shellType);
            object sc = null;

            try
            {
                sc = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null, shell,
                    new object[] { shortcutPath });

                Type scType = sc.GetType();

                SetProperty(scType, sc, "TargetPath", targetPath);
                SetProperty(scType, sc, "WorkingDirectory", Path.GetDirectoryName(targetPath));
                SetProperty(scType, sc, "IconLocation", targetPath + ",0");

                if (!string.IsNullOrEmpty(description))
                    SetProperty(scType, sc, "Description", description);

                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            finally
            {
                if (sc != null && Marshal.IsComObject(sc))
                    Marshal.ReleaseComObject(sc);

                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.ReleaseComObject(shell);
            }
        }

        private static void SetProperty(Type scType, object sc, string name, object value) =>
            scType.InvokeMember(name, BindingFlags.SetProperty, null, sc, new object[] { value });
    }
}
