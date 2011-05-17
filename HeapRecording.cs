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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Squared.Data.Mangler;
using Squared.Task;
using System.IO;
using Squared.Util;
using System.Threading;
using Squared.Task.IO;

namespace HeapProfiler {
    public class HeapRecording : IDisposable {
        public const string DefaultSymbolPath = @"%windir%\symbols";
        public const string DefaultSymbolServers = @"http://msdl.microsoft.com/download/symbols";

        public const int SymbolResolveBatchSize = 1024;

        public static class SnapshotLoadState {
            public static int Count = 0;
        }

        public static class SymbolResolveState {
            public static int Count = 0;
            public static ActivityIndicator.CountedItem Progress;
        }

        public readonly TaskScheduler Scheduler;
        public readonly ActivityIndicator Activities;
        public readonly OwnedFutureSet Futures = new OwnedFutureSet();
        public readonly List<HeapSnapshotInfo> Snapshots = new List<HeapSnapshotInfo>();
        public readonly HashSet<string> TemporaryFiles = new HashSet<string>();
        public readonly ProcessStartInfo StartInfo;

        public event EventHandler SymbolsChanged;
        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;
        public event EventHandler TracebacksFiltered;

        public Process Process;

        protected DatabaseFile _Database;
        protected bool DatabaseIsTemporary;

        protected IFuture _PendingFilterUpdate = null;

