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
            using (var shape = Round(4, 4, 248, 248, 48))
            using (var fill = new SolidBrush(Color.FromArgb(28, 28, 29)))
            using (var edge = new Pen(Color.FromArgb(55, 55, 57), 2)) { g.FillPath(fill, shape); g.DrawPath(edge, shape); }
            using (var line = new Pen(Color.FromArgb(223, 182, 108), 8))
            {
                line.StartCap = line.EndCap = LineCap.Round;
                line.LineJoin = LineJoin.Round;
                using (var frame = Round(48, 36, 160, 184, 16)) g.DrawPath(line, frame);
                g.DrawLines(line, new[] { new Point(68, 132), new Point(103, 96), new Point(130, 122), new Point(152, 100), new Point(188, 136) });
                g.DrawEllipse(line, 157, 65, 13, 13);
                g.DrawLine(line, 76, 184, 108, 184);
                g.DrawLines(line, new[] { new Point(143, 168), new Point(154, 160), new Point(154, 192) });
                g.DrawLine(line, 141, 192, 168, 192);
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
