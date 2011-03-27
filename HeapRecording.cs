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
using Squared.Data.Mangler;
using Squared.Task;
using System.IO;
using System.Windows.Forms;
using Squared.Util;
using Squared.Util.RegexExtensions;
using System.Text.RegularExpressions;
using System.Threading;
using Squared.Task.IO;
using System.Reflection;
using Squared.Data.Mangler.Internal;

namespace HeapProfiler {
    public class HeapRecording : IDisposable {
        public const int SymbolResolveBatchSize = 1024;

        public static class SnapshotLoadState {
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

        protected HeapRecording (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            string recordingFilename
        ) {
            Scheduler = scheduler;
            Activities = activities;

            Futures.Add(Scheduler.Start(
                LoadRecordingMainTask(recordingFilename), 
                TaskExecutionPolicy.RunAsBackgroundTask
            ));

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
                            "\"{0}\" -f:\"{1}\"", infile, outfile
                        )
                    );

                    using (var rp = Scheduler.Start(
                        Program.RunProcess(psi, ProcessPriorityClass.Idle), 
                        TaskExecutionPolicy.RunAsBackgroundTask
                    ))
                        yield return rp;

                    using (Finally.Do(() => {
                        try {
                            File.Delete(infile);
                        } catch {
                        }
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

                        var fProcess = Future.RunInThread(() => {
                            var cacheBatch = _Database.SymbolCache.CreateBatch(batch.Count);

                            foreach (var traceback in rtc.Result.Tracebacks) {
                                var index = (int)(traceback.Key) - 1;

                                var key = batch[index].Frame;
                                var frame = traceback.Value.Frames.Array[traceback.Value.Frames.Offset];
                                batch[index].Result.Complete(frame);

                                lock (SymbolResolveLock) {
                                    ResolvedSymbolCache[key] = frame;
                                    PendingSymbolResolves.Remove(key);
                                }

                                cacheBatch.Add(key, frame);
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

                                cacheBatch.Add(frame.Frame, tf);

                                // Console.WriteLine("Could not resolve: {0:X8}", frame.Frame);
                            }

                            return cacheBatch;
                        });

                        yield return fProcess;

                        var fUpdateCache = fProcess.Result;
                        yield return fUpdateCache.Execute(Database.SymbolCache);

                        Interlocked.Add(ref SymbolResolveState.Count, batch.Count);
                        batch.Clear();
                    }
                }
            }
        }

