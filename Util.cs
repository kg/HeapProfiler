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
using System.Linq;
using System.Text;
using System.Drawing;

namespace HeapProfiler {
    class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class {

        public bool Equals (T x, T y) {
            return (x == y);
        }

        public int GetHashCode (T obj) {
            return obj.GetHashCode();
        }
    }

    public class ScratchBuffer : IDisposable {
        public struct Region : IDisposable {
            public readonly Graphics Graphics;
            public readonly Bitmap Bitmap;

            public readonly Graphics DestinationGraphics;
            public readonly Rectangle DestinationRegion;

            bool Cancelled;

            public Region (ScratchBuffer sb, Graphics graphics, Rectangle region) {
                var minWidth = (int)Math.Ceiling(region.Width / 16.0f) * 16;
                var minHeight = (int)Math.Ceiling(region.Height / 16.0f) * 16;

                var needNewBitmap =
                    (sb._ScratchBuffer == null) || (sb._ScratchBuffer.Width < minWidth) ||
                    (sb._ScratchBuffer.Height < minHeight);

                if (needNewBitmap && sb._ScratchBuffer != null)
                    sb._ScratchBuffer.Dispose();
                if (needNewBitmap && sb._ScratchGraphics != null)
                    sb._ScratchGraphics.Dispose();

                if (needNewBitmap) {
                    Bitmap = sb._ScratchBuffer = new Bitmap(
                        minWidth, minHeight, graphics
                    );
                    Graphics = sb._ScratchGraphics = Graphics.FromImage(Bitmap);
                } else {
                    Bitmap = sb._ScratchBuffer;
                    Graphics = sb._ScratchGraphics;
                }

                Graphics.ResetTransform();
                Graphics.TranslateTransform(-region.X, -region.Y, System.Drawing.Drawing2D.MatrixOrder.Prepend);
                Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                DestinationGraphics = graphics;
                DestinationRegion = region;

                Cancelled = false;
            }

            public void Cancel () {
                Cancelled = true;
            }

            public void Dispose () {
                if (Cancelled)
                    return;

                var oldCompositing = DestinationGraphics.CompositingMode;
                DestinationGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                var oldInterpolation = DestinationGraphics.InterpolationMode;
                DestinationGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                var oldSmoothing = DestinationGraphics.SmoothingMode;
                DestinationGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                DestinationGraphics.DrawImageUnscaledAndClipped(
                    Bitmap, DestinationRegion
                );

                DestinationGraphics.CompositingMode = oldCompositing;
                DestinationGraphics.InterpolationMode = oldInterpolation;
                DestinationGraphics.SmoothingMode = oldSmoothing;

                Cancelled = true;
            }
        }

        protected Bitmap _ScratchBuffer = null;
        protected Graphics _ScratchGraphics = null;

        public ScratchBuffer () {
        }

        public Region Get (Graphics graphics, Rectangle region) {
            return new Region(
                this, graphics, region
            );
        }

        public Region Get (Graphics graphics, RectangleF region) {
            return new Region(
                this, graphics, new Rectangle(
                    (int)Math.Floor(region.X), (int)Math.Floor(region.Y),
                    (int)Math.Ceiling(region.Width), (int)Math.Ceiling(region.Height)
                )
            );
        }

        public void Dispose () {
            if (_ScratchGraphics != null) {
                _ScratchGraphics.Dispose();
                _ScratchGraphics = null;
            }

            if (_ScratchBuffer != null) {
                _ScratchBuffer.Dispose();
                _ScratchBuffer = null;
            }
        }
    }
}
