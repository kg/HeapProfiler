using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TItem = HeapProfiler.RunningProcess.Snapshot;
using Squared.Util;

namespace HeapProfiler {
    public partial class SnapshotTimeline : UserControl {
        public event EventHandler SelectionChanged;

        public const int PixelsPerMinute = 120;
        public const int MarginWidth = 20;

        public List<TItem> Items = new List<TItem>();

        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected Button ZoomInButton = null, ZoomOutButton = null;
        protected ScrollBar ScrollBar = null;
        protected ToolTip ToolTip;

        protected int _ZoomRatio = 100;
        protected int _ContentWidth = 0; 
        protected int _ScrollOffset = 0;
        protected Point _MouseDownLocation;
        protected Pair<int> _Selection = new Pair<int>(-1, -1);
        protected Pair<int> _ToolTipRange = new Pair<int>(-1, -1);

        public SnapshotTimeline () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;

            ScrollBar = new HScrollBar {
                SmallChange = ClientSize.Width / 8,
                LargeChange = ClientSize.Width,
                TabStop = false
            };

            ScrollBar.Scroll += new ScrollEventHandler(ScrollBar_Scroll);

            ZoomInButton = new Button {
                Text = "+",
                TabStop = false,
                UseVisualStyleBackColor = true
            };
            ZoomInButton.Click += new EventHandler(ZoomInButton_Click);

            ZoomOutButton = new Button {
                Text = "-",
                TabStop = false,
                UseVisualStyleBackColor = true
            };
            ZoomOutButton.Click += new EventHandler(ZoomOutButton_Click);

            ToolTip = new ToolTip();

            OnResize(EventArgs.Empty);

            Controls.Add(ScrollBar);
            Controls.Add(ZoomInButton);
            Controls.Add(ZoomOutButton);
        }

        void ZoomOutButton_Click (object sender, EventArgs e) {
            ZoomRatio /= 2;
        }

        void ZoomInButton_Click (object sender, EventArgs e) {
            ZoomRatio *= 2;
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
            var buttonSize = preferredSize.Height;
            ScrollBar.SmallChange = ClientSize.Width / 8;
            ScrollBar.LargeChange = ClientSize.Width / 2;
            ScrollBar.SetBounds(
                0, ClientSize.Height - preferredSize.Height,
                ClientSize.Width - (buttonSize * 2), preferredSize.Height
            );

            ZoomInButton.SetBounds(
                ScrollBar.Width, ScrollBar.Top, buttonSize, buttonSize
            );
            ZoomOutButton.SetBounds(
                ZoomInButton.Width + ZoomInButton.Left, ScrollBar.Top, buttonSize, buttonSize
            );

            base.OnResize(e);
        }

        protected override void OnPaintBackground (PaintEventArgs e) {            
        }

        protected override void OnPaint (PaintEventArgs e) {
            int height = ClientSize.Height - ScrollBar.Height;

            int pixelsPerMinute = PixelsPerMinute * ZoomRatio / 100;
            double maxValue = 1024;
            long minTicks = 0, maxTicks = 0;
            int contentWidth;
            var getMemory = (Func<RunningProcess.Snapshot, long>)(
                (s) => {
                    if (s.Memory != null)
                        return s.Memory.Paged;
                    else
                        return 0;
                }
            );

            var minuteInTicks = Squared.Util.Time.SecondInTicks * 60;

            if (Items.Count > 0) {
                maxValue = Math.Max(1024, (double)((from s in Items select getMemory(s)).Max()));
                minTicks = (from s in Items select s.When.Ticks).Min();
                maxTicks = (from s in Items select s.When.Ticks).Max();
                _ContentWidth = contentWidth = (int)(
                    (maxTicks - minTicks) 
                    * pixelsPerMinute / minuteInTicks
                ) + MarginWidth;
            } else {
                _ContentWidth = contentWidth = MarginWidth;
            }

            if (_ScrollOffset > contentWidth - ClientSize.Width)
                _ScrollOffset = contentWidth - ClientSize.Width;
            if (_ScrollOffset < 0)
                _ScrollOffset = 0;

            int scrollMax = contentWidth - ClientSize.Width + ScrollBar.LargeChange - 1;
            bool enabled = (scrollMax > 0);
            if (scrollMax < 0)
                scrollMax = 0;

            // WinForms' scrollbar control is terrible
            if (ScrollBar.Maximum != scrollMax)
                ScrollBar.Maximum = scrollMax;
            if (ScrollBar.Value != _ScrollOffset)
                ScrollBar.Value = _ScrollOffset;
            if (ScrollBar.Enabled != enabled)
                ScrollBar.Enabled = enabled;

            using (var outlinePen = new Pen(Color.Black))
            using (var gridPen = new Pen(Color.FromArgb(96, 0, 0, 0)))
            using (var backgroundBrush = new SolidBrush(BackColor))
            using (var scratch = Scratch.Get(e.Graphics, ClientRectangle)) {
                var g = scratch.Graphics;
                g.Clear(BackColor);

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                int x = 0, lastX = 0;
                float y = 0, lastY = 0;

                var mousePoints = new List<PointF>();
                var points = new List<PointF>();

                for (int i = 0; (i < Items.Count) && (lastX <= ClientSize.Width); i++) {
                    bool selected = HasSelection && 
                        (i >= _Selection.First) && 
                        (i <= _Selection.Second);
                    var item = Items[i];
                    var value = getMemory(item);

                    lastY = y;
                    y = (float)(height - (value / maxValue) * height);
                    lastX = x;
                    x = (int)((item.When.Ticks - minTicks) 
                        * pixelsPerMinute / minuteInTicks) - _ScrollOffset;

                    using (var brush = new SolidBrush(selected ? SystemColors.HighlightText : ForeColor))
                        g.FillEllipse(brush, x - 1.5f, y - 1.5f, 3f, 3f);

                    points.Add(new PointF(x, y));

                    if (selected)
                        mousePoints.Add(new PointF(x, y));
                }

                for (int c = points.Count, i = c - 1; i >= 0; i--)
                    points.Add(new PointF(points[i].X, height));

                for (int c = mousePoints.Count, i = c - 1; i >= 0; i--)
                    mousePoints.Add(new PointF(mousePoints[i].X, height));

                if (points.Count >= 2) {
                    var poly = points.ToArray();

                    using (var brush = new SolidBrush(Color.FromArgb(127, ForeColor)))
                        g.FillPolygon(brush, poly);

                    g.DrawPolygon(outlinePen, poly);
                }

                if (mousePoints.Count >= 2) {
                    var poly = mousePoints.ToArray();

                    using (var brush = new SolidBrush(SystemColors.Highlight))
                        g.FillPolygon(brush, poly);

                    using (var pen = new Pen(SystemColors.HighlightText)) {
                        pen.Width = 2.0f;
                        g.DrawPolygon(pen, poly);
                    }
                }
            }
        }

