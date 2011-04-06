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
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace HeapProfiler {
    public partial class CustomTooltip : Form {
        public readonly ITooltipOwner Owner;
        public ITooltipContent Content;

        protected VisualStyleRenderer BackgroundRenderer;

        public CustomTooltip (ITooltipOwner owner) {
            Owner = owner;

            SetStyle(
                ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | 
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
                true
            );
            SetStyle(
                ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, false
            );

            try {
                BackgroundRenderer = new VisualStyleRenderer(VisualStyleElement.ToolTip.Standard.Normal);
            } catch {
                BackgroundRenderer = null;
            }

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

        protected void UpdateRegion () {
            if (BackgroundRenderer == null)
                return;
            try {
                if (BackgroundRenderer.IsBackgroundPartiallyTransparent())
                    using (var g = this.CreateGraphics())
                        this.Region = BackgroundRenderer.GetBackgroundRegion(g, ClientRectangle);
            } catch {
            }
        }

        protected override void OnShown (EventArgs e) {
            UpdateRegion();

            base.OnShown(e);
        }

        protected override void OnSizeChanged (EventArgs e) {
            UpdateRegion();

            base.OnSizeChanged(e);
        }

        protected override void OnPaint (PaintEventArgs e) {
            base.OnPaint(e);

            if (BackgroundRenderer == null) {
                e.Graphics.Clear(SystemColors.Info);
            } else {
                e.Graphics.Clear(SystemColors.Window);
                try {
                    BackgroundRenderer.DrawBackground(e.Graphics, ClientRectangle);
                } catch {
                }
            }

            Content.Render(e.Graphics);
        }

        protected override void OnPaintBackground (PaintEventArgs e) {
        }

        protected bool AdjustMouseEvent (ref MouseEventArgs e) {
            var adjustedPos = Owner.PointToClient(PointToScreen(e.Location));

            e = new MouseEventArgs(
                e.Button,
                e.Clicks,
                adjustedPos.X,
                adjustedPos.Y,
                e.Delta
            );

            return Owner.ClientRectangle.Contains(adjustedPos);
        }

        protected override void OnMouseDown (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Owner.MouseDown(e);
            else
                Hide();
        }

        protected override void OnMouseMove (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Owner.MouseMove(e);
            else
                Hide();
        }

        protected override void OnMouseUp (MouseEventArgs e) {
            if (AdjustMouseEvent(ref e))
                Owner.MouseUp(e);
            else
                Hide();
        }

        private void DeltaTooltip_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();
        }
    }

    public interface ITooltipOwner {
        void MouseDown (MouseEventArgs e);
        void MouseMove (MouseEventArgs e);
        void MouseUp (MouseEventArgs e);

        Point PointToClient (Point screenPoint);

        Rectangle ClientRectangle {
            get;
        }
    }

    public interface ITooltipContent {
        void Render (Graphics g);
    }
}
