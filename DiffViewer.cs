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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Squared.Task;
using System.IO;
using System.Text.RegularExpressions;

using Squared.Util;

namespace HeapProfiler {
    public partial class DiffViewer : TaskForm {
        public NameTable Modules = new NameTable();
        public NameTable FunctionNames = new NameTable();
        public List<DeltaInfo> Deltas = new List<DeltaInfo>();
        public StackGraph StackGraph = null;

        public List<DeltaInfo> ListItems = new List<DeltaInfo>();

        protected IFuture PendingLoad = null;
        protected Pair<int> PendingLoadPair = new Pair<int>(-1, -1);
        protected HeapRecording Instance = null;

        protected IFuture PendingRefresh = null;

        protected Pair<int> CurrentPair = new Pair<int>(-1, -1);
        protected string Filename;
        protected Regex FunctionFilter = null;
        protected StringFormat ListFormat;
        protected bool Updating = false;

        public DiffViewer (TaskScheduler scheduler, HeapRecording instance)
            : base (scheduler) {
            InitializeComponent();

            ListFormat = new StringFormat {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox
            };

            Timeline.ItemValueGetter = GetBytesTotal;
            Timeline.ItemValueFormatter = MainWindow.FormatSizeBytes;

            Instance = instance;
            if (Instance != null) {
                Timeline.Items = Instance.Snapshots;
                Instance.TracebacksFiltered += Instance_TracebacksFiltered;
                ViewFunctionHistogramMenu.Enabled = true;
                ViewFunctionTreemapMenu.Enabled = true;
            } else {
                Timeline.Visible = false;
                MainSplit.Height += Timeline.Bottom - MainSplit.Bottom;
                ViewFunctionHistogramMenu.Enabled = false;
                ViewFunctionTreemapMenu.Enabled = false;
            }
        }

        void Instance_TracebacksFiltered (object sender, EventArgs e) {
            SetBusy(true);
            Enabled = false;
            Start(ReloadTracebacks());
        }

        IEnumerator<object> ReloadTracebacks () {
            var keys = from delta in Deltas select delta.TracebackID;
            var fTracebacks = Instance.Database.FilteredTracebacks.CascadingSelect(
                new[] { Instance.Database.Tracebacks },
                keys
            );

            yield return fTracebacks;

            var resolvedTracebacks = new Dictionary<UInt32, TracebackInfo>();
            var functionNames = new NameTable();

            yield return Instance.ResolveTracebackSymbols(
                fTracebacks.Result, resolvedTracebacks, functionNames
            );

            foreach (var delta in Deltas)
                delta.Traceback = resolvedTracebacks[delta.TracebackID];

            FunctionNames = functionNames;

            DoneReloadingTracebacks();
        }

        void DoneReloadingTracebacks () {
            DeltaList.Invalidate();
            DeltaHistogram.Invalidate();
            GraphHistogram.Invalidate();
            SetBusy(false);
            Enabled = true;
        }

        public DiffViewer (TaskScheduler scheduler)
            : this(scheduler, null) {
        }

        public long GetBytesTotal (HeapSnapshotInfo item) {
            return (long)(item.BytesTotal);
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

            Timeline.Selection = range;

            var f = Start(Instance.DiffSnapshots(s1, s2));
            using (f)
                yield return f;

            var filename = f.Result as string;
            if (filename != null) {
                f = Start(LoadDiff(filename));
                using (f)
                    yield return f;
            } else {
                DiffLoaded(f.Result as HeapDiff, "unknown");
            }

            PendingLoadPair = Pair.New(-1, -1);
            CurrentPair = range;

            Text = "Diff Viewer - " + String.Format("{0} - {1}", s1.Timestamp.ToLongTimeString(), s2.Timestamp.ToLongTimeString());
        }

        protected void DiffLoaded (HeapDiff diff, string filename) {
            Modules = diff.Modules;
            FunctionNames = diff.FunctionNames;
            Deltas = diff.Deltas;

            TracebackFilter.AutoCompleteItems = FunctionNames;

            Text = "Diff Viewer - " + filename;
            Filename = filename;

            RefreshModules();

            if (PendingRefresh != null)
                PendingRefresh.Dispose();
            PendingRefresh = Start(RefreshDeltas());

            MainMenuStrip.Enabled = true;
            LoadingPanel.Visible = false;
            MainSplit.Visible = true;
            Timeline.Enabled = true;
            UseWaitCursor = false;
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
                        var v = value.Value;
                        if (LoadingProgress.Style != ProgressBarStyle.Continuous)
                            LoadingProgress.Style = ProgressBarStyle.Continuous;
                        LoadingProgress.Value = Math.Min(v + 1, LoadingProgress.Maximum);
                        LoadingProgress.Value = v;
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

            DiffLoaded(rtc.Result, filename);
        }

        public void RefreshModules () {
            if (Updating)
                return;

            SetBusy(true);

            ModuleList.Items = Modules.Keys;

            SetBusy(false);
        }

