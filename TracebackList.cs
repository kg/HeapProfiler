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

namespace HeapProfiler {
    public partial class DeltaList : UserControl {
        public struct ItemData {
            public bool Expanded;
        }

        public struct VisibleItem {
            public int Y1, Y2;
            public int Index;
        }

        public readonly List<TItem> Items = new List<TItem>();

        protected readonly LRUCache<TItem, ItemData> Data = new LRUCache<TItem, ItemData>(128, new ReferenceComparer<TItem>());
        protected readonly List<VisibleItem> VisibleItems = new List<VisibleItem>();
        protected int CollapsedSize;

        protected int _SelectedIndex = -1;
        protected int _ScrollOffset = 0;

        public DeltaList () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | 
                ControlStyles.Opaque | ControlStyles.ResizeRedraw, 
                true
            );

            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
        }

        protected override void OnPaint (PaintEventArgs e) {
            var g = e.Graphics;

            if (_ScrollOffset >= Items.Count)
                _ScrollOffset = Math.Max(0, Items.Count - 1);

            var sf = new StringFormat {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.NoWrap | StringFormatFlags.DisplayFormatControl | StringFormatFlags.MeasureTrailingSpaces,
                HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None,
                Trimming = StringTrimming.None
            };

            CollapsedSize = (int)Math.Ceiling(g.MeasureString("AaBbYyZz", Font).Height * 3);

            VisibleItems.Clear();

            ItemData data;

            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var textBrush = new SolidBrush(ForeColor))
            using (var highlightBrush = new SolidBrush(SystemColors.Highlight))
            using (var highlightTextBrush = new SolidBrush(SystemColors.HighlightText)) {
                int y = 0;
                for (int i = _ScrollOffset; (i < Items.Count) && (y < ClientSize.Height); i++) {
                    var item = Items[i];
                    GetItemData(i, out data);

                    var text = item.ToString();
                    var size = g.MeasureString(text, Font, ClientSize.Width, sf);

                    if (!data.Expanded)
                        size.Height = (float)Math.Min(size.Height, CollapsedSize);

                    var rgn = new RectangleF(0, y, ClientSize.Width, (float)Math.Ceiling(size.Height));

                    g.FillRectangle((i == SelectedIndex) ? highlightBrush : backgroundBrush, rgn);
                    g.DrawString(text, Font, (i == SelectedIndex) ? highlightTextBrush : textBrush, rgn, sf);

                    VisibleItems.Add(new VisibleItem {
                        Y1 = (int)Math.Floor(rgn.Top),
                        Y2 = (int)Math.Ceiling(rgn.Bottom),
                        Index = i
                    });

                    y += (int)size.Height;
                }

                g.FillRectangle(backgroundBrush, new Rectangle(0, y, ClientSize.Width, ClientSize.Height - y));

            }

            base.OnPaint(e);
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                foreach (var vi in VisibleItems) {
                    if ((e.Y >= vi.Y1) && (e.Y <= vi.Y2)) {
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
                    if ((e.Y >= vi.Y1) && (e.Y <= vi.Y2)) {
                        SelectedIndex = vi.Index;
                        break;
                    }
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnDoubleClick (EventArgs e) {
            ItemData data;
            GetItemData(SelectedIndex, out data);
            data.Expanded = !data.Expanded;
            SetItemData(SelectedIndex, ref data);
            Invalidate();
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
                if ((value < -1) || (value >= Items.Count))
                    throw new ArgumentOutOfRangeException("value", "-1 <= SelectedIndex < Count");

                if (value != _SelectedIndex) {
                    _SelectedIndex = value;
                    Invalidate();
                }
            }
        }

        public int ScrollOffset {
            get {
                return _ScrollOffset;
            }
            set {
                if ((value < 0) || (value >= Items.Count))
                    throw new ArgumentOutOfRangeException("value", "0 <= ScrollOffset < Count");

                if (value != _ScrollOffset) {
                    _ScrollOffset = value;
                    Invalidate();
                }
            }
        }
    }

    class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class {

        public bool Equals (T x, T y) {
            return (x == y);
        }

        public int GetHashCode (T obj) {
            return obj.GetHashCode();
        }
    }
}
