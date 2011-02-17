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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Squared.Task;

namespace HeapProfiler {
    public partial class MainWindow : TaskForm {
        public RunningProcess Instance = null;

        public MainWindow (TaskScheduler scheduler) 
            : base(scheduler) {
            InitializeComponent();
        }

        private void SelectExecutable_Click (object sender, EventArgs e) {
            using (var dialog = new OpenFileDialog()) {
                dialog.Title = "Select Executable";
                dialog.FileName = ExecutablePath.Text;
                dialog.ShowReadOnly = false;
                dialog.ValidateNames = true;
                dialog.CheckFileExists = true;
                dialog.AddExtension = false;
                dialog.Filter = "Executables|*.exe";

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                ExecutablePath.Text = dialog.FileName;
            }
        }

        private void ExecutablePath_DragOver (object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
        }

        private void ExecutablePath_DragDrop (object sender, DragEventArgs e) {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null)
                return;

            ExecutablePath.Text = files[0];
        }

        private void LaunchProcess_Click (object sender, EventArgs e) {
            if (Instance != null)
                Instance.Dispose();

            LaunchProcess.Enabled = false;

            Instance = RunningProcess.Start(Scheduler, ExecutablePath.Text);
            Instance.StatusChanged += (s, _) => RefreshStatus();
            Instance.SnapshotsChanged += (s, _) => RefreshSnapshots();

            RefreshStatus();
            RefreshSnapshots();
        }

        private void ExecutablePath_TextChanged (object sender, EventArgs e) {
            RefreshLaunchEnabled();
        }

        private void RefreshLaunchEnabled () {
            bool enabled = true;

            var path = Path.GetFullPath(ExecutablePath.Text);
            if (!File.Exists(path))
                enabled = false;
            if (Path.GetExtension(path).ToLowerInvariant() != ".exe")
                enabled = false;

            if (Instance != null && Instance.Running)
                enabled = false;

            LaunchProcess.Enabled = enabled;
        }

        private void RefreshStatus () {
            ExecutableStatus.Text = String.Format(
                "Status: {0}", 
                (Instance != null) ? (Instance.Running ? "Running" : "Exited") : "Not Started"
            );

            bool running = (Instance != null) && (Instance.Running);
            CaptureSnapshot.Enabled = running;

            RefreshLaunchEnabled();
        }

        private void RefreshSnapshots () {
            SnapshotList.BeginUpdate();
            SnapshotList.Items.Clear();
            foreach (var snapshot in Instance.Snapshots)
                SnapshotList.Items.Add(snapshot);
            SnapshotList.EndUpdate();

            DiffSelection.Enabled = false;
            SaveSelection.Enabled = false;
        }

        private void CaptureSnapshot_Click (object sender, EventArgs e) {
            CaptureSnapshot.Enabled = false;
            UseWaitCursor = true;
            Instance.CaptureSnapshot()
                .RegisterOnComplete((_) => {
                    CaptureSnapshot.Enabled = true;
                    UseWaitCursor = false;
                });
        }

        private void SnapshotList_SelectedIndexChanged (object sender, EventArgs e) {
            DiffSelection.Enabled = (SnapshotList.SelectedIndices.Count == 2);
            SaveSelection.Enabled = false;
        }

        private void MainWindow_FormClosing (object sender, FormClosingEventArgs e) {
            if (Instance != null)
                Instance.Dispose();
        }

        private void DiffSelection_Click (object sender, EventArgs e) {
            var file1 = SnapshotList.SelectedItems[0] as string;
            var file2 = SnapshotList.SelectedItems[1] as string;

            DiffSelection.Enabled = false;
            UseWaitCursor = true;

            Instance.DiffSnapshots(file1, file2)
                .RegisterOnComplete((_) => {
                    DiffSelection.Enabled = true;
                    UseWaitCursor = false;
                });
        }
    }
}
