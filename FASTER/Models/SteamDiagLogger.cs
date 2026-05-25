using BytexDigital.Steam.ContentDelivery.Models.Downloading;

namespace FASTER.Models
{
    /// <summary>
    /// Routes DefaultDownloadHandler's DiagnosticLog callback into FASTER's log file.
    /// Only active when debug logging is enabled in Settings.
    /// </summary>
    internal static class SteamDiagLogger
    {
        internal static void Attach(IDownloadHandler handler)
        {
            if (handler is DefaultDownloadHandler ddh)
                ddh.DiagnosticLog = msg => Logger.Log($"  {msg}");
        }
    }
}
