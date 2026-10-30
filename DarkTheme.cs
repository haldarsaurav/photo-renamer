using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FolderPhotoRenamer
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(22, 22, 23), Surface = Color.FromArgb(28, 28, 29), Raised = Color.FromArgb(38, 38, 40),
            Border = Color.FromArgb(47, 47, 49), Text = Color.FromArgb(237, 235, 230), Secondary = Color.FromArgb(177, 177, 180), Muted = Color.FromArgb(148, 148, 153),
            Accent = Color.FromArgb(223, 182, 108), Mint = Color.FromArgb(139, 191, 160), Error = Color.FromArgb(245, 146, 151);
        private static readonly PrivateFontCollection Fonts = new PrivateFontCollection();
        // GDI and GDI+ share these process-private font buffers for the app's lifetime.
        // Nothing is installed into Windows or loaded from the network.
        private static readonly List<IntPtr> FontBuffers = new List<IntPtr>();
        private static readonly FontFamily RegularFamily, MediumFamily, MonoFamily;
        public static readonly string FontName;
        static Theme()
        {
            foreach (string resource in new[] { "IBMPlexSans-Regular", "IBMPlexSans-Medium", "IBMPlexMono-Regular" })
            {
                using (var stream = typeof(Theme).Assembly.GetManifestResourceStream("PhotoRenamer." + resource + ".ttf"))
                {
                    if (stream == null) continue;
                    byte[] bytes = new byte[stream.Length]; int offset = 0, count;
                    while (offset < bytes.Length && (count = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += count;
                    IntPtr memory = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, memory, bytes.Length); FontBuffers.Add(memory);
                    uint added = 0; AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref added); Fonts.AddMemoryFont(memory, bytes.Length);
                }
            }
            RegularFamily = Fonts.Families.FirstOrDefault(x => x.Name == "IBM Plex Sans") ?? new FontFamily("Segoe UI");
            MediumFamily = Fonts.Families.FirstOrDefault(x => x.Name == "IBM Plex Sans Medium") ?? RegularFamily;
            MonoFamily = Fonts.Families.FirstOrDefault(x => x.Name == "IBM Plex Mono") ?? new FontFamily("Consolas");
            FontName = RegularFamily.Name;
        }
        public static Font UiFont(float size, bool medium = false) { return new Font(medium ? MediumFamily : RegularFamily, size, FontStyle.Regular); }
        public static Font CodeFont(float size) { return new Font(MonoFamily, size, FontStyle.Regular); }
        [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr file, uint size, IntPtr reserved, ref uint count);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string app, string id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string text);
        public static void CueText(TextBox box, string text)
        {
            box.HandleCreated += delegate { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); };
        }
        public static void DarkWindow(Form form)
        {
            int value = 1; try {
                if (DwmSetWindowAttribute(form.Handle, 20, ref value, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref value, 4);
                int caption = Background.R | Background.G << 8 | Background.B << 16, text = Text.R | Text.G << 8 | Text.B << 16;
                DwmSetWindowAttribute(form.Handle, 35, ref caption, 4); DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
            } catch { }
        }
        public static void DarkControl(Control control)
        {
            Action apply = delegate { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch { } };
            control.HandleCreated += delegate { apply(); }; if (control.IsHandleCreated) apply();
        }
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
    }
    internal sealed class DarkButton : Button
    {
        public bool Primary, Selected, Navigation;
        private bool pressed;
        private float hoverAmount;
        private float hoverFrom, hoverTarget;
        private long feedbackStarted;
        private readonly Timer feedback;
        public DarkButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Theme.Surface; ForeColor = Theme.Text;
            Font = Theme.UiFont(10, true); Cursor = Cursors.Hand; UseMnemonic = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            feedback = new Timer { Interval = 16 };
            feedback.Tick += delegate { float t = Math.Min(1F, (float)((Stopwatch.GetTimestamp() - feedbackStarted) * 1000.0 / Stopwatch.Frequency / 140)); hoverAmount = hoverFrom + (hoverTarget - hoverFrom) * Ease(t); if (t >= 1F) feedback.Stop(); Invalidate(); };
        }
        private void StartFeedback(bool entering)
        {
            hoverFrom = hoverAmount; hoverTarget = entering ? 1F : 0F; feedbackStarted = Stopwatch.GetTimestamp();
            if (SystemInformation.IsMenuAnimationEnabled) feedback.Start(); else { hoverAmount = hoverTarget; Invalidate(); }
        }
        // cubic-bezier(0.22, 1, 0.36, 1), shared by every hover transition.
        private static float Ease(float progress)
        {
            if (progress <= 0 || progress >= 1) return progress;
            float lo = 0, hi = 1, t = 0;
            for (int i = 0; i < 10; i++) { t = (lo + hi) / 2; float v = 1 - t; float x = 3 * v * v * t * 0.22F + 3 * v * t * t * 0.36F + t * t * t; if (x < progress) lo = t; else hi = t; }
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }
        protected override void OnMouseEnter(EventArgs e) { StartFeedback(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { pressed = false; StartFeedback(false); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) feedback.Dispose(); base.Dispose(disposing); }
        private static Color Blend(Color a, Color b, float amount) { return Color.FromArgb(a.R + (int)((b.R - a.R) * amount), a.G + (int)((b.G - a.G) * amount), a.B + (int)((b.B - a.B) * amount)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(Parent == null ? Theme.Background : Parent.BackColor)) e.Graphics.FillRectangle(background, ClientRectangle);
            Color normal = Primary && Enabled ? Theme.Text : Navigation ? Theme.Background : Theme.Surface;
            Color fill = Enabled ? Blend(normal, Primary ? Color.White : Theme.Raised, pressed ? 1F : hoverAmount) : Theme.Surface;
            using (var p = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 8))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Focused && ShowFocusCues ? Theme.Accent : Theme.Border)) { e.Graphics.FillPath(brush, p); if (!Navigation || Focused && ShowFocusCues) e.Graphics.DrawPath(pen, p); }
            if (Navigation && Selected) using (var line = new Pen(Theme.Accent, 2)) e.Graphics.DrawLine(line, 16, Height - 2, Width - 16, Height - 2);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, pressed ? 1 : 0, Width, Height), !Enabled ? Theme.Muted : Primary ? Theme.Background : Navigation && !Selected ? Theme.Secondary : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
