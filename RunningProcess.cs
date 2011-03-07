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

namespace HeapProfiler {
    public class RunningProcess : IDisposable {
        public const int TraceDatabaseSizeMB = 128;

        public readonly TaskScheduler Scheduler;
        public readonly ActivityIndicator Activities;
        public readonly OwnedFutureSet Futures = new OwnedFutureSet();
        public readonly List<HeapSnapshot> Snapshots = new List<HeapSnapshot>();
        public readonly HashSet<string> TemporaryFiles = new HashSet<string>();
        public readonly ProcessStartInfo StartInfo;
        public readonly IFuture LoadComplete;

        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;

        protected Process Process;

        protected readonly HashSet<string> PreloadedSymbols = new HashSet<string>();
        protected readonly BlockingQueue<string> SymbolPreloadQueue = new BlockingQueue<string>();
        protected readonly LRUCache<Pair<string>, string> DiffCache = new LRUCache<Pair<string>, string>(32);

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

            LoadComplete = new SignalFuture(true);

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected RunningProcess (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            string[] snapshots
        ) {
            Scheduler = scheduler;
            Activities = activities;

            LoadComplete = Scheduler.Start(
                LoadSnapshots(snapshots),
                TaskExecutionPolicy.RunAsBackgroundTask
            );
            Futures.Add(LoadComplete);

            StartHelperTasks();

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        public IEnumerator<object> LoadSnapshots (IEnumerable<string> filenames) {
            var newSnaps = new List<HeapSnapshot>();

            int c = filenames.Count(), i = 0;
            using (var progress = Activities.AddItem("Loading snapshots"))
            foreach (var filename in filenames) {
                progress.Maximum = c;
                progress.Progress = i;

                var fSnapshot = Future.RunInThread(
                    () => new HeapSnapshot(filename)
                );

                yield return fSnapshot;
                newSnaps.Add(fSnapshot.Result);

                i += 1;
            }

            // Resort the loaded snapshots, since it's possible for the user to load
            //  a subset of a full capture, or load snapshots in the wrong order
            newSnaps.Sort((lhs, rhs) => lhs.When.CompareTo(rhs.When));

            Snapshots.Clear();
            Snapshots.AddRange(newSnaps);

            var allModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var snapshot in newSnaps)
                foreach (var module in snapshot.Modules)
                    allModules.Add(module);

            SymbolPreloadQueue.EnqueueMultiple(allModules);

            OnSnapshotsChanged();
        }

        protected void StartHelperTasks () {
            Futures.Add(Scheduler.Start(
                SymbolPreloadTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
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
                    "-i {0} +ust -tracedb {1}", shortName, TraceDatabaseSizeMB
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
                    "-i {0} -ust", shortName
                )
            ));

            OnStatusChanged();
        }

        protected IEnumerator<object> SymbolPreloadTask () {
            while (true) {
                var f = SymbolPreloadQueue.Dequeue();
                using (f)
                    yield return f;

                int c = SymbolPreloadQueue.Count + 1;
                int i = 0;

                using (var a = Activities.AddItem("Loading symbols"))
                do {
                    if (i > c) {
                        c = SymbolPreloadQueue.Count;
                        a.Progress = i = 0;
                        a.Maximum = c;
                    } else if (c > 1) {
                        a.Maximum = c;
                        a.Progress = i;
                    }

                    if (f == null) {
                        f = SymbolPreloadQueue.Dequeue();
                        yield return f;
                    }

                    var filename = f.Result;
                    if (!PreloadedSymbols.Contains(filename)) {
                        var rtc = new RunToCompletion<RunProcessResult>(
                            Program.RunProcessWithResult(new ProcessStartInfo(
                                Settings.SymChkPath, String.Format(
                                    "\"{0}\" /q /oi /op /oe", filename
                                )
                            ))
                        );
                        yield return rtc;

                        /*
                        Console.WriteLine(rtc.Result.StdOut);
                        Console.WriteLine(rtc.Result.StdErr);
                         */

                        PreloadedSymbols.Add(filename);
                    }

                    f = null;
                    i += 1;
                } while (SymbolPreloadQueue.Count > 0);
            }
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

            using (Activities.AddItem("Capturing heap snapshot"))
                yield return Program.RunProcess(psi);

            File.AppendAllText(targetFilename, mem.GetFileText());

            var snap = new HeapSnapshot(
                Snapshots.Count + 1, now, targetFilename
            );

            Snapshots.Add(snap);

            TemporaryFiles.Add(targetFilename);

            OnSnapshotsChanged();
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
