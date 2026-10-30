using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class CreateIcon
{
    private static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static Bitmap Draw(int size)
    {
        var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.ScaleTransform(size / 256F, size / 256F);
            using (var shape = Round(4, 4, 248, 248, 55))
            using (var fill = new LinearGradientBrush(new Point(20, 10), new Point(220, 250), Color.FromArgb(118, 108, 255), Color.FromArgb(67, 47, 196))) g.FillPath(fill, shape);
            using (var shadow = Round(48, 57, 164, 150, 19)) using (var brush = new SolidBrush(Color.FromArgb(40, 24, 14, 95))) g.FillPath(brush, shadow);
            g.TranslateTransform(128, 126); g.RotateTransform(-12); g.TranslateTransform(-128, -126);
            using (var back = Round(38, 48, 162, 145, 17)) using (var brush = new SolidBrush(Color.FromArgb(130, 255, 255, 255))) g.FillPath(brush, back);
            g.ResetTransform(); g.ScaleTransform(size / 256F, size / 256F);
            using (var card = Round(51, 56, 157, 145, 17)) using (var white = new SolidBrush(Color.FromArgb(252, 252, 255))) g.FillPath(white, card);
            using (var image = Round(64, 69, 131, 91, 8))
            {
                using (var sky = new SolidBrush(Color.FromArgb(228, 225, 255))) g.FillPath(sky, image);
                g.SetClip(image);
                using (var sun = new SolidBrush(Color.FromArgb(255, 195, 95))) g.FillEllipse(sun, 152, 82, 23, 23);
                using (var mountain = new SolidBrush(Color.FromArgb(139, 126, 239))) g.FillPolygon(mountain, new[] { new Point(58, 160), new Point(100, 111), new Point(135, 150), new Point(161, 120), new Point(202, 163) });
                using (var mountain = new SolidBrush(Color.FromArgb(93, 76, 205))) g.FillPolygon(mountain, new[] { new Point(65, 165), new Point(119, 132), new Point(143, 148), new Point(173, 122), new Point(203, 165) });
                g.ResetClip();
            }
            using (var line = new Pen(Color.FromArgb(175, 165, 236), 6)) { line.StartCap = line.EndCap = LineCap.Round; g.DrawLine(line, 71, 178, 120, 178); }
            using (var fill = new SolidBrush(Color.FromArgb(105, 240, 197))) g.FillEllipse(fill, 153, 153, 78, 78);
            using (var line = new Pen(Color.FromArgb(29, 82, 83), 7))
            {
                line.StartCap = line.EndCap = LineCap.Round;
                g.DrawLine(line, 184, 174, 177, 210); g.DrawLine(line, 202, 174, 195, 210);
                g.DrawLine(line, 172, 186, 211, 186); g.DrawLine(line, 170, 199, 209, 199);
            }
        }
        return b;
    }
    private static void Main(string[] args)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var payloads = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) using (var image = Draw(sizes[i])) using (var stream = new MemoryStream())
        { image.Save(stream, ImageFormat.Png); payloads[i] = stream.ToArray(); if (sizes[i] == 256) image.Save(Path.Combine(args[0], "app.png"), ImageFormat.Png); }
        using (var stream = File.Create(Path.Combine(args[0], "app.ico"))) using (var w = new BinaryWriter(stream))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length); int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++) { w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)32); w.Write(payloads[i].Length); w.Write(offset); offset += payloads[i].Length; }
            foreach (var data in payloads) w.Write(data);
        }
    }
}
