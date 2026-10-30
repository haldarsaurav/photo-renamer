using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace FolderPhotoRenamer
{
    public enum NumberPosition { End, Start }

    public sealed class RenameItem
    {
        public string OriginalName { get; set; }
        public string NewName { get; set; }
        public string TempName { get; set; }
        public long Length { get; set; }
        public long ModifiedUtc { get; set; }
        public bool Changed { get { return OriginalName != NewName; } }
    }
    public sealed class RenamePlan
    {
        public string Folder { get; set; }
        public string Identity { get; set; }
        public string ObjectId { get; set; }
        public string Prefix { get; set; }
        public NumberPosition NumberPosition { get; set; }
        public List<RenameItem> Items { get; set; }
        public int ChangeCount { get { return Items.Count(x => x.Changed); } }
    }
    public sealed class FolderJob
    {
        public string Folder { get; set; }
        public string Identity { get; set; }
        public string ObjectId { get; set; }
        public string Phase { get; set; }
        public List<RenameItem> Items { get; set; }
    }
    public sealed class BatchJournal
    {
        public int Version { get; set; }
        public string Id { get; set; }
        public string Operation { get; set; }
        public string Phase { get; set; }
        public List<FolderJob> Jobs { get; set; }
        public string Kind { get; set; }
        public List<FolderRename> FolderRenames { get; set; }
        public int PhotoCount { get { return Jobs == null ? 0 : Jobs.Sum(x => x.Items.Count); } }
        public int ItemCount { get { return Kind == "folders" ? FolderRenames.Count : PhotoCount; } }
    }
    public static class Renamer
    {
        public static string StateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Photo Renamer");
        public static string UndoPath { get { return Path.Combine(StateRoot, "undo.dat"); } }
        public static string PendingPath { get { return Path.Combine(StateRoot, "pending.dat"); } }
        public static bool HasUndo { get { return File.Exists(UndoPath); } }
        public static bool HasPending { get { return File.Exists(PendingPath); } }
        private static readonly HashSet<string> Extensions = new HashSet<string>((".jpg .jpeg .jpe .jfif .png .heic .heif .avif .webp .gif .bmp .tif .tiff .dng .cr2 .cr3 .crw .nef .nrw .arw .srf .sr2 .raf .orf .rw2 .rwl .raw .pef .ptx .srw .3fr .fff .iiq .kdc .dcr .mos .mrw .x3f").Split(' '), StringComparer.OrdinalIgnoreCase);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);

        internal static string FolderIdentity(string folder)
        {
            using (var handle = CreateFile(folder, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("Cannot open folder: " + folder);
                var buffer = new StringBuilder(32768);
                uint size = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 1);
                if (size == 0) size = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                if (size == 0 || size >= buffer.Capacity) throw new IOException("Cannot identify folder safely: " + folder);
                return buffer.ToString().TrimEnd('\\').ToUpperInvariant();
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
        internal static string DirectoryObjectId(string folder)
        {
            using (var handle = CreateFile(folder, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                FileInformation info;
                if (handle.IsInvalid || !GetFileInformationByHandle(handle, out info)) throw new IOException("Cannot verify folder identity: " + folder);
                var path = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 1);
                if (length == 0) length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0);
                if (length == 0 || length >= path.Capacity) throw new IOException("Cannot verify the folder's volume: " + folder);
                string full = path.ToString().ToUpperInvariant();
                string volume = full.StartsWith("\\\\?\\VOLUME{") ? full.Substring(0, full.IndexOf('}') + 1) : full.StartsWith("\\\\?\\UNC\\") ? String.Join("\\", full.Split('\\').Take(6)) : full.Substring(0, Math.Min(7, full.Length));
                return volume + ":" + info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8");
            }
        }
        public static RenamePlan Preview(string folder)
        {
            return Preview(folder, NumberPosition.End);
        }
        public static RenamePlan Preview(string folder, NumberPosition position)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(folder.Trim().Trim('"')));
            if (!directory.Exists) throw new IOException("Folder not found: " + folder);
            if (directory.Parent == null) throw new IOException("Choose a photo folder inside the drive.");
            var files = directory.GetFiles().Where(f => Extensions.Contains(f.Extension) && (f.Attributes & FileAttributes.ReparsePoint) == 0).ToList();
            files.Sort((a, b) => NaturalCompare(a.Name, b.Name));
            var plan = new RenamePlan { Folder = directory.FullName, Identity = FolderIdentity(directory.FullName), ObjectId = DirectoryObjectId(directory.FullName), Prefix = directory.Name, Items = new List<RenameItem>() };
            for (int i = 0; i < files.Count; i++)
            {
                string target = PhotoName(directory.Name, i + 1, files[i].Extension, position);
                if (target.Length > 255) throw new IOException("Shorten this folder's name first: " + directory.Name);
                plan.Items.Add(new RenameItem { OriginalName = files[i].Name, NewName = target, Length = files[i].Length, ModifiedUtc = files[i].LastWriteTimeUtc.Ticks });
            }
            plan.NumberPosition = position;
            return plan;
        }
        private static string PhotoName(string folderName, int number, string extension, NumberPosition position)
        {
            if (position != NumberPosition.Start && position != NumberPosition.End) throw new ArgumentOutOfRangeException("position");
            string index = number.ToString(CultureInfo.InvariantCulture);
            return (position == NumberPosition.Start ? index + "_" + folderName : folderName + "_" + index) + extension;
        }
        public static void SetNumberPosition(RenamePlan plan, NumberPosition position)
        {
            // Only reformat the preview. Preserve its source names and metadata for the failsafe check.
            for (int i = 0; i < plan.Items.Count; i++) plan.Items[i].NewName = PhotoName(plan.Prefix, i + 1, Path.GetExtension(plan.Items[i].OriginalName), position);
            plan.NumberPosition = position;
        }
        public static int NaturalCompare(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (IsDigit(a[i]) && IsDigit(b[j]))
                {
                    int ai = i, bj = j;
                    while (i < a.Length && IsDigit(a[i])) i++;
                    while (j < b.Length && IsDigit(b[j])) j++;
                    int az = ai, bz = bj;
                    while (az < i - 1 && a[az] == '0') az++;
                    while (bz < j - 1 && b[bz] == '0') bz++;
                    int result = (i - az).CompareTo(j - bz);
                    if (result == 0) result = String.CompareOrdinal(a, az, b, bz, i - az);
                    if (result != 0) return result;
                    result = (i - ai).CompareTo(j - bj);
                    if (result != 0) return result;
                }
                else
                {
                    int result = Char.ToUpperInvariant(a[i]).CompareTo(Char.ToUpperInvariant(b[j]));
                    if (result != 0) return result;
                    i++; j++;
                }
            }
            int length = (a.Length - i).CompareTo(b.Length - j);
            return length == 0 ? String.CompareOrdinal(a, b) : length;
        }
        private static bool IsDigit(char c) { return c >= '0' && c <= '9'; }

        public static int RenameBatch(IList<RenamePlan> plans, Action<int, int> progress, CancellationToken cancellation)
        {
            using (AcquireLock())
            {
                if (HasPending) throw new IOException("Click Recover batch before starting another rename.");
                if (plans == null || plans.Count == 0) return 0;
                var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var plan in plans)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!identities.Add(plan.Identity)) throw new IOException("The same physical folder was added twice: " + plan.Folder);
                    if (!SamePlan(plan, Preview(plan.Folder, plan.NumberPosition))) throw new IOException("Photos changed since the preview in " + plan.Prefix + ". Refresh first. Nothing was renamed.");
                }
                var batch = MakeBatch(plans.Where(x => x.ChangeCount > 0).Select(p => new FolderJob { Folder = p.Folder, Identity = p.Identity, ObjectId = p.ObjectId, Items = p.Items.Where(x => x.Changed).ToList() }).ToList(), "rename");
                if (batch.Jobs.Count == 0) return 0;
                Execute(batch, progress, cancellation);
                return batch.PhotoCount;
            }
        }
        public static int UndoBatch(Action<int, int> progress, CancellationToken cancellation)
        {
            using (AcquireLock())
            {
                if (HasPending) throw new IOException("Recover the interrupted batch first.");
                if (!HasUndo) throw new IOException("There is no saved batch to undo.");
                var last = ReadJournal(UndoPath);
                if (last.Kind == "folders") return FolderRenamer.UndoLocked(last, progress, cancellation);
                var batch = MakeBatch(last.Jobs, "undo");
                Execute(batch, progress, cancellation);
                return batch.PhotoCount;
            }
        }
        public static int RecoverBatch()
        {
            using (AcquireLock())
            {
                if (!HasPending) return 0;
                var batch = ReadJournal(PendingPath);
                if (batch.Kind == "folders") return FolderRenamer.RecoverLocked(batch);
                if (batch.Phase == "committed") FinishCommit(batch);
                else { RollBackAll(batch); File.Delete(PendingPath); }
                return batch.PhotoCount;
            }
        }
        internal static FileStream AcquireLock()
        {
            Directory.CreateDirectory(StateRoot);
            try { return new FileStream(Path.Combine(StateRoot, "batch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException error) { throw new IOException("Another Photo Renamer window is working. Wait for it to finish.", error); }
        }
        private static BatchJournal MakeBatch(List<FolderJob> jobs, string operation)
        {
            string id = Guid.NewGuid().ToString("N");
            return new BatchJournal { Version = 2, Id = id, Operation = operation, Phase = "running", Jobs = jobs.Select((job, n) => new FolderJob {
                Folder = job.Folder, Identity = job.Identity, ObjectId = job.ObjectId, Phase = "staging", Items = job.Items.Select((x, i) => new RenameItem {
                    OriginalName = x.OriginalName, NewName = x.NewName, Length = x.Length, ModifiedUtc = x.ModifiedUtc,
                    TempName = ".photo-renamer-" + id + "-" + n + "-" + i + ".tmp" }).ToList() }).ToList() };
        }
        private static string Source(BatchJournal b, RenameItem x) { return b.Operation == "rename" ? x.OriginalName : x.NewName; }
        private static string Target(BatchJournal b, RenameItem x) { return b.Operation == "rename" ? x.NewName : x.OriginalName; }
        private static void Preflight(BatchJournal batch)
        {
            foreach (var job in batch.Jobs)
            {
                if (FolderIdentity(job.Folder) != job.Identity) throw new IOException("Folder moved or was replaced: " + job.Folder);
                if (job.ObjectId != null && DirectoryObjectId(job.Folder) != job.ObjectId) throw new IOException("This folder was replaced since the preview: " + job.Folder);
                var sources = new HashSet<string>(job.Items.Select(x => Source(batch, x)), StringComparer.OrdinalIgnoreCase);
                foreach (var item in job.Items)
                {
                    RequireMatch(Path.Combine(job.Folder, Source(batch, item)), item);
                    string target = Path.Combine(job.Folder, Target(batch, item));
                    if ((File.Exists(target) || Directory.Exists(target)) && !sources.Contains(Target(batch, item))) throw new IOException("Name already in use: " + target + ". Nothing was renamed.");
                    string temporary = Path.Combine(job.Folder, item.TempName);
                    if (File.Exists(temporary) || Directory.Exists(temporary)) throw new IOException("A temporary filename is in use. Try again.");
                }
            }
        }
        private static void Execute(BatchJournal batch, Action<int, int> progress, CancellationToken cancellation)
        {
            Preflight(batch);
            cancellation.ThrowIfCancellationRequested();
            WriteJournal(PendingPath, batch);
            int done = 0, total = batch.PhotoCount * 2;
            try
            {
                foreach (var job in batch.Jobs)
                {
                    foreach (var item in job.Items)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        RequireMatch(Path.Combine(job.Folder, Source(batch, item)), item);
                        File.Move(Path.Combine(job.Folder, Source(batch, item)), Path.Combine(job.Folder, item.TempName));
                        if (progress != null) progress(++done, total);
                    }
                    job.Phase = "finishing";
                    WriteJournal(PendingPath, batch);
                    foreach (var item in job.Items)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        File.Move(Path.Combine(job.Folder, item.TempName), Path.Combine(job.Folder, Target(batch, item)));
                        if (progress != null) progress(++done, total);
                    }
                    job.Phase = "complete";
                    WriteJournal(PendingPath, batch);
                }
                cancellation.ThrowIfCancellationRequested();
                batch.Phase = "committed";
                WriteJournal(PendingPath, batch);
            }
            catch (Exception error)
            {
                try { RollBackAll(ReadJournal(PendingPath)); File.Delete(PendingPath); }
                catch (Exception recovery) { throw new IOException("Batch stopped. Recovery is saved in the app. Click Recover batch. " + recovery.Message, error); }
                if (error is OperationCanceledException) throw new OperationCanceledException("Cancelled. All folders restored to their pre-batch names.");
                throw new IOException("Batch stopped. All folders restored to their pre-batch names. " + error.Message, error);
            }
            try { FinishCommit(batch); }
            catch (Exception error) { throw new IOException("Names updated, but Undo could not finish saving. Click Recover batch to finish saving it.", error); }
        }
        internal static void FinishCommit(BatchJournal batch)
        {
            if (batch.Operation == "rename") WriteJournal(UndoPath, batch);
            else File.Delete(UndoPath);
            File.Delete(PendingPath);
        }
        private static void RollBackAll(BatchJournal batch)
        {
            foreach (var job in batch.Jobs.AsEnumerable().Reverse())
            {
                if (job.Phase == "restored") continue;
                if (FolderIdentity(job.Folder) != job.Identity) throw new IOException("Restore this folder's original location before recovery: " + job.Folder);
                if (job.ObjectId != null && DirectoryObjectId(job.Folder) != job.ObjectId) throw new IOException("The original folder is needed for recovery: " + job.Folder);
                bool finishing = job.Phase == "finishing" || job.Phase == "complete";
                foreach (var item in job.Items)
                {
                    string temporary = Path.Combine(job.Folder, item.TempName);
                    // Untouched source files need no rollback, even if another app changed them.
                    if (finishing || File.Exists(temporary)) RequireMatch(File.Exists(temporary) ? temporary : Path.Combine(job.Folder, Target(batch, item)), item);
                }
                if (finishing)
                {
                    foreach (var item in job.Items)
                    {
                        string temporary = Path.Combine(job.Folder, item.TempName);
                        if (!File.Exists(temporary)) File.Move(Path.Combine(job.Folder, Target(batch, item)), temporary);
                    }
                    job.Phase = "staging";
                    WriteJournal(PendingPath, batch);
                }
                foreach (var item in job.Items)
                {
                    string temporary = Path.Combine(job.Folder, item.TempName);
                    if (File.Exists(temporary)) File.Move(temporary, Path.Combine(job.Folder, Source(batch, item)));
                }
                job.Phase = "restored";
                WriteJournal(PendingPath, batch);
            }
        }
        private static void RequireMatch(string path, RenameItem expected)
        {
            var f = new FileInfo(path);
            if (!f.Exists || (f.Attributes & FileAttributes.ReparsePoint) != 0 || f.Length != expected.Length || f.LastWriteTimeUtc.Ticks != expected.ModifiedUtc)
                throw new IOException("Photo missing or modified: " + path + ". Refresh the preview, or restore the file before Undo or recovery.");
        }
        private static bool SamePlan(RenamePlan a, RenamePlan b)
        {
            return a.Identity == b.Identity && a.ObjectId == b.ObjectId && a.Prefix == b.Prefix && a.NumberPosition == b.NumberPosition && a.Items.Count == b.Items.Count && a.Items.Zip(b.Items, (x, y) => x.OriginalName == y.OriginalName && x.NewName == y.NewName && x.Length == y.Length && x.ModifiedUtc == y.ModifiedUtc).All(x => x);
        }
        public static void WriteJournal(string path, BatchJournal batch)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".new";
            byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(batch));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        public static BatchJournal ReadJournal(string path)
        {
            var b = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<BatchJournal>(File.ReadAllText(path, Encoding.UTF8));
            if (b != null && b.Kind == "folders") { FolderRenamer.ValidateJournal(b); return b; }
            if (b == null || b.Version != 2 || (b.Kind != null && b.Kind != "photos") || String.IsNullOrEmpty(b.Id) || b.Jobs == null || b.Jobs.Count == 0 || (b.Operation != "rename" && b.Operation != "undo") || (b.Phase != "running" && b.Phase != "committed")) throw new IOException("Saved batch history is invalid.");
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var job in b.Jobs)
            {
                if (job == null || String.IsNullOrWhiteSpace(job.Folder) || !Path.IsPathRooted(job.Folder) || String.IsNullOrWhiteSpace(job.Identity) || !folders.Add(job.Identity) || job.Items == null || job.Items.Count == 0 || !new[] { "staging", "finishing", "complete", "restored" }.Contains(job.Phase)) throw new IOException("Saved folder history is invalid.");
                var originals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var temps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var x in job.Items)
                {
                    if (x == null || !SafeName(x.OriginalName) || !SafeName(x.NewName) || !SafeName(x.TempName) || !Extensions.Contains(Path.GetExtension(x.OriginalName)) || Path.GetExtension(x.OriginalName) != Path.GetExtension(x.NewName) || !x.TempName.StartsWith(".photo-renamer-", StringComparison.Ordinal) || !x.TempName.EndsWith(".tmp", StringComparison.Ordinal) || x.Length < 0 || x.ModifiedUtc < 0 || !originals.Add(x.OriginalName) || !targets.Add(x.NewName) || !temps.Add(x.TempName)) throw new IOException("Saved history contains an invalid filename.");
                }
                if (temps.Overlaps(originals) || temps.Overlaps(targets)) throw new IOException("Saved history contains overlapping filenames.");
            }
            return b;
        }
        private static bool SafeName(string n) { return !String.IsNullOrWhiteSpace(n) && n != "." && n != ".." && n.Length <= 255 && n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && n == Path.GetFileName(n) && !n.EndsWith(".", StringComparison.Ordinal) && !n.EndsWith(" ", StringComparison.Ordinal); }
    }
}
