using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace FolderPhotoRenamer
{
    internal static class FolderTests
    {
        private static int checks;
        private static string root;
        private static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("FAIL: " + label); }
        private static void Throws(Action action, string label) { bool failed = false; try { action(); } catch (IOException) { failed = true; } Check(failed, label); }
        private static string Scenario(string name) { string p = Path.Combine(root, name); Directory.CreateDirectory(p); Renamer.StateRoot = Path.Combine(root, "state-" + name); return p; }
        private static string Add(string parent, string name) { string p = Path.Combine(parent, name); Directory.CreateDirectory(p); File.WriteAllText(Path.Combine(p, "keep.jpg"), name); return p; }
        private static int Rename(FolderRenamePlan p) { return FolderRenamer.Rename(p, null, CancellationToken.None); }
        private static int Undo() { return Renamer.UndoBatch(null, CancellationToken.None); }
        public static int Run(string parent)
        {
            root = parent; Basic(); Collision(); Nested(); CancelAndFailure(); Recovery(); UndoRecovery(); PhotoFolderReplacement(); FolderReplacement(); HistoryValidation(); LegacyUndo(); return checks;
        }
        private static void Basic()
        {
            string p = Scenario("FolderBasic"), a = Add(p, "IMG_10"), b = Add(p, "IMG_2"); string common = "\u2708\ufe0f Holiday";
            var plan = FolderRenamer.Preview(new[] { a, b }, common, 1);
            Check(plan.Items[0].OriginalPath == b && Path.GetFileName(plan.Items[0].NewPath) == common + "_01", "folder sequence natural order and emoji");
            Check(Rename(plan) == 2 && File.ReadAllText(Path.Combine(p, common + "_01", "keep.jpg")) == "IMG_2", "folder rename preserves photo name and content");
            Check(Directory.GetFiles(p).Length == 0 && !Directory.GetDirectories(p).Any(x => Path.GetFileName(x).StartsWith(".photo-renamer")), "folder batch has no sidecars or leftover temp folders");
            Check(Undo() == 2 && Directory.Exists(a) && Directory.Exists(b), "global undo restores folder names");
            FolderRenamer.SetNames(plan, common, 110); Check(Path.GetFileName(plan.Items[0].NewPath) == common + "_110", "number expands to 110");
            FolderRenamer.SetNames(plan, common, 1119); Check(Path.GetFileName(plan.Items[1].NewPath) == common + "_1120", "number expands beyond 1119");
            FolderRenamer.SetNames(plan, common, 1); Rename(plan);
            Check(Rename(FolderRenamer.Preview(plan.Items.Select(x => x.NewPath), common, 1)) == 0 && Renamer.HasUndo, "folder mode no-op retains undo"); Undo();
        }
        private static void Collision()
        {
            string p = Scenario("FolderCollision"), a = Add(p, "Alpha"), b = Add(p, "Zulu");
            Directory.CreateDirectory(Path.Combine(p, "Trip_02"));
            Throws(() => Rename(FolderRenamer.Preview(new[] { a, b }, "Trip", 1)), "occupied folder target blocks whole batch");
            Check(Directory.Exists(a) && Directory.Exists(b) && !Renamer.HasPending, "folder collision moves nothing");
            Throws(() => FolderRenamer.Preview(new[] { a }, "Bad:Name", 1), "invalid folder name rejected");
            Throws(() => FolderRenamer.Preview(new[] { a }, "..\\escape", 1), "folder traversal name rejected");
            string p2 = Scenario("FolderOverlap"), c = Add(p2, "Trip_02"), d = Add(p2, "Zulu");
            Rename(FolderRenamer.Preview(new[] { c, d }, "Trip", 1));
            Check(File.ReadAllText(Path.Combine(p2, "Trip_01", "keep.jpg")) == "Trip_02" && File.ReadAllText(Path.Combine(p2, "Trip_02", "keep.jpg")) == "Zulu", "overlapping folder targets never overwrite"); Undo();
            Check(Directory.Exists(c) && Directory.Exists(d), "overlapping folder undo");
        }
        private static void Nested()
        {
            string p = Scenario("FolderNested"), a = Add(p, "Parent"), b = Add(a, "Child");
            Throws(() => FolderRenamer.Preview(new[] { a, b }, "Trip", 1), "parent plus child selection blocked");
            Throws(() => FolderRenamer.Preview(new[] { a, a.ToUpperInvariant() }, "Trip", 1), "duplicate physical folder blocked");
            Check(Directory.Exists(b), "nested validation preserves directories");
        }
        private static void CancelAndFailure()
        {
            foreach (int failAt in new[] { 1, 2, 3, 4 })
            {
                string p = Scenario("FolderFail" + failAt), a = Add(p, "A"), b = Add(p, "B");
                Throws(() => FolderRenamer.Rename(FolderRenamer.Preview(new[] { a, b }, "Trip", 1), (done, total) => { if (done == failAt) throw new IOException("Injected move failure"); }, CancellationToken.None), "folder failure at " + failAt);
                Check(File.ReadAllText(Path.Combine(a, "keep.jpg")) == "A" && File.ReadAllText(Path.Combine(b, "keep.jpg")) == "B" && !Renamer.HasPending, "folder rollback across phases " + failAt);
            }
            string parent = Scenario("FolderCancel"), x = Add(parent, "A"), y = Add(parent, "B");
            using (var token = new CancellationTokenSource())
            {
                bool cancelled = false; try { FolderRenamer.Rename(FolderRenamer.Preview(new[] { x, y }, "Trip", 1), (done, total) => { if (done == 3) token.Cancel(); }, token.Token); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && Directory.Exists(x) && Directory.Exists(y) && !Renamer.HasPending, "cancel restores all directory names");
            }
        }
        private static void Recovery()
        {
            foreach (string phase in new[] { "staging", "finishing", "committed" })
            {
                string p = Scenario("FolderCrash" + phase), a = Add(p, "A"), b = Add(p, "B");
                var plan = FolderRenamer.Preview(new[] { a, b }, "Trip", 1);
                for (int i = 0; i < plan.Items.Count; i++) plan.Items[i].TempPath = Path.Combine(p, ".photo-renamer-dir-test-" + i + ".tmp");
                var log = new BatchJournal { Version = 3, Id = "crash-test", Kind = "folders", Operation = "rename", Phase = phase, FolderRenames = plan.Items, Jobs = new List<FolderJob>() };
                Directory.Move(a, plan.Items[0].TempPath);
                if (phase != "staging") { Directory.Move(b, plan.Items[1].TempPath); Directory.Move(plan.Items[0].TempPath, plan.Items[0].NewPath); }
                if (phase == "committed") Directory.Move(plan.Items[1].TempPath, plan.Items[1].NewPath);
                Renamer.WriteJournal(Renamer.PendingPath, log); Renamer.RecoverBatch();
                if (phase == "committed") { Check(Renamer.HasUndo && Directory.Exists(plan.Items[0].NewPath), "completed folder recovery commits undo"); Undo(); }
                Check(Directory.Exists(a) && Directory.Exists(b) && !Renamer.HasPending, phase + " folder recovery restores expected names");
            }
        }
        private static void UndoRecovery()
        {
            foreach (string phase in new[] { "staging", "finishing", "committed" })
            {
                string p = Scenario("FolderUndoCrash" + phase), a = Add(p, "A"), b = Add(p, "B");
                Rename(FolderRenamer.Preview(new[] { a, b }, "Trip", 1));
                var last = Renamer.ReadJournal(Renamer.UndoPath);
                for (int i = 0; i < last.FolderRenames.Count; i++) last.FolderRenames[i].TempPath = Path.Combine(p, ".photo-renamer-dir-undo-" + i + ".tmp");
                last.Operation = "undo"; last.Phase = phase;
                Directory.Move(last.FolderRenames[0].NewPath, last.FolderRenames[0].TempPath);
                if (phase != "staging") { Directory.Move(last.FolderRenames[1].NewPath, last.FolderRenames[1].TempPath); Directory.Move(last.FolderRenames[0].TempPath, a); }
                if (phase == "committed") Directory.Move(last.FolderRenames[1].TempPath, b);
                Renamer.WriteJournal(Renamer.PendingPath, last); Renamer.RecoverBatch();
                if (phase == "committed") Check(!Renamer.HasUndo && Directory.Exists(a) && Directory.Exists(b), "completed interrupted directory undo finalizes");
                else { Check(Renamer.HasUndo && Directory.Exists(last.FolderRenames[0].NewPath) && Directory.Exists(last.FolderRenames[1].NewPath), phase + " interrupted directory undo rolls back"); Undo(); }
                Check(!Renamer.HasPending && Directory.Exists(a) && Directory.Exists(b), phase + " folder undo recovery clean");
            }
        }
        private static void PhotoFolderReplacement()
        {
            string p = Scenario("ReplacedPhotoFolder"), a = Add(p, "Album"); var preview = Renamer.Preview(a);
            long stamp = File.GetLastWriteTimeUtc(Path.Combine(a, "keep.jpg")).Ticks;
            Directory.Move(a, Path.Combine(p, "Original")); Directory.CreateDirectory(a); File.WriteAllText(Path.Combine(a, "keep.jpg"), "Album"); File.SetLastWriteTimeUtc(Path.Combine(a, "keep.jpg"), new DateTime(stamp, DateTimeKind.Utc));
            Throws(() => Renamer.RenameBatch(new List<RenamePlan> { preview }, null, CancellationToken.None), "same-path replacement caught despite matching photo metadata");
            Check(File.Exists(Path.Combine(a, "keep.jpg")), "replacement folder untouched");
        }
        private static void FolderReplacement()
        {
            string p = Scenario("ReplacedFolder"), a = Add(p, "Album"); var plan = FolderRenamer.Preview(new[] { a }, "Trip", 1);
            Directory.Move(a, Path.Combine(p, "Original")); Directory.CreateDirectory(a);
            Throws(() => Rename(plan), "folder name preview rejects same-path directory replacement");
            Check(Directory.Exists(a) && !Directory.Exists(Path.Combine(p, "Trip_01")), "replaced folder not renamed");
        }
        private static void HistoryValidation()
        {
            string p = Scenario("FolderHistory"), a = Add(p, "A"); Rename(FolderRenamer.Preview(new[] { a }, "Trip", 1));
            var last = Renamer.ReadJournal(Renamer.UndoPath); last.FolderRenames[0].OriginalPath = Path.Combine(root, "Outside"); Renamer.WriteJournal(Renamer.UndoPath, last);
            Throws(() => Undo(), "folder history cannot move directories to another parent");
            Check(Directory.Exists(Path.Combine(p, "Trip_01")), "invalid folder history changes nothing");
        }
        private static void LegacyUndo()
        {
            string p = Scenario("OldPhotoHistory"), a = Add(p, "Album"); Renamer.RenameBatch(new List<RenamePlan> { Renamer.Preview(a) }, null, CancellationToken.None);
            var last = Renamer.ReadJournal(Renamer.UndoPath); last.Jobs[0].ObjectId = null; Renamer.WriteJournal(Renamer.UndoPath, last);
            Check(Undo() == 1 && File.Exists(Path.Combine(a, "keep.jpg")), "previous photo-only undo history remains supported");
        }
    }
}
