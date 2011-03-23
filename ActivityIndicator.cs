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
using System.Threading;

namespace HeapProfiler {
    public partial class ActivityIndicator : UserControl {
        public const int RefreshDelay = 10;

        public class CountedItem : IProgressListener, IDisposable {
            public string Description;

            internal readonly ActivityIndicator Parent;
            internal Item Item = null;
            internal int Count = 0;

            public CountedItem (ActivityIndicator parent, string description) {
                Parent = parent;
                Description = description;
            }

            public CountedItem Increment () {
                Count += 1;
                if (Count == 1)
                    Item = Parent.AddItem(Description);

                return this;
            }

            public void Decrement () {
                Count -= 1;
                if (Count <= 0) {
                    Count = 0;
                    if (Item != null) {
                        Item.Dispose();
                        Item = null;
                    }
                }
            }

            public bool Active {
                get {
                    return (Count > 0);
                }
            }

            public string Status {
                set {
                    if (Item != null)
                        Item.Status = value;

                    Description = value;
                }
            }

            public int? Progress {
                set {
                    if (Item != null)
                        Item.Progress = value;
                }
            }

            public int Maximum {
                set {
                    if (Item != null)
                        Item.Maximum = value;
                }
            }

            void IDisposable.Dispose () {
                Decrement();
            }
        }

        public class Item : IDisposable, IProgressListener {
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
        protected bool RelayoutPending = false, SizeChangedPending = false;

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
                size.Height, (progressBarSize.Height * _Items.Count) +
                Math.Max(0, _Items.Count - 1) * 2
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
                    Math.Min(ClientSize.Width - progressSize.Width, labelSize.Width),
                    Math.Max(labelSize.Height, progressSize.Height)
                );
                item.ProgressBar.SetBounds(
                    ClientSize.Width - progressSize.Width, y,
                    progressSize.Width, progressSize.Height
                );

                item.Label.Visible = true;
                item.ProgressBar.Visible = true;

                y += Math.Max(labelSize.Height, progressSize.Height) + 2;
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

        protected void DoOnPreferredSizeChanged () {
            if (!SizeChangedPending)
                return;

            var oldSize = ClientSize;

            SizeChangedPending = false;
            if (PreferredSizeChanged != null)
                PreferredSizeChanged(this, EventArgs.Empty);

            if (ClientSize == oldSize && !RelayoutPending) {
                RelayoutPending = true;
                ThreadPool.RegisterWaitForSingleObject(
                    WaitHandle,
                    (s, t) => {
                        BeginInvoke((Action)DoPendingRelayout);
                    }, null, RefreshDelay, true
                );
            }
        }

        protected void OnPreferredSizeChanged () {
            if (PreferredSizeChanged != null && !SizeChangedPending) {
                SizeChangedPending = true;
                ThreadPool.RegisterWaitForSingleObject(
                    WaitHandle,
                    (s, t) => {
                        BeginInvoke((Action)DoOnPreferredSizeChanged);
                    }, null, RefreshDelay, true
                );
            }
        }
    }
}
