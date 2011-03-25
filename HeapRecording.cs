/*
The contents of this file are subject to the Mozilla Public License
Version 1.1 (the "License"); you may not use this file except in
compliance with the License. You may obtain a copy of the License at
http://www.mozilla.org/MPL/

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
License for the specific language governing rights and limitations
under the License.

The Original Code is Windows Heap Profiler Frontend.

The Initial Developer of the Original Code is Mozilla Corporation.

Original Author: Kevin Gadd (kevin.gadd@gmail.com)
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Squared.Task;
using System.IO;
using System.Windows.Forms;
using Squared.Util;
using Squared.Util.RegexExtensions;
using System.Text.RegularExpressions;
using System.Threading;
using Squared.Task.IO;
using System.Reflection;

namespace HeapProfiler {
    public class HeapRecording : IDisposable {
        public const int SymbolResolveBatchSize = 1024;
        public const int MaxFramesPerTraceback = 31;

        public static class SnapshotLoadState {
            public static int Count = 0;
        }

        public static class DatabaseSaveState {
            public static int Count = 0;
        }

        public static class SymbolResolveState {
            public static int Count = 0;
            public static ActivityIndicator.CountedItem Progress;
        }

        public struct PendingSymbolResolve {
            public readonly UInt32 Frame;
            public readonly Future<TracebackFrame> Result;

            public PendingSymbolResolve (UInt32 frame, Future<TracebackFrame> result) {
                Frame = frame;
                Result = result;
            }
        }

        public readonly TaskScheduler Scheduler;
        public readonly ActivityIndicator Activities;
        public readonly OwnedFutureSet Futures = new OwnedFutureSet();
        public readonly List<HeapSnapshotInfo> Snapshots = new List<HeapSnapshotInfo>();
        public readonly HashSet<string> TemporaryFiles = new HashSet<string>();
        public readonly ProcessStartInfo StartInfo;

        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;

        public Process Process;

        protected DatabaseFile _Database;
        protected bool DatabaseIsTemporary;

        protected readonly HashSet<HeapSnapshot.Module> SymbolModules = new HashSet<HeapSnapshot.Module>();
        protected readonly Dictionary<UInt32, TracebackFrame> ResolvedSymbolCache = new Dictionary<UInt32, TracebackFrame>();
        protected readonly Dictionary<UInt32, Future<TracebackFrame>> PendingSymbolResolves = new Dictionary<UInt32, Future<TracebackFrame>>();
        protected readonly BlockingQueue<PendingSymbolResolve> SymbolResolveQueue = new BlockingQueue<PendingSymbolResolve>();
        protected readonly BlockingQueue<string> SnapshotLoadQueue = new BlockingQueue<string>();
        protected readonly BlockingQueue<HeapSnapshot> SnapshotDatabaseSaveQueue = new BlockingQueue<HeapSnapshot>();
        protected readonly LRUCache<Pair<string>, string> DiffCache = new LRUCache<Pair<string>, string>(32);
        protected readonly object SymbolResolveLock = new object();

        protected int TotalFrameResolveFailures = 0;

        protected HeapRecording (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            ProcessStartInfo startInfo
        ) {
            StartInfo = startInfo;
            Activities = activities;

            Scheduler = scheduler;

            Futures.Add(Scheduler.Start(
                ProfileMainTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected HeapRecording (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            IEnumerable<string> snapshots
        ) {
            Scheduler = scheduler;
            Activities = activities;

            Futures.Add(Scheduler.Start(
                LoadExistingMainTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));

            SnapshotLoadQueue.EnqueueMultiple(snapshots);

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        public IEnumerator<object> SaveAs (string filename) {
            DatabaseIsTemporary = false;
            yield return _Database.Move(filename, Activities);
        }

        protected void StartHelperTasks () {
            var numWorkers = Math.Max(1, Environment.ProcessorCount / 2);

            SymbolResolveState.Progress = new ActivityIndicator.CountedItem(Activities, "Caching symbols");
            SymbolResolveState.Count = 0;

            for (int i = 0; i < numWorkers; i++)
                Futures.Add(Scheduler.Start(
                    SymbolResolverTask(), TaskExecutionPolicy.RunAsBackgroundTask
                ));

            Futures.Add(Scheduler.Start(
                SnapshotIOTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));

            Futures.Add(Scheduler.Start(
                DatabaseSaveTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
        }

        protected bool ResolveFrame (UInt32 frame, out TracebackFrame resolved, out Future<TracebackFrame> pendingResolve) {
            if (ResolvedSymbolCache.TryGetValue(frame, out resolved)) {
                pendingResolve = null;
                return true;
            }

            if (!PendingSymbolResolves.TryGetValue(frame, out pendingResolve)) {
                var f = PendingSymbolResolves[frame] = new Future<TracebackFrame>();
                var item = new PendingSymbolResolve(frame, f);

                SymbolResolveQueue.Enqueue(item);
            }

            return false;
        }

        protected IEnumerator<object> ResolveSymbolsForSnapshot (HeapSnapshot snapshot) {
            TracebackFrame tf;
            Future<TracebackFrame> ftf;

            SymbolResolveState.Count = 0;

            yield return Future.RunInThread(() => {
                for (int i = 0, c = snapshot.Tracebacks.Count; i < c; i++) {
                    var traceback = snapshot.Tracebacks[i];

                    lock (SymbolResolveLock)
                    foreach (var frame in traceback)
                        ResolveFrame(frame, out tf, out ftf);
                }
            });
        }

        protected IEnumerator<object> SymbolResolverTask () {
            var batch = new List<PendingSymbolResolve>();
            var nullProgress = new CallbackProgressListener();
            ActivityIndicator.CountedItem progress = null;

            while (true) {
                var count = SymbolResolveBatchSize - batch.Count;
                SymbolResolveQueue.DequeueMultiple(batch, count);

                if (batch.Count == 0) {
                    if (progress != null) {
                        progress.Decrement();
                        progress = null;
                    }

                    if (!SymbolResolveState.Progress.Active && (SymbolResolveQueue.Count <= 0))
                        SymbolResolveState.Count = 0;

                    var f = SymbolResolveQueue.Dequeue();
                    using (f)
                        yield return f;

                    batch.Add(f.Result);
                } else {
                    if (progress == null)
                        progress = SymbolResolveState.Progress.Increment();

                    var maximum = SymbolResolveState.Count + Math.Max(0, SymbolResolveQueue.Count) + 1;
                    progress.Maximum = maximum;
                    progress.Progress = Math.Min(maximum, SymbolResolveState.Count);

                    string infile = Path.GetTempFileName(), outfile = Path.GetTempFileName();

                    var symbolModules = SymbolModules.ToArray();
                    yield return Future.RunInThread(() => {
                        using (var sw = new StreamWriter(infile, false, Encoding.ASCII)) {
                            sw.WriteLine("// Loaded modules:");
                            sw.WriteLine("//     Base Size Module");

                            foreach (var module in symbolModules)
                                sw.WriteLine(
                                    "//            {0:X8} {1:X8} {2}", 
                                    module.BaseAddress, module.Size, module.Filename
                                );

                            sw.WriteLine("//");
                            sw.WriteLine("// Process modules enumerated.");

                            sw.WriteLine();
                            sw.WriteLine("*- - - - - - - - - - Heap 0 Hogs - - - - - - - - - -");
                            sw.WriteLine();

                            for (int i = 1; i < batch.Count; i++) {
                                sw.WriteLine(
                                    "{0:X8} bytes + {1:X8} at {2:X8} by BackTrace{3:X8}",
                                    1, 0, i, i
                                );

                                sw.WriteLine("\t{0:X8}", batch[i].Frame);
                                sw.WriteLine();
                            }
                        }
                    });

                    var psi = new ProcessStartInfo(
                        Settings.UmdhPath, String.Format(
                            "-d \"{0}\" -f:\"{1}\"", infile, outfile
                        )
                    );

                    using (Finally.Do(() => {
                        try {
                            File.Delete(infile);
                        } catch {
                        }
                    }))
                    using (var rp = Scheduler.Start(
                        Program.RunProcess(psi, ProcessPriorityClass.Idle), 
                        TaskExecutionPolicy.RunAsBackgroundTask
                    ))
                        yield return rp;

                    using (Finally.Do(() => {
                        try {
                            File.Delete(outfile);
                        } catch {
                        }
                    })) {
                        var rtc = new RunToCompletion<HeapDiff>(
                            HeapDiff.FromFile(outfile, nullProgress)
                        );

                        using (rtc)
                            yield return rtc;

                        yield return Future.RunInThread(() => {
                            foreach (var traceback in rtc.Result.Tracebacks) {
                                var index = (int)(traceback.Key) - 1;

                                var key = batch[index].Frame;
                                var frame = traceback.Value.Frames[0];
                                batch[index].Result.Complete(frame);

                                lock (SymbolResolveLock) {
                                    ResolvedSymbolCache[key] = frame;
                                    PendingSymbolResolves.Remove(key);
                                }

                                _Database.SymbolCache.Add(key, frame);
                            }

                            foreach (var frame in batch) {
                                if (frame.Result.Completed)
                                    continue;

                                Interlocked.Increment(ref TotalFrameResolveFailures);

                                var tf = new TracebackFrame(frame.Frame);

                                frame.Result.Complete(tf);

                                lock (SymbolResolveLock) {
                                    ResolvedSymbolCache[frame.Frame] = tf;
                                    PendingSymbolResolves.Remove(frame.Frame);
                                }
                            }
                        });

                        Interlocked.Add(ref SymbolResolveState.Count, batch.Count);
                        batch.Clear();
                    }
                }
            }
        }

        protected IEnumerator<object> DatabaseSaveTask () {
            ActivityIndicator.Item progress = null;

            while (true) {
                if (SnapshotDatabaseSaveQueue.Count <= 0) {
                    DatabaseSaveState.Count = 0;
                    if (progress != null) {
                        progress.Dispose();
                        progress = null;
                    }
                }

                var f = SnapshotDatabaseSaveQueue.Dequeue();
                using (f)
                    yield return f;

                if (progress == null)
                    progress = Activities.AddItem("Updating database");

                var maximum = DatabaseSaveState.Count + Math.Max(0, SnapshotDatabaseSaveQueue.Count) + 1;
                progress.Maximum = maximum;
                progress.Progress = Math.Min(maximum, DatabaseSaveState.Count);

                var snapshot = f.Result;
                yield return snapshot.SaveToDatabase(_Database);
                Interlocked.Increment(ref DatabaseSaveState.Count);
            }
        }

        protected IEnumerator<object> SnapshotIOTask () {
            ActivityIndicator.Item progress = null;
            IFuture previousSnapshot = null;

            while (true) {
                if (SnapshotLoadQueue.Count <= 0) {
                    SnapshotLoadState.Count = 0;
                    if (progress != null) {
                        progress.Dispose();
                        progress = null;
                    }
                }

                var f = SnapshotLoadQueue.Dequeue();
                using (f)
                    yield return f;

                var filename = f.Result;

                if (progress == null)
                    progress = Activities.AddItem("Loading snapshots");

                using (var fda = new FileDataAdapter(
                    filename, FileMode.Open, 
                    FileAccess.Read, FileShare.Read, 1024 * 128
                )) {
                    var maximum = SnapshotLoadState.Count + Math.Max(0, SnapshotLoadQueue.Count) + 1;
                    progress.Maximum = maximum;
                    progress.Progress = Math.Min(maximum, SnapshotLoadState.Count);

                    var fBytes = fda.ReadToEnd();
                    yield return fBytes;

                    var fText = Future.RunInThread(
                        () => Encoding.ASCII.GetString(fBytes.Result)
                    );
                    yield return fText;

                    var text = fText.Result;
                    fBytes = null;
                    fText = null;

                    // Wait for the last snapshot load we triggered to finish
                    if (previousSnapshot != null)
                        yield return previousSnapshot;

                    previousSnapshot = Scheduler.Start(
                        FinishLoadingSnapshot(filename, text), TaskExecutionPolicy.RunAsBackgroundTask
                    );

                    text = null;
                }
            }
        }

        protected IEnumerator<object> AddSnapshot (HeapSnapshot snapshot) {
            Snapshots.Add(snapshot.Info);
            Snapshots.Sort((lhs, rhs) => lhs.Timestamp.CompareTo(rhs.Timestamp));

            OnSnapshotsChanged();

            SnapshotDatabaseSaveQueue.Enqueue(snapshot);

            yield break;
        }

        private IEnumerator<object> FinishLoadingSnapshotEpilogue (HeapSnapshot snapshot) {
            // Resort the loaded snapshots, since it's possible for the user to load
            //  a subset of a full capture, or load snapshots in the wrong order
            yield return AddSnapshot(snapshot);

            foreach (var module in snapshot.Modules)
                SymbolModules.Add(module);

            yield return new Start(
                ResolveSymbolsForSnapshot(snapshot),
                TaskExecutionPolicy.RunAsBackgroundTask
            );

            Interlocked.Increment(ref SnapshotLoadState.Count);
        }

        protected IEnumerator<object> FinishLoadingSnapshot (int index, DateTime when, string filename, string text) {
            var fSnapshot = Future.RunInThread(
                () => new HeapSnapshot(index, when, filename, text)
            );
            yield return fSnapshot;
            var snapshot = fSnapshot.Result;
            text = null;

            yield return FinishLoadingSnapshotEpilogue(snapshot);
        }

        protected IEnumerator<object> FinishLoadingSnapshot (string filename, string text) {
            var fSnapshot = Future.RunInThread(
                () => new HeapSnapshot(filename, text)
            );
            yield return fSnapshot;
            var snapshot = fSnapshot.Result;
            text = null;

            yield return FinishLoadingSnapshotEpilogue(snapshot);

            snapshot.Info.ReleaseStrongReference();
        }

        void DiffCache_ItemEvicted (KeyValuePair<Pair<string>, string> item) {
            if (TemporaryFiles.Contains(item.Value)) {
                TemporaryFiles.Remove(item.Value);

                try {
                    File.Delete(item.Value);
                } catch {
                }
            }
        }

        protected void OnStatusChanged () {
            if (StatusChanged != null)
                StatusChanged(this, EventArgs.Empty);
        }

        protected void OnSnapshotsChanged () {
            if (SnapshotsChanged != null)
                SnapshotsChanged(this, EventArgs.Empty);
        }

        protected IEnumerator<object> CreateTemporaryDatabase () {
            var filename = Path.GetTempFileName();

            DatabaseIsTemporary = true;
            yield return Future.RunInThread(() =>
                new DatabaseFile(Scheduler, filename)
            ).Bind(() => _Database);
        }

        protected IEnumerator<object> LoadExistingMainTask () {
            yield return CreateTemporaryDatabase();

            StartHelperTasks();
        }

        protected IEnumerator<object> ProfileMainTask () {
            var shortName = Path.GetFileName(Path.GetFullPath(StartInfo.FileName));

            using (Activities.AddItem("Enabling heap instrumentation"))
            yield return Program.RunProcess(new ProcessStartInfo(
                Settings.GflagsPath, String.Format(
                    "-i \"{0}\" +ust", shortName
                )
            ));

            yield return CreateTemporaryDatabase();

            StartHelperTasks();

            var f = Program.StartProcess(StartInfo);
            using (Activities.AddItem("Starting process"))
                yield return f;

            using (Process = f.Result) {
                OnStatusChanged();
                yield return Program.WaitForProcessExit(Process);
            }

            Process = null;

            using (Activities.AddItem("Disabling heap instrumentation"))
            yield return Program.RunProcess(new ProcessStartInfo(
                Settings.GflagsPath, String.Format(
                    "-i \"{0}\" -ust", shortName
                )
            ));

            OnStatusChanged();
        }

        public static HeapRecording StartProcess (
            TaskScheduler scheduler, ActivityIndicator activities, string executablePath, string arguments, string workingDirectory
        ) {
            var psi = new ProcessStartInfo(
                executablePath, arguments
            );
            psi.UseShellExecute = false;

            if ((workingDirectory != null) && (workingDirectory.Trim().Length > 0))
                psi.WorkingDirectory = workingDirectory;
            else
                psi.WorkingDirectory = Path.GetDirectoryName(executablePath);

            return new HeapRecording(scheduler, activities, psi);
        }

        public DatabaseFile Database {
            get {
                return _Database;
            }
        }

        public bool Running {
            get {
                if (Process == null)
                    return false;

                return !Process.HasExited;
            }
        }

        public void Dispose () {
            Snapshots.Clear();

            var databasePath = Database.Storage.Folder;
            Database.Dispose();

            if (DatabaseIsTemporary) 
                Directory.Delete(databasePath, true);

            foreach (var fn in TemporaryFiles) {
                try {
                    File.Delete(fn);
                } catch {
                }
            }
            TemporaryFiles.Clear();

            if (Process != null) {
                Process.Dispose();
                Process = null;
            }

            Futures.Dispose();
        }

        public IFuture CaptureSnapshot () {
            var filename = Path.GetTempFileName();

            var f = Scheduler.Start(
                CaptureSnapshotTask(filename), TaskExecutionPolicy.RunAsBackgroundTask
            );

            Futures.Add(f);
            return f;
        }

        protected IEnumerator<object> CaptureSnapshotTask (string targetFilename) {
            var now = DateTime.Now;

            var mem = new MemoryStatistics(Process);

            var psi = new ProcessStartInfo(
                Settings.UmdhPath, String.Format(
                    "-p:{0} -f:\"{1}\"", Process.Id, targetFilename
                )
            );

            TemporaryFiles.Add(targetFilename);

            using (Activities.AddItem("Capturing heap snapshot"))
                yield return Program.RunProcess(psi);

            yield return Future.RunInThread(
                () => File.AppendAllText(targetFilename, mem.GetFileText())
            );

            var fText = Future.RunInThread(
                () => File.ReadAllText(targetFilename)
            );
            yield return fText;

            var text = fText.Result;
            fText = null;

            yield return FinishLoadingSnapshot(Snapshots.Count, now, targetFilename, text);
        }

        public IEnumerator<object> DiffSnapshots (string file1, string file2) {
            file1 = Path.GetFullPath(file1);
            file2 = Path.GetFullPath(file2);
            var pair = Pair.New(file1, file2);

            string filename;
            if (DiffCache.TryGetValue(pair, out filename)) {
                yield return new Result(filename);
            } else {
                filename = Path.GetTempFileName();

                var psi = new ProcessStartInfo(
                    Settings.UmdhPath, String.Format(
                        "-d \"{0}\" \"{1}\" -f:\"{2}\"", file1, file2, filename
                    )
                );

                var rp = Scheduler.Start(Program.RunProcess(psi), TaskExecutionPolicy.RunAsBackgroundTask);

                using (Activities.AddItem("Generating heap diff"))
                using (rp)
                    yield return rp;

                DiffCache[pair] = filename;
                TemporaryFiles.Add(filename);

                yield return new Result(filename);
            }
        }

        public static HeapRecording FromSnapshots (TaskScheduler scheduler, ActivityIndicator activities, IEnumerable<string> snapshots) {
            return new HeapRecording(
                scheduler, activities, snapshots
            );
        }
    }
}
