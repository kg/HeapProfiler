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
        public struct Snapshot {
            public int Index;
            public DateTime When;
            public MemoryStatistics Memory;
            public string Filename;

            public override string ToString () {
                return String.Format("#{0} - {1}", Index, When.ToLongTimeString());
            }
        }

        public const int TraceDatabaseSizeMB = 128;

        public readonly TaskScheduler Scheduler;
        public readonly ActivityIndicator Activities;
        public readonly OwnedFutureSet Futures = new OwnedFutureSet();
        public readonly List<Snapshot> Snapshots = new List<Snapshot>();
        public readonly HashSet<string> TemporaryFiles = new HashSet<string>();
        public readonly ProcessStartInfo StartInfo;

        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;

        protected Process Process;

        protected readonly HashSet<string> PreloadedSymbols = new HashSet<string>();
        protected readonly BlockingQueue<string> SymbolPreloadQueue = new BlockingQueue<string>();
        protected readonly BlockingQueue<string> SnapshotPreprocessQueue = new BlockingQueue<string>();
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

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected RunningProcess (
            TaskScheduler scheduler,
            ActivityIndicator activities,
            string[] snapshots
        ) {
            Scheduler = scheduler;
            Activities = activities;

            foreach (var snapshot in snapshots.OrderBy((s) => s)) {
                var parts = Path.GetFileNameWithoutExtension(snapshot)
                    .Split(new[] { '_' }, 2);

                Snapshots.Add(new Snapshot {
                    Index = int.Parse(parts[0]),
                    Filename = snapshot,
                    When = DateTime.ParseExact(
                        parts[1].Replace("_", ":"), "u", 
                        System.Globalization.DateTimeFormatInfo.InvariantInfo
                    )
                });
            }

            StartHelperTasks();

            SnapshotPreprocessQueue.EnqueueMultiple(snapshots);

            DiffCache.ItemEvicted += DiffCache_ItemEvicted;
        }

        protected void StartHelperTasks () {
            Futures.Add(Scheduler.Start(
                SnapshotPreprocessTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
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

        protected IEnumerator<object> SnapshotPreprocessTask () {
            while (true) {
                var f = SnapshotPreprocessQueue.Dequeue();
                using (f)
                    yield return f;

                using (Activities.AddItem("Preprocessing snapshot")) {
                    var fModules = Future.RunInThread(() =>
                        ReadModuleListFromSnapshot(f.Result)
                    );
                    yield return fModules;

                    foreach (var filename in fModules.Result)
                        if (!PreloadedSymbols.Contains(filename))
                            SymbolPreloadQueue.Enqueue(filename);
                }
            }
        }

        protected string[] ReadModuleListFromSnapshot (string filename) {
            var result = new List<string>();
            var moduleRegex = new Regex(
                "//\\s*([A-F0-9]+)\\s+([A-F0-9]+)\\s+(?'module'[^\r\n]+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
            );
            string line = null;
            bool scanningForStart = true, scanningForMemory = false;

            using (var f = File.OpenRead(filename))
            using (var sr = new StreamReader(f))
            while ((line = sr.ReadLine()) != null) {
                if (scanningForStart) {
                    if (line.Contains("Loaded modules"))
                        scanningForStart = false;
                    else if (line.Contains("Start of data for heap"))
                        break;
                    else
                        continue;
                } else if (scanningForMemory) {
                    if (line.StartsWith("// Memory=")) {
                        var mem = new MemoryStatistics(line);
                        Console.WriteLine(mem.GetFileText());
                        scanningForMemory = false;
                        break;
                    }
                } else {
                    Match m;
                    if (!moduleRegex.TryMatch(line, out m)) {
                        if (line.Contains("Process modules enumerated"))
                            scanningForMemory = true;
                        else
                            continue;
                    } else {
                        result.Add(Path.GetFullPath(m.Groups["module"].Value).ToLowerInvariant());
                    }
                }
            }

            return result.ToArray();
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

            Snapshots.Add(new Snapshot {
                Index = Snapshots.Count + 1,
                When = now,
                Memory = mem,
                Filename = targetFilename
            });

            TemporaryFiles.Add(targetFilename);
            SnapshotPreprocessQueue.Enqueue(targetFilename);

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
