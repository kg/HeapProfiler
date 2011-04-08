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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Squared.Task;
using Squared.Util;

using TItem = HeapProfiler.DeltaInfo;

namespace HeapProfiler {
    public class DeltaList : UserControl {
        public struct ItemData {
            public bool Expanded;
        }

        public struct VisibleItem {
            public int Y1, Y2;
            public int Index;
        }

        public IList<TItem> Items = new List<TItem>();

        public Regex FunctionFilter = null;

        protected readonly LRUCache<TItem, ItemData> Data = new LRUCache<TItem, ItemData>(256, new ReferenceComparer<TItem>());
        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected int CollapsedSize;
        protected bool ShouldAutoscroll = false;

        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected ScrollBar ScrollBar = null;

        protected int _SelectedIndex = -1;
        protected int _ScrollOffset = 0;

        public DeltaList () {
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

        protected override void OnPaint (PaintEventArgs e) {
            bool retrying = false, selectedItemVisible = false;
            int minVisibleIndex = int.MaxValue,  maxVisibleIndex = int.MinValue;
            var width = ClientSize.Width - ScrollBar.Width;

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
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.NoWrap |
                    StringFormatFlags.DisplayFormatControl | StringFormatFlags.MeasureTrailingSpaces,
                HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None,
                Trimming = StringTrimming.None
            })
            using (var shadeBrush = new SolidBrush(Color.FromArgb(31, 0, 0, 0)))
            using (var elideBackgroundBrush = new SolidBrush(Color.FromArgb(192, SystemColors.Window)))
            using (var elideTextBrush = new SolidBrush(Color.FromArgb(220, SystemColors.WindowText)))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var textBrush = new SolidBrush(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight))
            using (var highlightTextBrush = new SolidBrush(SystemColors.HighlightText)) {
                var lineHeight = e.Graphics.MeasureString("AaBbYyZz", Font, width, sf).Height;
                CollapsedSize = (int)Math.Ceiling(lineHeight * 3);

                var renderParams = new DeltaInfo.RenderParams {
                    Font = Font,
                    FunctionFilter = FunctionFilter, 
                    FunctionHighlightBrush = highlightBrush,
                    FunctionHighlightTextBrush = highlightTextBrush,
                    ElideBackgroundBrush = elideBackgroundBrush,
                    ElideTextBrush = elideTextBrush,
                    ShadeBrush = shadeBrush,
                    StringFormat = sf,
                };

                int y = 0;
                for (int i = _ScrollOffset; (i < Items.Count) && (y < ClientSize.Height); i++) {
                    var y1 = y;
                    var selected = (i == SelectedIndex);

                    var item = Items[i];
                    GetItemData(i, out data);

                    var rgn = new Rectangle(
                        0, y, width, 
                        data.Expanded ? 
                            (int)Math.Ceiling(lineHeight * (item.Traceback.Frames.Count + 1)) :
                            CollapsedSize
                    );

                    using (var scratch = Scratch.Get(e.Graphics, rgn)) {
                        var g = scratch.Graphics;

                        g.ResetClip();
                        g.Clear(selected ? highlightBrush.Color : backgroundBrush.Color);

                        renderParams.BackgroundColor = selected ? highlightBrush.Color : backgroundBrush.Color;
                        renderParams.BackgroundBrush = selected ? highlightBrush : backgroundBrush;
                        renderParams.TextBrush = selected ? highlightTextBrush : textBrush;

                        renderParams.ContentRegion = rgn;
                        renderParams.IsExpanded = data.Expanded;
                        renderParams.IsSelected = selected;

                        y += (int)Math.Ceiling(item.Render(g, ref renderParams));
                    }

                    VisibleItems.Add(new VisibleItem {
                        Y1 = y1, Y2 = y, Index = i
                    });

                    if ((y1 >= 0) && (y < ClientSize.Height) || ((y - y1) >= ClientSize.Height)) {
                        minVisibleIndex = Math.Min(minVisibleIndex, i);
                        maxVisibleIndex = Math.Max(maxVisibleIndex, i);
                        selectedItemVisible |= selected;
                    }
                }

                if (y < ClientSize.Height)
                    e.Graphics.FillRectangle(backgroundBrush, new Rectangle(0, y, ClientSize.Width, ClientSize.Height - y));
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

            int largeChange = Math.Max(4, ClientSize.Height / CollapsedSize);
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
                if ((pt.Y >= vi.Y1) && (pt.Y <= vi.Y2))
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

        public void ToggleExpandedState (int index) {
            ItemData data;
            GetItemData(index, out data);
            data.Expanded = !data.Expanded;
            SetItemData(index, ref data);

            ShouldAutoscroll = true;
            Invalidate();
        }

        protected override void OnDoubleClick (EventArgs e) {
            ToggleExpandedState(SelectedIndex);
        }

        protected override void OnPreviewKeyDown (PreviewKeyDownEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Down:
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
                case Keys.Space:
                    ToggleExpandedState(SelectedIndex);
                break;
                case Keys.Up:
                    SelectedIndex -= 1;
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
    }
}
