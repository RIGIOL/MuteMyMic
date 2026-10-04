using System;
using System.Threading;
using System.Windows.Forms;

namespace MuteMyMic
{
    static class Program
    {
        // Added to the "Start with Windows" entry so the app knows it was started at sign-in.
        public const string AutostartArg = "--autostart";

        [STAThread]
        static void Main(string[] args)
        {
            // An unexpected error must not kill a tray app that may be holding the mic muted:
            // log it and keep running instead of showing the .NET crash dialog.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Log.Write(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write(e.ExceptionObject as Exception);

            bool autostarted = false;
            MicAction? command = null;
            foreach (string a in args)
            {
                string arg = a.Trim().ToLowerInvariant();
                if (arg == AutostartArg) autostarted = true;
                else if (arg == "--toggle") command = MicAction.Toggle;
                else if (arg == "--mute") command = MicAction.Mute;
                else if (arg == "--unmute") command = MicAction.Unmute;
            }

            var settings = Settings.Load();
            L.Set(settings.Language == "" ? L.SystemLanguage() : settings.Language);

            bool createdNew;
            using (var mutex = new Mutex(true, "MuteMyMic_SingleInstance_7F3A2C", out createdNew))
            {
                if (!createdNew)
                {
                    // Already running: pass the command on, or explain where the app is.
                    if (command.HasValue) HotkeyManager.SendToRunningInstance(command.Value, 5000); // it may still be starting
                    else if (!autostarted)
                        MessageBox.Show(L.Get("already_running"), "Mute my Mic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool firstRun = settings.Language == "";
                if (firstRun)
                {
                    using (var form = new FirstRunForm())
                    {
                        form.ShowDialog();
                        settings.Language = form.Language;
                        try
                        {
                            settings.Save();
                            if (form.Autostart) Autostart.Set(true);
                        }
                        catch { }
                    }
                    L.Set(settings.Language);
                }

                // AudioService posts its results through this context.
                if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                Application.Run(new TrayApp(settings, firstRun, autostarted, command));
            }
        }
    }
}
