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
    public class GenericTreemap<TItem> : UserControl 
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

        public Func<TItem, long> GetItemValue;
        public Func<TItem, string> GetItemText;
        public Func<TItem, TooltipContentBase> GetItemTooltip;

        public IList<TItem> Items = new List<TItem>();
        public readonly List<LayoutGroup> Layout = new List<LayoutGroup>();

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

                if (packVertically) {
                    resultRectangle = new RectangleF(
                        unfilledRectangle.X, unfilledRectangle.Y,
                        variableSize, unfilledRectangle.Height
                    );
                } else {
                    resultRectangle = new RectangleF(
                        unfilledRectangle.X, unfilledRectangle.Y,
                        unfilledRectangle.Width, variableSize
                    );
                }

                if ((lastAspectRatio.HasValue) && (lastAspectRatio.Value < aspectRatio)) {
                    lastIndex = i;
                    if (packVertically) {
                        unfilledRectangle = new RectangleF(
                            unfilledRectangle.X + lastVariableSize.Value, unfilledRectangle.Y,
                            unfilledRectangle.Width - lastVariableSize.Value, unfilledRectangle.Height
                        );
                    } else {
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
            e.Graphics.Clear(BackColor);

            using (var textBrush = new SolidBrush(ForeColor))
            foreach (var group in Layout) {
                using (var scratch = Scratch.Get(e.Graphics, group.Rectangle)) {
                    var g = scratch.Graphics;
                    if (scratch.IsCancelled)
                        continue;

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    foreach (var item in group.Items) {
                        var itemColor = SelectItemColor(item.Item);

                        using (var brush = new SolidBrush(itemColor))
                            g.FillRectangle(brush, item.Rectangle);

                        g.DrawString(GetItemText(item.Item), Font, textBrush, item.Rectangle);
                    }
                }
            }
        }

        protected override void OnSizeChanged (EventArgs e) {
            ComputeLayout();
        }

        public override void Refresh () {
            ComputeLayout();
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
