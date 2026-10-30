using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FolderPhotoRenamer
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(13, 18, 29), Surface = Color.FromArgb(21, 29, 44), Raised = Color.FromArgb(29, 40, 59),
            Border = Color.FromArgb(43, 57, 79), Text = Color.FromArgb(237, 242, 255), Muted = Color.FromArgb(150, 166, 190), Accent = Color.FromArgb(142, 120, 255),
            Mint = Color.FromArgb(109, 226, 186), Error = Color.FromArgb(255, 145, 160);
        public static readonly string FontName = HasFont("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        private static bool HasFont(string name) { try { using (var f = new FontFamily(name)) return f.Name == name; } catch { return false; } }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string app, string id);
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
        public bool Primary, Selected;
        private bool hover;
        public DarkButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Theme.Surface; ForeColor = Theme.Text;
            Font = new Font(Theme.FontName, 9.5F, FontStyle.Bold); Cursor = Cursors.Hand; UseMnemonic = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(Parent == null ? Theme.Background : Parent.BackColor)) e.Graphics.FillRectangle(background, ClientRectangle);
            Color fill = Primary && Enabled ? Theme.Accent : Selected ? Color.FromArgb(49, 43, 79) : hover && Enabled ? Theme.Raised : Theme.Surface;
            using (var p = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 9))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Focused || Selected ? Theme.Accent : Theme.Border)) { e.Graphics.FillPath(brush, p); e.Graphics.DrawPath(pen, p); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, !Enabled ? Color.FromArgb(91, 108, 135) : Primary ? Color.FromArgb(18, 14, 40) : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
