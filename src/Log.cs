using System;
using System.IO;

namespace MuteMyMic
{
    // Small error log in %APPDATA%\MuteMyMic\error.log, so failures are visible instead of silently swallowed.
    // Thread-safe; never throws; keeps the file under ~256 KB.
    static class Log
    {
        static readonly object Gate = new object();
        const long MaxBytes = 256 * 1024;

        public static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "MuteMyMic", "error.log");
            }
        }

        public static void Write(Exception ex)
        {
            if (ex != null) Write(ex.ToString());
        }

        public static void Write(string text)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        string old = FilePath + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + AppInfo.Version + "  " + text + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never become the reason the app fails.
            }
        }
    }
}
