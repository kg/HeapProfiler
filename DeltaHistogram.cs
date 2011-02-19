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

using TItem = HeapProfiler.DiffViewer.DeltaInfo;
using System.Globalization;

namespace HeapProfiler {
    public partial class DeltaHistogram : UserControl {
        protected struct ScratchRegion : IDisposable {
            public readonly Graphics Graphics;
            public readonly Bitmap Bitmap;

            public readonly Graphics DestinationGraphics;
            public readonly Rectangle DestinationRegion;

            bool Cancelled;

            public ScratchRegion (DeltaHistogram histogram, Graphics graphics, Rectangle region) {
                var minWidth = (int)Math.Ceiling(region.Width / 16.0f) * 16;
                var minHeight = (int)Math.Ceiling(region.Height / 16.0f) * 16;

                var needNewBitmap =
                    (histogram._ScratchBuffer == null) || (histogram._ScratchBuffer.Width < minWidth) ||
                    (histogram._ScratchBuffer.Height < minHeight);

                if (needNewBitmap && histogram._ScratchBuffer != null)
                    histogram._ScratchBuffer.Dispose();
                if (needNewBitmap && histogram._ScratchGraphics != null)
                    histogram._ScratchGraphics.Dispose();

                if (needNewBitmap) {
                    Bitmap = histogram._ScratchBuffer = new Bitmap(
                        minWidth, minHeight, graphics
                    );
                    Graphics = histogram._ScratchGraphics = Graphics.FromImage(Bitmap);
                } else {
                    Bitmap = histogram._ScratchBuffer;
                    Graphics = histogram._ScratchGraphics;
                }

                Graphics.ResetTransform();
                Graphics.TranslateTransform(-region.X, -region.Y, System.Drawing.Drawing2D.MatrixOrder.Prepend);
                Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                DestinationGraphics = graphics;
                DestinationRegion = region;

                Cancelled = false;
            }

            public void Cancel () {
                Cancelled = true;
            }

            public void Dispose () {
                if (Cancelled)
                    return;

                var oldCompositing = DestinationGraphics.CompositingMode;
                DestinationGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                var oldInterpolation = DestinationGraphics.InterpolationMode;
                DestinationGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                var oldSmoothing = DestinationGraphics.SmoothingMode;
                DestinationGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                DestinationGraphics.DrawImageUnscaledAndClipped(
                    Bitmap, DestinationRegion
                );

                DestinationGraphics.CompositingMode = oldCompositing;
                DestinationGraphics.InterpolationMode = oldInterpolation;
                DestinationGraphics.SmoothingMode = oldSmoothing;

                Cancelled = true;
            }
        }

        public struct ItemData {
        }

        public struct VisibleItem {
            public RectangleF Rectangle;
            public int Index;
        }

        public IList<TItem> Items = new List<TItem>();
        public int Maximum = 1024;

        protected readonly LRUCache<TItem, ItemData> Data = new LRUCache<TItem, ItemData>(256, new ReferenceComparer<TItem>());
        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected bool ShouldAutoscroll = false;

        protected ScrollBar ScrollBar = null;

        protected int _SelectedIndex = -1;
        protected int _ScrollOffset = 0;

        protected Bitmap _ScratchBuffer = null;
        protected Graphics _ScratchGraphics = null;

        public DeltaHistogram () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;

            ScrollBar = new HScrollBar {
                SmallChange = 1,
                LargeChange = 8,
                TabStop = false
            };

            ScrollBar.Scroll += new ScrollEventHandler(ScrollBar_Scroll);
            OnResize(EventArgs.Empty);

            Controls.Add(ScrollBar);
        }

        protected override void Dispose (bool disposing) {
            DisposeGraphics();

            base.Dispose(disposing);
        }

        void DisposeGraphics () {
            if (_ScratchGraphics != null) {
                _ScratchGraphics.Dispose();
                _ScratchGraphics = null;
            }

            if (_ScratchBuffer != null) {
                _ScratchBuffer.Dispose();
                _ScratchBuffer = null;
            }
        }

        protected ScratchRegion GetScratch (Graphics graphics, Rectangle region) {
            return new ScratchRegion(
                this, graphics, region
            );
        }

        protected ScratchRegion GetScratch (Graphics graphics, RectangleF region) {
            return new ScratchRegion(
                this, graphics, new Rectangle(
                    (int)Math.Floor(region.X), (int)Math.Floor(region.Y),
                    (int)Math.Ceiling(region.Width), (int)Math.Ceiling(region.Height)
                )
            );
        }

        void ScrollBar_Scroll (object sender, ScrollEventArgs e) {
            ScrollOffset = e.NewValue;
        }

