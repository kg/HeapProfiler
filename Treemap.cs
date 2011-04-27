using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Squared.Data.Mangler;

namespace HeapProfiler {
    public class GenericTreemap<TItem> : UserControl, ITooltipOwner
        where TItem: class {

        public struct LayoutGroup : IEnumerable<LayoutItem> {
            public RectangleF Rectangle;
            public LayoutItem[] Items;

            public IEnumerator<LayoutItem> GetEnumerator () {
                foreach (var item in Items)
                    yield return item;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () {
                return Items.GetEnumerator();
            }
        }

        public class LayoutItem {
            public RectangleF Rectangle;
            public TItem Item;
            public int NestingDepth = 0;
        }

        public const int LayoutNestingLimit = 2;
        public const int MinimumTextWidth = 32;
        public const int MinimumTextHeight = 14;

        public Func<TItem, long> GetItemValue;
        public Func<TItem, string> GetItemText;
        public Func<TItem, TooltipContentBase> GetItemTooltip;

        public IList<TItem> Items = new List<TItem>();
        public readonly Stack<TItem> DrilldownStack = new Stack<TItem>();
        public readonly List<LayoutGroup> Layout = new List<LayoutGroup>();

        protected LayoutItem _HoverItem = null;
        protected CustomTooltip Tooltip = null;
        protected OutlinedTextCache TextCache = new OutlinedTextCache();
        protected ScratchBuffer Scratch = new ScratchBuffer();

        static float ComputeAspectRatio (float width, float height) {
            return Math.Max(width / height, height / width);
        }

        public GenericTreemap () {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.Opaque | ControlStyles.Selectable,
                true
            );

            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
        }

        protected override void Dispose (bool disposing) {
            TextCache.Dispose();
            Scratch.Dispose();

            base.Dispose(disposing);
        }

        protected void ComputeLayoutGroup (
            IList<TItem> items, int firstIndex, ref int lastIndex, 
            ref RectangleF unfilledRectangle, double scaleRatio, 
            List<LayoutItem> result, out RectangleF resultRectangle,
            int nestingDepth = 0
        ) {
            var currentPass = new List<LayoutItem>();
            var lastPass = new List<LayoutItem>();

            bool packVertically = (unfilledRectangle.Width > unfilledRectangle.Height);
            float? lastAspectRatio = null, lastVariableSize = null;
            float fixedSize = (packVertically) ? unfilledRectangle.Height : unfilledRectangle.Width;

            resultRectangle = unfilledRectangle;

            for (int i = firstIndex; i <= lastIndex; i++) {
                double area = 0;
                for (int j = firstIndex; j <= i; j++)
                    area += GetItemValue(items[j]);
                area *= scaleRatio;

                var temp = currentPass;
                currentPass = lastPass;
                lastPass = temp;
                currentPass.Clear();

                float variableSize = Math.Min(
                    (float)(area / fixedSize),
                    (packVertically) ? unfilledRectangle.Width : unfilledRectangle.Height
                );
                float pos = 0f;
                float aspectRatio = 999f;

                for (int j = firstIndex; j <= i; j++) {
                    var item = items[j];
                    double itemArea = GetItemValue(item) * scaleRatio;
                    float itemSize = (float)(itemArea / variableSize);
                    RectangleF itemRect;

                    if (packVertically) {
                        itemRect = new RectangleF(
                            unfilledRectangle.X, unfilledRectangle.Y + pos,
                            variableSize, Math.Min(itemSize, unfilledRectangle.Height - pos)
                        );
                    } else {
                        itemRect = new RectangleF(
                            unfilledRectangle.X + pos, unfilledRectangle.Y,
                            Math.Min(itemSize, unfilledRectangle.Width - pos), variableSize
                        );
                    }

                    currentPass.Add(new LayoutItem {
                        Item = item,
                        Rectangle = itemRect,
                        NestingDepth = nestingDepth
                    });

                    pos += itemSize;
                    aspectRatio = ComputeAspectRatio(itemRect.Width, itemRect.Height);
                }

                if ((lastAspectRatio.HasValue) && (lastAspectRatio.Value < aspectRatio)) {
                    lastIndex = i;

                    if (packVertically) {
                        resultRectangle = new RectangleF(
                            unfilledRectangle.X, unfilledRectangle.Y,
                            lastVariableSize.Value, unfilledRectangle.Height
                        );
                        unfilledRectangle = new RectangleF(
                            unfilledRectangle.X + lastVariableSize.Value, unfilledRectangle.Y,
                            unfilledRectangle.Width - lastVariableSize.Value, unfilledRectangle.Height
                        );
                    } else {
                        resultRectangle = new RectangleF(
                            unfilledRectangle.X, unfilledRectangle.Y,
                            unfilledRectangle.Width, lastVariableSize.Value
                        );
                        unfilledRectangle = new RectangleF(
                            unfilledRectangle.X, unfilledRectangle.Y + lastVariableSize.Value,
                            unfilledRectangle.Width, unfilledRectangle.Height - lastVariableSize.Value
                        );
                    }

                    result.AddRange(lastPass);

                    return;
                }

                lastAspectRatio = aspectRatio;
                lastVariableSize = variableSize;
            }

            result.AddRange(currentPass);
            lastIndex = -1;
        }

        protected LayoutItem ItemFromPoint (Point pt) {
            foreach (var group in Layout) {
                if (group.Rectangle.Contains(pt.X, pt.Y))
                    for (var i = group.Items.Length - 1; i >= 0; i--)
                        if (group.Items[i].Rectangle.Contains(pt.X, pt.Y))
                            return group.Items[i];
            }

            return null;
        }

        protected override void OnMouseClick (MouseEventArgs e) {
            var item = ItemFromPoint(e.Location);
            TItem currentDrilldown = null;
            if (DrilldownStack.Count > 0)
                currentDrilldown = DrilldownStack.Peek();

            if (
                (e.Button == System.Windows.Forms.MouseButtons.Right) ||
                (item.Item == currentDrilldown)
            ) {
                if (DrilldownStack.Count > 0) {
                    DrilldownStack.Pop();
                    ComputeLayout();
                }

                return;
            }

            var ie = item.Item as IEnumerable<TItem>;
            if (ie == null)
                return;

            DrilldownStack.Push(item.Item);
            ComputeLayout();

            base.OnMouseClick(e);
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            var newItem = ItemFromPoint(e.Location);

            if (newItem != _HoverItem) {
                if (newItem != null)
                    ShowTooltip(newItem, e.Location);
                else
                    HideTooltip();
            }

            base.OnMouseMove(e);
        }
        protected void ShowTooltip (LayoutItem item, Point location) {
            if (Tooltip == null)
                Tooltip = new CustomTooltip(this);

            using (var g = CreateGraphics()) {
                var content = GetItemTooltip(item.Item);

                content.Font = Font;
                content.Location = PointToScreen(new Point(
                    (int)item.Rectangle.Left,
                    (int)item.Rectangle.Bottom + 2
                ));

                CustomTooltip.FitContentOnScreen(
                    g, content,
                    ref content.Font, ref content.Location, ref content.Size
                );

                Tooltip.SetContent(content);
            }

            if (_HoverItem != item) {
                var oldItem = _HoverItem;
                _HoverItem = item;

                if (oldItem != null)
                    Invalidate(oldItem.Rectangle);

                Invalidate(item.Rectangle);
            }
        }

        protected void Invalidate (RectangleF rectangle) {
            if ((rectangle.Width <= 0f) || (rectangle.Height <= 0f))
                return;

            Invalidate(new Rectangle(
                (int)Math.Floor(rectangle.X - 1f), (int)Math.Floor(rectangle.Y - 1f),
                (int)Math.Ceiling(rectangle.Width + 2f), (int)Math.Ceiling(rectangle.Height + 2f)
            ), false);
        }

        protected void HideTooltip () {
            if ((Tooltip != null) && Tooltip.Visible)
                Tooltip.Hide();

            if (_HoverItem != null) {
                var oldItem = _HoverItem;
                _HoverItem = null;

                if (oldItem != null)
                    Invalidate(oldItem.Rectangle);
            }
        }

        protected void ComputeNestedLayouts (List<LayoutItem> buffer, int firstIndex, int lastIndex, int depth = 1) {
            var limit = LayoutNestingLimit;
            if (DrilldownStack.Count > 0)
                limit += 1;

            if (depth > limit)
                return;

            var newRanges = new List<Tuple<int, int>>();

            for (int j = firstIndex; j <= lastIndex; j++) {
                var item = buffer[j];
                var ie = item.Item as IEnumerable<TItem>;
                if (ie == null)
                    continue;

                var children = ie.ToArray();
                if (children.Length == 0)
                    continue;

                RectangleF resultChildRect;
                var childRect = item.Rectangle;

                childRect.Inflate(-4f, -4f);
                childRect.Height -= 20;
                if ((childRect.Height <= 0) || (childRect.Width <= 0))
                    continue;

                float totalChildArea = 0;
                foreach (var child in children)
                    totalChildArea += GetItemValue(child);

                double childScaleRatio = (childRect.Width * childRect.Height) / totalChildArea;

                int previousCount = buffer.Count;

                int k = 0;
                var lastChildIndex = children.Length - 1;
                while (k <= lastChildIndex) {
                    ComputeLayoutGroup(
                        children, k, ref lastChildIndex,
                        ref childRect, childScaleRatio,
                        buffer, out resultChildRect,
                        depth
                    );

                    if (lastChildIndex == -1)
                        break;

                    k = lastChildIndex;
                    lastChildIndex = children.Length - 1;
                }

                if (buffer.Count != previousCount)
                    newRanges.Add(new Tuple<int, int>(
                        previousCount, buffer.Count - 1
                    ));
            }

            foreach (var range in newRanges) {
                ComputeNestedLayouts(buffer, range.Item1, range.Item2, depth + 1);
            }
        }

        protected void ComputeLayout () {
            var items = Items;
            if (DrilldownStack.Count > 0) {
                items = new TItem[] { DrilldownStack.Peek() };
            }

            float totalArea = 0;
            foreach (var item in items)
                totalArea += GetItemValue(item);

            var buffer = new List<LayoutItem>();
            var totalRect = new RectangleF(0f, 0f, ClientSize.Width, ClientSize.Height);
            double scaleRatio = (totalRect.Width * totalRect.Height) / totalArea;

            float resultAspect;
            RectangleF resultRect;

            _HoverItem = null;
            Layout.Clear();

            var last = items.Count - 1;
            for (int i = 0; i <= last;) {
                int lastIndex = last;

                buffer.Clear();
                ComputeLayoutGroup(
                    items, i, ref lastIndex,
                    ref totalRect, scaleRatio,
                    buffer, out resultRect
                );

                ComputeNestedLayouts(
                    buffer, 0, buffer.Count - 1
                );

                Layout.Add(new LayoutGroup {
                    Rectangle = resultRect,
                    Items = buffer.ToArray()
                });

                if (lastIndex == -1)
                    break;
                else
                    i = lastIndex;
            }

            Invalidate();
        }

        protected Color SelectItemColor (TItem item) {
            var hashBytes = ImmutableBufferPool.GetBytes(item.GetHashCode());
            var id = BitConverter.ToInt32(hashBytes.Array, hashBytes.Offset);

            int hue = (id & 0xFFFF) % (HSV.HueMax);
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 60 / 100))
                + (HSV.ValueMax * 20 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaint (PaintEventArgs e) {
            var clipRectF = (RectangleF)e.ClipRectangle;

            if (Layout.Count == 0)
                e.Graphics.Clear(BackColor);

            var textToFlush = new HashSet<string>(TextCache.Keys);
            int textSuppressLimit = 2;

            foreach (var group in Layout) {
                if (!group.Rectangle.IntersectsWith(clipRectF)) {
                    foreach (var item in group.Items)
                        textToFlush.Remove(GetItemText(item.Item));

                    continue;
                }

                using (var scratch = Scratch.Get(e.Graphics, group.Rectangle)) {
                    var g = scratch.Graphics;
                    if (scratch.IsCancelled)
                        continue;

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.Clear(BackColor);

                    foreach (var item in group.Items) {
                        var opacity = 255;
                        Color itemColor;
                        Color outlineColor;

                        if ((item.NestingDepth > 0) && (item != _HoverItem)) {
                            opacity = (int)(opacity * Math.Pow(0.7, item.NestingDepth));
                        }

                        if (item == _HoverItem) {
                            itemColor = SystemColors.Highlight;
                            outlineColor = SystemColors.HighlightText;
                        } else {
                            itemColor = SelectItemColor(item.Item);
                            outlineColor = Color.FromArgb(opacity / 2, 0, 0, 0);
                        }

                        bool white = (itemColor.GetBrightness() <= 0.25f);

                        using (var brush = new SolidBrush(
                            Color.FromArgb(opacity, itemColor.R, itemColor.G, itemColor.B)
                        ))
                            g.FillRectangle(brush, item.Rectangle);

                        using (var outlinePen = new Pen(outlineColor))
                            g.DrawRectangle(
                                outlinePen, 
                                item.Rectangle.X, item.Rectangle.Y,
                                item.Rectangle.Width, item.Rectangle.Height
                            );

                        if ((item.NestingDepth >= textSuppressLimit) && (item != _HoverItem))
                            continue;

                        if ((item.Rectangle.Width > MinimumTextWidth) && (item.Rectangle.Height > MinimumTextHeight)) {
                            var itemText = GetItemText(item.Item);
                            textToFlush.Remove(itemText);

                            var bitmap = TextCache.Get(
                                g, itemText, Font, RotateFlipType.RotateNoneFlipNone, 
                                white ? Color.White : Color.Black, 
                                white ? Color.Black : Color.LightGray, 
                                StringFormat.GenericDefault
                            );

                            var w = (int)Math.Min(item.Rectangle.Width, bitmap.Width);
                            var h = (int)Math.Min(item.Rectangle.Height, bitmap.Height);
                            g.DrawImageUnscaledAndClipped(
                                bitmap, new Rectangle(
                                    (int)item.Rectangle.Right - w, 
                                    (int)item.Rectangle.Bottom - h,
                                    w, h
                                )
                            );
                        }
                    }
                }
            }

            TextCache.Flush(textToFlush);
        }

        protected override void OnSizeChanged (EventArgs e) {
            ComputeLayout();
        }

        protected override void OnMouseLeave (EventArgs e) {
            base.OnMouseLeave(e);

            if (Tooltip == null)
                return;

            if (!Tooltip.ClientRectangle.Contains(Tooltip.PointToClient(Cursor.Position)))
                HideTooltip();
        }

        protected override void OnVisibleChanged (EventArgs e) {
            base.OnVisibleChanged(e);

            Refresh();
        }

        public override void Refresh () {
            if (!Visible)
                return;

            TextCache.Flush();
            DrilldownStack.Clear();
            ComputeLayout();
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

    public class GraphTreemap : GenericTreemap<StackGraphNode> {
        public GraphTreemap () {
            base.GetItemValue = (sgn) => Math.Abs(sgn.BytesRequested);
            base.GetItemText = (sgn) => {
                var text = sgn.Key.FunctionName ?? "";
                if (text.Length > 64)
                    return text.Substring(0, 64);
                else
                    return text;
            };
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
