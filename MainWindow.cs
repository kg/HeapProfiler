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
using Squared.Util;
using Squared.Util.Bind;
using Microsoft.Win32;

namespace HeapProfiler {
    public partial class MainWindow : TaskForm {
        public RunningProcess Instance = null;

        protected IFuture AutoCaptureFuture = null;

        IBoundMember[] PersistedControls;

        public MainWindow (TaskScheduler scheduler) 
            : base(scheduler) {
            InitializeComponent();

            PersistedControls = new[] {
                BoundMember.New(() => ExecutablePath.Text),
                BoundMember.New(() => Arguments.Text),
                BoundMember.New(() => WorkingDirectory.Text)
            };

            LoadPersistedValues();
        }

        protected string ChooseName (IBoundMember bm) {
            return String.Format("{0}_{1}", (bm.Target as Control).Name, bm.Name);
        }

        protected void LoadPersistedValues () {
            if (!Registry.CurrentUser.SubKeyExists("Software\\HeapProfiler"))
                return;

            using (var key = Registry.CurrentUser.OpenSubKey("Software\\HeapProfiler"))
            foreach (var pc in PersistedControls)
                pc.Value = key.GetValue(ChooseName(pc), pc.Value);
        }

        protected void SavePersistedValues () {
            using (var key = Registry.CurrentUser.OpenOrCreateSubKey("Software\\HeapProfiler"))
            foreach (var pc in PersistedControls)
                key.SetValue(ChooseName(pc), pc.Value);
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
                dialog.DereferenceLinks = true;

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

            Instance = RunningProcess.Start(
                Scheduler, 
                ExecutablePath.Text,
                Arguments.Text,
                WorkingDirectory.Text
            );
            Instance.StatusChanged += (s, _) => RefreshStatus();
            Instance.SnapshotsChanged += (s, _) => RefreshSnapshots();

            RefreshStatus();
            RefreshSnapshots();

            if (AutoCapture.Checked)
                AutoCaptureFuture = Start(AutoCaptureTask());
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

            if ((!running) && (AutoCaptureFuture != null)) {
                AutoCaptureFuture.Dispose();
                AutoCaptureFuture = null;
            }

            RefreshLaunchEnabled();
        }

        private void RefreshSnapshots () {
            SnapshotList.BeginUpdate();

            while (SnapshotList.Items.Count > Instance.Snapshots.Count)
                SnapshotList.Items.RemoveAt(SnapshotList.Items.Count - 1);

            for (int i = 0; i < Instance.Snapshots.Count; i++) {
                if (i >= SnapshotList.Items.Count)
                    SnapshotList.Items.Add(Instance.Snapshots[i]);
                else
                    SnapshotList.Items[i] = Instance.Snapshots[i];
            }

            SnapshotList.EndUpdate();

            DiffSelection.Enabled = false;
        }

        private void CaptureSnapshot_Click (object sender, EventArgs e) {
            AutoCapture.Enabled = CaptureSnapshot.Enabled = false;
            UseWaitCursor = true;
            Instance.CaptureSnapshot()
                .RegisterOnComplete((_) => {
                    AutoCapture.Enabled = CaptureSnapshot.Enabled = true;
                    UseWaitCursor = false;
                });
        }

        private void SnapshotList_SelectedIndexChanged (object sender, EventArgs e) {
            DiffSelection.Enabled = (SnapshotList.SelectedIndices.Count >= 2);
        }

        private void MainWindow_FormClosing (object sender, FormClosingEventArgs e) {
            SavePersistedValues();

            if (Instance != null)
                Instance.Dispose();
        }

        private void DiffSelection_Click (object sender, EventArgs e) {
            int i1 = SnapshotList.SelectedIndices[0], 
                i2 = SnapshotList.SelectedIndices[SnapshotList.SelectedIndices.Count - 1];

            DiffSelection.Enabled = false;
            UseWaitCursor = true;

            ShowDiff(i1, i2);
        }

        protected void ShowDiff (int index1, int index2) {
            var viewer = new DiffViewer(Scheduler, Instance);

            viewer.Start(viewer.LoadRange(Pair.New(index1, index2)));

            Scheduler.QueueWorkItem(() => {
                DiffSelection.Enabled = true;
                UseWaitCursor = false;
                viewer.ShowDialog(this);
            });
        }

        private void MainWindow_FormClosed (object sender, FormClosedEventArgs e) {
            Application.Exit();
        }

        private void ExitMenu_Click (object sender, EventArgs e) {
            Application.Exit();
        }

        private void SymbolPathMenu_Click (object sender, EventArgs e) {

        }

        private void OpenDiffMenu_Click (object sender, EventArgs e) {
            using (var dialog = new OpenFileDialog()) {
                dialog.Filter = "Heap Diffs|*.heapdiff";
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.Multiselect = false;
                dialog.ShowReadOnly = false;
                dialog.Title = "Open Diff";

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                var viewer = new DiffViewer(Scheduler);
                viewer.Start(viewer.LoadDiff(dialog.FileName));
                viewer.ShowDialog(this);
            }
        }

        private void SelectWorkingDirectory_Click (object sender, EventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                dialog.Description = "Select Working Directory";

                if (Directory.Exists(WorkingDirectory.Text))
                    dialog.SelectedPath = WorkingDirectory.Text;
                else if (File.Exists(ExecutablePath.Text))
                    dialog.SelectedPath = Path.GetDirectoryName(ExecutablePath.Text);

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                WorkingDirectory.Text = dialog.SelectedPath;
            }
        }

        private void WorkingDirectory_DragDrop (object sender, DragEventArgs e) {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null)
                return;

            WorkingDirectory.Text = Path.GetDirectoryName(files[0]);
        }

        private void WorkingDirectory_DragOver (object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
        }

        private void AutoCapture_CheckedChanged (object sender, EventArgs e) {
            if (AutoCapture.Checked && CaptureSnapshot.Enabled) {
                AutoCaptureFuture = Start(AutoCaptureTask());
            } else if (AutoCaptureFuture != null) {
                AutoCaptureFuture.Dispose();
                AutoCaptureFuture = null;
            }
        }

        protected IEnumerator<object> AutoCaptureTask () {
            var sleep = new Sleep(5.0);

            while (AutoCapture.Checked && Instance.Running) {
                UseWaitCursor = true;
                yield return Instance.CaptureSnapshot();
                UseWaitCursor = false;

                yield return sleep;
            }
        }

        protected FileAssociation HeapdiffAssociation {
            get {
                var executablePath = Application.ExecutablePath;

                return new FileAssociation(
                    ".heapdiff", "HeapProfiler_heapdiff",
                    "Heap Profiler Diff",
                    String.Format("{0},0", executablePath),
                    String.Format("{0} \"%1\"", executablePath)
                );
            }
        }

        private void AssociateHeapdiffsMenu_Click (object sender, EventArgs e) {
            var assoc = HeapdiffAssociation;
            assoc.IsAssociated = !assoc.IsAssociated;
        }

        private void OptionsMenu_DropDownOpening (object sender, EventArgs e) {
            AssociateHeapdiffsMenu.Checked = HeapdiffAssociation.IsAssociated;
        }
    }
}
