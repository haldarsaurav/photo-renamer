using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FolderPhotoRenamer
{
    internal static class Tests
    {
        private static int assertions;
        private static string root;
        private static void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception("FAIL: " + label); }
        private static string Scenario(string name) { Renamer.StateRoot = Path.Combine(root, "state-" + name); return Folder(name); }
        private static string Folder(string name) { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        private static void Put(string folder, string name, string value) { File.WriteAllText(Path.Combine(folder, name), value, new UTF8Encoding(false)); }
        private static void Throws(Action action, string label) { bool threw = false; try { action(); } catch (IOException) { threw = true; } Check(threw, label); }
        private static List<RenamePlan> Plans(params string[] paths) { return paths.Select(Renamer.Preview).ToList(); }
        private static int Rename(List<RenamePlan> plans) { return Renamer.RenameBatch(plans, null, CancellationToken.None); }
        private static int Undo() { return Renamer.UndoBatch(null, CancellationToken.None); }
        private static bool Clean(string folder) { return !Directory.GetFiles(folder).Any(x => Path.GetFileName(x).StartsWith(".photo-renamer", StringComparison.OrdinalIgnoreCase)); }
        private static void Main(string[] args)
        {
            root = Path.Combine(args[0], "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            EmojiBatch(); NumberAtStart(); PositionSwitch(); CollisionsAndStale(); UndoFailsafe(); MultiFolderFailure(); Cancellation(); Recovery(); UndoRecovery(); InvalidHistory(); LargeBatch();
            assertions += FolderTests.Run(root);
            Console.WriteLine("PASS: " + assertions + " assertions. Test data: " + root);
        }
        private static void EmojiBatch()
        {
            string a = Scenario("\u2708\ufe0f Holiday_01"), b = Folder("\u2728 Weekend");
            Put(a, "IMG_10.HEIC", "heic"); Put(a, "IMG_2.JPG", "jpg"); Put(a, "IMG_1.png", "png"); Put(b, "one.CR3", "raw");
            Put(a, "clip.mp4", "video"); Put(a, "IMG_2.xmp", "sidecar"); Directory.CreateDirectory(Path.Combine(a, "nested")); Put(Path.Combine(a, "nested"), "inside.jpg", "nested");
            long timestamp = File.GetLastWriteTimeUtc(Path.Combine(a, "IMG_2.JPG")).Ticks;
            var plans = Plans(a, b); string name = plans[0].Prefix;
            Check(plans[0].Items.Count == 3 && plans[0].Items[1].OriginalName == "IMG_2.JPG" && plans[0].Items[2].OriginalName == "IMG_10.HEIC", "natural order and direct photos");
            Check(plans[0].Items[0].NewName == name + "_1.png", "exact emoji prefix");
            Check(Rename(plans) == 4, "multiple folders one batch");
            Check(File.ReadAllText(Path.Combine(a, name + "_2.JPG")) == "jpg", "content and extension preserved");
            Check(File.Exists(Path.Combine(b, "\u2728 Weekend_1.CR3")), "each folder uses own prefix and starts at one");
            Check(File.GetLastWriteTimeUtc(Path.Combine(a, name + "_2.JPG")).Ticks == timestamp, "timestamp preserved");
            Check(File.Exists(Path.Combine(a, "clip.mp4")) && File.Exists(Path.Combine(a, "IMG_2.xmp")) && File.Exists(Path.Combine(a, "nested", "inside.jpg")), "video sidecar subfolder untouched");
            Check(Clean(a) && Clean(b) && !Directory.GetFiles(a, "*.json").Any(), "no logs or locks in photo folders");
            string previous = Renamer.ReadJournal(Renamer.UndoPath).Id;
            Check(Rename(Plans(a, b)) == 0 && Renamer.ReadJournal(Renamer.UndoPath).Id == previous, "no-op keeps last undo");
            Check(Undo() == 4 && File.Exists(Path.Combine(a, "IMG_10.HEIC")) && File.Exists(Path.Combine(b, "one.CR3")), "persistent undo across all folders");
            Check(!Renamer.HasUndo && !Renamer.HasPending && Clean(a), "undo cleans state and photo folders");
        }
        private static void NumberAtStart()
        {
            string a = Scenario("\u2708\ufe0f Prefix Trip"), b = Folder("PrefixWeekend");
            for (int i = 1; i <= 12; i++) Put(a, "IMG_" + i + (i == 2 ? ".HEIC" : ".jpg"), "photo" + i);
            Put(b, "one.CR3", "raw");
            var plans = new List<RenamePlan> { Renamer.Preview(a, NumberPosition.Start), Renamer.Preview(b, NumberPosition.Start) };
            Check(plans[0].Items[1].NewName == "2_\u2708\ufe0f Prefix Trip.HEIC" && plans[0].Items[11].NewName == "12_\u2708\ufe0f Prefix Trip.jpg", "start preview preserves emoji, numeric order and extension case");
            Check(Rename(plans) == 13 && File.ReadAllText(Path.Combine(a, "10_\u2708\ufe0f Prefix Trip.jpg")) == "photo10", "prefix batch executes selected mode");
            Check(File.Exists(Path.Combine(b, "1_PrefixWeekend.CR3")), "prefix restarts at one in each folder");
            Check(Renamer.Preview(a, NumberPosition.Start).ChangeCount == 0 && Renamer.Preview(b, NumberPosition.Start).ChangeCount == 0, "prefix numbering idempotent beyond ten");
            Check(Undo() == 13 && File.ReadAllText(Path.Combine(a, "IMG_10.jpg")) == "photo10" && File.Exists(Path.Combine(b, "one.CR3")), "prefix batch undo restores both folders");
            Check(Clean(a) && Clean(b), "prefix mode leaves folders clean");
            var stale = Renamer.Preview(a);
            Put(a, "IMG_1.jpg", "changed after preview");
            Renamer.SetNumberPosition(stale, NumberPosition.Start);
            Throws(() => Rename(new List<RenamePlan> { stale }), "changing placement retains stale-preview failsafe");
        }
        private static void PositionSwitch()
        {
            string a = Scenario("PositionSwitch"); Put(a, "one.jpg", "one"); Put(a, "two.PNG", "two");
            var plan = Renamer.Preview(a);
            Renamer.SetNumberPosition(plan, NumberPosition.Start);
            Check(plan.Items[0].NewName == "1_PositionSwitch.jpg" && File.Exists(Path.Combine(a, "one.jpg")), "position switch only changes preview");
            Renamer.SetNumberPosition(plan, NumberPosition.End);
            Check(plan.Items[0].NewName == "PositionSwitch_1.jpg", "switch back restores suffix preview");
            Rename(new List<RenamePlan> { plan });
            plan = Renamer.Preview(a);
            Renamer.SetNumberPosition(plan, NumberPosition.Start);
            Rename(new List<RenamePlan> { plan });
            Check(File.Exists(Path.Combine(a, "1_PositionSwitch.jpg")) && Renamer.Preview(a, NumberPosition.Start).ChangeCount == 0, "existing suffix names can change to prefix");
            Undo(); Check(File.Exists(Path.Combine(a, "PositionSwitch_1.jpg")) && File.Exists(Path.Combine(a, "PositionSwitch_2.PNG")), "undo restores the previous placement");
            string overlap = Folder("PrefixOverlap"); Put(overlap, "2_PrefixOverlap.jpg", "first"); Put(overlap, "z.jpg", "second");
            Rename(new List<RenamePlan> { Renamer.Preview(overlap, NumberPosition.Start) });
            Check(File.ReadAllText(Path.Combine(overlap, "1_PrefixOverlap.jpg")) == "first" && File.ReadAllText(Path.Combine(overlap, "2_PrefixOverlap.jpg")) == "second", "prefix target overlap avoids overwrite");
            Undo(); Check(File.ReadAllText(Path.Combine(overlap, "2_PrefixOverlap.jpg")) == "first" && File.Exists(Path.Combine(overlap, "z.jpg")), "prefix overlap undo");
        }
        private static void CollisionsAndStale()
        {
            string a = Scenario("Album"); Put(a, "Album_2.jpg", "first"); Put(a, "z.jpg", "second");
            Rename(Plans(a)); Check(File.ReadAllText(Path.Combine(a, "Album_1.jpg")) == "first" && File.ReadAllText(Path.Combine(a, "Album_2.jpg")) == "second", "existing numbered target safely vacated");
            Undo(); Check(File.ReadAllText(Path.Combine(a, "Album_2.jpg")) == "first" && File.ReadAllText(Path.Combine(a, "z.jpg")) == "second", "overlap undo");
            string b = Folder("Blocked"); Put(b, "b.jpg", "b"); Directory.CreateDirectory(Path.Combine(b, "Blocked_1.jpg"));
            Throws(() => Rename(Plans(a, b)), "later folder collision fails whole preflight");
            Check(File.Exists(Path.Combine(a, "z.jpg")) && File.Exists(Path.Combine(b, "b.jpg")) && !Renamer.HasPending, "preflight moves no files");
            var preview = Plans(a); Put(a, "new.jpg", "new");
            Throws(() => Rename(preview), "stale preview rejected");
            Throws(() => Rename(Plans(a, a.ToUpperInvariant())), "duplicate physical folder rejected");
            Check(Clean(a) && Clean(b), "failed checks leave no clutter");
        }
        private static void UndoFailsafe()
        {
            string a = Scenario("UndoA"), b = Folder("UndoB"); Put(a, "a.jpg", "a"); Put(b, "b.jpg", "b"); Rename(Plans(a, b));
            Put(b, "b.jpg", "new occupant");
            Throws(() => Undo(), "undo collision in later folder");
            Check(File.Exists(Path.Combine(a, "UndoA_1.jpg")) && File.ReadAllText(Path.Combine(b, "b.jpg")) == "new occupant", "undo collision changes no folder");
            File.Delete(Path.Combine(b, "b.jpg")); Put(b, "UndoB_1.jpg", "changed after rename");
            Throws(() => Undo(), "modified photo blocks entire undo");
            Check(File.Exists(Path.Combine(a, "UndoA_1.jpg")), "modified-photo undo preserves earlier folder");
        }
        private static void MultiFolderFailure()
        {
            foreach (int failAt in new[] { 1, 3, 4, 5, 7, 8 })
            {
                string a = Scenario("FailA" + failAt), b = Folder("FailB" + failAt);
                Put(a, "a.jpg", "a"); Put(a, "b.jpg", "b"); Put(b, "c.jpg", "c"); Put(b, "d.jpg", "d");
                Throws(() => Renamer.RenameBatch(Plans(a, b), (done, total) => { if (done == failAt) throw new IOException("Simulated failure"); }, CancellationToken.None), "failure at move " + failAt);
                Check(File.ReadAllText(Path.Combine(a, "a.jpg")) == "a" && File.ReadAllText(Path.Combine(b, "c.jpg")) == "c", "whole batch rolled back at " + failAt);
                Check(Clean(a) && Clean(b) && !Renamer.HasPending, "rollback clean at " + failAt);
            }
            string old = Scenario("PriorUndo"), first = Folder("LaterA"), second = Folder("LaterB");
            Put(old, "o.jpg", "o"); Rename(Plans(old)); string previous = Renamer.ReadJournal(Renamer.UndoPath).Id;
            Put(first, "a.jpg", "a"); Put(second, "b.jpg", "b");
            Throws(() => Renamer.RenameBatch(Plans(first, second), (done, total) => { if (done == 2) Put(second, "b.jpg", "externally changed"); }, CancellationToken.None), "external modification in later folder");
            Check(File.Exists(Path.Combine(first, "a.jpg")) && Renamer.ReadJournal(Renamer.UndoPath).Id == previous, "rollback keeps earlier batch undo");
        }
        private static void Cancellation()
        {
            string a = Scenario("CancelA"), b = Folder("CancelB"); Put(a, "a.jpg", "a"); Put(b, "b.jpg", "b");
            using (var source = new CancellationTokenSource())
            {
                bool cancelled = false;
                try { Renamer.RenameBatch(Plans(a, b), (done, total) => { if (done == 3) source.Cancel(); }, source.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && File.Exists(Path.Combine(a, "a.jpg")) && File.Exists(Path.Combine(b, "b.jpg")), "cancel restores completed and active folders");
                Check(Clean(a) && Clean(b) && !Renamer.HasPending, "cancel cleans up");
            }
        }
        private static BatchJournal SimulateInterrupted(List<RenamePlan> plans, string operation)
        {
            var jobs = new List<FolderJob>();
            for (int n = 0; n < plans.Count; n++)
            {
                var p = plans[n]; var items = p.Items;
                for (int i = 0; i < items.Count; i++) items[i].TempName = ".photo-renamer-simulate-" + n + "-" + i + ".tmp";
                var job = new FolderJob { Folder = p.Folder, Identity = p.Identity, Phase = n == 0 ? "complete" : "finishing", Items = items };
                jobs.Add(job);
                foreach (var x in items) File.Move(Path.Combine(p.Folder, operation == "rename" ? x.OriginalName : x.NewName), Path.Combine(p.Folder, x.TempName));
                for (int i = 0; i < (n == 0 ? items.Count : 1); i++) File.Move(Path.Combine(p.Folder, items[i].TempName), Path.Combine(p.Folder, operation == "rename" ? items[i].NewName : items[i].OriginalName));
            }
            return new BatchJournal { Version = 2, Id = "simulated", Operation = operation, Phase = "running", Jobs = jobs };
        }
        private static void Recovery()
        {
            string a = Scenario("CrashA"), b = Folder("CrashB"); Put(a, "a.jpg", "a"); Put(a, "b.jpg", "b"); Put(b, "c.jpg", "c"); Put(b, "d.jpg", "d");
            var log = SimulateInterrupted(Plans(a, b), "rename"); Renamer.WriteJournal(Renamer.PendingPath, log);
            Throws(() => Rename(Plans(a, b)), "new batch blocked while recovery pending");
            Check(Renamer.RecoverBatch() == 4 && File.Exists(Path.Combine(a, "a.jpg")) && File.Exists(Path.Combine(b, "d.jpg")), "crash recovery across mixed phases");
            Check(Clean(a) && Clean(b) && !Renamer.HasPending, "crash recovery clean");
            Rename(Plans(a, b)); log = Renamer.ReadJournal(Renamer.UndoPath); Renamer.WriteJournal(Renamer.PendingPath, log); File.Delete(Renamer.UndoPath);
            Check(Renamer.RecoverBatch() == 4 && Renamer.HasUndo, "committed recovery saves undo without reverting photos"); Undo();
        }
        private static void UndoRecovery()
        {
            string a = Scenario("UndoCrashA"), b = Folder("UndoCrashB"); Put(a, "a.jpg", "a"); Put(a, "b.jpg", "b"); Put(b, "c.jpg", "c"); Put(b, "d.jpg", "d");
            var plans = Plans(a, b); Rename(plans);
            var log = SimulateInterrupted(plans, "undo"); Renamer.WriteJournal(Renamer.PendingPath, log);
            Renamer.RecoverBatch();
            Check(File.Exists(Path.Combine(a, "UndoCrashA_1.jpg")) && File.Exists(Path.Combine(b, "UndoCrashB_2.jpg")) && Renamer.HasUndo, "interrupted undo rolls back to renamed state");
            Undo(); Check(File.Exists(Path.Combine(a, "a.jpg")) && File.Exists(Path.Combine(b, "d.jpg")), "undo still works after recovery");
        }
        private static void InvalidHistory()
        {
            string a = Scenario("InvalidLog"); Put(a, "a.jpg", "a"); Rename(Plans(a));
            var log = Renamer.ReadJournal(Renamer.UndoPath); log.Jobs[0].Items[0].OriginalName = "..\\outside.jpg"; Renamer.WriteJournal(Renamer.UndoPath, log);
            Throws(() => Undo(), "history traversal rejected"); Check(File.Exists(Path.Combine(a, "InvalidLog_1.jpg")), "invalid history moves no photos");
        }
        private static void LargeBatch()
        {
            string a = Scenario("LargeA"), b = Folder("LargeB");
            foreach (string folder in new[] { a, b }) for (int i = 0; i < 500; i++) Put(folder, "IMG_" + i + ".jpg", i.ToString());
            Check(Rename(Plans(a, b)) == 1000, "1000-photo multi-folder batch");
            Check(Plans(a, b).All(x => x.ChangeCount == 0) && File.ReadAllText(Path.Combine(b, "LargeB_500.jpg")) == "499", "large natural numbering stable");
            Check(Undo() == 1000 && File.Exists(Path.Combine(a, "IMG_499.jpg")) && Clean(a) && Clean(b), "large batch undo and clean folders");
        }
    }
}
