using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TItem = HeapProfiler.HeapSnapshotInfo;
using Squared.Util;

namespace HeapProfiler {
    public partial class SnapshotTimeline : UserControl {
        public event EventHandler SelectionChanged;
        public event EventHandler ItemValueGetterChanged;

        public const int VerticalMargin = 2;
        public const int PixelsPerMinute = 120;
        public const int MarginWidth = 20;
        public const double GridLineRatio = 0.4;
        public const int MaxGridLines = 16;
        public const long MaximumThreshold = 256;
        public const int MinSelectionDistance = 5;

        public List<TItem> Items = new List<TItem>();

        protected ScratchBuffer Scratch = new ScratchBuffer();
        protected Button ZoomInButton = null, ZoomOutButton = null;
        protected ScrollBar ScrollBar = null;
        protected ToolTip ToolTip;

        protected Func<TItem, long> _ItemValueGetter;
        protected Func<long, string> _ItemValueFormatter;
        protected int _ZoomRatio = 100;
        protected int _ContentWidth = 0; 
        protected int _ScrollOffset = 0;
        protected string _ToolTipText = null;
        protected Point? _MouseDownLocation;
        protected Point _MouseMoveLocation;
        protected Pair<int> _Selection = new Pair<int>(-1, -1);

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

            ScrollBar.Scroll += ScrollBar_Scroll;

            ZoomInButton = new Button {
                Text = "+",
                TabStop = false,
                UseVisualStyleBackColor = true
            };
            ZoomInButton.Click += ZoomInButton_Click;

            ZoomOutButton = new Button {
                Text = "-",
                TabStop = false,
                UseVisualStyleBackColor = true
            };
            ZoomOutButton.Click += ZoomOutButton_Click;

            ToolTip = new ToolTip();
            _ItemValueGetter = (s) => 0;
            _ItemValueFormatter = (v) => String.Format("{0}", v);

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
            long maxValue = MaximumThreshold;
            long minTicks = 0, maxTicks = 0;
            int contentWidth;

            const long minuteInTicks = Squared.Util.Time.SecondInTicks * 60;

            if (Items.Count > 0) {
                maxValue = Math.Max(MaximumThreshold, (from s in Items select _ItemValueGetter(s)).Max());
                minTicks = (from s in Items select s.Timestamp.Ticks).Min();
                maxTicks = (from s in Items select s.Timestamp.Ticks).Max();
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
            using (var scratch = Scratch.Get(e.Graphics, ClientRectangle)) {
                var g = scratch.Graphics;
                g.Clear(BackColor);

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                float y = 0;
                using (var textBrush = new SolidBrush(ForeColor))
                {
                    g.DrawLine(gridPen, 0, height - VerticalMargin, ClientSize.Width, height - VerticalMargin);
                    g.DrawLine(gridPen, 0, VerticalMargin, Width, VerticalMargin);

                    var lineHeight = g.MeasureString("AaBbYyZz", Font).Height;
                    var numLines = (int)Math.Min(
                        Math.Max(2, Math.Floor((height / lineHeight) * GridLineRatio)),
                        MaxGridLines
                    );
                    for (int i = 0; i <= numLines; i++) {
                        var value = (maxValue * i / numLines);
                        var text = _ItemValueFormatter(value);
                        y = (float)(height - VerticalMargin - ((value / (double)maxValue) * (height - VerticalMargin * 2)));

                        g.DrawLine(gridPen, 0, y, ClientSize.Width, y);
                        g.DrawString(text, Font, textBrush, new PointF(4, y)); 
                    }
                }

                int x = 0, lastX = 0;
                float lastY = 0;
                y = 0;

                List<PointF> points = new List<PointF>(), 
                    mousePoints = new List<PointF>();

                for (
                    int i = Math.Max(0, IndexFromPoint(new Point(0, 0), -1)); 
                    (i < Items.Count) && (lastX <= ClientSize.Width); i++
                ) {
                    bool selected = HasSelection &&
                        (i >= _Selection.First) &&
                        (i <= _Selection.Second);
                    var item = Items[i];
                    var value = _ItemValueGetter(item);

                    lastY = y;
                    y = (float)(height - VerticalMargin - ((value / (double)maxValue) * (height - VerticalMargin * 2)));
                    lastX = x;
                    x = (int)((item.Timestamp.Ticks - minTicks)
                        * pixelsPerMinute / minuteInTicks) - _ScrollOffset;

                    using (var brush = new SolidBrush(selected ? SystemColors.HighlightText : ForeColor))
                        g.FillEllipse(brush, x - 2.5f, y - 2.5f, 5f, 5f);

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

                var cursorPos = PointToClient(Cursor.Position);

                DateTime mouseTime;
                if (ClientRectangle.Contains(cursorPos) && TimeFromPoint(_MouseMoveLocation, out mouseTime)) {
                    using (var pen = new Pen(Color.FromArgb(127, SystemColors.HighlightText))) {
                        pen.Width = 2.0f;
                        g.DrawLine(pen, _MouseMoveLocation.X, 0, _MouseMoveLocation.X, height);
                    }

                    var mouseIndex = IndexFromTime(mouseTime, -1);
                    if (!_MouseDownLocation.HasValue)
                    using (var pen = new Pen(SystemColors.HighlightText)) {
                        pen.Width = 2.0f;
                        var item = Items[mouseIndex];
                        x = (int)((item.Timestamp.Ticks - minTicks) * pixelsPerMinute / minuteInTicks) - _ScrollOffset;
                        g.DrawLine(
                            pen, 
                            x, 0, x, height
                        );
                    }
                }
            }
        }

        class SnapshotTimeFinder : IComparer<TItem> {
            public readonly DateTime When;

            public SnapshotTimeFinder (DateTime when) {
                When = when;
            }

            public int Compare (TItem x, TItem y) {
                if ((y != null) || (x == null))
                    throw new InvalidOperationException("BinarySearch should be doing Comparer(currentItem, valueToFind), and valueToFind should be null.");

                return x.Timestamp.CompareTo(When);
            }
        }

        public bool TimeFromPoint (Point location, out DateTime time) {
            var absoluteX = (location.X + _ScrollOffset);
            if (absoluteX < 0) {
                time = new DateTime(DateTime.MinValue.Ticks, DateTimeKind.Utc);
                return false;
            } else if (absoluteX >= (_ContentWidth - MarginWidth)) {
                time = new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc);
                return false;
            }

            int pixelsPerMinute = PixelsPerMinute * ZoomRatio / 100;
            long minTicks = 0;
            const long minuteInTicks = Squared.Util.Time.SecondInTicks * 60;

            if (Items.Count > 0)
                minTicks = (from s in Items select s.Timestamp.Ticks).Min();

            time = new DateTime(
                (absoluteX * minuteInTicks / pixelsPerMinute) + minTicks,
                DateTimeKind.Local
            );
            return true;
        }

