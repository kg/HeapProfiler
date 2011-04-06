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

using TItem = HeapProfiler.DeltaInfo;
using System.Globalization;

namespace HeapProfiler {
    public class DeltaHistogram : UserControl, ITooltipOwner {
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

        public const int MaxTooltipWidthPercent = 50;
        public const int MaxTooltipHeightPercent = 60;
        public const float MinTooltipSizeEm = 7.5f;

        public const int ItemWidth = 32;

        public IList<TItem> Items = new List<TItem>();
        public int? TotalDelta = null;
        public int Maximum = 1024;

        public string FunctionFilter = null;

        protected readonly LRUCache<long, string> FormattedSizeCache = new LRUCache<long, string>(128);
        protected readonly LRUCache<TItem, ItemData> Data = new LRUCache<TItem, ItemData>(256, new ReferenceComparer<TItem>());
        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected bool ShouldAutoscroll = false;

        protected CustomTooltip Tooltip = null;
        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected ScrollBar ScrollBar = null;

        protected int _HoverIndex = -1;
        protected int _SelectedIndex = -1;
        protected int _ScrollOffset = 0;

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
            Scratch.Dispose();

            base.Dispose(disposing);
        }

        void ScrollBar_Scroll (object sender, ScrollEventArgs e) {
            ScrollOffset = e.NewValue;
        }

        protected override void OnResize (EventArgs e) {
            var preferredSize = ScrollBar.GetPreferredSize(ClientSize);
            ScrollBar.SetBounds(0, ClientSize.Height - preferredSize.Height, ClientSize.Width, preferredSize.Height);

            HideTooltip();

            base.OnResize(e);
        }

