using System;
using System.Reflection;

namespace MuteMyMic
{
    // Version of this build, read from the exe itself (AssemblyVersion).
    // The app is fully offline: it never connects to the internet, not even to look for updates.
    static class AppInfo
    {
        public static Version Version
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }
    }
}