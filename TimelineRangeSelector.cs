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
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Snapshot = HeapProfiler.RunningProcess.Snapshot;
using Squared.Util;

namespace HeapProfiler {
    public partial class TimelineRangeSelector : UserControl {
        public event EventHandler RangeChanged;

        public IList<Snapshot> Items;

        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected int BeginIndex, EndIndex;

        public TimelineRangeSelector () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
        }

        protected override void Dispose (bool disposing) {
            Scratch.Dispose();

            base.Dispose(disposing);
        }

        public Snapshot Begin {
            get {
                return default(Snapshot);
            }
            set {
                BeginIndex = Items.IndexOf(value);
                OnRangeChanged();
            }
        }

        public Snapshot End {
            get {
                return default(Snapshot);
            }
            set {
                EndIndex = Items.IndexOf(value);
                OnRangeChanged();
            }
        }

        public Pair<int> Indices {
            get {
                return new Pair<int>(BeginIndex, EndIndex);
            }
            set {
                BeginIndex = value.First;
                EndIndex = value.Second;
                OnRangeChanged();
            }
        }

        protected void OnRangeChanged () {
            if (RangeChanged != null)
                RangeChanged(this, EventArgs.Empty);

            Invalidate();
        }

        protected override void OnPaintBackground (PaintEventArgs e) {            
        }

        protected override void OnPaint (PaintEventArgs e) {
            var width = ClientSize.Width - 2.0f;
            var height = ClientSize.Height - 2.0f;

            using (var textBrush = new SolidBrush(ForeColor))
            using (var scratch = Scratch.Get(e.Graphics, e.ClipRectangle)) {
                var g = scratch.Graphics;

                g.Clear(BackColor);

                g.DrawString("Hello World", Font, textBrush, new PointF(1f, 1f));
            }
        }
    }
}
