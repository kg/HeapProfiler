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
using Squared.Task;
using System.Diagnostics;
using System.IO;
using Squared.Task.IO;
using System.Text.RegularExpressions;
using Squared.Util.RegexExtensions;
using System.Globalization;

using Snapshot = HeapProfiler.HeapSnapshot;
using Squared.Util;

namespace HeapProfiler {
    public partial class DiffViewer : TaskForm {
        public Dictionary<string, ModuleInfo> Modules = new Dictionary<string, ModuleInfo>();
        public NameTable FunctionNames = new NameTable();
        public List<DeltaInfo> Deltas = new List<DeltaInfo>();

        public List<DeltaInfo> ListItems = new List<DeltaInfo>();

        protected IFuture PendingLoad = null;
        protected Pair<int> PendingLoadPair = new Pair<int>(-1, -1);
        protected HeapRecording Instance = null;

        protected Pair<int> CurrentPair = new Pair<int>(-1, -1);
        protected string Filename;
        protected string FunctionFilter = null;
        protected StringFormat ListFormat;
        protected bool Updating = false;

        public DiffViewer (TaskScheduler scheduler, HeapRecording instance)
            : base (scheduler) {
            InitializeComponent();

            ListFormat = new StringFormat();
            ListFormat.Trimming = StringTrimming.None;
            ListFormat.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;

            Instance = instance;
            if (Instance != null) {
                Timeline.Items = Instance.Snapshots;
            } else {
                Timeline.Visible = false;
                MainSplit.Height += Timeline.Bottom - MainSplit.Bottom;
            }
        }

        public DiffViewer (TaskScheduler scheduler)
            : this(scheduler, null) {
        }

        protected void SetBusy (bool busy) {
            UseWaitCursor = Updating = busy;
        }

        public IEnumerator<object> LoadRange (Pair<int> range) {
            PendingLoadPair = range;

            LoadingPanel.Text = "Generating diff...";
            LoadingProgress.Value = 0;
            LoadingProgress.Style = ProgressBarStyle.Marquee;

            MainMenuStrip.Enabled = false;
            LoadingPanel.Visible = true;
            MainSplit.Visible = false;
            UseWaitCursor = true;

            var s1 = Instance.Snapshots[range.First];
            var s2 = Instance.Snapshots[range.Second];

            Timeline.Indices = range;

            var f = Start(Instance.DiffSnapshots(s1.Filename, s2.Filename));
            using (f)
                yield return f;

            var filename = f.Result as string;
            f = Start(LoadDiff(filename));
            using (f)
                yield return f;

            PendingLoadPair = Pair.New(-1, -1);
            CurrentPair = range;

            Text = "Diff Viewer - " + String.Format("{0} - {1}", s1.When.ToLongTimeString(), s2.When.ToLongTimeString());
        }

        public IEnumerator<object> LoadDiff (string filename) {
            var progress = new CallbackProgressListener {
                OnSetStatus = (status) => {
                    LoadingPanel.Text = status;
                },
                OnSetMaximum = (maximum) => {
                    if (LoadingProgress.Maximum != maximum)
                        LoadingProgress.Maximum = maximum;
                },
                OnSetProgress = (value) => {
                    if (value.HasValue) {
                        if (LoadingProgress.Style != ProgressBarStyle.Continuous)
                            LoadingProgress.Style = ProgressBarStyle.Continuous;
                        LoadingProgress.Value = Math.Min(value.Value + 1, LoadingProgress.Maximum);
                        LoadingProgress.Value = value.Value;
                    } else {
                        if (LoadingProgress.Style != ProgressBarStyle.Marquee)
                            LoadingProgress.Style = ProgressBarStyle.Marquee;
                    }
                }
            };

            var rtc = new RunToCompletion<HeapDiff>(
                HeapDiff.FromFile(filename, progress)
            );
            using (rtc)
                yield return rtc;

            Modules = rtc.Result.Modules;
            FunctionNames = rtc.Result.FunctionNames;
            Deltas = rtc.Result.Deltas;

            TracebackFilter.AutoCompleteCustomSource.Clear();
            TracebackFilter.AutoCompleteCustomSource.AddRange(FunctionNames.ToArray());

            Text = "Diff Viewer - " + filename;
            Filename = filename;

            RefreshModules();
            RefreshDeltas();

            MainMenuStrip.Enabled = true;
            LoadingPanel.Visible = false;
            MainSplit.Visible = true;
            Timeline.Enabled = true;
            UseWaitCursor = false;
        }

