using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MuteMyMic
{
    // Small always-on-top, click-through window with the crossed-out mic.
    // Uses a per-pixel-alpha layered window so the edges are smooth.
    class OverlayForm : Form
    {
        const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                  WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x8000000;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        /// <summary>The monitor with this device name, or the primary one if it's gone.</summary>
        public static Screen FindScreen(string deviceName)
        {
            foreach (var s in Screen.AllScreens)
                if (s.DeviceName == deviceName) return s;
            return Screen.PrimaryScreen;
        }

        public void ShowAt(Settings s)
        {
            float scale;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            int size = (int)Math.Round(s.OverlaySize * scale);
            int margin = (int)Math.Round(16 * scale);

            Screen screen = FindScreen(s.OverlayScreen);
            Rectangle wa = screen.WorkingArea;
            int x, y;
            if (s.Corner == Corner.Custom)
            {
                // Keep the dragged position, but never let the icon leave the monitor.
                Rectangle b = screen.Bounds;
                x = Math.Max(b.Left, Math.Min(b.Right - size, b.Left + s.OverlayX));
                y = Math.Max(b.Top, Math.Min(b.Bottom - size, b.Top + s.OverlayY));
            }
            else
            {
                x = (s.Corner == Corner.TopLeft || s.Corner == Corner.BottomLeft) ? wa.Left + margin : wa.Right - margin - size;
                y = (s.Corner == Corner.TopLeft || s.Corner == Corner.TopRight) ? wa.Top + margin : wa.Bottom - margin - size;
            }

            Bounds = new Rectangle(x, y, size, size);
            if (!Visible) Show();
            using (var bmp = IconPainter.DrawBadge(size, true)) SetBitmap(bmp, x, y);
            BringToTop();
        }

        public void BringToTop()
        {
            if (Visible) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // TOPMOST, NOSIZE|NOMOVE|NOACTIVATE
        }

        // ---- Dragging: allowed only while the settings window is open ----

        const int GWL_EXSTYLE = -20;
        const int WM_NCHITTEST = 0x0084, WM_SETCURSOR = 0x0020, WM_EXITSIZEMOVE = 0x0232, HTCAPTION = 2;
        bool draggable;

        /// <summary>Raised when the user drops the icon; gives its new top-left corner on screen.</summary>
        public event Action<Point> Dragged;

        public void SetDraggable(bool on)
        {
            draggable = on;
            if (!IsHandleCreated) return;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            ex = on ? ex & ~WS_EX_TRANSPARENT : ex | WS_EX_TRANSPARENT;
            SetWindowLong(Handle, GWL_EXSTYLE, ex);
            // Make Windows pick up the changed style (NOSIZE|NOMOVE|NOZORDER|NOACTIVATE|FRAMECHANGED).
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
            // The modal settings dialog disables the app's other windows; let this one take the mouse.
            if (on) EnableWindow(Handle, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (draggable)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    m.Result = new IntPtr(HTCAPTION); // let Windows move the window like a title bar
                    return;
                }
                if (m.Msg == WM_SETCURSOR)
                {
                    Cursor.Current = Cursors.SizeAll;
                    m.Result = new IntPtr(1);
                    return;
                }
                if (m.Msg == WM_EXITSIZEMOVE)
                {
                    base.WndProc(ref m);
                    RECT r;
                    GetWindowRect(Handle, out r);
                    if (Dragged != null) Dragged(new Point(r.Left, r.Top));
                    return;
                }
            }
            base.WndProc(ref m);
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] static extern bool EnableWindow(IntPtr hWnd, bool enable);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        void SetBitmap(Bitmap bmp, int x, int y)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);
                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var src = new POINT();
                var dst = new POINT { x = x, y = y };
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBmp != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBmp);
                    DeleteObject(hBmp);
                }
                DeleteDC(memDc);
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    }
}
