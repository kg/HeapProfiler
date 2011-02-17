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

namespace HeapProfiler {
    public class RunningProcess : IDisposable {
        public struct Snapshot {
            public int Index;
            public DateTime When;
            public string Filename;

            public override string ToString () {
                return String.Format("#{0} - {1}", Index, When.ToShortTimeString());
            }
        }

        public readonly TaskScheduler Scheduler;
        public readonly OwnedFutureSet Futures = new OwnedFutureSet();
        public readonly List<Snapshot> Snapshots = new List<Snapshot>();
        public readonly ProcessStartInfo StartInfo;

        public event EventHandler StatusChanged;
        public event EventHandler SnapshotsChanged;

        protected Process Process;

        protected RunningProcess (TaskScheduler scheduler, ProcessStartInfo startInfo) {
            StartInfo = startInfo;

            Scheduler = scheduler;
            Futures.Add(Scheduler.Start(
                MainTask(), TaskExecutionPolicy.RunAsBackgroundTask
            ));
        }

        protected static SignalFuture WaitForExit (Process process) {
            var exited = new SignalFuture();

            process.Exited += (s, e) =>
                exited.Complete();

            if (process.HasExited) 
            try {
                exited.Complete();
            } catch {
            }

            return exited;
        }

        protected Future<Process> StartProcess (ProcessStartInfo psi) {
            return Future.RunInThread(
                () => {
                    var p = Process.Start(psi);
                    p.EnableRaisingEvents = true;
                    return p;
                }
            );
        }

        protected IEnumerator<object> RunProcess (ProcessStartInfo psi) {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            var fProcess = StartProcess(psi);
            yield return fProcess;

            using (var process = fProcess.Result) {
                yield return WaitForExit(process);

                var stdout = process.StandardOutput.ReadToEnd().Trim();
                var stderr = process.StandardError.ReadToEnd().Trim();

                if (stdout.Length > 0)
                    Console.WriteLine("{0} stdout:\n{1}", Path.GetFileNameWithoutExtension(psi.FileName), stdout);
                if (stderr.Length > 0)
                    Console.WriteLine("{0} stderr:\n{1}", Path.GetFileNameWithoutExtension(psi.FileName), stderr);

                if (stderr.Contains("_NT_SYMBOL_PATH variable is not defined") && !Program.ShownSymbolPathWarning) {
                    Program.ShownSymbolPathWarning = true;
                    MessageBox.Show("Please define the _NT_SYMBOL_PATH variable to get correct stack traces.", "Warning");
                }

                if (process.ExitCode != 0)
                    throw new Exception(String.Format("Process exited with code {0}", process.ExitCode));
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

            yield return RunProcess(new ProcessStartInfo(
                Settings.GflagsPath, String.Format(
                    "-i {0} +ust", shortName
                )
            ));

            var f = StartProcess(StartInfo);
            yield return f;

            using (Process = f.Result) {
                OnStatusChanged();
                yield return WaitForExit(Process);
            }

            Process = null;

            yield return RunProcess(new ProcessStartInfo(
                Settings.GflagsPath, String.Format(
                    "-i {0} -ust", shortName
                )
            ));

            OnStatusChanged();
        }

        public static RunningProcess Start (TaskScheduler scheduler, string executablePath) {
            var psi = new ProcessStartInfo(executablePath);
            psi.UseShellExecute = false;

            return new RunningProcess(scheduler, psi);
        }

        public bool Running {
            get {
                if (Process == null)
                    return false;

                return !Process.HasExited;
            }
        }

        public void Dispose () {
            foreach (var ss in Snapshots) {
                try {
                    File.Delete(ss.Filename);
                } catch {
                }
            }
            Snapshots.Clear();

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

            var psi = new ProcessStartInfo(
                Settings.UmdhPath, String.Format(
                    "-p:{0} -f:\"{1}\"", Process.Id, targetFilename
                )
            );

            yield return RunProcess(psi);

            Snapshots.Add(new Snapshot {
                Index = Snapshots.Count + 1,
                When = now,
                Filename = targetFilename
            });
            OnSnapshotsChanged();
        }

        public IFuture DiffSnapshots (string file1, string file2) {
            var filename = Path.GetTempFileName();

            var f = Scheduler.Start(
                DiffSnapshotsTask(file1, file2, filename), TaskExecutionPolicy.RunAsBackgroundTask
            );

            Futures.Add(f);
            return f;
        }

        protected IEnumerator<object> DiffSnapshotsTask (string file1, string file2, string targetFilename) {
            var psi = new ProcessStartInfo(
                Settings.UmdhPath, String.Format(
                    "-d \"{0}\" \"{1}\" -f:\"{2}\"", file1, file2, targetFilename
                )
            );

            yield return RunProcess(psi);

            Process.Start("notepad.exe", targetFilename);
        }
    }
}
