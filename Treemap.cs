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

        public struct LayoutItem {
            public RectangleF Rectangle;
            public TItem Item;
        }

        public const int MinimumTextWidth = 32;
        public const int MinimumTextHeight = 14;

        public Func<TItem, long> GetItemValue;
        public Func<TItem, string> GetItemText;
        public Func<TItem, TooltipContentBase> GetItemTooltip;

        public IList<TItem> Items = new List<TItem>();
        public readonly List<LayoutGroup> Layout = new List<LayoutGroup>();

        protected LayoutItem? _HoverItem = null;
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

        protected void ComputeLayoutGroup (int firstIndex, ref int lastIndex, ref RectangleF unfilledRectangle, double scaleRatio, List<LayoutItem> result, out RectangleF resultRectangle) {
            var buffer = new List<LayoutItem>();
            bool packVertically = (unfilledRectangle.Width > unfilledRectangle.Height);
            float? lastAspectRatio = null, lastVariableSize = null;
            float fixedSize = (packVertically) ? unfilledRectangle.Height : unfilledRectangle.Width;

            resultRectangle = unfilledRectangle;

            for (int i = firstIndex; i <= lastIndex; i++) {
                double area = 0;
                for (int j = firstIndex; j <= i; j++)
                    area += GetItemValue(Items[j]);
                area *= scaleRatio;

                result.Clear();
                result.AddRange(buffer);
                buffer.Clear();

                float variableSize = (float)(area / fixedSize);
                float pos = 0f;
                float aspectRatio = 999f;

                for (int j = firstIndex; j <= i; j++) {
                    var item = Items[j];
                    double itemArea = GetItemValue(item) * scaleRatio;
                    float itemSize = (float)(itemArea / variableSize);
                    RectangleF itemRect;

                    if (packVertically) {
                        itemRect = new RectangleF(
                            unfilledRectangle.X, unfilledRectangle.Y + pos,
                            variableSize, itemSize
                        );
                    } else {
                        itemRect = new RectangleF(
                            unfilledRectangle.X + pos, unfilledRectangle.Y,
                            itemSize, variableSize
                        );
                    }

                    buffer.Add(new LayoutItem {
                        Item = item,
                        Rectangle = itemRect
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

                    return;
                }

                lastAspectRatio = aspectRatio;
                lastVariableSize = variableSize;
            }
        }

        protected LayoutItem? ItemFromPoint (Point pt) {
            foreach (var group in Layout) {
                if (group.Rectangle.Contains(pt.X, pt.Y))
                    foreach (var item in group.Items)
                        if (item.Rectangle.Contains(pt.X, pt.Y))
                            return item;
            }

            return null;
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            var newItem = ItemFromPoint(e.Location);

            if (newItem.GetValueOrDefault().Item != _HoverItem.GetValueOrDefault().Item) {
                if (newItem != null)
                    ShowTooltip(newItem.Value, e.Location);
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
                content.Location = PointToScreen(location);

                CustomTooltip.FitContentOnScreen(
                    g, content,
                    ref content.Font, ref content.Location, ref content.Size
                );

                Tooltip.SetContent(content);
            }

            if (_HoverItem.GetValueOrDefault().Item != item.Item) {
                var oldItem = _HoverItem;
                _HoverItem = item;
                Invalidate(oldItem.GetValueOrDefault().Rectangle);
                Invalidate(item.Rectangle);
            }
        }

        protected void Invalidate (RectangleF rectangle) {
            if ((rectangle.Width <= 0f) || (rectangle.Height <= 0f))
                return;

            Invalidate(new Rectangle(
                (int)Math.Floor(rectangle.X), (int)Math.Floor(rectangle.Y),
                (int)Math.Ceiling(rectangle.Width), (int)Math.Ceiling(rectangle.Height)
            ), false);
        }

        protected void HideTooltip () {
            if ((Tooltip != null) && Tooltip.Visible)
                Tooltip.Hide();

            if (_HoverItem != null) {
                var oldItem = _HoverItem;
                _HoverItem = null;
                Invalidate(oldItem.GetValueOrDefault().Rectangle);
            }
        }

        protected void ComputeLayout () {
            float totalArea = 0;
            foreach (var item in Items)
                totalArea += GetItemValue(item);

            var buffer = new List<LayoutItem>();
            var totalRect = new RectangleF(0f, 0f, ClientSize.Width, ClientSize.Height);
            double scaleRatio = (totalRect.Width * totalRect.Height) / totalArea;

            float resultAspect;
            RectangleF resultRect;

            Layout.Clear();

            var last = Items.Count - 1;
            for (int i = 0; i <= last;) {
                int lastIndex = last;

                ComputeLayoutGroup(
                    i, ref lastIndex,
                    ref totalRect, scaleRatio,
                    buffer, out resultRect
                );

                Layout.Add(new LayoutGroup {
                    Rectangle = resultRect,
                    Items = buffer.ToArray()
                });

                if (i == lastIndex)
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
            int value = ((id & (0xFFFF << 16)) % (HSV.ValueMax * 70 / 100)) + (HSV.ValueMax * 25 / 100);

            return HSV.ColorFromHSV(
                (UInt16)hue, HSV.SaturationMax, (UInt16)value
            );
        }

        protected override void OnPaint (PaintEventArgs e) {
            var clipRectF = (RectangleF)e.ClipRectangle;

            if (Layout.Count == 0)
                e.Graphics.Clear(BackColor);

            var textToFlush = new HashSet<string>(TextCache.Keys);

            using (var textBrush = new SolidBrush(ForeColor))
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
                        var itemColor = SelectItemColor(item.Item);

                        bool white = (itemColor.GetBrightness() <= 0.25f);

                        using (var brush = new SolidBrush(itemColor))
                            g.FillRectangle(brush, item.Rectangle);

                        if ((item.Rectangle.Width > MinimumTextWidth) && (item.Rectangle.Height > MinimumTextHeight)) {
                            var itemText = GetItemText(item.Item);
                            textToFlush.Remove(itemText);

                            var bitmap = TextCache.Get(
                                g, itemText, Font, RotateFlipType.RotateNoneFlipNone, 
                                white ? Color.White : Color.Black, 
                                white ? Color.Black : Color.LightGray, 
                                StringFormat.GenericDefault
                            );

                            g.DrawImageUnscaled(
                                bitmap, new Rectangle(
                                    (int)item.Rectangle.X, (int)item.Rectangle.Y,
                                    (int)Math.Min(item.Rectangle.Width, bitmap.Width),
                                    (int)Math.Min(item.Rectangle.Height, bitmap.Height)
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

            TextCache.Flush();
        }

        public override void Refresh () {
            ComputeLayout();
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
