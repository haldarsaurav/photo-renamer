using System;
using System.IO;
using System.Windows.Forms;
using FolderPhotoRenamer;

internal static class UiCheck
{
    [STAThread] private static void Main()
    {
        string app = AppDomain.CurrentDomain.BaseDirectory;
        string root = Path.Combine(app, "tests", "desktop-" + Guid.NewGuid().ToString("N"));
        string[] folders = { Path.Combine(root, "First album"), Path.Combine(root, "Second album") };
        foreach (string folder in folders) { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "IMG_1.jpg"), "sample one"); File.WriteAllText(Path.Combine(folder, "IMG_2.HEIC"), "sample two"); }
        File.WriteAllText(Path.Combine(app, "tests", "last-desktop-check.txt"), root);
        Renamer.StateRoot = Path.Combine(root, "private-test-state");
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var form = new RenamerForm(folders); form.Text = "Photo Renamer — UI check"; Application.Run(form);
    }
}
