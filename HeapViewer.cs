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
    public partial class HeapViewer : TaskForm {
        public Dictionary<string, ModuleInfo> Modules = new Dictionary<string, ModuleInfo>();
        public NameTable FunctionNames = new NameTable();
        public List<DeltaInfo> Deltas = new List<DeltaInfo>();
        public Dictionary<UInt32, TracebackInfo> Tracebacks = new Dictionary<UInt32, TracebackInfo>();

        public List<DeltaInfo> ListItems = new List<DeltaInfo>();

        protected IFuture PendingLoad = null;
        protected Pair<int> PendingLoadPair = new Pair<int>(-1, -1);
        protected RunningProcess Instance = null;

        protected Pair<int> CurrentPair = new Pair<int>(-1, -1);
        protected string Filename;
        protected string FunctionFilter = null;
        protected StringFormat DeltaListFormat;
        protected bool Updating = false;

        public HeapViewer (TaskScheduler scheduler, RunningProcess instance)
            : base (scheduler) {
            InitializeComponent();

            DeltaListFormat = new StringFormat();
            DeltaListFormat.Trimming = StringTrimming.None;
            DeltaListFormat.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;

            Instance = instance;
            if (Instance != null) {
                Timeline.Items = Instance.Snapshots;
            } else {
                Timeline.Visible = false;
                MainSplit.Height += Timeline.Bottom - MainSplit.Bottom;
            }
        }

        public HeapViewer (TaskScheduler scheduler)
            : this(scheduler, null) {
        }

        protected void SetBusy (bool busy) {
            UseWaitCursor = Updating = busy;
        }

        public void RefreshModules () {
            if (Updating)
                return;

            SetBusy(true);

            ModuleList.Items = Modules;

            SetBusy(false);
        }

        public void RefreshHeap () {
            if (Updating)
                return;

            SetBusy(true);

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
                RefreshHeap();
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

        private void ViewSplit_SizeChanged (object sender, EventArgs e) {
            TracebackFilter.Width = ViewSplit.Panel1.ClientSize.Width - FindIcon.Width;
        }
    }
}
