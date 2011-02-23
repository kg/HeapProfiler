using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace HeapProfiler {
    public partial class ActivityIndicator : UserControl {
        public class Item : IDisposable {
            readonly ActivityIndicator Owner;
            internal readonly Label Label;
            internal readonly ProgressBar ProgressBar;

            internal Item (ActivityIndicator owner) {
                Owner = owner;
                Label = new Label {
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Visible = false
                };
                ProgressBar = new ProgressBar {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 50,
                    Visible = false
                };
            }

            public string Status {
                get {
                    return Label.Text;
                }
                set {
                    Label.Text = value;
                }
            }

            public int? Progress {
                get {
                    if (ProgressBar.Style == ProgressBarStyle.Marquee)
                        return null;
                    else
                        return ProgressBar.Value;
                }
                set {
                    if (value.HasValue) {
                        var v = value.Value;
                        if (v < ProgressBar.Maximum - 1)
                            ProgressBar.Value = v + 1;
                        ProgressBar.Value = v;
                        ProgressBar.Style = ProgressBarStyle.Continuous;
                    } else {
                        ProgressBar.Style = ProgressBarStyle.Marquee;
                    }
                }
            }

            public int Maximum {
                get {
                    return ProgressBar.Maximum;
                }
                set {
                    ProgressBar.Maximum = value;
                }
            }

            public void Dispose () {
                Owner.RemoveItem(this);
            }
        }

        public event EventHandler PreferredSizeChanged;

        protected WaitHandle WaitHandle = new AutoResetEvent(false);
        protected readonly HashSet<Item> _Items = new HashSet<Item>();
        protected bool RelayoutPending = false;

        public ActivityIndicator () {
        }

        protected override void Dispose (bool disposing) {
            foreach (var item in _Items) {
                item.Label.Dispose();
                item.ProgressBar.Dispose();
            }

            WaitHandle.Close();

            base.Dispose(disposing);
        }

        protected override void OnVisibleChanged (EventArgs e) {
            if (Visible)
                OnPreferredSizeChanged();

            base.OnVisibleChanged(e);
        }

        protected override void OnFontChanged (EventArgs e) {
            OnPreferredSizeChanged();

            base.OnFontChanged(e);
        }

        public Item AddItem (string description) {
            var result = new Item(this);
            result.Status = description;
            _Items.Add(result);
            Controls.Add(result.Label);
            Controls.Add(result.ProgressBar);
            OnPreferredSizeChanged();
            return result;
        }

        public IEnumerable<Item> Items {
            get {
                return _Items;
            }
        }

        public void RemoveItem (Item item) {
            Controls.Remove(item.Label);
            Controls.Remove(item.ProgressBar);
            _Items.Remove(item);
            OnPreferredSizeChanged();
        }

        public override Size GetPreferredSize (Size size) {
            if (_Items.Count == 0)
                return new Size(size.Width, 0);

            var progressBarSize = Items.First().ProgressBar.GetPreferredSize(size);

            size.Width = Math.Max(
                size.Width, progressBarSize.Width + (
                    from i in _Items select i.Label.GetPreferredSize(size).Width
                ).Max()
            );
            size.Height = Math.Min(
                size.Height, progressBarSize.Height * _Items.Count
            );

            return size;
        }

        protected void Relayout () {
            int y = 0;
            foreach (var item in Items) {
                var labelSize = item.Label.GetPreferredSize(ClientSize);
                var progressSize = item.ProgressBar.GetPreferredSize(ClientSize);

                item.Label.SetBounds(
                    0, y,
                    Math.Max(ClientSize.Width - progressSize.Width, labelSize.Width),
                    Math.Max(labelSize.Height, progressSize.Height)
                );
                item.ProgressBar.SetBounds(
                    item.Label.Width, y,
                    progressSize.Width, progressSize.Height
                );

                item.Label.Visible = true;
                item.ProgressBar.Visible = true;
            }

            Invalidate(true);
        }

        protected override void OnSizeChanged (EventArgs e) {
            Relayout();

            base.OnSizeChanged(e);
        }

        protected void DoPendingRelayout () {
            if (!RelayoutPending)
                return;

            RelayoutPending = false;
            Relayout();
        }

        protected void OnPreferredSizeChanged () {
            var oldSize = ClientSize;

            if (PreferredSizeChanged != null)
                PreferredSizeChanged(this, EventArgs.Empty);

            if (ClientSize == oldSize && !RelayoutPending) {
                RelayoutPending = true;
                ThreadPool.RegisterWaitForSingleObject(
                    WaitHandle, 
                    (s, t) => {
                        BeginInvoke((Action)DoPendingRelayout);
                    }, null, 50, true
                );
            }
        }
    }
}