        public int IndexFromPoint (Point location, int direction) {
            DateTime time;
            if (!TimeFromPoint(location, out time)) {
                if (time.Ticks <= DateTime.MinValue.Ticks)
                    return 0;
                else if (time.Ticks >= DateTime.MaxValue.Ticks)
                    return Items.Count - 1;
            }

            return IndexFromTime(time, direction);
        }

        public int IndexFromTime (DateTime time, int direction) {
            var index = Items.BinarySearch(
                null, new SnapshotTimeFinder(time)
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
            if (!_MouseDownLocation.HasValue)
                return;

            var index1 = IndexFromPoint(_MouseDownLocation.Value, -1);
            var index2 = IndexFromPoint(mouseLocation, 1);

            if (Math.Abs(mouseLocation.X - _MouseDownLocation.Value.X) >= MinSelectionDistance) {
                var newSelection = Pair.New(
                    Math.Min(index1, index2),
                    Math.Max(index1, index2)
                );
                if (newSelection.First == newSelection.Second) {
                    if (newSelection.Second > 0)
                        newSelection.First = newSelection.Second - 1;
                    else if (newSelection.First < (Items.Count - 1))
                        newSelection.Second = newSelection.First + 1;
                }

                Selection = newSelection;
            } else {
                Selection = Pair.New(index1, index1);
            }

            SetToolTip(Selection);
        }

        protected void UpdateToolTip (Point mouseLocation) {
            var index1 = IndexFromPoint(mouseLocation, -1);
            var index2 = IndexFromPoint(mouseLocation, 1);

            if ((index1 < 0) && (index2 < 0)) {
                SetToolTip();
                return;
            }

            if (index1 < 0)
                SetToolTip(index2);
            else
                SetToolTip(index1);
        }

        protected void SetToolTip () {
            if (_ToolTipText == null)
                return;

            _ToolTipText = null;
            ToolTip.SetToolTip(this, null);
        }

        protected void SetToolTip (int index) {
            var item = Items[index];
            var value = _ItemValueGetter(item);
            var formattedValue = _ItemValueFormatter(value);
            var text = String.Format(
                "{0}: {1}",
                Items[index].Timestamp.ToLongTimeString(),
                formattedValue
            );

            if (text == _ToolTipText)
                return;

            _ToolTipText = text;
            ToolTip.SetToolTip(this, text);
        }

        protected void SetToolTip (Pair<int> range) {
            if ((range.First < 0) || (range.Second < 0)) {
                SetToolTip();
                return;
            }

            var text = String.Format(
                "{0} - {1}",
                Items[range.First].Timestamp.ToLongTimeString(),
                Items[range.Second].Timestamp.ToLongTimeString()
            );

            if (text == _ToolTipText)
                return;

            _ToolTipText = text;
            ToolTip.SetToolTip(this, text);
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                _MouseDownLocation = _MouseMoveLocation = e.Location;
                UpdateSelection(e.Location);
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            _MouseMoveLocation = e.Location;

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                UpdateSelection(e.Location);
            else
                UpdateToolTip(e.Location);

            Invalidate();

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp (MouseEventArgs e) {
            _MouseDownLocation = null;
            Invalidate();

            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave (EventArgs e) {
            Invalidate();

            base.OnMouseLeave(e);
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool HasSelection {
            get {
                return (_Selection.First != -1) && (_Selection.Second != -1);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Func<TItem, long> ItemValueGetter {
            get {
                return _ItemValueGetter;
            }
            set {
                if (value != _ItemValueGetter) {
                    _ItemValueGetter = value;

                    Invalidate();
                    if (ItemValueGetterChanged != null)
                        ItemValueGetterChanged(this, EventArgs.Empty);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Func<long, string> ItemValueFormatter {
            get {
                return _ItemValueFormatter;
            }
            set {
                if (value != _ItemValueFormatter) {
                    _ItemValueFormatter = value;

                    Invalidate();
                }
            }
        }
    }
}
