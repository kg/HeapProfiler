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
using System.Windows.Forms;
using Squared.Task;
using System.Diagnostics;
using System.IO;
using Squared.Task.IO;

namespace HeapProfiler {
    static class Program {
        public static TaskScheduler Scheduler;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main () {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Scheduler = new TaskScheduler(JobQueue.WindowsMessageBased)) {
                Scheduler.ErrorHandler = OnTaskError;

                using (var f = Scheduler.Start(MainTask(), TaskExecutionPolicy.RunAsBackgroundTask)) {
                    f.RegisterOnComplete((_) => {
                        if (_.Failed)
                            Application.Exit();
                    }); 

                    Application.Run();
                }
            }
        }

        static bool OnTaskError (Exception error) {
            MessageBox.Show(error.ToString(), "Unhandled exception in background task");
            return true;
        }

        public static IEnumerator<object> MainTask () {
            while (!Settings.DebuggingToolsInstalled) {
                bool isSdkInstalled = false;
                bool areRedistsInstalled = false;

                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Microsoft SDKs\Windows\v7.1"
                );
                if (!Directory.Exists(defaultPath))
                    defaultPath = defaultPath.Replace(" (x86)", "");

                isSdkInstalled = Directory.Exists(defaultPath);

                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Microsoft SDKs\Windows\v7.1\Redist\Debugging Tools for Windows\dbg_x86.msi"
                );
                if (!File.Exists(defaultPath))
                    defaultPath = defaultPath.Replace(" (x86)", "");

                areRedistsInstalled = File.Exists(defaultPath);

                if (areRedistsInstalled) {
                    var result = MessageBox.Show("The x86 Debugging Tools for Windows from SDK 7.1 are not installed. Would you like to install them now?", "Error", MessageBoxButtons.YesNo);
                    if (result == DialogResult.No) {
                        Application.Exit();
                        yield break;
                    }

                    yield return RunProcess(new ProcessStartInfo(
                        "msiexec.exe", String.Format("/package \"{0}\"", defaultPath)
                    ));
                } else if (isSdkInstalled) {
                    var result = MessageBox.Show("The x86 Debugging Tools for Windows from SDK 7.1 are not installed, and you did not install the redistributables when you installed the SDK. Please either install the debugging tools or the redistributables.", "Error");
                    Application.Exit();
                    yield break;
                } else {
                    var result = MessageBox.Show("Windows SDK 7.1 is not installed. Would you like to download the SDK?", "Error", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                        Process.Start("http://www.microsoft.com/downloads/en/details.aspx?FamilyID=6b6c21d2-2006-4afa-9702-529fa782d63b&displaylang=en");

                    Application.Exit();
                    yield break;
                }

                yield return new Sleep(1.0);
            }

            var args = Environment.GetCommandLineArgs();
            if ((args.Length > 1) && File.Exists(args[1])) {
                using (var viewer = new DiffViewer(Scheduler)) {
                    Scheduler.Start(viewer.LoadDiff(args[1]), TaskExecutionPolicy.RunAsBackgroundTask);

                    yield return viewer.Show();

                    Application.Exit();
                }
            } else {
                using (var window = new MainWindow(Scheduler))
                    yield return window.Show();

                Application.Exit();
            }
        }

        public static SignalFuture WaitForProcessExit (Process process) {
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

        public static Future<Process> StartProcess (ProcessStartInfo psi) {
            return Future.RunInThread(
                () => {
                    var p = Process.Start(psi);
                    p.EnableRaisingEvents = true;
                    return p;
                }
            );
        }

        public static IEnumerator<object> RunProcess (ProcessStartInfo psi, ProcessPriorityClass? priority = null) {
            var rtc = new RunToCompletion<RunProcessResult>(RunProcessWithResult(psi, priority));
            yield return rtc;

            if ((rtc.Result.StdOut ?? "").Trim().Length > 0)
                Console.WriteLine("{0} stdout:\n{1}", Path.GetFileNameWithoutExtension(psi.FileName), rtc.Result.StdOut);
            if ((rtc.Result.StdErr ?? "").Trim().Length > 0)
                Console.WriteLine("{0} stderr:\n{1}", Path.GetFileNameWithoutExtension(psi.FileName), rtc.Result.StdErr);

            if (rtc.Result.ExitCode != 0)
                throw new Exception(String.Format("Process exited with code {0}", rtc.Result.ExitCode));
        }

        public static IEnumerator<object> RunProcessWithResult (ProcessStartInfo psi, ProcessPriorityClass? priority = null) {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            var fProcess = StartProcess(psi);
            yield return fProcess;

            using (var process = fProcess.Result)
            using (var stdout = new AsyncTextReader(
                new StreamDataAdapter(process.StandardOutput.BaseStream, false)
            ))
            using (var stderr = new AsyncTextReader(
                new StreamDataAdapter(process.StandardError.BaseStream, false)
            ))
            try {
                if (priority.HasValue)
                    process.PriorityClass = priority.Value;

                var fStdOut = stdout.ReadToEnd();
                var fStdErr = stderr.ReadToEnd();

                yield return WaitForProcessExit(process);

                yield return fStdOut;
                yield return fStdErr;

                yield return new Result(new RunProcessResult {
                    StdOut = fStdOut.Result,
                    StdErr = fStdErr.Result,
                    ExitCode = process.ExitCode
                });
            } finally {
                try {
                    if (!process.HasExited)
                        process.Kill();
                } catch {
                }
            }
        }
    }

    public class RunProcessResult {
        public string StdOut, StdErr;
        public int ExitCode;
    }
}
