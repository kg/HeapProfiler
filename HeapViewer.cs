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

using Snapshot = HeapProfiler.HeapSnapshot;
using Allocation = HeapProfiler.HeapSnapshot.Allocation;
using Squared.Util;

namespace HeapProfiler {
    public partial class HeapViewer : TaskForm {
        protected HeapSnapshot Snapshot = null;
        public NameTable FunctionNames = new NameTable();

        public List<Allocation> ListItems = new List<Allocation>();

        protected IFuture PendingLoad = null;
        protected HeapRecording Instance = null;

        protected string Filename;
        protected string FunctionFilter = null;
        protected StringFormat ListFormat;
        protected bool Updating = false;

        public HeapViewer (TaskScheduler scheduler, HeapRecording instance)
            : base (scheduler) {
            InitializeComponent();

            ListFormat = new StringFormat {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox
            };

            LayoutView.Instance = instance;

            Timeline.ItemValueGetter = GetBytesTotal;
            Timeline.ItemValueFormatter = MainWindow.FormatSizeBytes;

            Instance = instance;
            if (Instance != null) {
                Timeline.Items = Instance.Snapshots;
            } else {
                Timeline.Visible = false;
                LayoutView.Height += Timeline.Bottom - LayoutView.Bottom;
            }
        }

        public HeapViewer (TaskScheduler scheduler)
            : this(scheduler, null) {
        }

        public long GetBytesTotal (HeapSnapshotInfo item) {
            return (long)(item.BytesTotal);
        }

        protected void SetBusy (bool busy) {
            UseWaitCursor = Updating = busy;
        }

        public void RefreshHeap () {
            if (Updating)
                return;

            SetBusy(true);

            LayoutView.Snapshot = Snapshot;
            LayoutView.Invalidate();

            SetBusy(false);
        }

        private void HeapViewer_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();

            Snapshot.Info.ReleaseStrongReference();
            Timeline.Items = new List<HeapSnapshotInfo>();
            LayoutView.Snapshot = Snapshot = null;
        }

        private void CloseMenu_Click (object sender, EventArgs e) {
            Close();
        }

        private IEnumerator<object> SetCurrentSnapshot (HeapSnapshotInfo info) {
            var oldSnapshot = Snapshot;
            if (oldSnapshot != null)
                oldSnapshot.Info.ReleaseStrongReference();

            using (Finally.Do(() => {
                UseWaitCursor = false;
            })) {
                var fSnapshot = Instance.GetSnapshot(info);
                yield return fSnapshot;

                Snapshot = fSnapshot.Result;
                RefreshHeap();
            }
        }

        private void Timeline_RangeChanged (object sender, EventArgs e) {
            var info = Instance.Snapshots[Timeline.Selection.First];
            UseWaitCursor = true;
            Start(SetCurrentSnapshot(info));
        }

        public void SetSnapshot (int index) {
            Timeline.Selection = Pair.New(index, index);
        }
    }
}
