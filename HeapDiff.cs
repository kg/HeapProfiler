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
using System.Linq;
using System.Diagnostics;
using Squared.Task.Data.Mapper;
using System.Web.Script.Serialization;
using Squared.Util.Bind;
using Squared.Util.RegexExtensions;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Globalization;
using Squared.Util;
using Squared.Task;
using Squared.Task.IO;

namespace HeapProfiler {
    public class ModuleInfo {
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

        protected string FormattedBytesCache = null;

        public string FormattedBytesDelta {
            get {
                if (FormattedBytesCache == null)
                    FormattedBytesCache = FileSize.Format(
                        BytesDelta * (Added ? 1 : -1)
                    );

                return FormattedBytesCache;
            }
        }

        public float Render (Graphics g, ref RenderParams rp) {
            g.ResetClip();
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
                "{0} ({1} - {2})",
                FormattedBytesDelta, OldBytes, NewBytes
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
        public UInt32 TraceId;
        public TracebackFrame[] Frames;
        public NameTable Functions;
        public NameTable Modules;

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

        public TracebackFrame (UInt32 rawOffset) {
            Module = Function = "???";
            Offset = 0;
            SourceFile = null;
            SourceLine = null;
            Offset2 = rawOffset;
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
        public const int ProgressInterval = 200;

        public readonly string Filename;
        public readonly Dictionary<string, ModuleInfo> Modules;
        public readonly NameTable FunctionNames;
        public readonly List<DeltaInfo> Deltas;
        public readonly Dictionary<UInt32, TracebackInfo> Tracebacks;

        protected HeapDiff (
            string filename, Dictionary<string, ModuleInfo> modules,
            NameTable functionNames, List<DeltaInfo> deltas,
            Dictionary<UInt32, TracebackInfo> tracebacks
        ) {
            Filename = filename;
            Modules = modules;
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

            var modules = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);
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
                    var moduleName = moduleNames[m.Groups[groupModule].Value];
                    var symbolType = symbolTypes[m.Groups[groupSymbolType].Value];

                    var info = new ModuleInfo {
                        ModuleName = moduleName,
                        SymbolType = symbolType,
                    };

                    if (lr.ReadLine(out line)) {
                        if (!regexes.DiffModule.IsMatch(ref line)) {
                            info.SymbolPath = line.ToString().Trim();
                        } else {
                            goto retryFromHere;
                        }
                    }

                    modules[moduleName] = info;
                } else if (regexes.BytesDelta.TryMatch(ref line, out m)) {
                    var traceId = UInt32.Parse(m.Groups[groupTraceId].Value, NumberStyles.HexNumber);
                    var info = new DeltaInfo {
                        Added = (m.Groups[groupType].Value == "+"),
                        BytesDelta = int.Parse(m.Groups[groupDeltaBytes].Value),
                        NewBytes = int.Parse(m.Groups[groupNewBytes].Value),
                        OldBytes = int.Parse(m.Groups[groupOldBytes].Value),
                        NewCount = int.Parse(m.Groups[groupNewCount].Value),
                    };

                    if (lr.ReadLine(out line)) {
                        if (regexes.CountDelta.TryMatch(ref line, out m)) {
                            info.OldCount = int.Parse(m.Groups[groupOldCount].Value);
                            info.CountDelta = int.Parse(m.Groups[groupCountDelta].Value);
                        }
                    }

                    bool readingLeadingWhitespace = true;

                    var frames = new List<TracebackFrame>();
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

                            if (!modules.ContainsKey(moduleName)) {
                                modules[moduleName] = new ModuleInfo {
                                    ModuleName = moduleName,
                                    SymbolType = "Unknown",
                                    References = 1
                                };
                            } else {
                                modules[moduleName].References += 1;
                            }

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
                            i--;
                            break;
                        }
                    }

                    if (tracebacks.ContainsKey(traceId)) {
                        info.Traceback = tracebacks[traceId];
                        Console.WriteLine("Duplicate traceback for id {0}!", traceId);
                    } else {
                        info.Traceback = tracebacks[traceId] = new TracebackInfo {
                            TraceId = traceId,
                            Frames = frames.ToArray(),
                            Modules = itemModules,
                            Functions = itemFunctions
                        };
                    }

                    deltas.Add(info);
                } else {
                }
            }

            foreach (var key in modules.Keys.ToArray()) {
                if (modules[key].References == 0)
                    modules.Remove(key);
            }

            var result = new HeapDiff(
                filename, modules, functionNames, deltas, tracebacks
            );
            yield return new Result(result);
        }
    }
}
