using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuteMyMic
{
    // UI thread only. Every audio call goes through AudioService (its own thread),
    // so nothing here can block on a slow or hung audio driver.
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly HotkeyManager hotkeys;
        readonly AudioService audio;
        readonly OverlayForm overlay;
        readonly Timer topmostTimer;    // keeps the on-screen icon above other "always on top" windows
        readonly Timer watchdog;        // a user command got no answer from the audio thread in time
        ToolStripMenuItem toggleItem;
        Font menuBoldFont;
        Icon iconLive, iconMuted;

        Settings settings;
        SettingsForm settingsForm;
        bool muted;                     // last state reported by AudioService
        bool mutedByLock;
        int pendingCommands;
        bool exitRequested, exiting;


        public TrayApp(Settings loaded, bool firstRun, bool autostarted, MicAction? command)
        {
            settings = loaded;
            Autostart.RefreshPath();

            tray = new NotifyIcon { Visible = false };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Do(MicAction.Toggle); };
            BuildMenu();
            MakeTrayIcons();

            overlay = new OverlayForm();
            overlay.Dragged += p => { if (settingsForm != null) settingsForm.OverlayMoved(p); };

            // Created after the first Control, so it captures the WinForms SynchronizationContext.
            audio = new AudioService(settings.ExcludedMics);
            audio.StateChanged += OnAudioState;
            audio.CommandCompleted += OnCommandCompleted;
            // Started with Windows: microphones always on. Manual start: optionally muted.
            audio.Initialize(autostarted, settings.StartMuted && !autostarted && !command.HasValue);

            hotkeys = new HotkeyManager();
            hotkeys.Triggered += a => Do(a);
            List<Keys> busy = ApplyHotkeys();

            topmostTimer = new Timer { Interval = 3000 };
            topmostTimer.Tick += (s, e) => { if (overlay.Visible) overlay.BringToTop(); };

            watchdog = new Timer { Interval = 3000 };
            watchdog.Tick += (s, e) =>
            {
                watchdog.Stop();
                Balloon(L.Get("err.audio_busy"), ToolTipIcon.Warning);
            };

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.SessionEnded += OnSessionEnded;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            tray.Visible = true;
            UpdateUi();

            if (command.HasValue) Do(command.Value);

            if (busy.Count > 0)
                Balloon(L.F("err.hotkey_busy", string.Join(", ", busy.ConvertAll(Settings.HotkeyToString))), ToolTipIcon.Warning);
            else if (firstRun && settings.Hotkey != Keys.None)
                Balloon(L.F("started", Settings.HotkeyToString(settings.Hotkey)), ToolTipIcon.Info);
        }

        /// <summary>Registers all hotkeys; returns the ones that could not be registered.</summary>
        List<Keys> ApplyHotkeys()
        {
            return hotkeys.Apply(new[]
            {
                new KeyValuePair<MicAction, Keys>(MicAction.Toggle, settings.Hotkey),
                new KeyValuePair<MicAction, Keys>(MicAction.Mute, settings.HotkeyMute),
                new KeyValuePair<MicAction, Keys>(MicAction.Unmute, settings.HotkeyUnmute),
            });
        }

        void BuildMenu()
        {
            var old = tray.ContextMenuStrip;
            var menu = new ContextMenuStrip();
            toggleItem = new ToolStripMenuItem("", null, (s, e) => Do(MicAction.Toggle));
            if (menuBoldFont == null) menuBoldFont = new Font(toggleItem.Font, FontStyle.Bold); // created once, not per rebuild
            toggleItem.Font = menuBoldFont;
            toggleItem.Text = L.Get(muted ? "menu.unmute" : "menu.mute");
            menu.Items.Add(toggleItem);
            menu.Items.Add(L.Get("menu.settings"), null, (s, e) => OpenSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.Get("menu.exit"), null, (s, e) => ExitApp());
            tray.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        // ---------------------------------------------------------------- muting

        void Do(MicAction action)
        {
            if (exiting) return;
            pendingCommands++;
            watchdog.Stop();
            watchdog.Start();
            audio.Do(action, true);
        }

        void OnCommandCompleted(MicAction action, CommandResult result, bool nowMuted, string error)
        {
            if (exiting) return;
            if (--pendingCommands <= 0)
            {
                pendingCommands = 0;
                watchdog.Stop();
            }
            if (result == CommandResult.NoMics)
            {
                Balloon(L.Get("err.no_mics"), ToolTipIcon.Warning);
            }
            else if (result == CommandResult.Failed)
            {
                Balloon(error != null ? L.F("err.generic", error) : L.Get("err.audio_busy"), ToolTipIcon.Error);
            }
            else
            {
                mutedByLock = false; // the user took over
                if (settings.PlaySounds) Sounds.Play(nowMuted, settings.SoundVolume);
            }
        }

        void OnAudioState(bool nowMuted)
        {
            if (exiting) return;
            muted = nowMuted;
            if (!muted) mutedByLock = false;
            UpdateUi();
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (exiting) return;
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                if (settings.MuteOnLock && !muted)
                    audio.MuteForLock(changed => { if (changed) mutedByLock = true; });
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                // Only undo what the lock did; if the user muted by hand, leave it muted.
                if (mutedByLock && muted) audio.Do(MicAction.Unmute, false);
                mutedByLock = false;
            }
        }

        // Windows remembers the mute state across restarts; switch the mics back on when the
        // session really ends (WM_ENDSESSION), so the computer always starts with them on.
        // Not on SessionEnding: that is only the question — if the shutdown is cancelled,
        // the mic would be live while the user believes it is muted.
        void OnSessionEnded(object sender, SessionEndedEventArgs e)
        {
            audio.UnmuteAllAndWait(3000);
        }

        // ---------------------------------------------------------------- UI

        void UpdateUi()
        {
            tray.Icon = muted ? iconMuted : iconLive;

            string tip = "Mute my Mic — " + L.Get(muted ? "tip.muted" : "tip.live");
            if (settings.Hotkey != Keys.None) tip += " (" + Settings.HotkeyToString(settings.Hotkey) + ")";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip; // NotifyIcon throws above 63 chars

            toggleItem.Text = L.Get(muted ? "menu.unmute" : "menu.mute");

            // While the settings window is open it drives a live preview instead.
            if (settingsForm == null) ShowOverlayFor(settings, muted);
        }

        void ShowOverlayFor(Settings s, bool visible)
        {
            if (visible && s.ShowOverlay)
            {
                overlay.ShowAt(s);
                topmostTimer.Start();
            }
            else
            {
                topmostTimer.Stop();
                if (overlay.Visible) overlay.Hide();
            }
        }

        // Tray icons follow the taskbar colour: white glyph on a dark taskbar, black on a light one.
        void MakeTrayIcons()
        {
            int size = SystemInformation.SmallIconSize.Width;
            Color live = TaskbarIsLight() ? Color.Black : Color.White;
            Icon oldLive = iconLive, oldMuted = iconMuted;
            iconLive = IconPainter.ToIcon(IconPainter.DrawTray(size, live, false));
            iconMuted = IconPainter.ToIcon(IconPainter.DrawTray(size, IconPainter.TrayMutedColor, true));
            if (oldLive != null) oldLive.Dispose();
            if (oldMuted != null) oldMuted.Dispose();
        }

        static bool TaskbarIsLight()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("SystemUsesLightTheme");
                    return v is int && (int)v == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (exiting || e.Category != UserPreferenceCategory.General) return;
            MakeTrayIcons();
            tray.Icon = muted ? iconMuted : iconLive;
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            if (!exiting && settingsForm == null) ShowOverlayFor(settings, muted);
        }

        // ---------------------------------------------------------------- settings

        void OpenSettings()
        {
            if (exiting) return;
            if (settingsForm != null)
            {
                settingsForm.Activate();
                return;
            }

            List<MicInfo> mics = audio.ListMics(2000);
            // Release the hotkeys so they can be pressed into the settings boxes.
            hotkeys.UnregisterAll();
            settingsForm = new SettingsForm(settings, mics);
            settingsForm.PreviewChanged += s =>
            {
                ShowOverlayFor(s, true);
                overlay.SetDraggable(true);
            };
            try
            {
                if (settingsForm.ShowDialog() == DialogResult.OK && settingsForm.Result != null)
                    ApplySettings(settingsForm);
            }
            finally
            {
                overlay.SetDraggable(false);
                settingsForm.Dispose();
                settingsForm = null;
            }

            if (exitRequested)
            {
                ExitApp();   // "Exit" was clicked while the settings window was open
                return;
            }

            L.Set(settings.Language);
            BuildMenu();
            List<Keys> busy = ApplyHotkeys();
            if (busy.Count > 0)
                MessageBox.Show(L.F("err.hotkey_busy", string.Join(", ", busy.ConvertAll(Settings.HotkeyToString))),
                                "Mute my Mic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateUi();
        }

        void ApplySettings(SettingsForm form)
        {
            Settings next = form.Result;
            if (string.Join("|", next.ExcludedMics) != string.Join("|", settings.ExcludedMics))
                audio.SetExcluded(next.ExcludedMics);
            settings = next;

            try
            {
                settings.Save();
                Autostart.Set(form.AutostartResult);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
                MessageBox.Show(L.F("err.save", ex.Message), "Mute my Mic", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------------------------------------------------------- misc

        void Balloon(string text, ToolTipIcon icon)
        {
            if (exiting) return;
            tray.ShowBalloonTip(5000, "Mute my Mic", text, icon);
        }

        void ExitApp()
        {
            if (exiting) return;
            if (settingsForm != null)
            {
                // Close the dialog first; OpenSettings finishes the exit when ShowDialog returns.
                exitRequested = true;
                settingsForm.Close();
                return;
            }
            exiting = true;

            topmostTimer.Stop();
            watchdog.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.SessionEnded -= OnSessionEnded;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            hotkeys.Dispose();

            // Don't leave the microphone dead after the app is gone.
            if (muted) audio.UnmuteAllAndWait(2000);
            audio.Dispose();
            Sounds.Shutdown();

            overlay.Close();
            tray.Visible = false;
            if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose();
            tray.Dispose();
            topmostTimer.Dispose();
            watchdog.Dispose();
            if (iconLive != null) iconLive.Dispose();
            if (iconMuted != null) iconMuted.Dispose();
            if (menuBoldFont != null) menuBoldFont.Dispose();
            ExitThread();
        }
    }
}
