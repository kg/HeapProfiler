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

        protected void ComputeLayoutGroup (int firstIndex, int lastIndex, RectangleF rectangle, double scaleRatio, List<LayoutItem> result, out float aspectRatio) {
            bool packVertically = (rectangle.Width > rectangle.Height);
            float? lastAspectRatio = null;
            float availableArea = rectangle.Width * rectangle.Height;
            float fixedSize = (packVertically) ? rectangle.Height : rectangle.Width;

            aspectRatio = 0f;

            for (int i = lastIndex; i <= lastIndex; i++) {
                double area = 0;
                for (int j = firstIndex; j <= i; j++)
                    area += GetItemValue(Items[j]);
                area *= scaleRatio;

                /*
                if (area > availableArea)
                    Console.WriteLine("Too big");
                 */

                result.Clear();

                float variableSize = (float)(area / fixedSize);
                float pos = 0f;

                for (int j = firstIndex; j <= i; j++) {
                    var item = Items[j];
                    double itemArea = GetItemValue(item) * scaleRatio;
                    float itemSize = (float)(itemArea / variableSize);
                    RectangleF itemRect;

                    if (packVertically) {
                        itemRect = new RectangleF(
                            rectangle.X, rectangle.Y + pos,
                            variableSize, itemSize
                        );
                    } else {
                        itemRect = new RectangleF(
                            rectangle.X + pos, rectangle.Y,
                            itemSize, variableSize
                        );
                    }

                    result.Add(new LayoutItem {
                        Item = item,
                        Rectangle = itemRect
                    });

                    pos += itemSize;
                    aspectRatio = ComputeAspectRatio(itemRect.Width, itemRect.Height);
                    if ((lastAspectRatio.HasValue) && (lastAspectRatio.Value < aspectRatio))
                        Console.WriteLine("Worse");
                }
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
            ComputeLayoutGroup(0, Items.Count - 1, totalRect, scaleRatio, buffer, out resultAspect);

            Layout.Clear();
            Layout.Add(new LayoutGroup {
                Rectangle = totalRect,
                Items = buffer.ToArray()
            });

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
            var g = e.Graphics;
            g.Clear(BackColor);

            using (var textBrush = new SolidBrush(ForeColor))
            foreach (var group in Layout) {
                foreach (var item in group.Items) {
                    var itemColor = SelectItemColor(item.Item);

                    using (var brush = new SolidBrush(itemColor))
                        g.FillRectangle(brush, item.Rectangle);

                    g.DrawString(GetItemText(item.Item), Font, textBrush, item.Rectangle);
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
            base.GetItemValue = (sgn) => sgn.BytesRequested;
            base.GetItemText = (sgn) => sgn.Key.FunctionName;
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