        struct TimeComparer : IComparer<TItem> {
            public int Compare (TItem x, TItem y) {
                return x.When.CompareTo(y.When);
            }
        }

        public int IndexFromPoint (Point location, int direction) {
            var absoluteX = (location.X + _ScrollOffset);
            if (absoluteX < 0)
                return 0;
            else if (absoluteX >= (_ContentWidth - MarginWidth))
                return Items.Count - 1;

            int pixelsPerMinute = PixelsPerMinute * ZoomRatio / 100;
            long minTicks = 0;
            var minuteInTicks = Squared.Util.Time.SecondInTicks * 60;

            if (Items.Count > 0)
                minTicks = (from s in Items select s.When.Ticks).Min();

            var time = new DateTime(
                (absoluteX * minuteInTicks / pixelsPerMinute) + minTicks,
                DateTimeKind.Local
            );

            var index = Items.BinarySearch(
                new TItem { When = time },
                new TimeComparer()
            );

            if (index < 0) {
                index = ~index;
                if (direction < 0)
                    index -= 1;
            }

            if (direction < 0) {
                if (index >= Items.Count - 1)
                    index = Items.Count - 2;
            } else {
                if (index <= 0)
                    index = 1;
            }

            return index;
        }

        protected void UpdateSelection (Point mouseLocation) {
            int direction = 1;
            if (mouseLocation.X < _MouseDownLocation.X)
                direction = -1;

            var index1 = IndexFromPoint(_MouseDownLocation, -direction);
            var index2 = IndexFromPoint(mouseLocation, direction);

            var newSelection = Pair.New(
                Math.Min(index1, index2),
                Math.Max(index1, index2)
            );
            Selection = newSelection;

            SetToolTip(newSelection);
        }

        protected void UpdateToolTip (Point mouseLocation) {
            var index1 = IndexFromPoint(mouseLocation, -1);
            var index2 = IndexFromPoint(mouseLocation, 1);

            SetToolTip(Pair.New(index1, index2));
        }

        protected void SetToolTip (Pair<int> range) {
            if (range.CompareTo(_ToolTipRange) == 0)
                return;

            _ToolTipRange = range;
            ToolTip.SetToolTip(this, String.Format(
                "{0} - {1}",
                Items[range.First].When.ToLongTimeString(),
                Items[range.Second].When.ToLongTimeString()
            ));
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                _MouseDownLocation = e.Location;
                UpdateSelection(e.Location);
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                UpdateSelection(e.Location);
            else
                UpdateToolTip(e.Location);

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
                case Keys.Left:
                    ScrollOffset -= ClientSize.Width / 8;
                break;
                case Keys.Right:
                    ScrollOffset += ClientSize.Width / 8;
                break;
                case Keys.End:
                    ScrollOffset = _ContentWidth;
                break;
                case Keys.Home:
                    ScrollOffset = 0;
                break;
                case Keys.PageDown:
                    ScrollOffset += ClientSize.Width;
                break;
                case Keys.PageUp:
                    ScrollOffset -= ClientSize.Width;
                break;

                default:
                    base.OnPreviewKeyDown(e);
                break;
            }
        }

        public int ContentWidth {
            get {
                return _ContentWidth;
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
                    return true;
            }

            return false;
        }

        public int ScrollOffset {
            get {
                return _ScrollOffset;
            }
            set {
                if (value > (_ContentWidth - ClientSize.Width))
                    value = (_ContentWidth - ClientSize.Width);
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

        public int ZoomRatio {
            get {
                return _ZoomRatio;
            }
            set {
                if (value > 800)
                    value = 800;
                if (value < 25)
                    value = 25;

                if (value != _ZoomRatio) {
                    _ZoomRatio = value;

                    Invalidate();
                }
            }
        }

        public Pair<int> Selection {
            get {
                return _Selection;
            }
            set {
                if ((value.First < 0) || (value.First >= Items.Count))
                    value.First = -1;
                if ((value.Second < 0) || (value.Second >= Items.Count))
                    value.Second = -1;

                if (_Selection.CompareTo(value) != 0) {
                    _Selection = value;

                    Invalidate();
                    if (SelectionChanged != null)
                        SelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        public bool HasSelection {
            get {
                return (_Selection.First != -1) && (_Selection.Second != -1);
            }
        }
    }
}