        protected Color SelectItemColor (ref TItem item) {
            var id = BitConverter.ToInt32(BitConverter.GetBytes(item.Traceback.TraceId), 0);

            int hue = (id & 0xFFFF) % (HSV.HueMax);
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 70 / 100)) + (HSV.ValueMax * 25 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaintBackground (PaintEventArgs e) {            
        }

        protected float GraphLog (int value) {
            const double scale = 2.0;
            double result = (Math.Log(Math.Abs(value) + 1) / Math.Log(scale)) * Math.Sign(value);
            return (float)result;
        }

        protected RectangleF ComputeBarRectangle (int bytesDelta, float x, float centerY, float height, float max) {
            float y1, y2;

            var value = GraphLog(bytesDelta);
            if (value >= 0) {
                y2 = centerY;
                y1 = y2 - (value / (float)max) * ((height / 2.0f) - 1);
            } else {
                y1 = centerY;
                y2 = y1 + (-value / (float)max) * ((height / 2.0f) - 1);
            }

            return new RectangleF(
                x + 2.5f, y1 + 0.5f, ItemWidth - 6f, (y2 - y1) - 1f
            );
        }

        protected override void OnPaint (PaintEventArgs e) {
            bool retrying = false, selectedItemVisible = false;
            int minVisibleIndex = int.MaxValue,  maxVisibleIndex = -int.MaxValue;

            int height = ClientSize.Height - ScrollBar.Height;
            float centerY = height / 2.0f;

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

            using (var sf = CustomTooltip.GetDefaultStringFormat())
            using (var gridLineFont = new Font(Font.FontFamily, Font.Size * 0.85f, Font.Style))
            using (var outlinePen = new Pen(Color.Black))
            using (var activeOutlinePen = new Pen(SystemColors.HighlightText))
            using (var gridPen = new Pen(Color.FromArgb(96, 0, 0, 0)))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var textBrush = new SolidBrush(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight)) {
                int marginWidth = 0;
                for (int i = Maximum; i >= Math.Min(Maximum, 16); i /= 4) {
                    marginWidth = Math.Max(marginWidth,
                        (int)Math.Ceiling(e.Graphics.MeasureString(
                            "+" + FormatSize(i), gridLineFont, ClientSize.Width, sf
                        ).Width) + 1
                    );
                }

                var rgn = new Rectangle(0, 0, marginWidth, height);

                if (rgn.IntersectsWith(e.ClipRectangle))
                using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                    var g = scratch.Graphics;
                    g.Clear(BackColor);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawLine(gridPen, 0, centerY, marginWidth, centerY);

                    for (int i = Maximum; i >= Math.Min(Maximum, 16); i /= 4) {
                        float y = (GraphLog(i) / max) * (height / 2.0f);

                        var formatted = FormatSize(i);

                        g.DrawLine(gridPen, 0, centerY - y, marginWidth, centerY - y);
                        var text = String.Format("+{0}", formatted);
                        g.DrawString(text, gridLineFont, textBrush, new PointF(0, centerY - y));

                        g.DrawLine(gridPen, 0, centerY + y, marginWidth, centerY + y);
                        text = String.Format("-{0}", formatted);
                        var sz = g.MeasureString(text, gridLineFont, marginWidth, sf);
                        g.DrawString(text, gridLineFont, textBrush, new PointF(0, centerY + y - sz.Height));
                    }
                }

                int x = marginWidth;

                rgn = new Rectangle(x, 0, ItemWidth, height);
                if ((TotalDelta.GetValueOrDefault(0) != 0) && rgn.IntersectsWith(e.ClipRectangle))
                using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                    var barRectangle = ComputeBarRectangle(
                        TotalDelta.Value, rgn.X, centerY, height, max
                    );

                    var g = scratch.Graphics;

                    g.ResetClip();
                    g.Clear(BackColor);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    g.DrawLine(gridPen, rgn.X, centerY, rgn.Right, centerY);

                    g.DrawRectangle(
                        outlinePen, barRectangle.X, barRectangle.Y, barRectangle.Width, barRectangle.Height
                    );

                    var oldTransform = g.Transform;
                    var oldAlignment = sf.LineAlignment;
                    var oldRenderingHint = g.TextRenderingHint;

                    try {
                        g.ResetTransform();
                        g.RotateTransform(90);
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        sf.LineAlignment = StringAlignment.Far;
                        g.DrawString("Delta bytes", Font, textBrush, Math.Min(barRectangle.Y, barRectangle.Bottom), 0f, sf);
                    } finally {
                        sf.LineAlignment = oldAlignment;
                        g.Transform = oldTransform;
                        g.TextRenderingHint = oldRenderingHint;
                    }
                }

                if ((TotalDelta.GetValueOrDefault(0) != 0))
                    x += ItemWidth;

                for (int i = _ScrollOffset; (i < Items.Count) && (x < ClientSize.Width); i++) {
                    var selected = (i == SelectedIndex);

                    var item = Items[i];
                    GetItemData(i, out data);

                    rgn = new Rectangle(x, 0, ItemWidth, height);

                    var barRectangle = ComputeBarRectangle(
                        item.BytesDelta * (item.Added ? 1 : -1), 
                        rgn.X, centerY, height, max
                    );

                    if (rgn.IntersectsWith(e.ClipRectangle))
                    using (var itemBrush = new SolidBrush(SelectItemColor(ref item)))
                    using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                        var g = scratch.Graphics;

                        g.ResetClip();
                        g.Clear(BackColor);

                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        g.DrawLine(gridPen, rgn.X, centerY, rgn.Right, centerY); 

                        g.FillRectangle(
                            (_HoverIndex == i) ? highlightBrush : itemBrush, 
                            barRectangle
                        );

                        g.DrawRectangle(
                            (_HoverIndex == i) ? activeOutlinePen : outlinePen,
                            barRectangle.X - 0.5f, barRectangle.Y - 0.5f,
                            barRectangle.Width + 1f, barRectangle.Height + 1f
                        );
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

                    x += ItemWidth;

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

            int largeChange = Math.Max(4, ClientSize.Width / ItemWidth);
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

        protected int? IndexFromPoint (Point pt) {
            foreach (var vi in VisibleItems)
                if (vi.Rectangle.Contains(pt))
                    return vi.Index;

            return null;
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

        protected void ShowTooltip (int itemIndex, Point location) {
            if (Tooltip == null)
                Tooltip = new CustomTooltip(this);

            var item = Items[itemIndex];
            var sf = CustomTooltip.GetDefaultStringFormat();

            float lineHeight = 0;
            var fontSize = Font.Size;

            using (var g = CreateGraphics()) {
                var screen = Screen.FromPoint(Cursor.Position);
                var screenBounds = screen.Bounds;

                var content = new DeltaInfoTooltipContent(
                    item, new DeltaInfo.RenderParams {
                        BackgroundBrush = new SolidBrush(SystemColors.Info),
                        BackgroundColor = SystemColors.Info,
                        TextBrush = new SolidBrush(SystemColors.InfoText),
                        IsExpanded = true,
                        IsSelected = false,
                        ElideBackgroundBrush = null,
                        ElideTextBrush = null,
                        FunctionHighlightBrush = new SolidBrush(SystemColors.Window),
                        FunctionFilter = FunctionFilter,
                        Font = Font,
                        ShadeBrush = new SolidBrush(Color.FromArgb(31, 0, 0, 0)),
                        StringFormat = sf
                    }
                );
                
                // Iterate a few times to shrink the tooltip's font size if it's too big
                for (int i = 0; i < 10; i++) {
                    var size = content.Measure(g);

                    fontSize *= 0.9f;
                    if (fontSize < MinTooltipSizeEm)
                        fontSize = MinTooltipSizeEm;

                    content.RenderParams.Font = new Font(
                        content.RenderParams.Font.FontFamily, 
                        Math.Min(fontSize, MinTooltipSizeEm),
                        content.RenderParams.Font.Style
                    );
                    content.RenderParams.Region = new Rectangle(
                        0, 0, size.Width, size.Height
                    );

                    if (fontSize <= MinTooltipSizeEm)
                        break;
                    if (size.Width < (screenBounds.Width * MaxTooltipWidthPercent / 100) &&
                        size.Height < (screenBounds.Height * MaxTooltipHeightPercent / 100))
                        break;
                }

                bool wasVisible = Tooltip.Visible;
                Tooltip.Visible = false;

                Tooltip.Content = content;

                MoveTooltip(screen, this.PointToScreen(location), content.RenderParams.Region);

                Tooltip.Refresh();

                if (!wasVisible)
                    Tooltip.Show(this);
                else
                    Tooltip.Visible = true;
            }

            if (_HoverIndex != itemIndex) {
                var oldIndex = _HoverIndex;
                _HoverIndex = itemIndex;
                InvalidateItem(oldIndex);
                InvalidateItem(_HoverIndex);
            }
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

        protected void MoveTooltip (Screen screen, Point location, Rectangle rgn) {
            int x = location.X + 4, y = location.Y + 24;

            var screenBounds = screen.WorkingArea;
            var maxWidth = (screenBounds.Width * MaxTooltipWidthPercent / 100);
            var maxHeight = (screenBounds.Height * MaxTooltipHeightPercent / 100);

            if (rgn.Width > maxWidth)
                rgn.Width = maxWidth;
            if (rgn.Height > maxHeight)
                rgn.Height = maxHeight;

            if ((x + rgn.Width) >= screenBounds.Right)
                x = (screenBounds.Right - rgn.Width - 1);
            if ((y + rgn.Height) >= screenBounds.Bottom)
                y = (screenBounds.Bottom - rgn.Height - 1);

            if ((Tooltip.Left != x) || 
                (Tooltip.Top != y) || 
                (Tooltip.Width != rgn.Width) || 
                (Tooltip.Height != rgn.Height))
                Tooltip.SetBounds(x, y, rgn.Width, rgn.Height);
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
}
