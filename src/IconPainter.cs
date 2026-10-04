using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MuteMyMic
{
    // Draws the microphone icons in code, so the app needs no image files.
    static class IconPainter
    {
        public static readonly Color MutedColor = Color.FromArgb(230, 200, 35, 35);
        public static readonly Color LiveColor = Color.FromArgb(230, 70, 70, 70);
        public static readonly Color TrayMutedColor = Color.FromArgb(255, 235, 60, 55);

        /// <summary>Mic in a filled circle: on-screen indicator, window and exe icon.</summary>
        public static Bitmap DrawBadge(int size, bool muted)
        {
            var bmp = NewBitmap(size);
            using (var g = NewGraphics(bmp))
            {
                Color bg = muted ? MutedColor : LiveColor;
                using (var b = new SolidBrush(bg))
                    g.FillEllipse(b, 0.5f, 0.5f, size - 1f, size - 1f);

                float inner = size * 0.6f;
                float off = (size - inner) / 2f;
                DrawGlyph(g, new RectangleF(off, off, inner, inner), Color.White, muted, Color.FromArgb(255, bg), 2.6f);
            }
            return bmp;
        }

        /// <summary>Bare mic that fills the whole square, like other tray icons.</summary>
        public static Bitmap DrawTray(int size, Color color, bool muted)
        {
            var bmp = NewBitmap(size);
            using (var g = NewGraphics(bmp))
                DrawGlyph(g, new RectangleF(0, 0, size, size), color, muted, Color.Transparent, 1.8f);
            return bmp;
        }

        // Glyph geometry is in fractions of the box, so it scales to any size.
        static void DrawGlyph(Graphics g, RectangleF box, Color color, bool muted, Color gapColor, float gapWidth)
        {
            float s = box.Width;
            Func<float, float> X = f => box.X + f * s;
            Func<float, float> Y = f => box.Y + f * s;
            float stroke = Math.Max(1.6f, s * 0.09f);

            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(color, stroke))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;

                // Capsule (mic head)
                float w = s * 0.36f, h = s * 0.54f;
                using (var path = Capsule(new RectangleF(X(0.5f) - w / 2f, Y(0.03f), w, h)))
                    g.FillPath(brush, path);

                // Holder arc, stem and base
                g.DrawArc(pen, X(0.17f), Y(0.24f), s * 0.66f, s * 0.48f, 0, 180);
                g.DrawLine(pen, X(0.5f), Y(0.72f), X(0.5f), Y(0.90f));
                g.DrawLine(pen, X(0.30f), Y(0.92f), X(0.70f), Y(0.92f));

                if (muted)
                {
                    PointF a = new PointF(X(0.10f), Y(0.06f)), b = new PointF(X(0.90f), Y(0.94f));
                    // Cut a gap around the slash (transparent in the tray, background colour in the badge)
                    var oldMode = g.CompositingMode;
                    g.CompositingMode = CompositingMode.SourceCopy;
                    using (var gap = new Pen(gapColor, stroke * gapWidth))
                    {
                        gap.StartCap = gap.EndCap = LineCap.Round;
                        g.DrawLine(gap, a, b);
                    }
                    g.CompositingMode = oldMode;
                    g.DrawLine(pen, a, b);
                }
            }
        }

        /// <summary>
        /// Turns a bitmap into an Icon that owns its own handle, and frees everything else.
        /// (Icon.FromHandle(bmp.GetHicon()) alone leaks a GDI icon every time.)
        /// </summary>
        public static Icon ToIcon(Bitmap bmp)
        {
            using (bmp)
            {
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (var borrowed = Icon.FromHandle(h))
                        return (Icon)borrowed.Clone(); // Clone copies the handle (CopyImage) and owns the copy
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        static Bitmap NewBitmap(int size)
        {
            return new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        static Graphics NewGraphics(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            return g;
        }

        static GraphicsPath Capsule(RectangleF r)
        {
            var p = new GraphicsPath();
            float d = r.Width;
            p.AddArc(r.X, r.Y, d, d, 180, 180);
            p.AddArc(r.X, r.Bottom - d, d, d, 0, 180);
            p.CloseFigure();
            return p;
        }
    }
}
