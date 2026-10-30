using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using FolderPhotoRenamer;

// Hosts the real release UI with generated photos and isolated history.
internal static class MediaDemo
{
    [STAThread] private static void Main()
    {
        string repository = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        string root = Path.Combine(repository, ".build", "media-demo-" + Guid.NewGuid().ToString("N"));
        string[] folders = { Path.Combine(root, "\u2708\ufe0f Holiday"), Path.Combine(root, "\ud83c\udf32 Weekend") };
        foreach (string folder in folders)
        {
            Directory.CreateDirectory(folder);
            foreach (int number in new[] { 1, 2, 10, 11 })
            {
                using (var photo = new Bitmap(160, 100))
                using (var graphics = Graphics.FromImage(photo))
                using (var fill = new LinearGradientBrush(new Point(0, 0), new Point(160, 100), Color.FromArgb(60, 80 + number, 150), Color.FromArgb(100, 190, 160)))
                {
                    graphics.FillRectangle(fill, 0, 0, 160, 100);
                    bool png = number == 10;
                    photo.Save(Path.Combine(folder, "IMG_" + number.ToString("D4") + (png ? ".png" : ".jpg")), png ? ImageFormat.Png : ImageFormat.Jpeg);
                }
            }
        }
        Renamer.StateRoot = Path.Combine(root, "isolated-history");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new RenamerForm(folders));
    }
}
