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
        public struct VisibleItem {
            public float X;
            public int Index;
        }

        public const float HorizontalMargin = 18.0f;
        public const float BottomMargin = 8.0f;

        public event EventHandler RangeChanged;

        public IList<Snapshot> Items = new List<Snapshot>();

        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected int BeginIndex = -1, EndIndex = -1;
        protected Point MouseDownLocation;

        protected Pair<int> ToolTipIndices = Pair.New(-1, -1);
        protected ToolTip ToolTip;

        public TimelineRangeSelector () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;

            ToolTip = new ToolTip();
        }

        protected override void Dispose (bool disposing) {
            Scratch.Dispose();
            ToolTip.Dispose();

            base.Dispose(disposing);
        }

        public Snapshot Begin {
            get {
                if ((BeginIndex < 0) || (BeginIndex >= Items.Count))
                    return default(Snapshot);

                return Items[BeginIndex];
            }
            set {
                var newIndex = Items.IndexOf(value);
                if (newIndex != BeginIndex) {
                    BeginIndex = Items.IndexOf(value);
                    OnRangeChanged();
                }
            }
        }

        public Snapshot End {
            get {
                if ((EndIndex < 0) || (EndIndex >= Items.Count))
                    return default(Snapshot);

                return Items[EndIndex];
            }
            set {
                var newIndex = Items.IndexOf(value);
                if (newIndex != EndIndex) {
                    EndIndex = newIndex;
                    OnRangeChanged();
                }
            }
        }

        public Pair<int> Indices {
            get {
                return new Pair<int>(BeginIndex, EndIndex);
            }
            set {
                if ((value.First != BeginIndex) || (value.Second != EndIndex)) {
                    BeginIndex = value.First;
                    EndIndex = value.Second;
                    OnRangeChanged();
                }
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
            var width = ClientSize.Width - HorizontalMargin;
            var height = ClientSize.Height - BottomMargin;

            VisibleItems.Clear();

            using (var textBrush = new SolidBrush(ForeColor))
            using (var outlinePen = new Pen(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight))
            using (var highlightPen = new Pen(SystemColors.HighlightText))
            using (var scratch = Scratch.Get(e.Graphics, e.ClipRectangle)) {
                var g = scratch.Graphics;
                var xOffset = (ClientSize.Width - width) / 2f;
                var itemWidth = width / (float)(Items.Count - 1);

                g.Clear(BackColor);
                g.DrawRectangle(
                    outlinePen, xOffset, 0.0f, width - 1, height - 1
                );

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                for (int i = 0; i < Items.Count - 1; i++) {
                    float x = itemWidth * i;
                    g.DrawLine(outlinePen, x + xOffset, 0, x + xOffset, height - 1);

                    VisibleItems.Add(new VisibleItem {
                        Index = i, X = x
                    });
                }

                var x1 = (itemWidth * BeginIndex) + xOffset;
                var x2 = (itemWidth * EndIndex) + xOffset;
                g.FillRectangle(
                    highlightBrush, new RectangleF(
                        x1, 0, x2 - x1, height
                ));
                g.DrawRectangle(
                    highlightPen, x1, 0, x2 - x1, height - 1
                );

                for (int i = BeginIndex; i <= EndIndex; i++) {
                    float x = itemWidth * i;
                    g.DrawLine(highlightPen, x + xOffset, 0, x + xOffset, height - 1);
                }

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                float arrowWidth = (ClientSize.Height - height);
                foreach (float x in new [] { 
                    Math.Ceiling((itemWidth * BeginIndex) + xOffset), 
                    Math.Floor((itemWidth * EndIndex) + xOffset)
                }) {
                    g.FillPolygon(
                        highlightBrush, new PointF[] {
                            new PointF(x, height), 
                            new PointF(x - arrowWidth, ClientSize.Height - 1),
                            new PointF(x + arrowWidth, ClientSize.Height - 1)
                        }
                    );
                    g.DrawLine(outlinePen, x, height, x - arrowWidth, ClientSize.Height - 1);
                    g.DrawLine(outlinePen, x, height, x + arrowWidth, ClientSize.Height - 1);
                    g.DrawLine(outlinePen, x - arrowWidth, ClientSize.Height - 1, x + arrowWidth, ClientSize.Height - 1);
                }
            }
        }

        protected int IndexFromPoint (Point pt, int direction) {
            if (direction < 0) {
                for (int i = VisibleItems.Count - 1; i >= 0; i--) {
                    var vi = VisibleItems[i];
                    if (pt.X >= vi.X)
                        return vi.Index;
                }

                return 0;
            } else {
                for (int i = 0; i < VisibleItems.Count; i++) {
                    var vi = VisibleItems[i];
                    if (pt.X <= vi.X)
                        return vi.Index;
                }

                return Items.Count - 1;
            }
        }

        protected void SetRangeFromPoints (Point start, Point end) {
            int i1, i2;

            if (start.X > end.X) {
                i1 = IndexFromPoint(end, -1);
                i2 = IndexFromPoint(start, 1);
            } else {
                i1 = IndexFromPoint(start, -1);
                i2 = IndexFromPoint(end, 1);
            }
            if (i2 < i1) {
                var t = i1;
                i1 = i2;
                i2 = t;
            }

            if (i1 > (Items.Count - 2))
                i1 = Items.Count - 2;

            SetTooltipRange(Pair.New(i1, i2));

            if ((i1 != BeginIndex) || (i2 != EndIndex)) {
                BeginIndex = i1;
                EndIndex = i2;
                OnRangeChanged();
            }
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                MouseDownLocation = e.Location;

                SetRangeFromPoints(MouseDownLocation, e.Location);
            }

            base.OnMouseDown(e);
        }

        protected void SetTooltipRange (Pair<int> range) {
            if (range.CompareTo(ToolTipIndices) == 0)
                return;

            ToolTipIndices = range;
            ToolTip.SetToolTip(
                this, String.Format(
                    "{0} - {1}",
                    Items[range.First].When.ToLongTimeString(),
                    Items[range.Second].When.ToLongTimeString()
                )
            );
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                SetRangeFromPoints(MouseDownLocation, e.Location);
            } else {
                var i1 = IndexFromPoint(e.Location, -1);
                var i2 = IndexFromPoint(e.Location, 1);

                if ((i1 >= BeginIndex) && (i2 <= EndIndex)) {
                    i1 = BeginIndex;
                    i2 = EndIndex;
                }

                SetTooltipRange(Pair.New(i1, i2));
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left)
                SetRangeFromPoints(MouseDownLocation, e.Location);

            base.OnMouseUp(e);
        }
    }
}
