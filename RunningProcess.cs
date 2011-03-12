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

namespace HeapProfiler {
    public class RunningProcess : IDisposable {
        public const int SymbolResolveBatchSize = 256;
        public const int MaxFramesPerTraceback = 31;
        public const int MaxConcurrentLoads = 4;

        public static class SymbolResolveState {
            public static int Count = 0;
            public static ActivityIndicator.CountedItem Progress;
        }

        public static class SnapshotLoadState {
            public static int PendingLoads = 0;
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
        public readonly List<HeapSnapshot> Snapshots = new List<HeapSnapshot>();
        public readonly HashSet<string> TemporaryFiles = new HashSet<string>();
        public readonly ProcessStartInfo StartInfo;

        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;

        public Process Process;

        protected readonly HashSet<HeapSnapshot.Module> SymbolModules = new HashSet<HeapSnapshot.Module>();
        protected readonly Dictionary<UInt32, TracebackFrame> ResolvedSymbolCache = new Dictionary<UInt32, TracebackFrame>();
        protected readonly Dictionary<UInt32, Future<TracebackFrame>> PendingSymbolResolves = new Dictionary<UInt32, Future<TracebackFrame>>();
        protected readonly BlockingQueue<PendingSymbolResolve> SymbolResolveQueue = new BlockingQueue<PendingSymbolResolve>();
        protected readonly BlockingQueue<string> SnapshotLoadQueue = new BlockingQueue<string>();
        protected readonly LRUCache<Pair<string>, string> DiffCache = new LRUCache<Pair<string>, string>(32);

        protected int MaxPendingLoads = Math.Min(Environment.ProcessorCount, MaxConcurrentLoads);