        public IEnumerator<object> RefreshDeltas () {
            try {
                if (Updating)
                    yield break;

                SetBusy(true);

                Future<int> fTotalBytes = new Future<int>(),
                    fTotalAllocs = new Future<int>(),
                    fMax = new Future<int>();
                var newListItems = new List<DeltaInfo>();
                var moduleList = ModuleList;
                var deltas = Deltas;
                var functionFilter = FunctionFilter;

                bool useModuleFilter = moduleList.SelectedItems.Count > 0;

                yield return Future.RunInThread(() => {
                    int max = -int.MaxValue;
                    int totalBytes = 0, totalAllocs = 0;

                    foreach (var delta in deltas) {
                        if (functionFilter != null) {
                            bool matched = false;

                            foreach (var functionName in delta.Traceback.Functions) {
                                if (functionFilter.IsMatch(functionName)) {
                                    matched = true;
                                    break;
                                }
                            }

                            if (!matched)
                                continue;
                        }

                        bool filteredOut = false;
                        if (useModuleFilter) {
                            filteredOut = true;
                            foreach (var module in delta.Traceback.Modules) {
                                filteredOut &= !moduleList.SelectedItems.Contains(module);

                                if (!filteredOut)
                                    break;
                            }
                        }

                        if (!filteredOut) {
                            newListItems.Add(delta);
                            totalBytes += delta.BytesDelta;
                            totalAllocs += delta.CountDelta.GetValueOrDefault(0);
                            max = Math.Max(max, delta.BytesDelta);
                        }
                    }

                    max = Math.Max(max, Math.Abs(totalBytes));
                    fTotalBytes.Complete(totalBytes);
                    fTotalAllocs.Complete(totalAllocs);
                    fMax.Complete(max);
                });

                StatusLabel.Text = String.Format("Showing {0} out of {1} item(s)", newListItems.Count, deltas.Count);
                AllocationTotals.Text = String.Format("Delta bytes: {0} Delta allocations: {1}", FileSize.Format(fTotalBytes.Result), fTotalAllocs.Result);

                if (Instance != null) {
                    StackGraph = new StackGraph();
                    yield return StackGraph.Build(Instance, newListItems);
                } else
                    StackGraph = null;

                if (StackGraph != null) {
                    GraphHistogram.Items = StackGraph.Functions.OrderedItems.ToArray();
                    GraphTreemap.Items = StackGraph.OrderedItems.ToArray();
                } else {
                    GraphHistogram.Items = null;
                    GraphTreemap.Items = null;
                }

                DeltaHistogram.Items = DeltaList.Items = newListItems;
                if (newListItems.Count > 0)
                    GraphHistogram.Maximum = DeltaHistogram.Maximum = fMax.Result;
                else
                    GraphHistogram.Maximum = DeltaHistogram.Maximum = 1024;

                GraphHistogram.TotalDelta = DeltaHistogram.TotalDelta = fTotalBytes.Result;

                DeltaList.Invalidate();
                DeltaHistogram.Invalidate();
                GraphHistogram.Invalidate();
                GraphTreemap.Refresh();
            } finally {
                SetBusy(false);
            }
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

        private void TracebackFilter_FilterChanging (object sender, FilterChangingEventArgs e) {
            if (e.Filter.Trim().Length == 0)
                return;

            Regex regex;
            try {
                regex = MainWindow.FilterToRegex(e.Filter);
            } catch {
                e.SetValid(false);
                return;
            }

            foreach (var name in FunctionNames) {
                if (regex.IsMatch(name)) {
                    e.SetValid(true);
                    return;
                }
            }

            e.SetValid(false);
        }

        private void TracebackFilter_FilterChanged (object sender, EventArgs e) {
            var filter = MainWindow.FilterToRegex(TracebackFilter.Filter);
            GraphHistogram.FunctionFilter = DeltaHistogram.FunctionFilter = DeltaList.FunctionFilter = FunctionFilter = filter;

            if (PendingRefresh != null)
                PendingRefresh.Dispose();

            PendingRefresh = Start(RefreshDeltas());
        }

        private void ViewListMenu_Click (object sender, EventArgs e) {
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = false;
            DeltaList.Visible = ViewListMenu.Checked = true;
            GraphHistogram.Visible = ViewFunctionHistogramMenu.Checked = false;
            GraphTreemap.Visible = ViewFunctionTreemapMenu.Checked = false;
        }

        private void ViewHistogramMenu_Click (object sender, EventArgs e) {
            DeltaList.Visible = ViewListMenu.Checked = false;
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = true;
            GraphHistogram.Visible = ViewFunctionHistogramMenu.Checked = false;
            GraphTreemap.Visible = ViewFunctionTreemapMenu.Checked = false;
        }

        private void Timeline_RangeChanged (object sender, EventArgs e) {
            var pair = Timeline.Selection;

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
            if (PendingRefresh != null)
                PendingRefresh.Dispose();
            PendingRefresh = Start(RefreshDeltas());
        }

        private void ViewFunctionHistogramMenu_Click (object sender, EventArgs e) {
            DeltaList.Visible = ViewListMenu.Checked = false;
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = false;
            GraphHistogram.Visible = ViewFunctionHistogramMenu.Checked = true;
            GraphTreemap.Visible = ViewFunctionTreemapMenu.Checked = false;
        }

        private void ViewFunctionTreemapMenu_Click (object sender, EventArgs e) {
            DeltaList.Visible = ViewListMenu.Checked = false;
            DeltaHistogram.Visible = ViewHistogramMenu.Checked = false;
            GraphHistogram.Visible = ViewFunctionHistogramMenu.Checked = false;
            GraphTreemap.Visible = ViewFunctionTreemapMenu.Checked = true;
        }
    }
}
