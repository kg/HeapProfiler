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
        public const int CaptureMemoryChangeThresholdPercentage = 5;
        public const double CaptureCheckIntervalSeconds = 1.0;
        public const double CaptureMaxIntervalSeconds = 60.0;

        public HeapRecording Instance = null;

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

            SnapshotTimeline.ItemValueGetter = GetPagedMemory;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;

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

            Instance = HeapRecording.StartProcess(
                Scheduler, Activities,
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

            try {
                var path = Path.GetFullPath(ExecutablePath.Text);
                if (!File.Exists(path))
                    enabled = false;
                if (Path.GetExtension(path).ToLowerInvariant() != ".exe")
                    enabled = false;
            } catch {
                enabled = false;
            }

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
            SnapshotTimeline.Items = Instance.Snapshots;
            SnapshotTimeline.Invalidate();

            DiffSelection.Enabled = SnapshotTimeline.HasSelection &&
                (SnapshotTimeline.Selection.First != SnapshotTimeline.Selection.Second);
            ViewSelection.Enabled = SnapshotTimeline.HasSelection &&
                (SnapshotTimeline.Selection.First == SnapshotTimeline.Selection.Second);
        }

        private void CaptureSnapshot_Click (object sender, EventArgs e) {
            AutoCapture.Enabled = CaptureSnapshot.Enabled = false;
            Instance.CaptureSnapshot()
                .RegisterOnComplete((_) => {
                    AutoCapture.Enabled = CaptureSnapshot.Enabled = true;
                });
        }

        private void MainWindow_FormClosing (object sender, FormClosingEventArgs e) {
            SavePersistedValues();

            if (Instance != null)
                Instance.Dispose();
        }

        private void DiffSelection_Click (object sender, EventArgs e) {
            var indices = SnapshotTimeline.Selection;

            ShowDiff(indices.First, indices.Second);
        }

        protected void ShowDiff (int index1, int index2) {
            var viewer = new DiffViewer(Scheduler, Instance);

            viewer.Start(viewer.LoadRange(Pair.New(index1, index2)));

            Scheduler.QueueWorkItem(() => {
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
            using (var dialog = new SymbolSettingsDialog())
                dialog.ShowDialog(this);
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
            var sleep = new Sleep(0.1);

            while ((Instance == null) || (!Instance.Running))
                yield return sleep;

            sleep = new Sleep(CaptureCheckIntervalSeconds);

            long captureInterval = (long)(CaptureMaxIntervalSeconds * Time.SecondInTicks);
            long lastPaged = 0, lastWorking = 0, lastCaptureWhen = 0;
            bool shouldCapture = false;

            while (AutoCapture.Checked && Instance.Running) {
                Instance.Process.Refresh();
                var pagedDelta = Math.Abs(Instance.Process.PagedMemorySize64 - lastPaged);
                var workingDelta = Math.Abs(Instance.Process.WorkingSet64 - lastWorking);
                var deltaPercent = Math.Max(
                    pagedDelta * 100 / Math.Max(Instance.Process.PagedMemorySize64, lastPaged),
                    workingDelta * 100 / Math.Max(Instance.Process.WorkingSet64, lastWorking)
                );
                var elapsed = Time.Ticks - lastCaptureWhen;

                shouldCapture = (deltaPercent >= CaptureMemoryChangeThresholdPercentage) || 
                    (elapsed > captureInterval);

                if (shouldCapture) {
                    lastPaged = Instance.Process.PagedMemorySize64;
                    lastWorking = Instance.Process.WorkingSet64;
                    lastCaptureWhen = Time.Ticks;
                    yield return Instance.CaptureSnapshot();
                }

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

        private void SaveAllSnapshots_Click (object sender, EventArgs e) {
            if (Instance == null)
                return;
            if (Instance.Snapshots.Count == 0)
                return;

            using (var dialog = new FolderBrowserDialog()) {
                dialog.Description = "Save snapshots to folder";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                using (Activities.AddItem("Saving snapshots"))
                foreach (var snap in Instance.Snapshots) {
                    var destPath = Path.Combine(
                        dialog.SelectedPath,
                        String.Format(
                            "{0:0000}_{1}.heapsnap", 
                            snap.Index, 
                            snap.When.ToString("u").Replace(":", "_")
                        )
                    );
                    try {
                        File.Copy(snap.Filename, destPath, true);
                    } catch (Exception ex) {
                        MessageBox.Show("Save failed: " + ex.ToString());
                        return;
                    }
                }
            }
        }

        private void OpenSnapshotsMenu_Click (object sender, EventArgs e) {
            using (var dialog = new OpenFileDialog()) {
                dialog.Title = "Open Snapshots";
                dialog.Filter = "Saved Snapshots|*.heapsnap";
                dialog.Multiselect = true;
                dialog.ShowReadOnly = false;
                dialog.AddExtension = false;
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                Instance = HeapRecording.FromSnapshots(
                    Scheduler, Activities, dialog.FileNames
                );
                Instance.StatusChanged += (s, _) => RefreshStatus();
                Instance.SnapshotsChanged += (s, _) => RefreshSnapshots();

                RefreshStatus();
                RefreshSnapshots();
            }
        }

        private void Activities_PreferredSizeChanged (object sender, EventArgs e) {
            var margin = GroupSnapshots.Left;
            var ps = Activities.GetPreferredSize(new Size(
                GroupSnapshots.Width, ClientSize.Height
            ));

            int newTop = ClientSize.Height - ps.Height - margin;
            var newHeight = newTop - (ps.Height > 0 ? margin : 0) - GroupSnapshots.Top;

            if ((newTop == Activities.Top) && (newHeight == GroupSnapshots.Height))
                return;

            SuspendLayout();

            GroupSnapshots.SetBounds(
                GroupSnapshots.Left, GroupSnapshots.Top,
                GroupSnapshots.Width, newHeight
            );
            Activities.SetBounds(
                GroupSnapshots.Left, newTop, GroupSnapshots.Width, ps.Height
            );

            ResumeLayout(true);
        }

        private void SnapshotTimeline_SelectionChanged (object sender, EventArgs e) {
            DiffSelection.Enabled = SnapshotTimeline.HasSelection && 
                (SnapshotTimeline.Selection.First != SnapshotTimeline.Selection.Second);
            ViewSelection.Enabled = SnapshotTimeline.HasSelection &&
                (SnapshotTimeline.Selection.First == SnapshotTimeline.Selection.Second);
        }

        protected long GetPagedMemory (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;

            return item.Memory.Paged;
        }

        protected long GetVirtualMemory (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;

            return item.Memory.Virtual;
        }

        protected long GetWorkingSet (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;

            return item.Memory.WorkingSet;
        }

        protected long GetLargestFreeHeapBlock (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;
            else if (item.Heaps.Count == 0)
                return 0;

            return (long)(from heap in item.Heaps select heap.LargestFreeSpan).Max();
        }

        protected long GetAverageFreeHeapBlockSize (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;
            else if (item.Heaps.Count == 0)
                return 0;

            return (long)(from heap in item.Heaps select (heap.EstimatedFree) / Math.Max(heap.EmptySpans, 1)).Average();
        }

        protected long GetLargestOccupiedHeapBlock (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;
            else if (item.Heaps.Count == 0)
                return 0;

            return (long)(from heap in item.Heaps select heap.LargestOccupiedSpan).Max();
        }

        protected long GetAverageOccupiedHeapBlockSize (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;
            else if (item.Heaps.Count == 0)
                return 0;

            return (long)(from heap in item.Heaps select (heap.TotalOverhead + heap.TotalRequested) / Math.Max(heap.OccupiedSpans, 1)).Average();
        }

        protected long GetHeapFragmentation (HeapSnapshot item) {
            if (item.Memory == null)
                return 0;

            return (long)(item.HeapFragmentation * 10000);
        }

        protected string FormatSizeBytes (long bytes) {
            return FileSize.Format(bytes);
        }

        protected string FormatPercentage (long percentage) {
            return String.Format("{0}%", (percentage / 100.0f));
        }

        private void ViewPagedMemoryMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetPagedMemory;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewVirtualMemoryMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetVirtualMemory;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewWorkingSetMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetWorkingSet;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewLargestFreeHeapMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetLargestFreeHeapBlock;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewLargestOccupiedHeapMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetLargestOccupiedHeapBlock;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewAverageHeapBlockSizeMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetAverageOccupiedHeapBlockSize;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewAverageFreeBlockSizeMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetAverageFreeHeapBlockSize;
            SnapshotTimeline.ItemValueFormatter = FormatSizeBytes;
        }

        private void ViewHeapFragmentationMenu_Click (object sender, EventArgs e) {
            SnapshotTimeline.ItemValueGetter = GetHeapFragmentation;
            SnapshotTimeline.ItemValueFormatter = FormatPercentage;
        }

        private void SnapshotTimeline_ItemValueGetterChanged (object sender, EventArgs e) {
            var getter = SnapshotTimeline.ItemValueGetter;
            ViewPagedMemoryMenu.Checked = (getter == GetPagedMemory);
            ViewVirtualMemoryMenu.Checked = (getter == GetVirtualMemory);
            ViewWorkingSetMenu.Checked = (getter == GetWorkingSet);
            ViewLargestFreeHeapMenu.Checked = (getter == GetLargestFreeHeapBlock);
            ViewAverageFreeBlockSizeMenu.Checked = (getter == GetAverageFreeHeapBlockSize);
            ViewLargestOccupiedHeapMenu.Checked = (getter == GetLargestOccupiedHeapBlock);
            ViewAverageHeapBlockSizeMenu.Checked = (getter == GetAverageOccupiedHeapBlockSize);
            ViewHeapFragmentationMenu.Checked = (getter == GetHeapFragmentation);
        }

        private void ViewSelection_Click (object sender, EventArgs e) {
            var index = SnapshotTimeline.Selection.First;
            var viewer = new HeapViewer(Scheduler, Instance);
            viewer.SetSnapshot(index);
            viewer.ShowDialog(this);
        }
    }
}
