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
using Squared.Task;
using Squared.Util;
using System.Drawing.Drawing2D;

namespace HeapProfiler {
    public class HeapLayoutView : UserControl {
        public HeapSnapshot Snapshot = null;

        protected const int BytesPerPixel = 16;
        protected const int RowHeight = 12;
        protected const int MarginWidth = 2;

        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected ScrollBar ScrollBar = null;

        protected int _ScrollOffset = 0;

        public HeapLayoutView () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;

            ScrollBar = new VScrollBar {
                SmallChange = 1,
                LargeChange = 8,
                TabStop = false
            };

            ScrollBar.Scroll += new ScrollEventHandler(ScrollBar_Scroll);
            OnResize(EventArgs.Empty);

            Controls.Add(ScrollBar);
        }

        protected override void Dispose (bool disposing) {
            Scratch.Dispose();

            base.Dispose(disposing);
        }

        void ScrollBar_Scroll (object sender, ScrollEventArgs e) {
            ScrollOffset = e.NewValue;
        }

        protected override void OnResize (EventArgs e) {
            var preferredSize = ScrollBar.GetPreferredSize(ClientSize);
            ScrollBar.SetBounds(ClientSize.Width - preferredSize.Width, 0, preferredSize.Width, ClientSize.Height);

            base.OnResize(e);
        }

        protected override void OnPaintBackground (PaintEventArgs e) {
        }

        protected Color SelectItemColor (HeapSnapshot.Allocation alloc) {
            var id = BitConverter.ToInt32(BitConverter.GetBytes(alloc.TracebackID), 0);

            int hue = (id & 0xFFFF) % (HSV.HueMax);
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 70 / 100)) + (HSV.ValueMax * 25 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaint (PaintEventArgs e) {
            if (Snapshot == null) {
                e.Graphics.Clear(BackColor);
                return;
            }

            var width = ClientSize.Width - ScrollBar.Width;

            var bytesPerRow = BytesPerRow;
            var contentHeight = ContentHeight;

            if (_ScrollOffset >= contentHeight)
                _ScrollOffset = contentHeight - 1;
            if (_ScrollOffset < 0)
                _ScrollOffset = 0;

            using (var functionHighlightBrush = new SolidBrush(Color.PaleGoldenrod))
            using (var shadeBrush = new SolidBrush(SystemColors.ControlLight))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight)) {
                int y = -_ScrollOffset * RowHeight;

                foreach (var heapId in Snapshot.Heaps.Keys) {
                    var heap = Snapshot.Heaps[heapId];
                    var itemHeight = (int)(Math.Ceiling(heap.EstimatedSize / (float)BytesPerRow) + 1) * RowHeight;
                    var rgn = new Rectangle(0, y, width, itemHeight);
                    var maxX = width - MarginWidth;

                    var clippedRgn = rgn;
                    clippedRgn.Intersect(e.ClipRectangle);

                    if (rgn.IntersectsWith(e.ClipRectangle))
                    using (var scratch = Scratch.Get(e.Graphics, clippedRgn)) {
                        var g = scratch.Graphics;

                        g.SmoothingMode = SmoothingMode.None;

                        g.ResetClip();
                        g.Clear(shadeBrush.Color);
                        foreach (var allocation in heap.Allocations) {
                            var color = SelectItemColor(allocation);

                            int pos = (int)(allocation.Address - heap.BaseAddress);
                            int y1 = y + ((pos / BytesPerRow) * RowHeight),
                                y2 = y1 + RowHeight;
                            float x1 = (pos % BytesPerRow) / (float)BytesPerPixel,
                                x2 = x1 + ((allocation.Size + allocation.Overhead) / (float)BytesPerPixel);

                            if (y2 < rgn.Top)
                                continue;
                            if (y1 > rgn.Bottom)
                                break;

                            using (var itemBrush = new SolidBrush(color))
                            do {
                                g.FillRectangle(itemBrush, new RectangleF(
                                    x1 + MarginWidth, y1,
                                    Math.Min(x2, maxX) + MarginWidth - x1, y2 - y1
                                ));

                                var w = Math.Min(x2, maxX) - x1;

                                x2 -= w;
                                x1 = 0;
                            } while (x2 > width);
                        }
                    }

                    y += itemHeight;
                    if (y >= ClientSize.Height)
                        break;
                }

                if (y < ClientSize.Height)
                    e.Graphics.FillRectangle(backgroundBrush, new Rectangle(0, y, ClientSize.Width, ClientSize.Height - y));
            }

            var largeChange = ClientSize.Height / RowHeight;
            if (ScrollBar.LargeChange != largeChange)
                ScrollBar.LargeChange = largeChange;

            int scrollMax = Math.Max(1, contentHeight) + largeChange - 1;
            if (ScrollBar.Maximum != scrollMax)
                ScrollBar.Maximum = scrollMax;
            if (ScrollBar.Value != ScrollOffset)
                ScrollBar.Value = ScrollOffset;

            base.OnPaint(e);
        }

        protected int? IndexFromPoint (Point pt) {
            return null;
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var index = IndexFromPoint(e.Location);
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var index = IndexFromPoint(e.Location);
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel (MouseEventArgs e) {
            var delta = (int)Math.Ceiling(
                (e.Delta / (float)SystemInformation.MouseWheelScrollDelta)
                * SystemInformation.MouseWheelScrollLines
            );
            ScrollOffset -= delta;

            base.OnMouseWheel(e);
        }

        protected override void OnPreviewKeyDown (PreviewKeyDownEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Down:
                    ScrollOffset += 1;
                break;
                case Keys.End:
                    ScrollOffset = ContentHeight - 1;
                break;
                case Keys.Home:
                    ScrollOffset = 0;
                break;
                case Keys.PageDown:
                    ScrollOffset += ScrollBar.LargeChange;
                break;
                case Keys.PageUp:
                    ScrollOffset -= ScrollBar.LargeChange;
                break;
                case Keys.Up:
                    ScrollOffset -= 1;
                break;

                default:
                    base.OnPreviewKeyDown(e);
                break;
            }
        }

        protected override void OnKeyDown (KeyEventArgs e) {
            if (IsInputKey(e.KeyCode)) {
                e.Handled = e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);

        }

        protected override void OnKeyUp (KeyEventArgs e) {
            if (IsInputKey(e.KeyCode)) {
                e.Handled = e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyUp(e);
        }

        protected override bool IsInputKey (Keys keyData) {
            switch (keyData) {
                case Keys.Down:
                case Keys.End:
                case Keys.Home:
                case Keys.PageDown:
                case Keys.PageUp:
                case Keys.Space:
                case Keys.Up:
                return true;
            }

            return false;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int ScrollOffset {
            get {
                return _ScrollOffset;
            }
            set {
                var contentHeight = ContentHeight;
                if (value >= contentHeight)
                    value = contentHeight - 1;
                if (value < 0)
                    value = 0;

                if (value != _ScrollOffset) {
                    _ScrollOffset = value;
                    if (ScrollBar.Value != value)
                        ScrollBar.Value = value;

                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int BytesPerRow {
            get {
                return (ClientSize.Width - ScrollBar.Width - (MarginWidth * 2)) * BytesPerPixel;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int ContentHeight {
            get {
                long bytes = 0;
                foreach (var heapId in Snapshot.Heaps.Keys) {
                    var heap = Snapshot.Heaps[heapId];
                    bytes += heap.EstimatedSize;
                }

                return (int)((bytes / BytesPerRow) + Snapshot.Heaps.Count);
            }
        }
    }
}