        public void RefreshModules () {
            if (Updating)
                return;

            SetBusy(true);

            ModuleList.Items = Modules.Keys;

            SetBusy(false);
        }

        public void RefreshDeltas () {
            if (Updating)
                return;

            SetBusy(true);

            int max = -int.MaxValue;
            int totalBytes = 0, totalAllocs = 0;

            ListItems.Clear();
            foreach (var delta in Deltas) {
                if (FunctionFilter != null) {
                    if (!delta.Traceback.Functions.Contains(FunctionFilter))
                        continue;
                }

                bool filteredOut = (delta.Traceback.Modules.Count > 0);
                foreach (var module in delta.Traceback.Modules) {
                    filteredOut &= !ModuleList.SelectedItems.Contains(module);

                    if (!filteredOut)
                        break;
                }

                if (!filteredOut) {
                    ListItems.Add(delta);
                    totalBytes += delta.BytesDelta * (delta.Added ? 1 : -1);
                    totalAllocs += delta.CountDelta.GetValueOrDefault(0) * (delta.Added ? 1 : -1);
                    max = Math.Max(max, delta.BytesDelta);
                }
            }

            max = Math.Max(max, Math.Abs(totalBytes));

            StatusLabel.Text = String.Format("Showing {0} out of {1} item(s)", ListItems.Count, Deltas.Count);
            AllocationTotals.Text = String.Format("Delta bytes: {0} Delta allocations: {1}", FileSize.Format(totalBytes), totalAllocs);

            DeltaHistogram.Items = DeltaList.Items = ListItems;
            if (ListItems.Count > 0)
                DeltaHistogram.Maximum = max;
            else
                DeltaHistogram.Maximum = 1024;
            DeltaHistogram.TotalDelta = totalBytes;

            DeltaList.Invalidate();
            DeltaHistogram.Invalidate();

            SetBusy(false);
        }

        private void DiffViewer_Shown (object sender, EventArgs e) {
            UseWaitCursor = true;
        }

        private void DiffViewer_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();
        }

        private void SaveDiffMenu_Click (object sender, EventArgs e) {
            using (var dialog = new SaveFileDialog()) {
                dialog.Title = "Save Diff";
                dialog.Filter = "Heap Diffs|*.heapdiff";
                dialog.AddExtension = true;
                dialog.CheckPathExists = true;
                dialog.DefaultExt = ".heapdiff";

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                File.Copy(Filename, dialog.FileName, true);
                Filename = dialog.FileName;
                Text = "Diff Viewer - " + Filename;
            }
        }

        private void CloseMenu_Click (object sender, EventArgs e) {
            Close();
        }

        private void TracebackFilter_TextChanged (object sender, EventArgs e) {
            string newFilter = null;
            if (FunctionNames.Contains(TracebackFilter.Text))
                newFilter = TracebackFilter.Text;

            var newColor =
                (TracebackFilter.Text.Length > 0) ?
                    ((newFilter == null) ?
                        Color.LightPink : Color.LightGoldenrodYellow)
                    : SystemColors.Window;

            if (newColor != TracebackFilter.BackColor)
                TracebackFilter.BackColor = newColor;

            if (newFilter != FunctionFilter) {
                DeltaHistogram.FunctionFilter = DeltaList.FunctionFilter = FunctionFilter = newFilter;
                RefreshDeltas();
            }
        }

        private void ViewListMenu_Click (object sender, EventArgs e) {
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = false;
            DeltaList.Visible = ViewListMenu.Checked = true;
        }

        private void ViewHistogramMenu_Click (object sender, EventArgs e) {
            DeltaList.Visible = ViewListMenu.Checked = false;
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = true;
        }

        private void Timeline_RangeChanged (object sender, EventArgs e) {
            var pair = Timeline.Indices;

            if (pair.CompareTo(PendingLoadPair) != 0) {
                if (PendingLoad != null) {
                    PendingLoad.Dispose();
                    PendingLoad = null;
                    PendingLoadPair = Pair.New(-1, -1);
                }
            } else {
                return;
            }

            if (pair.CompareTo(CurrentPair) != 0)
                PendingLoad = Start(LoadRange(pair));
        }

        private void ModuleList_FilterChanged (object sender, EventArgs e) {
            RefreshDeltas();
        }

        private void ViewSplit_SizeChanged (object sender, EventArgs e) {
            TracebackFilter.Width = ViewSplit.Panel1.ClientSize.Width - FindIcon.Width;
        }
    }
}
