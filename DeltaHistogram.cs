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
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Squared.Data.Mangler;
using Squared.Util;

namespace HeapProfiler {
    public class GenericHistogram<TItem> : UserControl, ITooltipOwner
        where TItem : class {
        public struct ItemData {
        }

        public struct VisibleItem {
            public RectangleF Rectangle;
            public int Index;
        }

        public class VisibleItemComparer : IComparer<VisibleItem> {
            public int Compare (VisibleItem x, VisibleItem y) {
                return x.Index.CompareTo(y.Index);
            }
        }

        public const int ItemHeight = 32;

        public Func<TItem, long> GetItemValue;
        public Func<TItem, string> GetItemText;
        public Func<TItem, TooltipContentBase> GetItemTooltip;

        public IList<TItem> Items = new List<TItem>();
        public int? TotalDelta = null;
        public int Maximum = 1024;

        public Regex FunctionFilter = null;

        protected readonly LRUCache<long, string> FormattedSizeCache = new LRUCache<long, string>(128);
        protected readonly LRUCache<TItem, ItemData> Data = new LRUCache<TItem, ItemData>(256, new ReferenceComparer<TItem>());
        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected bool ShouldAutoscroll = false;

        protected OutlinedTextCache TextCache = new OutlinedTextCache();
        protected CustomTooltip Tooltip = null;
        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected ScrollBar ScrollBar = null;

        protected int _HoverIndex = -1;
        protected int _SelectedIndex = -1;
        protected int _ScrollOffset = 0;

        public GenericHistogram () {
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

            ScrollBar.Scroll += ScrollBar_Scroll;
            OnResize(EventArgs.Empty);

            Controls.Add(ScrollBar);
        }

        protected string FormatSize (long sizeBytes) {
            string result;
            if (!FormattedSizeCache.TryGetValue(sizeBytes, out result))
                FormattedSizeCache[sizeBytes] = result = FileSize.Format(sizeBytes);

            return result;
        }

        protected override void Dispose (bool disposing) {
            TextCache.Dispose();
            Scratch.Dispose();

            base.Dispose(disposing);
        }

        void ScrollBar_Scroll (object sender, ScrollEventArgs e) {
            ScrollOffset = e.NewValue;
        }

        protected override void OnResize (EventArgs e) {
            var preferredSize = ScrollBar.GetPreferredSize(ClientSize);
            ScrollBar.SetBounds(ClientSize.Width - preferredSize.Width, 0, preferredSize.Width, ClientSize.Height);

            HideTooltip();

            base.OnResize(e);
        }

        protected Color SelectItemColor (TItem item) {
            var hashBytes = ImmutableBufferPool.GetBytes(item.GetHashCode());
            var id = BitConverter.ToInt32(hashBytes.Array, hashBytes.Offset);

            int hue = (id & 0xFFFF) % (HSV.HueMax);
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 70 / 100)) + (HSV.ValueMax * 25 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaintBackground (PaintEventArgs e) {            
        }

        protected float GraphLog (long value) {
            const double scale = 2.0;
            double result = (Math.Log(Math.Abs(value) + 1) / Math.Log(scale)) * Math.Sign(value);
            return (float)result;
        }

        protected RectangleF ComputeBarRectangle (long bytesDelta, float centerX, float y, float width, float max) {
            float x1, x2;

            var value = GraphLog(bytesDelta);
            if (value >= 0) {
                x1 = centerX;
                x2 = x1 + (value / (float)max) * ((width / 2.0f) - 1);
            } else {
                x2 = centerX;
                x1 = x2 - (-value / (float)max) * ((width / 2.0f) - 1);
            }

            return new RectangleF(
                x1 + 0.5f, y + 2.5f, (x2 - x1) - 1f, ItemHeight - 6f
            );
        }

        protected override void OnPaint (PaintEventArgs e) {
            bool retrying = false, selectedItemVisible = false;
            int minVisibleIndex = int.MaxValue,  maxVisibleIndex = -int.MaxValue;

            int width = ClientSize.Width - ScrollBar.Width;
            float centerX = width / 2.0f;

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
            HashSet<string> textToFlush = new HashSet<string>(TextCache.Keys);

            using (var sf = CustomTooltip.GetDefaultStringFormat())
            using (var gridLineFont = new Font(Font.FontFamily, Font.Size * 0.85f, Font.Style))
            using (var outlinePen = new Pen(Color.Black))
            using (var activeOutlinePen = new Pen(SystemColors.HighlightText))
            using (var gridPen = new Pen(Color.FromArgb(96, 0, 0, 0)))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var whiteOutlinePen = new Pen(Color.White, 2.5f))
            using (var blackOutlinePen = new Pen(Color.Black, 2.5f))
            using (var textBrush = new SolidBrush(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight)) {
                blackOutlinePen.LineJoin = whiteOutlinePen.LineJoin = LineJoin.Round;

                int marginHeight = 0;
                for (int i = Maximum; i >= Math.Min(Maximum, 16); i /= 4) {
                    marginHeight = Math.Max(marginHeight,
                        (int)Math.Ceiling(e.Graphics.MeasureString(
                            "+" + FormatSize(i), gridLineFont, ClientSize.Height, sf
                        ).Width) + 1
                    );
                }

                var rgn = new Rectangle(0, 0, width, marginHeight);

                if (rgn.IntersectsWith(e.ClipRectangle))
                using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                    var g = scratch.Graphics;
                    g.Clear(BackColor);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawLine(gridPen, centerX, 0, centerX, marginHeight);

                    for (int i = Maximum; i >= Math.Min(Maximum, 16); i /= 4) {
                        float x = (GraphLog(i) / max) * (width / 2.0f);

                        var formatted = FormatSize(i);

                        g.DrawLine(gridPen, centerX + x, 0, centerX + x, marginHeight);
                        var text = String.Format("+{0}", formatted);
                        textToFlush.Remove(text);
                        var bitmap = TextCache.Get(g, text, gridLineFont, RotateFlipType.Rotate90FlipNone, ForeColor, BackColor, sf, new SizeF(marginHeight, 999));
                        g.DrawImageUnscaled(bitmap, (int)(centerX + x), 0);

                        g.DrawLine(gridPen, centerX - x, 0, centerX - x, marginHeight);
                        text = String.Format("-{0}", formatted);
                        textToFlush.Remove(text);
                        bitmap = TextCache.Get(g, text, gridLineFont, RotateFlipType.Rotate90FlipNone, ForeColor, BackColor, sf, new SizeF(marginHeight, 999));
                        g.DrawImageUnscaled(bitmap, (int)(centerX - x - bitmap.Width), 0);
                    }
                }

                int y = marginHeight;

                rgn = new Rectangle(0, y, width, ItemHeight);

                if ((TotalDelta.GetValueOrDefault(0) != 0) && rgn.IntersectsWith(e.ClipRectangle))
                using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                    var barRectangle = ComputeBarRectangle(
                        TotalDelta.Value, centerX, rgn.Y, width, max
                    );

                    var g = scratch.Graphics;

                    g.ResetClip();
                    g.Clear(BackColor);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    g.DrawLine(gridPen, centerX, rgn.Y, centerX, rgn.Bottom);

                    g.DrawRectangle(
                        outlinePen, barRectangle.X, barRectangle.Y, barRectangle.Width, barRectangle.Height
                    );

                    var oldRenderingHint = g.TextRenderingHint;

                    try {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        g.DrawString("Delta bytes", Font, textBrush, barRectangle.X, barRectangle.Y, sf);
                    } finally {
                        g.TextRenderingHint = oldRenderingHint;
                    }
                }