        protected RunningProcess (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            ProcessStartInfo startInfo
        ) {
            StartInfo = startInfo;
            Activities = activities;

            Scheduler = scheduler;

            Futures.Add(Scheduler.Start(
                MainTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
            StartHelperTasks();

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected RunningProcess (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            string[] snapshots
        ) {
            Scheduler = scheduler;
            Activities = activities;

            StartHelperTasks();

            SnapshotLoadQueue.EnqueueMultiple(snapshots);

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected void StartHelperTasks () {
            var numWorkers = Math.Max(1, Environment.ProcessorCount / 2);

            SymbolResolveState.Progress = new ActivityIndicator.CountedItem(Activities, "Resolving symbols");
            SymbolResolveState.Count = 0;
            SnapshotLoadState.Progress = new ActivityIndicator.CountedItem(Activities, "Loading snapshots");

            for (int i = 0; i < numWorkers; i++)
                Futures.Add(Scheduler.Start(
                    SymbolResolverTask(), TaskExecutionPolicy.RunAsBackgroundTask
                ));

            Futures.Add(Scheduler.Start(
                SnapshotIOTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
        }

        protected bool ResolveFrame (UInt32 frame, out TracebackFrame resolved, out Future<TracebackFrame> pendingResolve) {
            lock (ResolvedSymbolCache)
            lock (PendingSymbolResolves) {
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
        }

        protected IEnumerator<object> ResolveSymbolsForSnapshot (HeapSnapshot snapshot) {
            TracebackFrame tf;
            Future<TracebackFrame> ftf;

            SymbolResolveState.Count = 0;

            yield return Future.RunInThread(() => {
                for (int i = 0, c = snapshot.Tracebacks.Count; i < c; i++) {
                    var traceback = snapshot.Tracebacks[i];
                    foreach (var frame in traceback.Frames)
                        ResolveFrame(frame, out tf, out ftf);
                }
            });
        }

        protected IEnumerator<object> SymbolResolverTask () {
            var yield = new Yield();
            var sleep = new Sleep(0.1);
            var batch = new List<PendingSymbolResolve>();
            var nullProgress = new CallbackProgressListener();
            ActivityIndicator.CountedItem progress = null;

            while (true) {
                while (SnapshotLoadQueue.Count > 0)
                    yield return sleep;

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

                    var maximum = SymbolResolveState.Count + Math.Max(0, SymbolResolveQueue.Count);
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
                                    module.Offset, module.Size, module.Filename
                                );

                            sw.WriteLine("//");
                            sw.WriteLine("// Process modules enumerated.");

                            sw.WriteLine();
                            sw.WriteLine("*- - - - - - - - - - Heap 0 Hogs - - - - - - - - - -");
                            sw.WriteLine();

                            for (int i = 0, j = 0; i < batch.Count; i++) {
                                if ((i == 0) || (i % MaxFramesPerTraceback == 0)) {
                                    sw.WriteLine(
                                        "{0:X8} bytes + {1:X8} at {2:X8} by BackTrace{3:X8}",
                                        1, 0, j, j
                                    );
                                    j += 1;
                                }

                                sw.WriteLine("\t{0:X8}", batch[i].Frame);
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
                            int i = 0;
                            foreach (var traceback in rtc.Result.Tracebacks) {
                                foreach (var frame in traceback.Value.Frames) {
                                    var key = batch[i].Frame;

                                    lock (ResolvedSymbolCache)
                                    lock (PendingSymbolResolves) {
                                        ResolvedSymbolCache[key] = frame;
                                        PendingSymbolResolves.Remove(key);
                                    }

                                    batch[i].Result.Complete(frame);
                                    i += 1;
                                }
                            }

                            foreach (var frame in batch) {
                                if (frame.Result.Completed)
                                    continue;

                                Console.WriteLine("Frame {0:x8} could not be resolved!", frame.Frame);

                                var tf = new TracebackFrame(frame.Frame);

                                lock (ResolvedSymbolCache) 
                                lock (PendingSymbolResolves) {
                                    ResolvedSymbolCache[frame.Frame] = tf;
                                    PendingSymbolResolves.Remove(frame.Frame);
                                }

                                frame.Result.Complete(tf);
                            }
                        });
                    }

                    Interlocked.Add(ref SymbolResolveState.Count, batch.Count);
                    batch.Clear();
                }
            }
        }

        protected IEnumerator<object> SnapshotIOTask () {
            ActivityIndicator.CountedItem progress = null;
            var sleep = new Sleep(0.25);

            while (true) {
                if (SnapshotLoadQueue.Count <= 0) {
                    if (progress != null) {
                        progress.Decrement();
                        progress = null;
                    }
                }

                var f = SnapshotLoadQueue.Dequeue();
                using (f)
                    yield return f;

                if (progress == null)
                    progress = SnapshotLoadState.Progress.Increment();

                while (SnapshotLoadState.PendingLoads >= MaxPendingLoads)
                    yield return sleep;

                using (var fda = new FileDataAdapter(f.Result, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    var length = (int)fda.BaseStream.Length;
                    var bytes = new byte[length];
                    yield return fda.Read(bytes, 0, length);

                    var fText = Future.RunInThread(
                        (Func<byte[], string>)StringFromRawBytes, bytes
                    );
                    yield return fText;
                    bytes = null;

                    Interlocked.Increment(ref SnapshotLoadState.PendingLoads);
                    progress.Increment();

                    var task = LoadSnapshotFromString(f.Result, (string)fText.Result, progress);
                    yield return new Start(task, TaskExecutionPolicy.RunAsBackgroundTask);

                    fText = null;
                }
            }
        }

        protected static unsafe string StringFromRawBytes (byte[] bytes) {
            fixed (byte * pBytes = bytes)
                return new String((sbyte*)pBytes, 0, bytes.Length);
        }

        protected IEnumerator<object> LoadSnapshotFromString (string filename, string text, ActivityIndicator.CountedItem progress) {
            var fSnapshot = Future.RunInThread(
                () => new HeapSnapshot(filename, text)
            );
            yield return fSnapshot;

            // Resort the loaded snapshots, since it's possible for the user to load
            //  a subset of a full capture, or load snapshots in the wrong order
            Snapshots.Add(fSnapshot.Result);
            Snapshots.Sort((lhs, rhs) => lhs.When.CompareTo(rhs.When));

            OnSnapshotsChanged();

            foreach (var module in fSnapshot.Result.Modules)
                SymbolModules.Add(module);

            yield return new Start(
                ResolveSymbolsForSnapshot(fSnapshot.Result), TaskExecutionPolicy.RunAsBackgroundTask
            );

            Interlocked.Decrement(ref SnapshotLoadState.PendingLoads);
            progress.Decrement();
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

        protected IEnumerator<object> MainTask () {
            var shortName = Path.GetFileName(Path.GetFullPath(StartInfo.FileName));

            using (Activities.AddItem("Enabling heap instrumentation"))
            yield return Program.RunProcess(new ProcessStartInfo(
                Settings.GflagsPath, String.Format(
                    "-i \"{0}\" +ust", shortName
                )
            ));

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

        public static RunningProcess Start (
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

            return new RunningProcess(scheduler, activities, psi);
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

            SnapshotLoadQueue.Enqueue(targetFilename);
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

        public static RunningProcess FromSnapshots (TaskScheduler scheduler, ActivityIndicator activities, string[] snapshots) {
            return new RunningProcess(
                scheduler, activities, snapshots
            );
        }
    }
}
