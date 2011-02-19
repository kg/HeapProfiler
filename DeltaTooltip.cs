using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeapProfiler {
    public partial class DeltaTooltip : Form {
        public readonly DeltaHistogram Histogram;
        public DeltaInfo Delta;
        public DeltaInfo.RenderParams RenderParams;

        public DeltaTooltip (DeltaHistogram histogram) {
            Histogram = histogram;

            SetStyle(
                ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | 
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, 
                true
            );
            SetStyle(
                ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, false
            );

            InitializeComponent();
        }

        protected override bool ShowWithoutActivation {
            get {
                return true;
            }
        }

        protected override CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                // CS_DROPSHADOW | CS_HREDRAW | CS_VREDRAW
                cp.ClassStyle = 0x00020000 | 0x0002 | 0x0001;
                return cp;
            }
        }

        protected override void OnPaint (PaintEventArgs e) {
            base.OnPaint(e);

            Delta.Render(e.Graphics, ref RenderParams);
        }

        protected override void OnPaintBackground (PaintEventArgs e) {
        }

        protected bool AdjustMouseEvent (ref MouseEventArgs e) {
            var adjustedPos = Histogram.PointToClient(PointToScreen(e.Location));

            e = new MouseEventArgs(
                e.Button,
                e.Clicks,
                adjustedPos.X,
                adjustedPos.Y,
                e.Delta
            );

            return Histogram.ClientRectangle.Contains(adjustedPos);
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Histogram.TooltipMouseDown(e);
            else
                Hide();
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Histogram.TooltipMouseMove(e);
            else
                Hide();
        }

        protected override void OnMouseUp (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Histogram.TooltipMouseUp(e);
            else
                Hide();
        }

        private void DeltaTooltip_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();
        }
    }
}
