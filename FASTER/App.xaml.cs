
using FASTER.Models;

using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using ControlzEx.Theming;

namespace FASTER
{
    public partial class App
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct EXCEPTION_RECORD
        {
            public uint   ExceptionCode;
            public uint   ExceptionFlags;
            public IntPtr ExceptionRecord;
            public IntPtr ExceptionAddress;
            public uint   NumberParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public ulong[] ExceptionInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EXCEPTION_POINTERS
        {
            public IntPtr ExceptionRecord;
            public IntPtr ContextRecord;
        }

        private delegate int UnhandledExceptionFilterDelegate(IntPtr exceptionInfo);
        // Field keeps the delegate alive so GC never collects it
        private static UnhandledExceptionFilterDelegate _nativeFilter;

        [DllImport("kernel32.dll")]
        private static extern IntPtr SetUnhandledExceptionFilter(IntPtr lpTopLevelExceptionFilter);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint   processId,
            IntPtr hFile,
            uint   dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        private const int  EXCEPTION_CONTINUE_SEARCH = 0;
        private const uint MiniDumpWithFullMemory     = 0x00000002;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InstallNativeCrashHandler();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.Log($"[FATAL] Unhandled exception (CLR): {args.ExceptionObject}");

            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Log($"[FATAL] Unhandled dispatcher exception: {args.Exception}");
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Logger.Log($"[FATAL] Unobserved task exception: {args.Exception}");
                args.SetObserved();
            };

            var countryCode = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            var userID = AppCenter.GetInstallIdAsync();

            ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncAll;
            ThemeManager.Current.ChangeTheme(Current, FASTER.Properties.Settings.Default.theme);

            AppCenter.SetCountryCode(countryCode);
            AppCenter.SetUserId($"{Environment.UserName}_{Environment.MachineName}_{Environment.UserDomainName}_{userID}");
            Analytics.SetEnabledAsync(true);
            AppCenter.Start("257a7dac-e53c-4bec-b672-b6b939ed5d1e", typeof(Analytics), typeof(Crashes));
        }

        private static void InstallNativeCrashHandler()
        {
            _nativeFilter = NativeCrashFilter;
            SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(_nativeFilter));
        }

        private static int NativeCrashFilter(IntPtr exceptionInfoPtr)
        {
            try
            {
                var exPtrs = Marshal.PtrToStructure<EXCEPTION_POINTERS>(exceptionInfoPtr);
                var exRec  = Marshal.PtrToStructure<EXCEPTION_RECORD>(exPtrs.ExceptionRecord);
                var code   = exRec.ExceptionCode;
                var addr   = exRec.ExceptionAddress;
                var tid    = GetCurrentThreadId();

                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FASTER");
                Directory.CreateDirectory(logDir);

                var line = $"[NATIVE CRASH] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  " +
                           $"Code=0x{code:X8}  Addr=0x{addr.ToInt64():X16}  TID={tid}";

                Logger.Log(line);
                File.AppendAllText(Path.Combine(logDir, "native_crash.log"), line + Environment.NewLine);

                WriteMiniDump(exceptionInfoPtr, logDir);
            }
            catch { /* must not throw from inside SetUnhandledExceptionFilter callback */ }

            return EXCEPTION_CONTINUE_SEARCH;
        }

        private static void WriteMiniDump(IntPtr exceptionInfoPtr, string dumpDir)
        {
            try
            {
                var dumpPath = Path.Combine(dumpDir, $"FASTER_crash_{DateTime.Now:yyyyMMdd_HHmmss}.dmp");
                using var fs = new FileStream(dumpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                var proc = Process.GetCurrentProcess();
                var ok = MiniDumpWriteDump(
                    proc.Handle,
                    (uint)proc.Id,
                    fs.SafeFileHandle.DangerousGetHandle(),
                    MiniDumpWithFullMemory,
                    exceptionInfoPtr,
                    IntPtr.Zero,
                    IntPtr.Zero);

                Logger.Log(ok
                    ? $"[NATIVE CRASH] Minidump written → {dumpPath}"
                    : $"[NATIVE CRASH] MiniDumpWriteDump failed (err={Marshal.GetLastWin32Error()})");
            }
            catch (Exception ex)
            {
                Logger.Log($"[NATIVE CRASH] Minidump failed: {ex.Message}");
            }
        }
    }
}
