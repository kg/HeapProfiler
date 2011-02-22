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
    public class ModuleInfo {
        public bool Filtered = false;
        public string ModuleName;
        public string SymbolType;
        public string SymbolPath;
        public int References = 0;

        public override string ToString () {
            return ModuleName;
        }
    }

    public class DeltaInfo {
        public struct RenderParams : IDisposable {
            public bool IsSelected, IsExpanded;
            public Rectangle Region;
            public Color BackgroundColor;
            public Brush BackgroundBrush, TextBrush;
            public Brush ShadeBrush, FunctionHighlightBrush;
            public Brush ElideBackgroundBrush, ElideTextBrush;
            public StringFormat StringFormat;
            public Font Font;
            public string FunctionFilter;
            public float LineHeight;

            public void Dispose () {
                BackgroundBrush.Dispose();
                TextBrush.Dispose();
                ShadeBrush.Dispose();
                if (FunctionHighlightBrush != null)
                    FunctionHighlightBrush.Dispose();
                if (ElideBackgroundBrush != null)
                    ElideBackgroundBrush.Dispose();
                if (ElideTextBrush != null)
                    ElideTextBrush.Dispose();
                StringFormat.Dispose();
            }
        }

        public bool Added;
        public int BytesDelta, OldBytes, NewBytes, NewCount;
        public int? CountDelta, OldCount;
        public TracebackInfo Traceback;

        public float Render (Graphics g, ref RenderParams rp) {
            char[] functionEndChars = new char[] { '@', '+' };

            g.ResetClip();
            g.Clear(rp.BackgroundColor);
            g.FillRectangle(rp.ShadeBrush, 0, rp.Region.Y, rp.Region.Width, rp.LineHeight - 1);

            var text = ToString(false);
            var y = 0.0f;

            g.DrawString(text, rp.Font, rp.TextBrush, 0.0f, rp.Region.Top + y, rp.StringFormat);
            y += g.MeasureString(text, rp.Font, rp.Region.Width, rp.StringFormat).Height;

            int f = 0;
            foreach (var frame in Traceback.Frames) {
                text = frame.ToString();

                var layoutRect = new RectangleF(
                    0.0f, rp.Region.Top + y, rp.Region.Width, rp.LineHeight
                );
                Region[] fillRegions = null;

                if (rp.FunctionFilter == frame.Function) {
                    var startIndex = text.IndexOf('!') + 1;
                    var endIndex = text.LastIndexOfAny(functionEndChars);
                    rp.StringFormat.SetMeasurableCharacterRanges(new[] { 
                        new CharacterRange(startIndex, endIndex - startIndex)
                    });

                    fillRegions = g.MeasureCharacterRanges(
                        text, rp.Font, layoutRect, rp.StringFormat
                    );

                    foreach (var fillRegion in fillRegions) {
                        g.FillRegion(rp.FunctionHighlightBrush, fillRegion);
                        g.ExcludeClip(fillRegion);
                    }
                }

                g.DrawString(text, rp.Font, rp.TextBrush, layoutRect, rp.StringFormat);
                if (fillRegions != null) {
                    bool first = true;
                    foreach (var fillRegion in fillRegions) {
                        g.SetClip(fillRegion, first ?
                            System.Drawing.Drawing2D.CombineMode.Replace :
                            System.Drawing.Drawing2D.CombineMode.Union
                        );
                        first = false;
                    }
                    g.DrawString(text, rp.Font, rp.TextBrush, layoutRect, rp.StringFormat);
                    g.ResetClip();
                }

                y += g.MeasureString(text, rp.Font, rp.Region.Width, rp.StringFormat).Height;

                f += 1;
                if ((f == 2) && !rp.IsExpanded) {
                    if (Traceback.Frames.Length > 2) {
                        rp.StringFormat.Alignment = StringAlignment.Far;

                        var elideString = String.Format(
                            "(+{0} frame(s))", Traceback.Frames.Length - 2
                        );
                        rp.StringFormat.SetMeasurableCharacterRanges(
                            new[] { new CharacterRange(0, elideString.Length) }
                        );

                        var regions = g.MeasureCharacterRanges(elideString, rp.Font, layoutRect, rp.StringFormat);
                        foreach (var region in regions)
                            g.FillRegion(rp.ElideBackgroundBrush, region);

                        g.DrawString(elideString, rp.Font, rp.ElideTextBrush, layoutRect, rp.StringFormat);

                        rp.StringFormat.Alignment = StringAlignment.Near;
                    }

                    break;
                }
            }

            return y;
        }

        public string ToString (bool includeTraceback) {
            var result = String.Format(
                "{0} {1} byte(s) ({2} - {3})",
                Added ? "+" : "-",
                BytesDelta, NewBytes, OldBytes
            );

            if (includeTraceback)
                result += Environment.NewLine + Traceback.ToString();

            return result;
        }

        public override string ToString () {
            return ToString(true);
        }
    }

    public class TracebackInfo {
        public string TraceId;
        public TracebackFrame[] Frames;
        public HashSet<string> Functions;
        public HashSet<string> Modules;

        public override string ToString () {
            var sb = new StringBuilder();

            foreach (var frame in Frames) {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(frame.ToString());
            }

            return sb.ToString();
        }
    }

    public struct TracebackFrame {
        public string Module;
        public string Function;
        public UInt32 Offset;
        public UInt32? Offset2;
        public string SourceFile;
        public int? SourceLine;

        public override string ToString () {
            if ((SourceFile != null) && (SourceLine.HasValue))
                return String.Format("{0}!{1} line {2}", Module, Function, SourceLine);
            else if (Offset2.HasValue)
                return String.Format("{0}!{1}@{2:x8}", Module, Function, Offset2.Value);
            else
                return String.Format("{0}!{1}+{2:x8}", Module, Function, Offset);
        }
    }
}
