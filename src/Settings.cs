using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuteMyMic
{
    enum Corner { TopLeft, TopRight, BottomLeft, BottomRight, Custom }

    // What a hotkey or a command-line switch does. The numbers double as hotkey ids.
    enum MicAction { Toggle = 1, Mute = 2, Unmute = 3 }

    // User settings, stored as a small key=value file in %APPDATA%\MuteMyMic\settings.ini
    class Settings
    {
        public string Language = "";            // "" = not chosen yet (first run)
        // Each may also be a mouse button (Keys.XButton1 etc.), Keys.None = not set
        public Keys Hotkey = Keys.Control | Keys.Alt | Keys.F12;  // toggle
        public Keys HotkeyMute = Keys.None;
        public Keys HotkeyUnmute = Keys.None;
        public bool PlaySounds = true;
        public int SoundVolume = 55;            // 0..100
        public bool ShowOverlay = true;
        public string OverlayScreen = "";       // Screen.DeviceName, "" = primary monitor
        public Corner Corner = Corner.BottomRight;
        public int OverlayX, OverlayY;          // Corner.Custom: offset from the monitor's top-left, in pixels
        public int OverlaySize = 64;
        public bool MuteOnLock = false;
        public bool StartMuted = false;         // only for manual starts, never when started with Windows
        public List<string> ExcludedMics = new List<string>();  // device ids the user unticked

        public Keys HotkeyFor(MicAction action)
        {
            return action == MicAction.Mute ? HotkeyMute : action == MicAction.Unmute ? HotkeyUnmute : Hotkey;
        }

        static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MuteMyMic", "settings.ini");
            }
        }

        public Settings Clone()
        {
            var s = (Settings)MemberwiseClone();
            s.ExcludedMics = new List<string>(ExcludedMics);
            return s;
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }

                string v;
                int n;
                if (values.TryGetValue("Language", out v) && L.IndexOf(v) >= 0) s.Language = v;
                if (values.TryGetValue("Hotkey", out v) && int.TryParse(v, out n) && IsValidHotkey((Keys)n)) s.Hotkey = (Keys)n;
                if (values.TryGetValue("HotkeyMute", out v) && int.TryParse(v, out n) && IsValidHotkey((Keys)n)) s.HotkeyMute = (Keys)n;
                if (values.TryGetValue("HotkeyUnmute", out v) && int.TryParse(v, out n) && IsValidHotkey((Keys)n)) s.HotkeyUnmute = (Keys)n;
                if (values.TryGetValue("PlaySounds", out v)) s.PlaySounds = v != "0";
                if (values.TryGetValue("SoundVolume", out v) && int.TryParse(v, out n) && n >= 0 && n <= 100) s.SoundVolume = n;
                if (values.TryGetValue("ShowOverlay", out v)) s.ShowOverlay = v != "0";
                if (values.TryGetValue("OverlayScreen", out v)) s.OverlayScreen = v;
                // Enum.TryParse also accepts any number ("17"), which would crash the settings window.
                Corner corner;
                if (values.TryGetValue("Corner", out v) && Enum.TryParse(v, out corner) && Enum.IsDefined(typeof(Corner), corner))
                    s.Corner = corner;
                if (values.TryGetValue("OverlayX", out v) && int.TryParse(v, out n)) s.OverlayX = n;
                if (values.TryGetValue("OverlayY", out v) && int.TryParse(v, out n)) s.OverlayY = n;
                if (values.TryGetValue("OverlaySize", out v) && int.TryParse(v, out n) && n >= 24 && n <= 256) s.OverlaySize = n;
                if (values.TryGetValue("MuteOnLock", out v)) s.MuteOnLock = v == "1";
                if (values.TryGetValue("StartMuted", out v)) s.StartMuted = v == "1";
                if (values.TryGetValue("ExcludedMics", out v))
                    foreach (var id in v.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                        s.ExcludedMics.Add(id);

                // Settings files from before the language choice existed: don't ask again.
                if (s.Language == "" && values.Count > 0) s.Language = L.SystemLanguage();
            }
            catch
            {
                // Broken settings file: fall back to defaults.
            }
            return s;
        }

        public string Serialize()
        {
            return string.Join("\r\n", new[]
            {
                "Language=" + Language,
                "Hotkey=" + (int)Hotkey,
                "HotkeyMute=" + (int)HotkeyMute,
                "HotkeyUnmute=" + (int)HotkeyUnmute,
                "PlaySounds=" + (PlaySounds ? "1" : "0"),
                "SoundVolume=" + SoundVolume,
                "ShowOverlay=" + (ShowOverlay ? "1" : "0"),
                "OverlayScreen=" + OverlayScreen,
                "Corner=" + Corner,
                "OverlayX=" + OverlayX,
                "OverlayY=" + OverlayY,
                "OverlaySize=" + OverlaySize,
                "MuteOnLock=" + (MuteOnLock ? "1" : "0"),
                "StartMuted=" + (StartMuted ? "1" : "0"),
                "ExcludedMics=" + string.Join("|", ExcludedMics),
            });
        }

        // Written to a temporary file first and then swapped in, so a crash or power loss
        // in the middle of saving can't leave an empty file (which would mean "first run" again).
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Serialize() + "\r\n");
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        // Only what the settings window can produce: None, or Ctrl/Alt/Shift + one real key or mouse button.
        static bool IsValidHotkey(Keys k)
        {
            if (k == Keys.None) return true;
            if ((k & ~(Keys.KeyCode | Keys.Control | Keys.Alt | Keys.Shift)) != 0) return false;
            Keys code = k & Keys.KeyCode;
            if (code == Keys.None || code == Keys.LButton || code == Keys.RButton) return false;
            return code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu;
        }

        public static bool IsMouseButton(Keys keyCode)
        {
            return keyCode == Keys.MButton || keyCode == Keys.XButton1 || keyCode == Keys.XButton2;
        }

        public static string HotkeyToString(Keys k)
        {
            if (k == Keys.None) return L.Get("key.none");
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            if ((k & Keys.Shift) != 0) parts.Add("Shift");
            parts.Add(KeyName(k & Keys.KeyCode));
            return string.Join(" + ", parts);
        }

        static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num " + (int)(key - Keys.NumPad0);
            switch (key)
            {
                case Keys.MButton: return L.Get("key.mouse_middle");
                case Keys.XButton1: return L.Get("key.mouse_x1");
                case Keys.XButton2: return L.Get("key.mouse_x2");
                case Keys.Oemtilde: return "`";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.OemPipe: return "\\";
                case Keys.Pause: return "Pause";
                case Keys.Scroll: return "Scroll Lock";
                case Keys.Next: return "Page Down";
                case Keys.Prior: return "Page Up";
                case Keys.Capital: return "Caps Lock";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
            }
            return key.ToString();
        }
    }

    // "Start with Windows" = a value in HKCU\...\Run (no admin rights needed).
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "MuteMyMic";

        // --autostart tells the app it was started with Windows (microphones must then be on).
        static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\" " + Program.AutostartArg; }
        }

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(ValueName) != null;
        }

        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, Command);
                else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
            }
        }

        // If the exe was moved, point the autostart entry at the new location.
        public static void RefreshPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    var current = key.GetValue(ValueName) as string;
                    if (current != null && current != Command) key.SetValue(ValueName, Command);
                }
            }
            catch { }
        }
    }
}