        protected readonly HashSet<HeapSnapshot.Module> SymbolModules = new HashSet<HeapSnapshot.Module>();
        protected readonly Dictionary<UInt32, TracebackFrame> ResolvedSymbolCache = new Dictionary<UInt32, TracebackFrame>();
        protected readonly HashSet<UInt32> PendingSymbolResolves = new HashSet<UInt32>();
        protected readonly BlockingQueue<UInt32> SymbolResolveQueue = new BlockingQueue<UInt32>();
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
            string filename
        ) {
            Scheduler = scheduler;
            Activities = activities;

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;

            if (filename.EndsWith(".heaprecording") || File.Exists(filename))
                filename = Path.GetDirectoryName(filename);

            if (!Directory.Exists(filename))
                throw new Exception(String.Format("Recording not found: {0}", filename));

            _Database = new DatabaseFile(Scheduler, filename); Futures.Add(Scheduler.Start(
                LoadRecordingMainTask(filename), 
                TaskExecutionPolicy.RunAsBackgroundTask
            ));
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

        protected bool ResolveFrame (UInt32 frame, out TracebackFrame resolved) {
            if (ResolvedSymbolCache.TryGetValue(frame, out resolved)) {
                return true;
            }

            bool temp;
            if (!PendingSymbolResolves.Contains(frame)) {
                PendingSymbolResolves.Add(frame);
                SymbolResolveQueue.Enqueue(frame);
            }

            return false;
        }

        protected IEnumerator<object> ResolveSymbolsForSnapshot (HeapSnapshot snapshot) {
            TracebackFrame tf;

            SymbolResolveState.Count = 0;

            yield return Future.RunInThread(() => {
                for (int i = 0, c = snapshot.Tracebacks.Count; i < c; i++) {
                    var traceback = snapshot.Tracebacks[i];

                    lock (SymbolResolveLock)
                    foreach (var frame in traceback)
                        ResolveFrame(frame, out tf);
                }
            });
        }

        protected void OnSymbolCacheUpdated () {
            if (SnapshotLoadQueue.Count <= 0)
                UpdateFilteredTracebacks();

            if (SymbolsChanged != null)
                SymbolsChanged(this, EventArgs.Empty);
        }

        protected IEnumerator<object> SymbolResolverTask () {
            var batch = new List<UInt32>();
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

                    if (!SymbolResolveState.Progress.Active && (SymbolResolveQueue.Count <= 0)) {
                        SymbolResolveState.Count = 0;
                        OnSymbolCacheUpdated();
                    }

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

                    var padNumberRight = (Func<uint, int, string>)((num, length) => {
                        var result = String.Format("{0:X}", num);
                        if (result.Length < length)
                            result = new String(' ', length - result.Length) + result;

                        return result;
                    });

                    var symbolModules = SymbolModules.ToArray();
                    yield return Future.RunInThread(() => {
                        using (var sw = new StreamWriter(infile, false, Encoding.ASCII)) {
                            sw.WriteLine("// Loaded modules:");
                            sw.WriteLine("//     Base Size Module");

                            foreach (var module in symbolModules)
                                sw.WriteLine(
                                    "//            {0} {1} {2}", 
                                    padNumberRight(module.BaseAddress, 8), 
                                    padNumberRight(module.Size, 8), 
                                    Path.GetFullPath(module.Filename)
                                );

                            sw.WriteLine("//");
                            sw.WriteLine("// Process modules enumerated.");

                            sw.WriteLine();
                            sw.WriteLine("*- - - - - - - - - - Heap 0 Hogs - - - - - - - - - -");
                            sw.WriteLine();

                            for (int i = 0; i < batch.Count; i++) {
                                sw.WriteLine(
                                    "{0:X8} bytes + {1:X8} at {2:X8} by BackTrace{3:X8}",
                                    1, 0, i + 1, i + 1
                                );

                                sw.WriteLine("\t{0:X8}", batch[i]);
                                sw.WriteLine();
                            }
                        }
                    });

                    IDictionary<string, string> environment = null;
                    yield return GetEnvironment().Bind(() => environment);

                    var psi = new ProcessStartInfo(
                        Settings.UmdhPath, String.Format(
                            "\"{0}\" -f:\"{1}\"", infile, outfile
                        )
                    );

                    using (var rp = Scheduler.Start(
                        Program.RunProcess(
                            psi, ProcessPriorityClass.Idle, environment
                        ), 
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
                            var cacheBatch = Database.SymbolCache.CreateBatch(batch.Count);

                            DecisionUpdateCallback<HashSet<UInt32>> updateCallback = (ref HashSet<UInt32> oldValue, ref HashSet<UInt32> newValue) => {
                                newValue.UnionWith(oldValue);
                                return true;
                            };

                            foreach (var traceback in rtc.Result.Tracebacks) {
                                var index = (int)(traceback.Key) - 1;

                                var key = batch[index];
                                var frame = traceback.Value.Frames.Array[traceback.Value.Frames.Offset];

                                lock (SymbolResolveLock) {
                                    ResolvedSymbolCache[key] = frame;
                                    PendingSymbolResolves.Remove(key);
                                }

                                cacheBatch.Add(key, frame);
                                if (frame.Function != null) {
                                    var tempHash = new HashSet<uint>();
                                    tempHash.Add(key);
                                }
                            }

                            foreach (var frame in batch) {
                                if (!PendingSymbolResolves.Contains(frame))
                                    continue;

                                Interlocked.Increment(ref TotalFrameResolveFailures);

                                var tf = new TracebackFrame(frame);

                                lock (SymbolResolveLock) {
                                    ResolvedSymbolCache[frame] = tf;
                                    PendingSymbolResolves.Remove(frame);
                                }

                                cacheBatch.Add(frame, tf);

                                // Console.WriteLine("Could not resolve: {0:X8}", frame.Frame);
                            }

                            return new IBatch[] { cacheBatch };
                        });

                        yield return fProcess;

                        foreach (var manglerBatch in fProcess.Result)
                            yield return manglerBatch.Execute();

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
                yield return snapshot.SaveToRecording(this);

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
            StartHelperTasks();

            using (Activities.AddItem("Loading snapshot info")) {
                var fSnapshots = Database.Snapshots.GetAllValues();
                yield return fSnapshots;

                var snapshots = fSnapshots.Result.OrderBy((s) => s.Timestamp);
                Snapshots.AddRange(snapshots);

                OnSnapshotsChanged();
            }

            yield return UpdateFilteredTracebacks();
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

        protected IEnumerator<object> LoadSnapshotFromDatabase (HeapSnapshotInfo info) {
            var result = new HeapSnapshot(info);

            var fModules = Database.SnapshotModules.Get(info.Index);
            var fHeaps = Database.SnapshotHeaps.Get(info.Index);

            using (fModules)
                yield return fModules;

            var fModuleInfos = Database.Modules.Select(fModules.Result);
            using (fModuleInfos)
                yield return fModuleInfos;

            foreach (var module in fModuleInfos.Result)
                result.Modules.Add(module);

            using (fHeaps)
                yield return fHeaps;

            var heapIDs = from heap in fHeaps.Result select heap.HeapID;
            var fAllocations = Database.HeapAllocations.Select(heapIDs);
            using (fAllocations)
                yield return fAllocations;

            var allocations = SequenceUtils.ToDictionary(heapIDs, fAllocations.Result);

            var tracebackIDs = new HashSet<UInt32>();

            foreach (var heapInfo in fHeaps.Result) {
                var theHeap = new HeapSnapshot.Heap(heapInfo);

                var allocationIds = allocations[heapInfo.HeapID];
                theHeap.Allocations.Capacity = allocationIds.Length;

                var fRanges = Database.Allocations.Select(allocationIds);
                using (fRanges)
                    yield return fRanges;

                yield return Future.RunInThread(() =>
                    SequenceUtils.Zip(
                        allocationIds, fRanges.Result, (id, ranges) => {
                            HeapSnapshot.AllocationRanges.Range range;

                            if (ranges.Get(info.Index, out range)) {
                                theHeap.Allocations.Add(new HeapSnapshot.Allocation(
                                    id, range.Size, range.Overhead, range.TracebackID
                                ));

                                tracebackIDs.Add(range.TracebackID);
                            }
                        }
                    )
                );

                result.Heaps.Add(theHeap);
            }

            var fTracebacks = Database.FilteredTracebacks.CascadingSelect(
                new [] { Database.Tracebacks },
                tracebackIDs
            );
            using (fTracebacks)
                yield return fTracebacks;

            result.Tracebacks.ReplaceWith(fTracebacks.Result);

            yield return Result.New(result);
        }

        protected bool FilterTraceback (HeapSnapshot.Traceback traceback, HashSet<uint> filteredFrames, out HeapSnapshot.Traceback filtered) {
            ArraySegment<uint>? newFrames = null;
            int count = 0;

            foreach (var frame in traceback) {
                if (filteredFrames.Contains(frame)) {
                    if (!newFrames.HasValue) {
                        newFrames = ImmutableArrayPool<uint>.Allocate(traceback.Frames.Count);
                        Array.Copy(
                            traceback.Frames.Array, traceback.Frames.Offset,
                            newFrames.Value.Array, newFrames.Value.Offset, count
                        );
                    }
                } else {
                    if (newFrames.HasValue)
                        newFrames.Value.Array[count + newFrames.Value.Offset] = frame;

                    count++;
                }
            }

            if (newFrames.HasValue) {
                filtered = new HeapSnapshot.Traceback(
                    traceback.ID, new ArraySegment<uint>(
                        newFrames.Value.Array, newFrames.Value.Offset, count
                    )
                );
                return true;
            }

            filtered = traceback;
            return false;
        }

        public IFuture UpdateFilteredTracebacks (IEnumerable<uint> tracebackIds = null) {
            if (_PendingFilterUpdate != null)
                _PendingFilterUpdate.Dispose();

            foreach (var si in Snapshots)
                si.FlushCachedInstance();

            _PendingFilterUpdate = Scheduler.Start(
                _UpdateFilteredTracebacks(tracebackIds), TaskExecutionPolicy.RunAsBackgroundTask
            );

            return _PendingFilterUpdate;
        }

        protected IEnumerator<object> _UpdateFilteredTracebacks (IEnumerable<uint> tracebackIds) {
            using (var progress = Activities.AddItem("Filtering symbols")) {
                var fFilters = Program.Preferences.Get("StackFilters");
                yield return fFilters;

                if (fFilters.Failed)
                    yield break;

                var compiledFilters = (from filter in (fFilters.Result as StackFilter[])
                                   select filter.Compile()).ToArray();

                var fAllKeys = Database.SymbolCache.GetAllKeys();
                yield return fAllKeys;

                if (fAllKeys.Result.Length == 0)
                    yield break;

                var filteredFrames = new HashSet<uint>();
                IFuture lastProcess = null;

                progress.Maximum = fAllKeys.Result.Length * 2;
                foreach (var range in Partitioner.Create(0, fAllKeys.Result.Length).GetDynamicPartitions()) {
                    var rangeKeys = from i in Enumerable.Range(range.Item1, range.Item2 - range.Item1)
                                    select fAllKeys.Result[i];

                    var fSymbols = Database.SymbolCache.Select(rangeKeys);
                    yield return fSymbols;

                    if (lastProcess != null)
                        yield return lastProcess;

                    progress.Progress = range.Item1;

                    lastProcess = Future.RunInThread(
                        () => SequenceUtils.Zip(
                            rangeKeys, fSymbols.Result,
                            (key, symbol) => {
                                foreach (var filter in compiledFilters)
                                if (filter.IsMatch(symbol)) {
                                    filteredFrames.Add((uint)key.Value);
                                    break;
                                }
                            }
                        )
                    );
                }

                if (lastProcess != null)
                    yield return lastProcess;

                if (tracebackIds != null) {
                    fAllKeys = new Future<TangleKey[]>(
                        (from tracebackId in tracebackIds select new TangleKey(tracebackId)).ToArray()
                    );
                } else {
                    fAllKeys = Database.Tracebacks.GetAllKeys();
                    yield return fAllKeys;
                }

                if (fAllKeys.Result.Length == 0)
                    yield break;

                progress.Maximum = fAllKeys.Result.Length * 2;
                progress.Progress = fAllKeys.Result.Length;
                lastProcess = null;

                yield return Database.FilteredTracebacks.Clear();

                foreach (var range in Partitioner.Create(0, fAllKeys.Result.Length).GetDynamicPartitions()) {
                    var rangeKeys = from i in Enumerable.Range(range.Item1, range.Item2 - range.Item1)
                                    select fAllKeys.Result[i];

                    var fTracebacks = Database.Tracebacks.Select(rangeKeys);
                    yield return fTracebacks;

                    var batch = Database.FilteredTracebacks.CreateBatch(fTracebacks.Result.Length);
                    SequenceUtils.Zip(rangeKeys, fTracebacks.Result, (key, traceback) => {
                        HeapSnapshot.Traceback filtered;
                        if (FilterTraceback(traceback, filteredFrames, out filtered))
                            batch.Set(key, filtered);
                    });

                    if (lastProcess != null)
                        yield return lastProcess;

                    progress.Progress = range.Item1 + fAllKeys.Result.Length;

                    lastProcess = batch.Execute();
                }

                if (lastProcess != null)
                    yield return lastProcess;
            }

            if (TracebacksFiltered != null)
                TracebacksFiltered(this, EventArgs.Empty);
        }

        public Future<Dictionary<string, int[]>> GenerateTopFunctions () {
            foreach (var si in Snapshots)
                si.FlushCachedInstance();

            var f = new Future<Dictionary<string, int[]>>();
            Scheduler.Start(
                f, new SchedulableGeneratorThunk(_GenerateTopFunctions()),
                TaskExecutionPolicy.RunAsBackgroundTask
            );

            return f;
        }

        protected IEnumerator<object> _GenerateTopFunctions () {
            var topNodes = new List<StackGraphNode[]>();
            const int resultCount = 10;

            using (var activity = Activities.AddItem("Finding top functions")) {
                for (int i = 0, c = Snapshots.Count; i < c; i++) {
                    activity.Maximum = c + 1;

                    var info = Snapshots[i];

                    var fSnapshot = GetSnapshot(info);
                    using (fSnapshot)
                        yield return fSnapshot;

                    var snapshot = fSnapshot.Result;

                    var tracebackIds = (from heap in snapshot.Heaps
                        from allocation in heap.Allocations
                        select allocation.TracebackID).Distinct().ToArray();

                    var fTracebacks = Database.FilteredTracebacks.CascadingSelect(
                        new [] { Database.Tracebacks }, tracebackIds
                    );

                    yield return fTracebacks;

                    var frameIds = (from traceback in fTracebacks.Result
                                    from frame in traceback
                                    select frame).Distinct().ToArray();

                    var fSymbols = Database.SymbolCache.Select(frameIds);

                    yield return fSymbols;

                    var tracebacks = SequenceUtils.ToDictionary(
                        tracebackIds, fTracebacks.Result
                    );

                    var symbols = SequenceUtils.ToDictionary(
                        frameIds, fSymbols.Result
                    );

                    var graph = new StackGraph(GraphKeyType.Function);

                    yield return graph.Build(snapshot, tracebacks, symbols);

                    topNodes.Add(graph.OrderedItems.Take(resultCount).ToArray());

                    info.ReleaseStrongReference();

                    activity.Progress = i;
                }

                var fTopFunctions = Future.RunInThread(() => {
                    var grouped =
                        from ti in topNodes
                        from node in ti
                        group node by node.Key
                            into nodeGroup
                            select new {
                                key = nodeGroup.Key,
                                maxBytes = nodeGroup.Max((sgn) => sgn.BytesRequested)
                            };

                    var sorted = (from item in grouped
                           orderby item.maxBytes descending
                           select item);

                    var result = new Dictionary<string, int[]>();

                    foreach (var item in sorted) {
                        var resultArray = new int[Snapshots.Count];

                        for (int j = 0; j < resultArray.Length; j++)
                            resultArray[j] = (from sgn in topNodes[j]
                                              where sgn.Key.Equals(item.key)
                                              select sgn.BytesRequested).FirstOrDefault();

                        result[item.key.FunctionName] = resultArray;
                    }

                    return result;
                });

                yield return fTopFunctions;

                yield return new Result(fTopFunctions.Result);
            }            
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
                try {
                    Directory.Delete(databasePath, true);
                } catch {
                }

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

        protected IEnumerator<object> GetEnvironment () {
            object[] prefs = null;
            yield return Program.Preferences.Select(
                new[] { "SymbolPath", "SymbolServers" }
            ).Bind(() => prefs);

            string symbolSrv = null;
            var symbolPath = (prefs[0] as string) ?? "";
            var symbolServers = (prefs[1] as string) ?? "";

            // If the user has not specified a symbol path or servers in preferences, and the _NT_SYMBOL_PATH environment
            //  variable is already set, let's use it.
            if (String.IsNullOrWhiteSpace(symbolPath) && String.IsNullOrWhiteSpace(symbolServers))
                symbolSrv = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") ?? "";

            if (String.IsNullOrWhiteSpace(symbolSrv)) {
                if (String.IsNullOrWhiteSpace(symbolPath))
                    symbolPath = DefaultSymbolPath;
                if (String.IsNullOrWhiteSpace(symbolServers))
                    symbolServers = DefaultSymbolServers;

                symbolSrv = String.Format(
                    "SRV*{0}*{1}",
                    Environment.ExpandEnvironmentVariables(symbolPath),
                    Environment.ExpandEnvironmentVariables(symbolServers)
                );
            }

            var environment = new Dictionary<string, string> {
                { "_NT_SYMBOL_PATH", symbolSrv }
            };

            yield return new Result(environment);
        }

        protected IEnumerator<object> CaptureSnapshotTask (string targetFilename) {
            var now = DateTime.Now;

            var mem = new MemoryStatistics(Process);

            var psi = new ProcessStartInfo(
                Settings.UmdhPath, String.Format(
                    "-g -p:{0} -f:\"{1}\"", Process.Id, targetFilename
                )
            );

            IDictionary<string, string> environment = null;
            yield return GetEnvironment().Bind(() => environment);

            TemporaryFiles.Add(targetFilename);

            using (Activities.AddItem("Capturing heap snapshot"))
                yield return Program.RunProcess(psi, customEnvironment: environment);

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

                IDictionary<string, string> environment = null;
                yield return GetEnvironment().Bind(() => environment);

                var psi = new ProcessStartInfo(
                    Settings.UmdhPath, String.Format(
                        "\"{0}\" \"{1}\" -f:\"{2}\"", file1, file2, filename
                    )
                );

                var rp = Scheduler.Start(Program.RunProcess(
                    psi, customEnvironment: environment
                ), TaskExecutionPolicy.RunAsBackgroundTask);

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
                heapIds.UnionWith(from heap in fHeapsFirst.Result select heap.HeapID);

                yield return fModulesLast;
                foreach (var moduleName in fModulesLast.Result)
                    moduleNames.Add(Path.GetFileNameWithoutExtension(moduleName));

                yield return fHeapsLast;
                heapIds.UnionWith(from heap in fHeapsLast.Result select heap.HeapID);
            }

            var allocationIds = new HashSet<UInt32>();

            {
                var fAllocations = Database.HeapAllocations.Select(heapIds);
                using (fAllocations)
                    yield return fAllocations;

                yield return Future.RunInThread(() => {
                    foreach (var ids in fAllocations.Result)
                        allocationIds.UnionWith(ids);
                });
            }

            {
                var tracebackIds = new HashSet<UInt32>();
                var oldCounts = new Dictionary<UInt32, int>();
                var oldBytes = new Dictionary<UInt32, int>();
                var newCounts = new Dictionary<UInt32, int>();
                var newBytes = new Dictionary<UInt32, int>();
                var deallocs = new Dictionary<UInt32, DeltaInfo>();
                var allocs = new Dictionary<UInt32, DeltaInfo>();

                var fAllocationRanges = Database.Allocations.Select(allocationIds);
                using (fAllocationRanges)
                    yield return fAllocationRanges;

                yield return Future.RunInThread(() => {
                    DeltaInfo delta;
                    foreach (var item in fAllocationRanges.Result) {

                        var ranges = item.Ranges.Array;
                        for (int i = 0, c = item.Ranges.Count, o = item.Ranges.Offset; i < c; i++) {
                            var range = ranges[i + o];

                            bool aliveAtStart = (range.First <= first.Index) && (range.Last > first.Index);
                            bool aliveAtEnd = (range.First <= last.Index) && (range.Last > last.Index);
                            bool allocatedInWindow = (range.First >= first.Index) && (range.First <= last.Index);
                            bool deallocatedInWindow = (range.Last >= first.Index) && (range.Last <= last.Index);

                            if (!aliveAtStart && !aliveAtEnd && !allocatedInWindow && !deallocatedInWindow)
                                continue;

                            if (aliveAtStart) {
                                int value;

                                if (!oldCounts.TryGetValue(range.TracebackID, out value))
                                    value = 0;
                                oldCounts[range.TracebackID] = value + 1;

                                if (!oldBytes.TryGetValue(range.TracebackID, out value))
                                    value = 0;
                                oldBytes[range.TracebackID] = (int)(value + range.Size);
                            }

                            if (aliveAtEnd) {
                                int value;

                                if (!newCounts.TryGetValue(range.TracebackID, out value))
                                    value = 0;
                                newCounts[range.TracebackID] = value + 1;

                                if (!newBytes.TryGetValue(range.TracebackID, out value))
                                    value = 0;
                                newBytes[range.TracebackID] = (int)(value + range.Size);
                            }

                            if (allocatedInWindow) {
                                // allocation

                                if (allocs.TryGetValue(range.TracebackID, out delta)) {
                                    delta.CountDelta += 1;
                                    delta.BytesDelta += (int)(range.Size);
                                } else {
                                    allocs.Add(range.TracebackID, new DeltaInfo {
                                        BytesDelta = (int)(range.Size),
                                        CountDelta = 1,
                                        TracebackID = range.TracebackID,
                                        Traceback = null
                                    });
                                }

                                tracebackIds.Add(range.TracebackID);
                            }

                            if (deallocatedInWindow) {
                                // deallocation

                                if (deallocs.TryGetValue(range.TracebackID, out delta)) {
                                    delta.CountDelta -= 1;
                                    delta.BytesDelta -= (int)(range.Size);
                                } else {
                                    deallocs.Add(range.TracebackID, new DeltaInfo {
                                        BytesDelta = -(int)(range.Size),
                                        CountDelta = -1,
                                        TracebackID = range.TracebackID,
                                        Traceback = null
                                    });
                                }

                                tracebackIds.Add(range.TracebackID);
                            }
                        }
                    }

                    foreach (var tracebackId in tracebackIds) {
                        DeltaInfo alloc = null, dealloc = null;

                        if (allocs.TryGetValue(tracebackId, out alloc) | deallocs.TryGetValue(tracebackId, out dealloc)) {
                            int currentOldBytes, currentOldCount;
                            int currentNewBytes, currentNewCount;

                            oldBytes.TryGetValue(tracebackId, out currentOldBytes);
                            oldCounts.TryGetValue(tracebackId, out currentOldCount);
                            newBytes.TryGetValue(tracebackId, out currentNewBytes);
                            newCounts.TryGetValue(tracebackId, out currentNewCount);

                            if (alloc != null) {
                                alloc.OldBytes = currentOldBytes;
                                alloc.OldCount = currentOldCount;
                                alloc.NewBytes = currentNewBytes;
                                alloc.NewCount = currentNewCount;
                                deltas.Add(alloc);
                            }

                            if (dealloc != null) {
                                dealloc.OldBytes = currentOldBytes;
                                dealloc.OldCount = currentOldCount;
                                dealloc.NewBytes = currentNewBytes;
                                dealloc.NewCount = currentNewCount;
                                deltas.Add(dealloc);
                            }
                        }
                    }
                });

                var fTracebacks = Database.FilteredTracebacks.CascadingSelect(
                    new [] { Database.Tracebacks },
                    tracebackIds
                );
                using (fTracebacks)
                    yield return fTracebacks;

                yield return ResolveTracebackSymbols(
                    fTracebacks.Result, tracebacks, functionNames
                );

                yield return Future.RunInThread(() => {
                    foreach (var d in deltas)
                        d.Traceback = tracebacks[d.TracebackID];
                });
            }

            yield return Future.RunInThread(() =>
                deltas.Sort((lhs, rhs) => {
                    var lhsBytes = lhs.BytesDelta;
                    var rhsBytes = rhs.BytesDelta;
                    return rhsBytes.CompareTo(lhsBytes);
                })
            );

            yield return Result.New(new HeapDiff(
                null, moduleNames, functionNames, deltas, tracebacks
            ));
        }

        public IEnumerator<object> ResolveTracebackSymbols (
            IEnumerable<HeapSnapshot.Traceback> unresolvedTracebacks,
            Dictionary<UInt32, TracebackInfo> resolvedTracebacks,
            NameTable functionNames
        ) {
            Dictionary<UInt32, TracebackFrame> frameSymbols;
            {
                var rawFrames = new HashSet<UInt32>();

                yield return Future.RunInThread(() => {
                    foreach (var traceback in unresolvedTracebacks)
                        foreach (var rawFrame in traceback)
                            rawFrames.Add(rawFrame);
                });

                var fSymbols = Database.SymbolCache.Select(rawFrames);
                using (fSymbols)
                    yield return fSymbols;

                var fSymbolDict = Future.RunInThread(() => 
                    SequenceUtils.ToDictionary(rawFrames, fSymbols.Result)
                );
                yield return fSymbolDict;

                frameSymbols = fSymbolDict.Result;
            }

            yield return Future.RunInThread(() => {
                if (functionNames != null)
                    foreach (var tf in frameSymbols.Values) {
                        if (tf.Function != null)
                            functionNames.Add(tf.Function);
                    }

                foreach (var traceback in unresolvedTracebacks) {
                    resolvedTracebacks[traceback.ID] = ConstructTracebackInfo(
                        traceback.ID, traceback.Frames, frameSymbols
                    );
                }
            });
        }

        public static TracebackInfo ConstructTracebackInfo (UInt32 tracebackID, ArraySegment<UInt32> rawFrames, IDictionary<UInt32, TracebackFrame> symbols) {
            var tracebackFunctions = new NameTable(StringComparer.Ordinal);
            var tracebackModules = new NameTable(StringComparer.Ordinal);
            var tracebackFrames = ImmutableArrayPool<TracebackFrame>.Allocate(rawFrames.Count);

            for (int i = 0, o = tracebackFrames.Offset, c = tracebackFrames.Count; i < c; i++) {
                var rawFrame = rawFrames.Array[rawFrames.Offset + i];
                var symbol = symbols[rawFrame];

                if ((symbol.Offset == 0) && (!symbol.Offset2.HasValue))
                    tracebackFrames.Array[i + o] = new TracebackFrame(rawFrame);
                else
                    tracebackFrames.Array[i + o] = symbol;

                if (symbol.Function != null)
                    tracebackFunctions.Add(symbol.Function);
                if (symbol.Module != null)
                    tracebackModules.Add(symbol.Module);
            }

            return new TracebackInfo {
                Frames = tracebackFrames,
                Functions = tracebackFunctions,
                Modules = tracebackModules,
                TraceId = tracebackID
            };
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
