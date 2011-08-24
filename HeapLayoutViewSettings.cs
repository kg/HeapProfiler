using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeapProfiler {
    public partial class HeapLayoutViewSettings : UserControl {
        public event EventHandler FindNextAllocation;
        public event EventHandler BytesPerPixelChanged;

        public HeapLayoutViewSettings () {
            InitializeComponent();
        }

        private void JumpToNext_Click (object sender, EventArgs e) {
            if (FindNextAllocation != null)
                FindNextAllocation(this, EventArgs.Empty);
        }

        public int BytesPerPixel {
            get {
                var value = Zoom.Maximum - Zoom.Value;

                if (value == 0)
                    return 1;
                else
                    return 2 << (value - 1);
            }
        }

        private void Zoom_Scroll (object sender, EventArgs e) {
            if (BytesPerPixelChanged != null)
                BytesPerPixelChanged(this, EventArgs.Empty);
        }
    }
}
