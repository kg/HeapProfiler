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
using System.Text;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using Squared.Task;
using Squared.Task.IO;
using Squared.Data.Mangler;

namespace HeapProfiler {
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
        public UInt32 TracebackID;
        public TracebackInfo Traceback;

        protected string FormattedBytesCache = null;

        public string FormattedBytesDelta {
            get {
                if (FormattedBytesCache == null)
                    FormattedBytesCache = 
                        ((Added) ? "+" : "-") +
                        FileSize.Format(BytesDelta);

                return FormattedBytesCache;
            }
        }

        public float Render (Graphics g, ref RenderParams rp) {
            return Traceback.Render(g, ref rp, ToString(false));
        }

        public string ToString (bool includeTraceback) {
            var result = String.Format(
                "{0} ({1} - {2}) (from {3} to {4} alloc(s))",
                FormattedBytesDelta, OldBytes, NewBytes, OldCount, NewCount
            );

            if (includeTraceback)
                result += Environment.NewLine + Traceback.ToString();

            return result;
        }

        public override string ToString () {
            return ToString(true);
        }
    }

    public class DeltaInfoTooltipContent : ITooltipContent {
        public readonly DeltaInfo Delta;
        public DeltaInfo.RenderParams RenderParams;

        public DeltaInfoTooltipContent (DeltaInfo delta, DeltaInfo.RenderParams renderParams) {
            Delta = delta;
            RenderParams = renderParams;
        }

        public void Render (Graphics g) {
            Delta.Render(g, ref RenderParams);
        }

        public Size Measure (Graphics g) {
            var font = RenderParams.Font;
            var sf = RenderParams.StringFormat;

            var width = (int)Math.Ceiling(g.MeasureString(Delta.ToString(true), font, 99999, sf).Width);
            var lineHeight = g.MeasureString("AaBbYyZz", font, width, sf).Height;
            return new Size(
                width, (int)Math.Ceiling(lineHeight * (Delta.Traceback.Frames.Count + 1))
            );
        }
    }

    public class TracebackInfo {
        public UInt32 TraceId;
        public ArraySegment<TracebackFrame> Frames;
        public NameTable Functions;
        public NameTable Modules;

        public float Render (Graphics g, ref DeltaInfo.RenderParams rp, string headerText) {
            var lineHeight = g.MeasureString(
                "AaBbYyZz", rp.Font, rp.Region.Width, rp.StringFormat
            ).Height;

            g.ResetClip();
            g.FillRectangle(rp.ShadeBrush, 0, rp.Region.Y, rp.Region.Width, lineHeight - 1);

            var y = 0.0f;

            g.DrawString(headerText, rp.Font, rp.TextBrush, 0.0f, rp.Region.Top + y, rp.StringFormat);
            y += g.MeasureString(headerText, rp.Font, rp.Region.Width, rp.StringFormat).Height;

            int f = 0;
            for (int i = 0, c = Frames.Count, o = Frames.Offset; i < c; i++) {
                var frame = Frames.Array[i + o];
                var text = frame.ToString();

                var layoutRect = new RectangleF(
                    0.0f, rp.Region.Top + y, rp.Region.Width, lineHeight
                );
                Region[] fillRegions = null;

                if ((rp.FunctionFilter != null) && (rp.FunctionFilter == frame.Function)) {
                    var startIndex = text.IndexOf(rp.FunctionFilter);
                    var endIndex = startIndex + rp.FunctionFilter.Length;

                    if ((endIndex > startIndex) && (startIndex >= 0)) {
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
                    if (Frames.Count > 2) {
                        rp.StringFormat.Alignment = StringAlignment.Far;

                        var elideString = String.Format(
                            "(+{0} frame(s))", Frames.Count - 2
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

        public override string ToString () {
            var sb = new StringBuilder();

            for (int i = 0, c = Frames.Count, o = Frames.Offset; i < c; i++) {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(Frames.Array[i + o].ToString());
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

        public TracebackFrame (UInt32 rawOffset) {
            Module = Function = "???";
            Offset = 0;
            SourceFile = null;
            SourceLine = null;
            Offset2 = rawOffset;
        }

        static string ReadString (BinaryReader br) {
            if (br.ReadBoolean())
                return br.ReadString();

            return null;
        }

        static void WriteString (BinaryWriter bw, string str) {
            bool isNull = str == null;
            bw.Write(!isNull);
            if (!isNull)
                bw.Write(str);
        }

        [TangleSerializer]
        static void Serialize (ref SerializationContext context, ref TracebackFrame input) {
            var bw = new BinaryWriter(context.Stream, Encoding.UTF8);

            bw.Write(input.Offset);
            bw.Write(input.Offset2.HasValue);
            if (input.Offset2.HasValue)
                bw.Write(input.Offset2.Value);

            WriteString(bw, input.Module);
            WriteString(bw, input.Function);
            WriteString(bw, input.SourceFile);

            bw.Write(input.SourceLine.HasValue);
            if (input.SourceLine.HasValue)
                bw.Write(input.SourceLine.Value);

            bw.Flush();
        }

        [TangleDeserializer]
        static void Deserialize (ref DeserializationContext context, out TracebackFrame output) {
            var br = new BinaryReader(context.Stream, Encoding.UTF8);

            output = new TracebackFrame();

            output.Offset = br.ReadUInt32();
            if (br.ReadBoolean())
                output.Offset2 = br.ReadUInt32();

            output.Module = ReadString(br);
            output.Function = ReadString(br);
            output.SourceFile = ReadString(br);

            if (br.ReadBoolean())
                output.SourceLine = br.ReadInt32();
        }

        public override string ToString () {
            if ((SourceFile != null) && (SourceLine.HasValue))
                return String.Format("{0}!{1} line {2} ({3})", Module, Function, SourceLine, Path.GetFileName(SourceFile));
            else if (Offset2.HasValue)
                return String.Format("{0}!{1}@{2:x8}", Module, Function, Offset2.Value);
            else
                return String.Format("{0}!{1}+{2:x8}", Module, Function, Offset);
        }
    }

    public class HeapDiff {
        public const int ProgressInterval = 100;

        public readonly string Filename;
        public readonly NameTable Modules;
        public readonly NameTable FunctionNames;
        public readonly List<DeltaInfo> Deltas;
        public readonly Dictionary<UInt32, TracebackInfo> Tracebacks;

        public HeapDiff (
            string filename, NameTable moduleNames,
            NameTable functionNames, List<DeltaInfo> deltas,
            Dictionary<UInt32, TracebackInfo> tracebacks
        ) {
            Filename = filename;
            Modules = moduleNames;
            FunctionNames = functionNames;
            Deltas = deltas;
            Tracebacks = tracebacks;
        }

        public static IEnumerator<object> FromFile (string filename, IProgressListener progress) {
            progress.Status = "Loading diff...";

            Future<string> fText;

            // We could stream the lines in from the IO thread while we parse them, but this
            //  part of the load is usually pretty quick even on a regular hard disk, and
            //  loading the whole diff at once eliminates some context switches
            using (var fda = new FileDataAdapter(
                filename, FileMode.Open,
                FileAccess.Read, FileShare.Read, 1024 * 128
            )) {
                var fBytes = fda.ReadToEnd();
                yield return fBytes;

                fText = Future.RunInThread(
                    () => Encoding.ASCII.GetString(fBytes.Result)
                );
                yield return fText;
            }

            yield return fText;
            var lr = new LineReader(fText.Result);
            LineReader.Line line;

            progress.Status = "Parsing diff...";

            var frames = new List<TracebackFrame>();
            var moduleNames = new NameTable(StringComparer.Ordinal);
            var symbolTypes = new NameTable(StringComparer.Ordinal);
            var functionNames = new NameTable(StringComparer.Ordinal);
            var deltas = new List<DeltaInfo>();
            var tracebacks = new Dictionary<UInt32, TracebackInfo>();

            var regexes = new Regexes();

            // Regex.Groups[string] does an inefficient lookup, so we do that lookup once here
            int groupModule = regexes.DiffModule.GroupNumberFromName("module");
            int groupSymbolType = regexes.DiffModule.GroupNumberFromName("symbol_type");
            int groupTraceId = regexes.BytesDelta.GroupNumberFromName("trace_id");
            int groupType = regexes.BytesDelta.GroupNumberFromName("type");
            int groupDeltaBytes = regexes.BytesDelta.GroupNumberFromName("delta_bytes");
            int groupNewBytes = regexes.BytesDelta.GroupNumberFromName("new_bytes");
            int groupOldBytes = regexes.BytesDelta.GroupNumberFromName("old_bytes");
            int groupNewCount = regexes.BytesDelta.GroupNumberFromName("new_count");
            int groupOldCount = regexes.CountDelta.GroupNumberFromName("old_count");
            int groupCountDelta = regexes.CountDelta.GroupNumberFromName("delta_count");
            int groupTracebackModule = regexes.TracebackFrame.GroupNumberFromName("module");
            int groupTracebackFunction = regexes.TracebackFrame.GroupNumberFromName("function");
            int groupTracebackOffset = regexes.TracebackFrame.GroupNumberFromName("offset");
            int groupTracebackOffset2 = regexes.TracebackFrame.GroupNumberFromName("offset2");
            int groupTracebackPath = regexes.TracebackFrame.GroupNumberFromName("path");
            int groupTracebackLine = regexes.TracebackFrame.GroupNumberFromName("line");

            int i = 0;
            while (lr.ReadLine(out line)) {
                if (i % ProgressInterval == 0) {
                    progress.Maximum = lr.Length;
                    progress.Progress = lr.Position;

                    // Suspend processing until any messages in the windows message queue have been processed
                    yield return new Yield();
                }

            retryFromHere:

                Match m;
                if (regexes.DiffModule.TryMatch(ref line, out m)) {
                    moduleNames.Add(m.Groups[groupModule].Value);
                } else if (regexes.BytesDelta.TryMatch(ref line, out m)) {
                    var traceId = UInt32.Parse(m.Groups[groupTraceId].Value, NumberStyles.HexNumber);
                    var info = new DeltaInfo {
                        Added = (m.Groups[groupType].Value == "+"),
                        BytesDelta = int.Parse(m.Groups[groupDeltaBytes].Value, NumberStyles.HexNumber),
                        NewBytes = int.Parse(m.Groups[groupNewBytes].Value, NumberStyles.HexNumber),
                        OldBytes = int.Parse(m.Groups[groupOldBytes].Value, NumberStyles.HexNumber),
                        NewCount = int.Parse(m.Groups[groupNewCount].Value, NumberStyles.HexNumber),
                    };

                    if (lr.ReadLine(out line)) {
                        if (regexes.CountDelta.TryMatch(ref line, out m)) {
                            info.OldCount = int.Parse(m.Groups[groupOldCount].Value, NumberStyles.HexNumber);
                            info.CountDelta = int.Parse(m.Groups[groupCountDelta].Value, NumberStyles.HexNumber);
                        }
                    }

                    bool readingLeadingWhitespace = true, doRetry = false;

                    frames.Clear();
                    var itemModules = new NameTable(StringComparer.Ordinal);
                    var itemFunctions = new NameTable(StringComparer.Ordinal);

                    while (lr.ReadLine(out line)) {
                        if (line.ToString().Trim().Length == 0) {
                            if (readingLeadingWhitespace)
                                continue;
                            else
                                break;
                        } else if (regexes.TracebackFrame.TryMatch(ref line, out m)) {
                            readingLeadingWhitespace = false;

                            var moduleName = moduleNames[m.Groups[groupTracebackModule].Value];
                            itemModules.Add(moduleName);

                            var functionName = functionNames[m.Groups[groupTracebackFunction].Value];
                            itemFunctions.Add(functionName);

                            var frame = new TracebackFrame {
                                Module = moduleName,
                                Function = functionName,
                                Offset = UInt32.Parse(m.Groups[groupTracebackOffset].Value, NumberStyles.HexNumber)
                            };
                            if (m.Groups[groupTracebackOffset2].Success)
                                frame.Offset2 = UInt32.Parse(m.Groups[groupTracebackOffset2].Value, NumberStyles.HexNumber);

                            if (m.Groups[groupTracebackPath].Success)
                                frame.SourceFile = m.Groups[groupTracebackPath].Value;

                            if (m.Groups[groupTracebackLine].Success)
                                frame.SourceLine = int.Parse(m.Groups[groupTracebackLine].Value);

                            frames.Add(frame);
                        } else {
                            // We hit the beginning of a new allocation, so make sure it gets parsed
                            doRetry = true;
                            break;
                        }
                    }

                    if (tracebacks.ContainsKey(traceId)) {
                        info.Traceback = tracebacks[traceId];
                        Console.WriteLine("Duplicate traceback for id {0}!", traceId);
                    } else {
                        var frameArray = ImmutableArrayPool<TracebackFrame>.Allocate(frames.Count);
                        frames.CopyTo(frameArray.Array, frameArray.Offset);

                        info.Traceback = tracebacks[traceId] = new TracebackInfo {
                            TraceId = traceId,
                            Frames = frameArray,
                            Modules = itemModules,
                            Functions = itemFunctions
                        };
                    }

                    deltas.Add(info);

                    if (doRetry)
                        goto retryFromHere;
                } else if (line.StartsWith("//")) {
                    // Comment, ignore it
                } else if (line.StartsWith("Total increase") || line.StartsWith("Total decrease")) {
                    // Ignore this too
                } else if (line.StartsWith("         ") && (line.EndsWith(".pdb"))) {
                    // Symbol path for a module, ignore it
                } else {
                    Console.WriteLine("Unrecognized diff content: {0}", line.ToString());
                }
            }

            var result = new HeapDiff(
                filename, moduleNames, functionNames, deltas, tracebacks
            );
            yield return new Result(result);
        }
    }
}
