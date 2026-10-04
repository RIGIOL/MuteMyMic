using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MuteMyMic
{
    class SettingsForm : Form
    {
        // Filled in when the user confirms (OK, or "Yes" to "Save the changes?")
        public Settings Result;
        public bool AutostartResult;

        /// <summary>Raised while the user changes overlay options, so the app can show a live preview.</summary>
        public event Action<Settings> PreviewChanged;

        static readonly int[] Sizes = { 40, 64, 96 };

        readonly Settings working;
        readonly string originalLanguage;
        readonly string originalState;
        readonly List<MicInfo> mics;
        readonly List<string> absentExcluded = new List<string>(); // unticked mics that aren't plugged in now
        bool autostart;
        bool closingViaButton;
        bool suppressPreview;
        float k = 1f;
        Font ownFont;
        Font boldFont;   // one shared font for all section titles (a new Font per label leaked GDI objects)

        readonly List<HotkeyBox> hotkeyBoxes = new List<HotkeyBox>();
        CheckedListBox micList;
        ComboBox monitorBox, cornerBox;
        List<string> screenNames;

        // mics: from AudioService (read on the audio thread, so a hung driver can't freeze this window)
        public SettingsForm(Settings current, List<MicInfo> mics)
        {
            working = current.Clone();
            originalLanguage = L.Current;
            autostart = Autostart.IsEnabled();

            this.mics = mics ?? new List<MicInfo>();
            foreach (var id in working.ExcludedMics)
                if (!this.mics.Exists(m => m.Id == id)) absentExcluded.Add(id);

            originalState = State();

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            Font = ownFont = SystemFonts.MessageBoxFont; // a new Font object every call: ours to dispose
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Icon = IconPainter.ToIcon(IconPainter.DrawBadge(32, true));
            boldFont = new Font(Font, FontStyle.Bold);

            BuildUi();
        }

        int Px(int logical)
        {
            return (int)Math.Round(logical * k);
        }

        // ---------------------------------------------------------------- UI

        void BuildUi()
        {
            SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in Controls) old.Add(c);
            Controls.Clear();
            foreach (var c in old) c.Dispose();
            hotkeyBoxes.Clear();
            micList = null;
            Text = "Mute my Mic — " + L.Get("set.title");
            int width = Px(480);

            var root = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };

            // Language
            var langTable = NewTable(2, width);
            var langBox = Combo();
            langBox.Items.AddRange(L.NativeNames);
            langBox.SelectedIndex = Math.Max(0, L.IndexOf(working.Language == "" ? L.Current : working.Language));
            langBox.SelectedIndexChanged += (s, e) =>
            {
                SyncMics();
                working.Language = L.Codes[langBox.SelectedIndex];
                L.Set(working.Language);
                BeginInvoke(new Action(BuildUi)); // rebuild in the new language
            };
            AddRow(langTable, L.Get("set.language"), langBox, null);
            root.Controls.Add(langTable);

            // Hotkeys
            root.Controls.Add(Section(L.Get("set.grp_hotkeys")));
            var hk = NewTable(3, width);
            AddHotkeyRow(hk, L.Get("set.hk_toggle"), MicAction.Toggle);
            AddHotkeyRow(hk, L.Get("set.hk_mute"), MicAction.Mute);
            AddHotkeyRow(hk, L.Get("set.hk_unmute"), MicAction.Unmute);
            var hint = Hint(L.Get("set.hotkey_hint"), width - Px(150));
            hk.Controls.Add(hint, 1, hk.RowCount);
            hk.SetColumnSpan(hint, 2);
            hk.RowCount++;
            root.Controls.Add(hk);

            // Sound
            var soundTable = NewTable(2, width);
            var volume = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10,
                Value = working.SoundVolume,
                AutoSize = false,
                Height = Px(30),
                Dock = DockStyle.Fill,
                Enabled = working.PlaySounds,
            };
            volume.ValueChanged += (s, e) => working.SoundVolume = volume.Value;
            volume.MouseUp += (s, e) => Sounds.Play(true, volume.Value);   // let the user hear the level
            volume.KeyUp += (s, e) => Sounds.Play(true, volume.Value);
            var soundCheck = Check(L.Get("set.sound"), working.PlaySounds, width, v => { working.PlaySounds = v; volume.Enabled = v; });
            soundTable.Controls.Add(soundCheck, 0, 0);
            soundTable.SetColumnSpan(soundCheck, 2);
            soundTable.RowCount = 1;
            AddRow(soundTable, L.Get("set.volume"), volume, null);
            root.Controls.Add(soundTable);

            // On-screen icon
            root.Controls.Add(Section(L.Get("set.grp_overlay")));
            var ot = NewTable(2, width);
            var showCheck = Check(L.Get("set.show_overlay"), working.ShowOverlay, width, v => { working.ShowOverlay = v; RaisePreview(); });
            ot.Controls.Add(showCheck, 0, ot.RowCount);
            ot.SetColumnSpan(showCheck, 2);
            ot.RowCount++;

            var screens = Screen.AllScreens;
            screenNames = new List<string> { "" };
            monitorBox = Combo();
            monitorBox.Items.Add(L.Get("set.monitor_primary"));
            for (int i = 0; i < screens.Length; i++)
            {
                screenNames.Add(screens[i].DeviceName);
                monitorBox.Items.Add(L.F("set.monitor_n", i + 1, screens[i].Bounds.Width, screens[i].Bounds.Height));
            }
            monitorBox.SelectedIndex = Math.Max(0, screenNames.IndexOf(working.OverlayScreen));
            monitorBox.SelectedIndexChanged += (s, e) =>
            {
                if (suppressPreview) return;
                working.OverlayScreen = screenNames[monitorBox.SelectedIndex];
                RaisePreview();
            };
            AddRow(ot, L.Get("set.monitor"), monitorBox, null);

            cornerBox = Combo();
            cornerBox.Items.AddRange(new object[] { L.Get("corner.tl"), L.Get("corner.tr"), L.Get("corner.bl"), L.Get("corner.br") });
            if (working.Corner == Corner.Custom) cornerBox.Items.Add(L.Get("corner.custom"));
            cornerBox.SelectedIndex = (int)working.Corner;
            cornerBox.SelectedIndexChanged += (s, e) =>
            {
                if (suppressPreview) return;
                working.Corner = (Corner)cornerBox.SelectedIndex;
                RaisePreview();
            };
            AddRow(ot, L.Get("set.corner"), cornerBox, null);

            var sizeBox = Combo();
            sizeBox.Items.AddRange(new object[] { L.Get("size.s"), L.Get("size.m"), L.Get("size.l") });
            int sizeIndex = Array.IndexOf(Sizes, working.OverlaySize);
            sizeBox.SelectedIndex = sizeIndex >= 0 ? sizeIndex : 1;
            sizeBox.SelectedIndexChanged += (s, e) => { working.OverlaySize = Sizes[sizeBox.SelectedIndex]; RaisePreview(); };
            AddRow(ot, L.Get("set.size"), sizeBox, null);
            var dragHint = Hint(L.Get("set.drag_hint"), width);
            ot.Controls.Add(dragHint, 0, ot.RowCount);
            ot.SetColumnSpan(dragHint, 2);
            ot.RowCount++;
            root.Controls.Add(ot);

            // Microphones
            root.Controls.Add(Section(L.Get("set.grp_mics")));
            if (mics.Count == 0)
            {
                root.Controls.Add(Hint(L.Get("err.no_mics"), width));
            }
            else
            {
                micList = new CheckedListBox
                {
                    CheckOnClick = true,
                    IntegralHeight = false,
                    Width = width - Px(6),
                    Margin = new Padding(3, Px(2), 3, Px(2)),
                    BorderStyle = BorderStyle.FixedSingle,
                };
                foreach (var m in mics) micList.Items.Add(m.Name, !working.ExcludedMics.Contains(m.Id));
                int rows = Math.Min(Math.Max(mics.Count, 2), 5);
                micList.Height = micList.ItemHeight * rows + micList.ItemHeight / 2 + Px(4);
                root.Controls.Add(micList);
                root.Controls.Add(Hint(L.Get("set.mics_hint"), width));
            }

            // Other options
            root.Controls.Add(Section(L.Get("set.grp_other")));
            root.Controls.Add(Check(L.Get("set.lock"), working.MuteOnLock, width, v => working.MuteOnLock = v));
            root.Controls.Add(Check(L.Get("set.start_muted"), working.StartMuted, width, v => working.StartMuted = v));
            root.Controls.Add(Check(L.Get("set.autostart"), autostart, width, v => autostart = v));

            // Version + buttons
            var bottom = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                Width = width,
                MinimumSize = new Size(width, 0),
                Margin = new Padding(0, Px(10), 0, 0),
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var version = new Label
            {
                Text = L.F("set.version", AppInfo.Version),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left,
            };
            var ok = new Button { Text = "OK", Width = Px(90), Height = Px(28) };
            var cancel = new Button { Text = L.Get("btn.cancel"), DialogResult = DialogResult.Cancel, Width = Px(90), Height = Px(28) };
            ok.Click += (s, e) => { Commit(); closingViaButton = true; DialogResult = DialogResult.OK; };
            cancel.Click += (s, e) => closingViaButton = true;
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            bottom.Controls.Add(version, 0, 0);
            bottom.Controls.Add(buttons, 1, 0);
            root.Controls.Add(bottom);

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(root);
            ResumeLayout(true);
        }

        void AddHotkeyRow(TableLayoutPanel t, string label, MicAction action)
        {
            var box = new HotkeyBox(working.HotkeyFor(action)) { Dock = DockStyle.Fill };
            box.Accept = combo => AcceptHotkey(box, combo);
            box.Changed += () =>
            {
                if (action == MicAction.Mute) working.HotkeyMute = box.Value;
                else if (action == MicAction.Unmute) working.HotkeyUnmute = box.Value;
                else working.Hotkey = box.Value;
            };
            hotkeyBoxes.Add(box);
            var clear = new Button { Text = L.Get("set.clear"), AutoSize = true, Margin = new Padding(3, 2, 0, 2) };
            clear.Click += (s, e) => box.SetValue(Keys.None);
            AddRow(t, label, box, clear);
        }

        TableLayoutPanel NewTable(int columns, int width)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = columns,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, Px(4)),
            };
            int labelWidth = Px(150), extraWidth = columns == 3 ? Px(100) : 0;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - labelWidth - extraWidth));
            if (columns == 3) t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, extraWidth));
            return t;
        }

        Label Section(string title)
        {
            return new Label
            {
                Text = title,
                AutoSize = true,
                Font = boldFont,
                Margin = new Padding(0, Px(12), 0, Px(4)),
            };
        }

        ComboBox Combo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        }

        Label Hint(string text, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 2, 3, Px(6)),
            };
        }

        CheckBox Check(string text, bool value, int width, Action<bool> onChange)
        {
            // Fixed width with word wrap, so long translations don't run off the window.
            Size textSize = TextRenderer.MeasureText(text, Font, new Size(width - Px(26), 0), TextFormatFlags.WordBreak);
            var c = new CheckBox
            {
                Text = text,
                Checked = value,
                AutoSize = false,
                Size = new Size(width, Math.Max(textSize.Height + Px(6), Px(24))),
                CheckAlign = ContentAlignment.TopLeft,
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(3, Px(2), 3, Px(2)),
            };
            c.CheckedChanged += (s, e) => onChange(c.Checked);
            return c;
        }

        void AddRow(TableLayoutPanel t, string label, Control main, Control extra)
        {
            int row = t.RowCount;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, Px(6), Px(8), 3) }, 0, row);
            t.Controls.Add(main, 1, row);
            if (extra != null) t.Controls.Add(extra, 2, row);
            t.RowCount++;
        }

        // ---------------------------------------------------------------- overlay dragging

        /// <summary>The user dropped the on-screen icon at a new place (top-left corner, screen pixels).</summary>
        public void OverlayMoved(Point topLeft)
        {
            Screen screen = Screen.FromPoint(new Point(topLeft.X + Px(8), topLeft.Y + Px(8)));
            if (OverlayForm.FindScreen(working.OverlayScreen).DeviceName != screen.DeviceName)
                working.OverlayScreen = screen.Primary ? "" : screen.DeviceName;
            working.OverlayX = topLeft.X - screen.Bounds.X;
            working.OverlayY = topLeft.Y - screen.Bounds.Y;
            working.Corner = Corner.Custom;

            suppressPreview = true;
            int screenIndex = screenNames.IndexOf(working.OverlayScreen);
            monitorBox.SelectedIndex = screenIndex >= 0 ? screenIndex : 0;
            if (cornerBox.Items.Count <= (int)Corner.Custom) cornerBox.Items.Add(L.Get("corner.custom"));
            cornerBox.SelectedIndex = (int)Corner.Custom;
            suppressPreview = false;
        }

        // ---------------------------------------------------------------- state

        void SyncMics()
        {
            if (micList == null || micList.Items.Count != mics.Count) return;
            var excluded = new List<string>(absentExcluded);
            for (int i = 0; i < mics.Count; i++)
                if (!micList.GetItemChecked(i)) excluded.Add(mics[i].Id);
            working.ExcludedMics = excluded;
        }

        string State()
        {
            return working.Serialize() + autostart;
        }

        void Commit()
        {
            SyncMics();
            if (working.Language == "") working.Language = L.Current;
            Result = working;
            AutostartResult = autostart;
        }

        protected override void Dispose(bool disposing)
        {
            Icon icon = Icon;
            base.Dispose(disposing);
            if (disposing)
            {
                if (icon != null) icon.Dispose();
                if (boldFont != null) boldFont.Dispose();
                if (ownFont != null) ownFont.Dispose();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RaisePreview();
            Activate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            FitToScreen();
        }

        // On small or heavily scaled screens the window would run past the bottom edge:
        // cap its height and let the content scroll instead.
        void FitToScreen()
        {
            if (Controls.Count == 0) return;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            Control root = Controls[0];
            int needed = root.PreferredSize.Height + Padding.Vertical + (Height - ClientSize.Height);
            int max = MaxHeightForTests > 0 ? MaxHeightForTests : wa.Height - Px(20);
            if (needed <= max)
            {
                if (!AutoSize) { AutoScroll = false; AutoSize = true; root.Dock = DockStyle.Fill; }
                return;
            }
            root.Dock = DockStyle.None;
            if (AutoSize)
            {
                AutoSize = false;
                AutoScroll = true;
                int width = root.PreferredSize.Width + Padding.Horizontal + (Width - ClientSize.Width)
                            + SystemInformation.VerticalScrollBarWidth;
                Size = new Size(width, max);
                Location = new Point(Location.X, Math.Max(wa.Top, wa.Top + (wa.Height - max) / 2));
            }
        }

        // Lets a test pretend the screen is small.
        internal static int MaxHeightForTests = 0;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closingViaButton && DialogResult != DialogResult.OK)
            {
                SyncMics();
                if (State() != originalState)
                {
                    var answer = MessageBox.Show(this, L.Get("ask.save"), "Mute my Mic",
                                                 MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (answer == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                    if (answer == DialogResult.Yes)
                    {
                        Commit();
                        DialogResult = DialogResult.OK;
                    }
                }
            }
            if (DialogResult != DialogResult.OK) L.Set(originalLanguage);
            base.OnFormClosing(e);
        }

        void RaisePreview()
        {
            if (!suppressPreview && PreviewChanged != null) PreviewChanged(working);
        }

        // ---------------------------------------------------------------- hotkey checks

        bool AcceptHotkey(HotkeyBox box, Keys combo)
        {
            if (combo == Keys.None) return true;
            Keys key = combo & Keys.KeyCode;
            bool hasModifier = (combo & Keys.Modifiers) != Keys.None;

            bool isTypingKey = (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9)
                               || key == Keys.Space || key == Keys.Back || key == Keys.Tab;
            if (!hasModifier && isTypingKey)
            {
                MessageBox.Show(this, L.Get("warn.typing_key"), "Mute my Mic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            foreach (var other in hotkeyBoxes)
            {
                if (other != box && other.Value == combo)
                {
                    MessageBox.Show(this, L.F("warn.dup_key", Settings.HotkeyToString(combo)), "Mute my Mic",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            // Keys and buttons that do something useful on their own in other programs
            bool hasOwnJob = key == Keys.End || key == Keys.Home || key == Keys.Delete || key == Keys.Escape || key == Keys.Return
                             || key == Keys.PageUp || key == Keys.PageDown
                             || key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right
                             || Settings.IsMouseButton(key);
            if (!hasModifier && hasOwnJob && combo != box.Value)
            {
                var answer = MessageBox.Show(this, L.F("warn.steals_key", Settings.HotkeyToString(combo)), "Mute my Mic",
                                             MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return false;
            }
            return true;
        }
    }

    // Read-only box that records a key combination or a mouse button (middle / side).
    class HotkeyBox : TextBox
    {
        public Keys Value { get; private set; }

        /// <summary>Asked before a new combination is taken; return false to reject it.</summary>
        public Func<Keys, bool> Accept;

        public event Action Changed;

        public HotkeyBox(Keys value)
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            BackColor = SystemColors.Window;
            Cursor = Cursors.Hand;
            Value = value;
            Text = Settings.HotkeyToString(value);
        }

        public void SetValue(Keys combo)
        {
            Value = combo;
            Text = Settings.HotkeyToString(combo);
            if (Changed != null) Changed();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true; // every key, including arrows and Tab, is a candidate hotkey
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            Keys key = e.KeyCode;
            bool modifierOnly = key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu
                                || key == Keys.LWin || key == Keys.RWin;
            if (modifierOnly)
            {
                // Show the modifiers pressed so far, e.g. "Ctrl + Alt + ..."
                string partial = Settings.HotkeyToString(e.Modifiers | Keys.A);
                Text = partial.Substring(0, partial.Length - 1) + "...";
                return;
            }
            Try(e.Modifiers | key);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Keys button = e.Button == MouseButtons.Middle ? Keys.MButton
                        : e.Button == MouseButtons.XButton1 ? Keys.XButton1
                        : e.Button == MouseButtons.XButton2 ? Keys.XButton2
                        : Keys.None;
            if (button == Keys.None) return; // left/right click just focus the box
            Focus();
            Try(Control.ModifierKeys | button);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            BackColor = Color.LightYellow;
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            BackColor = SystemColors.Window;
            Text = Settings.HotkeyToString(Value);
        }

        void Try(Keys combo)
        {
            if (Accept == null || Accept(combo)) SetValue(combo);
            else Text = Settings.HotkeyToString(Value);
        }
    }
}
