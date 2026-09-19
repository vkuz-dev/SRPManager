using System;
using System.Threading;
namespace AWLM.Core
{
    // This class manages a global mutex to prevent AppLocker automatic enabling through this app.
    // If the mutex is held, AppLocker enable function will exit early and not enable AppLocker.
    // This allows the administrator to keep AppLocker disabled while using this app.
    // When the app is closed or the mutex is released via GUI, AppLocker can be enabled again through this app.
    public static class MutexManager
    {
        public const string MutexName = @"Global\AppLockerFreezeLock";

        private static Mutex _mutex;

        /// <summary>
        /// True when the freeze lock is held by *this* process. A freeze set by another
        /// process — a second administrator on this machine — cannot be released here.
        /// </summary>
        public static bool IsOwnedByThisProcess => _mutex != null;

        /// <summary>
        /// Takes the global freeze lock. Returns false if another process already holds it,
        /// in which case this process takes no ownership.
        /// </summary>
        public static bool AcquireFreezeMutex()
        {
            if (_mutex != null)
                return true;

            // When the named mutex already exists, initiallyOwned is ignored and this thread
            // does NOT become the owner — createdNew is the only reliable signal. Storing a
            // mutex we do not own would make the later ReleaseMutex() throw.
            var mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool acquired);

            if (!acquired)
            {
                mutex.Dispose();
                return false;
            }

            _mutex = mutex;
            return true;
        }

        public static void ReleaseFreezeMutex()
        {
            if (_mutex == null)
                return;

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Thrown when this thread is not the owner — the handle is still ours to close.
            }

            _mutex.Dispose();
            _mutex = null;
        }

        public static bool IsMutexActive()
        {
            try
            {
                // Wrap in a 'using' block to instantly close the handle after checking
                using (var m = Mutex.OpenExisting(MutexName))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The mutex does not exist
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // The mutex exists, but we don't have rights to open it.
                // It's still active, so return true.
                return true;
            }
        }
    }
}