        protected override void OnResize (EventArgs e) {
            var preferredSize = ScrollBar.GetPreferredSize(ClientSize);
            ScrollBar.SetBounds(0, ClientSize.Height - preferredSize.Height, ClientSize.Width, preferredSize.Height);

            base.OnResize(e);
        }

        protected Color SelectItemColor (ref TItem item) {
            int id;
            if (!int.TryParse(
                    item.Traceback.TraceId, NumberStyles.HexNumber, 
                    CultureInfo.InvariantCulture.NumberFormat, out id
                ))
                id = item.Traceback.TraceId.GetHashCode();

            int hue = (id & 0xFFFF) % (HSV.HueMax);
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 70 / 100)) + (HSV.ValueMax * 25 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaintBackground (PaintEventArgs e) {            
        }

        protected float GraphLog (int value) {
            double scale = 2.0;
            double result = (Math.Log(Math.Abs(value) + 1) / Math.Log(scale)) * Math.Sign(value);
            return (float)result;
        }

        protected override void OnPaint (PaintEventArgs e) {
            char[] functionEndChars = new char[] { '@', '+' };
            bool retrying = false, selectedItemVisible = false;
            int minVisibleIndex = int.MaxValue,  maxVisibleIndex = -int.MaxValue;

            int height = ClientSize.Height - ScrollBar.Height;
            float centerY = height / 2;
            int itemWidth = 40;

            float max = GraphLog(Maximum);

        retryFromHere:

            if (_SelectedIndex >= Items.Count)
                _SelectedIndex = Items.Count - 1;
            if (_SelectedIndex < 0)
                _SelectedIndex = 0;

            if (_ScrollOffset >= Items.Count)
                _ScrollOffset = Items.Count - 1;
            if (_ScrollOffset < 0)
                _ScrollOffset = 0;

            VisibleItems.Clear();

            ItemData data;

            using (var sf = new StringFormat {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.NoWrap |
                    StringFormatFlags.DisplayFormatControl | StringFormatFlags.MeasureTrailingSpaces,
                HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None,
                Trimming = StringTrimming.None
            })
            using (var gridLineFont = new Font(Font.FontFamily, Font.Size * 0.85f, Font.Style))
            using (var outlinePen = new Pen(Color.Black))
            using (var gridPen = new Pen(Color.FromArgb(96, 0, 0, 0)))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var textBrush = new SolidBrush(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight))
            using (var highlightTextBrush = new SolidBrush(SystemColors.HighlightText)) {
                var marginWidth = (int)Math.Ceiling(e.Graphics.MeasureString(
                    Maximum.ToString(), Font, ClientSize.Width, sf
                ).Width);

                using (var scratch = GetScratch(e.Graphics, new Rectangle(0, 0, marginWidth, height))) {
                    var g = scratch.Graphics;
                    g.Clear(BackColor);

                    var format1 = sf.Clone();

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawLine(gridPen, 0, centerY, marginWidth, centerY);

                    for (int i = Maximum; i >= 16; i /= 4) {
                        float y = (GraphLog(i) / max) * (height / 2.0f);

                        g.DrawLine(gridPen, 0, centerY - y, marginWidth, centerY - y);
                        var text = String.Format("+{0}", i);
                        g.DrawString(text, gridLineFont, textBrush, new PointF(0, centerY - y));

                        g.DrawLine(gridPen, 0, centerY + y, marginWidth, centerY + y);
                        text = String.Format("-{0}", i);
                        var sz = g.MeasureString(text, gridLineFont, marginWidth, sf);
                        g.DrawString(text, gridLineFont, textBrush, new PointF(0, centerY + y - sz.Height));
                    }
                }

                int x = marginWidth;
                for (int i = _ScrollOffset; (i < Items.Count) && (x < ClientSize.Width); i++) {
                    var x1 = x;
                    var selected = (i == SelectedIndex);

                    var item = Items[i];
                    GetItemData(i, out data);

                    var rgn = new Rectangle(x, 0, itemWidth, height);

                    using (var itemBrush = new SolidBrush(SelectItemColor(ref item)))
                    using (var scratch = GetScratch(e.Graphics, rgn)) {
                        var g = scratch.Graphics;

                        g.ResetClip();
                        g.Clear(BackColor);

                        var brush = selected ? highlightBrush : itemBrush;

                        float y1, y2;

                        var value = GraphLog(item.BytesDelta);
                        if (item.Added) {
                            y2 = centerY;
                            y1 = y2 - (value / (float)max) * (height / 2.0f);
                        } else {
                            y1 = centerY;
                            y2 = y1 + (value / (float)max) * (height / 2.0f);
                        }

                        var barRectangle = new RectangleF(
                            rgn.X + 2.5f, y1 + 0.5f, itemWidth - 6f, (y2 - y1) - 1f
                        );

                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        g.DrawLine(gridPen, rgn.X, centerY, rgn.Right, centerY); 

                        g.FillRectangle(itemBrush, barRectangle);

                        g.DrawRectangle(
                            outlinePen, barRectangle.X - 0.5f, barRectangle.Y - 0.5f,
                            barRectangle.Width + 1f, barRectangle.Height + 1f
                        );

                        x += itemWidth;
                    }

                    VisibleItems.Add(new VisibleItem {
                        Rectangle = rgn, Index = i
                    });

                    if ((rgn.X >= 0) && (rgn.Right < ClientSize.Width)) {
                        minVisibleIndex = Math.Min(minVisibleIndex, i);
                        maxVisibleIndex = Math.Max(maxVisibleIndex, i);
                        selectedItemVisible |= selected;
                    }
                }

                if (x < ClientSize.Width) {
                    e.Graphics.FillRectangle(backgroundBrush, new Rectangle(x, 0, ClientSize.Width - x, ClientSize.Height));

                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    e.Graphics.DrawLine(gridPen, x, centerY, ClientSize.Width, centerY);
                }
            }

            if (!selectedItemVisible && !retrying && ShouldAutoscroll) {
                if (_SelectedIndex > maxVisibleIndex)
                    _ScrollOffset += _SelectedIndex - maxVisibleIndex;
                else if (_SelectedIndex < minVisibleIndex)
                    _ScrollOffset -= minVisibleIndex - _SelectedIndex;

                if (_ScrollOffset >= Items.Count)
                    _ScrollOffset = Items.Count - 1;
                if (_ScrollOffset < 0)
                    _ScrollOffset = 0;

                retrying = true;
                goto retryFromHere;
            }

            int largeChange = Math.Max(4, ClientSize.Width / itemWidth);
            if (ScrollBar.LargeChange != largeChange)
                ScrollBar.LargeChange = largeChange;

            int scrollMax = Math.Max(1, Items.Count - 1) + largeChange - 1;
            if (ScrollBar.Maximum != scrollMax)
                ScrollBar.Maximum = scrollMax;
            if (ScrollBar.Value != ScrollOffset)
                ScrollBar.Value = ScrollOffset;

            ShouldAutoscroll = false;
            base.OnPaint(e);
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                foreach (var vi in VisibleItems) {
                    if (vi.Rectangle.Contains(e.Location)) {
                        SelectedIndex = vi.Index;
                        break;
                    }
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                foreach (var vi in VisibleItems) {
                    if (vi.Rectangle.Contains(e.Location)) {
                        SelectedIndex = vi.Index;
                        break;
                    }
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel (MouseEventArgs e) {
            ScrollOffset -= (e.Delta / SystemInformation.MouseWheelScrollDelta) * SystemInformation.MouseWheelScrollLines;

            base.OnMouseWheel(e);
        }

        protected override void OnPreviewKeyDown (PreviewKeyDownEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Left:
                    SelectedIndex -= 1;
                break;
                case Keys.Right:
                    SelectedIndex += 1;
                break;
                case Keys.End:
                    SelectedIndex = Items.Count - 1;
                break;
                case Keys.Home:
                    SelectedIndex = 0;
                break;
                case Keys.PageDown:
                    SelectedIndex += ScrollBar.LargeChange;
                break;
                case Keys.PageUp:
                    SelectedIndex -= ScrollBar.LargeChange;
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
                case Keys.Left:
                case Keys.Right:
                case Keys.End:
                case Keys.Home:
                case Keys.PageDown:
                case Keys.PageUp:
                case Keys.Space:
                    return true;
            }

            return false;
        }

        protected void GetItemData (int index, out ItemData result) {
            if (!Data.TryGetValue(Items[index], out result))
                result = new ItemData();
        }

        void SetItemData (int index, ref ItemData newData) {
            Data[Items[index]] = newData;
        }

        public int SelectedIndex {
            get {
                return _SelectedIndex;
            }
            set {
                if (value >= Items.Count)
                    value = Items.Count - 1;
                if (value < 0)
                    value = 0;

                if (value != _SelectedIndex) {
                    _SelectedIndex = value;
                    ShouldAutoscroll = true;

                    Invalidate();
                }
            }
        }

        public int ScrollOffset {
            get {
                return _ScrollOffset;
            }
            set {
                if (value >= Items.Count)
                    value = Items.Count - 1;
                if (value < 0)
                    value = 0;

                if (value != _ScrollOffset) {
                    _ScrollOffset = value;
                    if (ScrollBar.Value != value)
                        ScrollBar.Value = value;

                    ShouldAutoscroll = false;

                    Invalidate();
                }
            }
        }
    }
}
