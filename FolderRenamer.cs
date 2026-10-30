using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace FolderPhotoRenamer
{
    public sealed class FolderRename
    {
        public string OriginalPath { get; set; }
        public string NewPath { get; set; }
        public string TempPath { get; set; }
        public string ObjectId { get; set; }
        public string ParentId { get; set; }
        public string CanonicalPath { get; set; }
        public bool Changed { get { return OriginalPath != NewPath; } }
    }
    public sealed class FolderRenamePlan
    {
        public List<FolderRename> Items { get; set; }
        public string BaseName { get; set; }
        public int StartNumber { get; set; }
        public int ChangeCount { get { return Items.Count(x => x.Changed); } }
    }
    public static class FolderRenamer
    {
        public static FolderRenamePlan Preview(IEnumerable<string> paths, string baseName, int start)
        {
            var items = new List<FolderRename>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                var d = new DirectoryInfo(Path.GetFullPath(path.Trim().Trim('"')));
                if (!d.Exists || d.Parent == null) throw new IOException("Choose existing folders inside a drive.");
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Folder links cannot be renamed. Choose their actual folders: " + d.FullName);
                string canonical = Renamer.FolderIdentity(d.FullName);
                string objectId = Renamer.DirectoryObjectId(d.FullName);
                if (!identities.Add(canonical)) throw new IOException("The same folder was selected twice: " + d.FullName);
                items.Add(new FolderRename { OriginalPath = d.FullName, ObjectId = objectId, ParentId = Renamer.DirectoryObjectId(d.Parent.FullName), CanonicalPath = canonical });
            }
            foreach (var a in items) foreach (var b in items)
                if (a != b && b.CanonicalPath.StartsWith(a.CanonicalPath + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Select either a parent or its nested folders, not both in the same folder-name batch.");
            items.Sort((a, b) => Renamer.NaturalCompare(a.OriginalPath, b.OriginalPath));
            var plan = new FolderRenamePlan { Items = items };
            SetNames(plan, baseName, start);
            return plan;
        }
        public static void SetNames(FolderRenamePlan plan, string baseName, int start)
        {
            if (String.IsNullOrWhiteSpace(baseName)) throw new IOException("Enter a shared folder name.");
            if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new IOException("The shared name contains a character Windows cannot use in a folder name.");
            if (start < 1 || (long)start + plan.Items.Count - 1 > Int32.MaxValue) throw new IOException("Choose a valid starting number.");
            var targets = new List<string>();
            for (int i = 0; i < plan.Items.Count; i++)
            {
                string name = baseName + "_" + ((long)start + i).ToString("D2", CultureInfo.InvariantCulture);
                if (!ValidLeaf(name)) throw new IOException("That folder name is too long or invalid for Windows.");
                targets.Add(Path.Combine(Path.GetDirectoryName(plan.Items[i].OriginalPath), name));
            }
            for (int i = 0; i < targets.Count; i++) plan.Items[i].NewPath = targets[i];
            plan.BaseName = baseName; plan.StartNumber = start;
        }
        public static int Rename(FolderRenamePlan plan, Action<int, int> progress, CancellationToken cancellation)
        {
            using (Renamer.AcquireLock())
            {
                if (Renamer.HasPending) throw new IOException("Recover the interrupted batch first.");
                var fresh = Preview(plan.Items.Select(x => x.OriginalPath), plan.BaseName, plan.StartNumber);
                if (fresh.Items.Count != plan.Items.Count || !fresh.Items.Zip(plan.Items, (a, b) => a.OriginalPath == b.OriginalPath && a.NewPath == b.NewPath && a.ObjectId == b.ObjectId && a.ParentId == b.ParentId).All(x => x)) throw new IOException("Folders changed since the preview. Refresh first. Nothing was renamed.");
                var changed = plan.Items.Where(x => x.Changed).ToList();
                if (changed.Count == 0) return 0;
                var batch = CreateBatch(changed, "rename"); Execute(batch, progress, cancellation); return changed.Count;
            }
        }
        internal static int UndoLocked(BatchJournal last, Action<int, int> progress, CancellationToken cancellation)
        {
            var batch = CreateBatch(last.FolderRenames, "undo"); Execute(batch, progress, cancellation); return batch.FolderRenames.Count;
        }
        internal static int RecoverLocked(BatchJournal batch)
        {
            if (batch.Phase == "committed") Renamer.FinishCommit(batch);
            else { RollBack(batch); File.Delete(Renamer.PendingPath); }
            return batch.FolderRenames.Count;
        }
        private static BatchJournal CreateBatch(List<FolderRename> items, string operation)
        {
            string id = Guid.NewGuid().ToString("N");
            return new BatchJournal { Version = 3, Id = id, Kind = "folders", Operation = operation, Phase = "staging", Jobs = new List<FolderJob>(), FolderRenames = items.Select((x, i) => new FolderRename {
                OriginalPath = x.OriginalPath, NewPath = x.NewPath, ObjectId = x.ObjectId, ParentId = x.ParentId, CanonicalPath = x.CanonicalPath,
                TempPath = Path.Combine(Path.GetDirectoryName(x.OriginalPath), ".photo-renamer-dir-" + id + "-" + i + ".tmp") }).ToList() };
        }
        private static string Source(BatchJournal b, FolderRename x) { return b.Operation == "rename" ? x.OriginalPath : x.NewPath; }
        private static string Target(BatchJournal b, FolderRename x) { return b.Operation == "rename" ? x.NewPath : x.OriginalPath; }
        private static void RequireFolder(string path, FolderRename item)
        {
            if (!Directory.Exists(path) || Renamer.DirectoryObjectId(path) != item.ObjectId) throw new IOException("Original folder missing or replaced: " + path);
            if (Renamer.DirectoryObjectId(Path.GetDirectoryName(path)) != item.ParentId) throw new IOException("The parent folder changed: " + path);
        }
        private static void Preflight(BatchJournal b)
        {
            ValidateJournal(b);
            var sources = new HashSet<string>(b.FolderRenames.Select(x => Source(b, x)), StringComparer.OrdinalIgnoreCase);
            foreach (var item in b.FolderRenames)
            {
                RequireFolder(Source(b, item), item);
                string target = Target(b, item);
                if ((Directory.Exists(target) || File.Exists(target)) && !sources.Contains(target)) throw new IOException("A file or folder already uses this name: " + target + ". Nothing was renamed.");
                if (Directory.Exists(item.TempPath) || File.Exists(item.TempPath)) throw new IOException("A temporary name is already in use. Try again.");
            }
        }
        private static void Execute(BatchJournal b, Action<int, int> progress, CancellationToken cancellation)
        {
            Preflight(b); cancellation.ThrowIfCancellationRequested(); Renamer.WriteJournal(Renamer.PendingPath, b);
            int done = 0, total = b.FolderRenames.Count * 2;
            try
            {
                foreach (var item in b.FolderRenames)
                {
                    cancellation.ThrowIfCancellationRequested(); RequireFolder(Source(b, item), item);
                    Directory.Move(Source(b, item), item.TempPath); if (progress != null) progress(++done, total);
                }
                b.Phase = "finishing"; Renamer.WriteJournal(Renamer.PendingPath, b);
                foreach (var item in b.FolderRenames)
                {
                    cancellation.ThrowIfCancellationRequested(); RequireFolder(item.TempPath, item);
                    Directory.Move(item.TempPath, Target(b, item)); if (progress != null) progress(++done, total);
                }
                cancellation.ThrowIfCancellationRequested(); b.Phase = "committed"; Renamer.WriteJournal(Renamer.PendingPath, b);
            }
            catch (Exception error)
            {
                try { RollBack(Renamer.ReadJournal(Renamer.PendingPath)); File.Delete(Renamer.PendingPath); }
                catch (Exception recovery) { throw new IOException("Batch stopped. Click Recover batch after restoring access to the original folders. " + recovery.Message, error); }
                if (error is OperationCanceledException) throw new OperationCanceledException("Cancelled. Original folder names restored.");
                throw new IOException("Batch stopped. Original folder names restored. " + error.Message, error);
            }
            try { Renamer.FinishCommit(b); }
            catch (Exception error) { throw new IOException("Folders renamed. Click Recover batch to finish saving Undo.", error); }
        }
        private static void RollBack(BatchJournal b)
        {
            bool finishing = b.Phase == "finishing";
            foreach (var item in b.FolderRenames)
                if (finishing || Directory.Exists(item.TempPath)) RequireFolder(Directory.Exists(item.TempPath) ? item.TempPath : Target(b, item), item);
            if (finishing)
            {
                foreach (var item in b.FolderRenames) if (!Directory.Exists(item.TempPath)) Directory.Move(Target(b, item), item.TempPath);
                b.Phase = "staging"; Renamer.WriteJournal(Renamer.PendingPath, b);
            }
            foreach (var item in b.FolderRenames) if (Directory.Exists(item.TempPath)) Directory.Move(item.TempPath, Source(b, item));
        }
        public static string Locate(FolderRename item)
        {
            foreach (string path in new[] { item.TempPath, item.NewPath, item.OriginalPath })
                if (!String.IsNullOrEmpty(path) && Directory.Exists(path) && Renamer.DirectoryObjectId(path) == item.ObjectId) return path;
            return null;
        }
        internal static void ValidateJournal(BatchJournal b)
        {
            if (b.Version != 3 || b.Kind != "folders" || String.IsNullOrEmpty(b.Id) || (b.Operation != "rename" && b.Operation != "undo") || !new[] { "staging", "finishing", "committed" }.Contains(b.Phase) || b.FolderRenames == null || b.FolderRenames.Count == 0) throw new IOException("Saved folder history is invalid.");
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var temps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in b.FolderRenames)
            {
                if (item == null || !SafePath(item.OriginalPath) || !SafePath(item.NewPath) || !SafePath(item.TempPath) || String.IsNullOrEmpty(item.ObjectId) || String.IsNullOrEmpty(item.ParentId)) throw new IOException("Saved folder paths are invalid.");
                string parent = Path.GetDirectoryName(item.OriginalPath);
                if (!String.Equals(parent, Path.GetDirectoryName(item.NewPath), StringComparison.OrdinalIgnoreCase) || !String.Equals(parent, Path.GetDirectoryName(item.TempPath), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(item.TempPath).StartsWith(".photo-renamer-dir-", StringComparison.Ordinal) || !Path.GetFileName(item.TempPath).EndsWith(".tmp", StringComparison.Ordinal) || !sources.Add(item.OriginalPath) || !targets.Add(item.NewPath) || !temps.Add(item.TempPath)) throw new IOException("Saved folder history contains unsafe or duplicate paths.");
            }
            if (temps.Overlaps(sources) || temps.Overlaps(targets)) throw new IOException("Saved folder names overlap recovery names.");
            foreach (string a in sources) foreach (string c in sources) if (a != c && c.StartsWith(a + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Saved folder history contains nested paths.");
        }
        private static bool SafePath(string p)
        {
            return !String.IsNullOrWhiteSpace(p) && Path.IsPathRooted(p) && String.Equals(p, Path.GetFullPath(p), StringComparison.Ordinal) && ValidLeaf(Path.GetFileName(p)) && !String.IsNullOrEmpty(Path.GetDirectoryName(p));
        }
        private static bool ValidLeaf(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 255 || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal)) return false;
            string device = name.Split('.')[0].ToUpperInvariant();
            return device != "CON" && device != "PRN" && device != "AUX" && device != "NUL" && !(device.Length == 4 && (device.StartsWith("COM") || device.StartsWith("LPT")) && "123456789¹²³".IndexOf(device[3]) >= 0);
        }
    }
}