        protected IEnumerator<object> SnapshotIOTask () {
            ActivityIndicator.Item progress = null;
            IFuture previousSnapshot = null;

            while (true) {
                if ((SnapshotLoadQueue.Count <= 0) && (previousSnapshot != null))
                    yield return previousSnapshot;

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

        protected void AddSnapshot (HeapSnapshot snapshot) {
            Snapshots.Add(snapshot.Info);

            // Resort the loaded snapshots, since it's possible for the user to load
            //  a subset of a full capture, or load snapshots in the wrong order
            Snapshots.Sort((lhs, rhs) => lhs.Timestamp.CompareTo(rhs.Timestamp));

            OnSnapshotsChanged();
        }

        private IEnumerator<object> FinishLoadingSnapshotEpilogue (HeapSnapshot snapshot) {
            AddSnapshot(snapshot);

            foreach (var module in snapshot.Modules)
                SymbolModules.Add(module);

            yield return new Start(
                ResolveSymbolsForSnapshot(snapshot),
                TaskExecutionPolicy.RunAsBackgroundTask
            );

            if (!snapshot.SavedToDatabase)
                yield return snapshot.SaveToDatabase(Database);

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

        protected IEnumerator<object> LoadRecordingMainTask (string filename) {
            if (filename.EndsWith(".heaprecording") || File.Exists(filename))
                filename = Path.GetDirectoryName(filename);

            if (!Directory.Exists(filename)) {
                MessageBox.Show(String.Format("Recording not found: {0}", filename), "Error");
                yield break;
            }

            yield return Future.RunInThread(() =>
                new DatabaseFile(Scheduler, filename)
            ).Bind(() => _Database);

            StartHelperTasks();

            using (Activities.AddItem("Loading snapshot info")) {
                var fSnapshots = Database.Snapshots.GetAllValues();
                yield return fSnapshots;

                var snapshots = fSnapshots.Result.OrderBy((s) => s.Timestamp);
                Snapshots.AddRange(snapshots);

                OnSnapshotsChanged();
            }
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

        private static unsafe TangleKey GetKeyForAddress (UInt32 address) {
            var bytes = ImmutableArrayPool<byte>.Allocate(4);
            fixed (byte * pBytes = bytes.Array)
                *(UInt32 *)(pBytes + bytes.Offset) = address;

            return new TangleKey(bytes, typeof(UInt32));
        }

        protected IEnumerator<object> LoadSnapshotFromDatabase (HeapSnapshotInfo info) {
            var result = new HeapSnapshot(info);

            var fModules = Database.SnapshotModules.Get(info.Index);
            var fHeaps = Database.SnapshotHeaps.Get(info.Index);

            using (Activities.AddItem("Loading modules")) {
                yield return fModules;

                var fModuleInfos = Database.Modules.Get(
                    from moduleName in fModules.Result select new TangleKey(moduleName)
                );
                yield return fModuleInfos;

                foreach (var kvp in fModuleInfos.Result)
                    result.Modules.Add(kvp.Value);
            }

            using (Activities.AddItem("Loading heaps")) {
                yield return fHeaps;

                var fAllocations = Database.HeapAllocations.Get(
                    from heap in fHeaps.Result select new TangleKey(heap.HeapID)
                );
                yield return fAllocations;

                var allocations = (from kvp in fAllocations.Result
                                   select new KeyValuePair<UInt32, HashSet<uint>>(
                                       BitConverter.ToUInt32(kvp.Key.Data.Array, kvp.Key.Data.Offset), kvp.Value
                                    )).ToDictionary((kvp) => kvp.Key, (kvp) => kvp.Value);

                foreach (var heapInfo in fHeaps.Result) {
                    var theHeap = new HeapSnapshot.Heap(heapInfo);

                    var allocationIds = allocations[heapInfo.HeapID];
                    theHeap.Allocations.Capacity = allocationIds.Count;

                    var keys = (from address in allocationIds select GetKeyForAddress(address)).ToArray();

                    var fRanges = Database.Allocations.Get(keys);
                    yield return fRanges;

                    foreach (var kvp in fRanges.Result) {
                        HeapSnapshot.AllocationRanges.Range range;

                        if (kvp.Value.Get(info.Index, out range))
                            theHeap.Allocations.Add(new HeapSnapshot.Allocation(
                                BitConverter.ToUInt32(kvp.Key.Data.Array, kvp.Key.Data.Offset), 
                                range.Size, range.Overhead, range.TracebackID
                            ));
                    }

                    result.Heaps.Add(theHeap);
                }
            }

            yield return Result.New(result);
        }

        public Future<HeapSnapshot> GetSnapshot (HeapSnapshotInfo info) {
            var snapshot = info.Snapshot;

            if (snapshot != null)
                return new Future<HeapSnapshot>(snapshot);

            var f = new Future<HeapSnapshot>();

            Scheduler.Start(
                f, new SchedulableGeneratorThunk(LoadSnapshotFromDatabase(info)), 
                TaskExecutionPolicy.RunAsBackgroundTask
            );

            return f;
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

        protected IEnumerator<object> DiffSnapshotFiles (string file1, string file2) {
            var pair = Pair.New(file1, file2);

            string filename;
            if (DiffCache.TryGetValue(pair, out filename)) {
                yield return new Result(filename);
            } else {
                filename = Path.GetTempFileName();

                var psi = new ProcessStartInfo(
                    Settings.UmdhPath, String.Format(
                        "\"{0}\" \"{1}\" -f:\"{2}\"", file1, file2, filename
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

        public IEnumerator<object> DiffSnapshots (HeapSnapshotInfo first, HeapSnapshotInfo last) {
            var moduleNames = new NameTable(StringComparer.Ordinal);
            var heapIds = new HashSet<UInt32>();
            var functionNames = new NameTable();
            var deltas = new List<DeltaInfo>();
            var tracebacks = new Dictionary<UInt32, TracebackInfo>();

            {
                var fModulesFirst = Database.SnapshotModules.Get(first.Index);
                var fModulesLast = Database.SnapshotModules.Get(last.Index);
                var fHeapsFirst = Database.SnapshotHeaps.Get(first.Index);
                var fHeapsLast = Database.SnapshotHeaps.Get(last.Index);

                yield return fModulesFirst;
                foreach (var moduleName in fModulesFirst.Result)
                    moduleNames.Add(Path.GetFileNameWithoutExtension(moduleName));

                yield return fHeapsFirst;
                foreach (var heap in fHeapsFirst.Result)
                    heapIds.Add(heap.HeapID);

                yield return fModulesLast;
                foreach (var moduleName in fModulesLast.Result)
                    moduleNames.Add(Path.GetFileNameWithoutExtension(moduleName));

                yield return fHeapsLast;
                foreach (var heap in fHeapsLast.Result)
                    heapIds.Add(heap.HeapID);
            }

            var allocationIds = new HashSet<UInt32>();

            {
                var fAllocations = Database.HeapAllocations.Get(
                    from heapId in heapIds select new TangleKey(heapId)
                );
                yield return fAllocations;

                foreach (var kvp in fAllocations.Result)
                    allocationIds.UnionWith(kvp.Value);
            }

            {
                var tracebackIds = new HashSet<UInt32>();

                var fAllocationRanges = Database.Allocations.Get(
                    from allocationId in allocationIds select new TangleKey(allocationId)
                );
                yield return fAllocationRanges;

                foreach (var kvp in fAllocationRanges.Result) {
                    var address = BitConverter.ToUInt32(kvp.Key.Data.Array, kvp.Key.Data.Offset);

                    var ranges = kvp.Value.Ranges.Array;
                    for (int i = 0, c = kvp.Value.Ranges.Count, o = kvp.Value.Ranges.Offset; i < c; i++) {
                        var range = ranges[i + o];

                        if ((range.First <= first.Index) && 
                            (range.Last >= first.Index) && 
                            (range.Last < last.Index)
                        ) {
                            // deallocation
                            deltas.Add(new DeltaInfo {
                                Added = false,
                                BytesDelta = (int)(range.Size + range.Overhead),
                                CountDelta = 1,
                                NewBytes = 0,
                                NewCount = 0,
                                OldBytes = (int)(range.Size + range.Overhead),
                                OldCount = 1,
                                TracebackID = range.TracebackID,
                                Traceback = null
                            });
                            tracebackIds.Add(range.TracebackID);
                        } else if (
                            (range.First <= last.Index) &&
                            (range.First > first.Index) && 
                            (range.Last >= last.Index)
                        ) {
                            // allocation
                            deltas.Add(new DeltaInfo {
                                Added = true,
                                BytesDelta = (int)(range.Size + range.Overhead),
                                CountDelta = 1,
                                NewBytes = (int)(range.Size + range.Overhead),
                                NewCount = 1,
                                OldBytes = 0,
                                OldCount = 0,
                                TracebackID = range.TracebackID,
                                Traceback = null
                            });
                            tracebackIds.Add(range.TracebackID);
                        }
                    }
                }

                var fTracebacks = Database.Tracebacks.Get(
                    from tracebackId in tracebackIds select new TangleKey(tracebackId)
                );
                yield return fTracebacks;

                foreach (var kvp in fTracebacks.Result) {
                    var tracebackId = BitConverter.ToUInt32(kvp.Key.Data.Array, kvp.Key.Data.Offset);
                    var tracebackFunctions = new NameTable(StringComparer.Ordinal);
                    var tracebackModules = new NameTable(StringComparer.Ordinal);
                    var tracebackFrames = ImmutableArrayPool<TracebackFrame>.Allocate(kvp.Value.Frames.Count);

                    var fSymbols = Database.SymbolCache.Get(
                        from rawFrame in kvp.Value.Frames.AsEnumerable() select new TangleKey(rawFrame)
                    );
                    yield return fSymbols;

                    for (int i = 0, o = tracebackFrames.Offset; i < fSymbols.Result.Length; i++) {
                        var item = fSymbols.Result[i];
                        var rawOffset = BitConverter.ToUInt32(item.Key.Data.Array, item.Key.Data.Offset);
                        var symbol = item.Value;

                        if ((symbol.Offset == 0) && (!symbol.Offset2.HasValue))
                            tracebackFrames.Array[i + o] = new TracebackFrame(rawOffset);
                        else
                            tracebackFrames.Array[i + o] = symbol;
                    }

                    var tracebackInfo = new TracebackInfo {
                        Frames = tracebackFrames,
                        Functions = tracebackFunctions,
                        Modules = tracebackModules,
                        TraceId = tracebackId
                    };

                    tracebacks[tracebackId] = tracebackInfo;
                }

                foreach (var delta in deltas)
                    delta.Traceback = tracebacks[delta.TracebackID];
            }

            yield return Future.RunInThread(() =>
                deltas.Sort((lhs, rhs) => {
                    var lhsBytes = (lhs.Added ? 1 : -1) * lhs.BytesDelta;
                    var rhsBytes = (rhs.Added ? 1 : -1) * rhs.BytesDelta;
                    return lhsBytes.CompareTo(rhsBytes);
                })
            );

            yield return Result.New(new HeapDiff(
                null, moduleNames, functionNames, deltas, tracebacks
            ));
        }

        public static HeapRecording FromSnapshots (TaskScheduler scheduler, ActivityIndicator activities, IEnumerable<string> snapshots) {
            return new HeapRecording(
                scheduler, activities, snapshots
            );
        }

        public static HeapRecording FromRecording (TaskScheduler scheduler, ActivityIndicator activities, string recordingFilename) {
            return new HeapRecording(
                scheduler, activities, recordingFilename
            );
        }
    }
}