                if ((TotalDelta.GetValueOrDefault(0) != 0))
                    y += ItemHeight;

                for (int i = _ScrollOffset; (i < Items.Count) && (y < ClientSize.Height); i++) {
                    var selected = (i == SelectedIndex);

                    var item = Items[i];
                    GetItemData(i, out data);

                    rgn = new Rectangle(0, y, width, ItemHeight);
                    var bytesDelta = GetItemValue(item);

                    var barRectangle = ComputeBarRectangle(
                        bytesDelta, centerX, rgn.Y, width, max
                    );

                    var itemColor = SelectItemColor(item);

                    if (rgn.IntersectsWith(e.ClipRectangle))
                    using (var itemBrush = new SolidBrush(itemColor))
                    using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                        var g = scratch.Graphics;

                        g.ResetClip();
                        g.Clear(BackColor);

                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        g.DrawLine(gridPen, centerX, rgn.Y, centerX, rgn.Bottom);

                        g.FillRectangle(
                            (_HoverIndex == i) ? highlightBrush : itemBrush, 
                            barRectangle
                        );

                        g.DrawRectangle(
                            (_HoverIndex == i) ? activeOutlinePen : outlinePen,
                            barRectangle.X - 0.5f, barRectangle.Y - 0.5f,
                            barRectangle.Width + 1f, barRectangle.Height + 1f
                        );

                        string itemText = null;

                        if ((GetItemText != null) && (itemText = GetItemText(item)) != null) {
                            bool white = (itemColor.GetBrightness() <= 0.25f);

                            textToFlush.Remove(itemText);
                            var bitmap = TextCache.Get(
                                g, itemText, Font, RotateFlipType.RotateNoneFlipNone,
                                white ? Color.White : Color.Black,
                                white ? Color.Black : Color.LightGray, sf,
                                new SizeF(barRectangle.Width, barRectangle.Height)
                            );

                            g.DrawImageUnscaled(
                                bitmap,
                                (int)Math.Floor(barRectangle.X), 
                                (int)Math.Floor(barRectangle.Y)
                            );
                        }
                    }

