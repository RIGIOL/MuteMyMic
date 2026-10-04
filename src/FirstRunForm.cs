using System;
using System.Drawing;
using System.Windows.Forms;

namespace MuteMyMic
{
    // Shown once, on the very first start: language and autostart.
    class FirstRunForm : Form
    {
        public string Language;
        public bool Autostart;

        readonly Label welcome, chooseLabel, laterLabel;
        readonly ListBox langList;
        readonly CheckBox autostartCheck;
        readonly Button continueBtn;
        readonly Font ownFont;

        public FirstRunForm()
        {
            Language = L.SystemLanguage();
            L.Set(Language);

            float k;
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Func<int, int> px = v => (int)Math.Round(v * k);

            Text = "Mute my Mic";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(px(16));
            Font = ownFont = SystemFonts.MessageBoxFont; // a new Font object every call: ours to dispose
            Icon = IconPainter.ToIcon(IconPainter.DrawBadge(32, true));

            int width = px(320);
            var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };

            welcome = new Label { AutoSize = true, MaximumSize = new Size(width, 0), Font = new Font(Font.FontFamily, Font.Size * 1.35f, FontStyle.Bold), Margin = new Padding(0, 0, 0, px(10)) };
            chooseLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, px(4)) };
            langList = new ListBox { Width = width, IntegralHeight = false };
            langList.Items.AddRange(L.NativeNames);
            langList.Height = langList.ItemHeight * 7 + px(4);
            langList.SelectedIndex = L.IndexOf(Language);
            langList.SelectedIndexChanged += (s, e) =>
            {
                Language = L.Codes[langList.SelectedIndex];
                L.Set(Language);
                ApplyTexts();
            };
            langList.DoubleClick += (s, e) => Finish();

            autostartCheck = new CheckBox { AutoSize = true, MaximumSize = new Size(width, 0), Checked = Autostart, Margin = new Padding(0, px(12), 0, px(2)) };
            autostartCheck.CheckedChanged += (s, e) => Autostart = autostartCheck.Checked;
            laterLabel = new Label { AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(0, px(8), 0, 0) };

            continueBtn = new Button { AutoSize = true, MinimumSize = new Size(px(110), px(30)) };
            continueBtn.Click += (s, e) => Finish();
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, MinimumSize = new Size(width, 0), Margin = new Padding(0, px(12), 0, 0) };
            buttons.Controls.Add(continueBtn);

            root.Controls.AddRange(new Control[] { welcome, chooseLabel, langList, autostartCheck, laterLabel, buttons });
            Controls.Add(root);
            AcceptButton = continueBtn;
            ApplyTexts();
        }

        void ApplyTexts()
        {
            welcome.Text = L.Get("first.welcome");
            chooseLabel.Text = L.Get("first.choose_lang");
            autostartCheck.Text = L.Get("set.autostart");
            laterLabel.Text = L.Get("first.later");
            continueBtn.Text = L.Get("first.continue");
        }

        // The window icon and the big welcome font are ours to free — after the window is gone.
        protected override void Dispose(bool disposing)
        {
            Icon icon = Icon;
            Font welcomeFont = welcome != null ? welcome.Font : null;
            base.Dispose(disposing);
            if (disposing)
            {
                if (icon != null) icon.Dispose();
                if (welcomeFont != null) welcomeFont.Dispose();
                if (ownFont != null) ownFont.Dispose();
            }
        }

        void Finish()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