                    VisibleItems.Add(new VisibleItem {
                        Rectangle = new Rectangle(
                            (int)Math.Floor(barRectangle.X),
                            (int)Math.Floor(barRectangle.Y),
                            (int)Math.Ceiling(barRectangle.Width),
                            (int)Math.Ceiling(barRectangle.Height)
                        ),
                        Index = i
                    });

                    y += ItemHeight;

                    if ((rgn.X >= 0) && (rgn.Right < ClientSize.Width)) {
                        minVisibleIndex = Math.Min(minVisibleIndex, i);
                        maxVisibleIndex = Math.Max(maxVisibleIndex, i);
                        selectedItemVisible |= selected;
                    }
                }

                if (y < ClientSize.Height) {
                    e.Graphics.FillRectangle(backgroundBrush, new Rectangle(0, y, ClientSize.Width, ClientSize.Height - y));

                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    e.Graphics.DrawLine(gridPen, centerX, y, centerX, ClientSize.Height);
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

            int largeChange = Math.Max(4, ClientSize.Width / ItemHeight);
            if (ScrollBar.LargeChange != largeChange)
                ScrollBar.LargeChange = largeChange;

            int scrollMax = Math.Max(1, Items.Count - 1) + largeChange - 1;
            if (ScrollBar.Maximum != scrollMax)
                ScrollBar.Maximum = scrollMax;
            if (ScrollBar.Value != ScrollOffset)
                ScrollBar.Value = ScrollOffset;

            TextCache.Flush(textToFlush);

            ShouldAutoscroll = false;
            base.OnPaint(e);
        }

        protected int? IndexFromPoint (Point pt) {
            foreach (var vi in VisibleItems)
                if (vi.Rectangle.Contains(pt))
                    return vi.Index;

            return null;
        }

        protected override void OnVisibleChanged (EventArgs e) {
            base.OnVisibleChanged(e);

            TextCache.Flush();
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var index = IndexFromPoint(e.Location);
                if (index.HasValue)
                    SelectedIndex = index.Value;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var index = IndexFromPoint(e.Location);
                if (index.HasValue)
                    SelectedIndex = index.Value;
            } else {
                var newIndex = IndexFromPoint(e.Location).GetValueOrDefault(-1);
                if (newIndex != _HoverIndex) {
                    if (newIndex >= 0)
                        ShowTooltip(newIndex, e.Location);
                    else
                        HideTooltip();
                } else if (_HoverIndex >= 0) {
                    // MoveTooltip(e.Location);
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave (EventArgs e) {
            base.OnMouseLeave(e);

            if (Tooltip == null)
                return;

            if (!Tooltip.ClientRectangle.Contains(Tooltip.PointToClient(Cursor.Position)))
                HideTooltip();
        }

        protected void InvalidateItem (int index) {
            if (index < VisibleItems[0].Index)
                return;
            if (index > VisibleItems[VisibleItems.Count - 1].Index)
                return;

            var vi = new VisibleItem { Index = index };
            var visibleIndex = VisibleItems.BinarySearch(vi, new VisibleItemComparer());
            var rectF = VisibleItems[visibleIndex].Rectangle;

            Invalidate(new Rectangle(
                (int)Math.Floor(rectF.X) - 4,
                (int)Math.Floor(rectF.Y) - 4,
                (int)Math.Ceiling(rectF.Width) + 8,
                (int)Math.Ceiling(rectF.Height) + 8
            ));
        }

        protected void ShowTooltip (int itemIndex, Point location) {
            if (Tooltip == null)
                Tooltip = new CustomTooltip(this);

            var item = Items[itemIndex];

            using (var g = CreateGraphics()) {
                var content = GetItemTooltip(item);

                content.Font = Font;
                content.Location = PointToScreen(location);

                CustomTooltip.FitContentOnScreen(
                    g, content,
                    ref content.Font, ref content.Location, ref content.Size
                );

                Tooltip.SetContent(content);
            }

            if (_HoverIndex != itemIndex) {
                var oldIndex = _HoverIndex;
                _HoverIndex = itemIndex;
                InvalidateItem(oldIndex);
                InvalidateItem(_HoverIndex);
            }
        }

        protected void HideTooltip () {
            if ((Tooltip != null) && Tooltip.Visible)
                Tooltip.Hide();

            if (_HoverIndex != -1) {
                var i = _HoverIndex;
                _HoverIndex = -1;
                InvalidateItem(i);
            }
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        void ITooltipOwner.Click (MouseEventArgs e) {
            OnMouseClick(e);
        }

        void ITooltipOwner.MouseDown (MouseEventArgs e) {
            OnMouseDown(e);
        }

        void ITooltipOwner.MouseMove (MouseEventArgs e) {
            OnMouseMove(e);
        }

        void ITooltipOwner.MouseUp (MouseEventArgs e) {
            OnMouseUp(e);
        }
    }

    public class DeltaHistogram : GenericHistogram<DeltaInfo> {
        public DeltaHistogram () {
            base.GetItemValue = (di) => di.BytesDelta;
            base.GetItemText = (di) => di.Traceback.Frames.Array[di.Traceback.Frames.Offset].Function;
            base.GetItemTooltip = (di) => {
                var sf = CustomTooltip.GetDefaultStringFormat();

                var content = new DeltaInfoTooltipContent(
                    di, new DeltaInfo.RenderParams {
                        BackgroundBrush = new SolidBrush(SystemColors.Info),
                        BackgroundColor = SystemColors.Info,
                        TextBrush = new SolidBrush(SystemColors.InfoText),
                        IsExpanded = true,
                        IsSelected = false,
                        FunctionHighlightBrush = new SolidBrush(SystemColors.Highlight),
                        FunctionHighlightTextBrush = new SolidBrush(SystemColors.HighlightText),
                        FunctionFilter = FunctionFilter,
                        ShadeBrush = new SolidBrush(Color.FromArgb(31, 0, 0, 0)),
                        StringFormat = sf
                    }
                );

                return content;
            };
        }
    }

    public class GraphHistogram : GenericHistogram<StackGraphNode> {
        public GraphHistogram () {
            base.GetItemValue = (sgn) => sgn.BytesRequested;
            base.GetItemText = (sgn) => sgn.Key.ToString();
            base.GetItemTooltip = (sgn) => {
                var sf = CustomTooltip.GetDefaultStringFormat();

                TooltipContentBase content = new StackGraphTooltipContent(
                    sgn, sf
                );

                return content;
            };
        }
    }
}
