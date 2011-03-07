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
using System.Diagnostics;
using Squared.Task.Data.Mapper;
using System.Web.Script.Serialization;
using Squared.Util.Bind;
using Squared.Util.RegexExtensions;
using System.Text.RegularExpressions;

namespace HeapProfiler {
    public static class Regexes {
        public static Regex DiffModule = new Regex(
            @"DBGHELP: (?'module'.*?)( - )(?'symbol_type'[^\n\r]*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex SnapshotModule = new Regex(
            "//\\s*([A-Fa-f0-9]+)\\s+([A-Fa-f0-9]+)\\s+(?'module'[^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex BytesDelta = new Regex(
            @"(?'type'\+|\-)(\s+)(?'delta_bytes'[0-9A-Fa-f]+)(\s+)\((\s*)(?'new_bytes'[0-9A-Fa-f]*)(\s*)-(\s*)(?'old_bytes'[0-9A-Fa-f]*)\)(\s*)(?'new_count'[0-9A-Fa-f]+) allocs\t(BackTrace(?'trace_id'\w*))",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex CountDelta = new Regex(
            @"(?'type'\+|\-)(\s+)(?'delta_count'[0-9A-Fa-f]+)(\s+)\((\s*)(?'new_count'[0-9A-Fa-f]*)(\s*)-(\s*)(?'old_count'[0-9A-Fa-f]*)\)\t(BackTrace(?'trace_id'\w*))\tallocations",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex TracebackFrame = new Regex(
            @"\t(?'module'[^!]+)!(?'function'[^+]+)\+(?'offset'[0-9A-Fa-f]+)(\s*:\s*(?'offset2'[0-9A-Fa-f]+))?(\s*\(\s*(?'path'[^,]+),\s*(?'line'[0-9]*)\))?",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex Allocation = new Regex(
            @"(?'size'[0-9A-Fa-f]+)(\s*bytes\s*\+\s*)(?'overhead'[0-9A-Fa-f]+)(\s*at\s*)(?'offset'[0-9A-Fa-f]+)(\s*by\s*BackTrace)(?'trace_id'\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
    }

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
            char[] functionEndChars = new char[] { '@', '+' };

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
                return String.Format("{0}!{1} line {2} ({3})", Module, Function, SourceLine, Path.GetFileName(SourceFile));
            else if (Offset2.HasValue)
                return String.Format("{0}!{1}@{2:x8}", Module, Function, Offset2.Value);
            else
                return String.Format("{0}!{1}+{2:x8}", Module, Function, Offset);
        }
    }

    public class MemoryStatistics {
        public long NonpagedSystem, Paged, PagedSystem, Private, Virtual, WorkingSet;
        public long PeakPaged, PeakVirtual, PeakWorking;

        public MemoryStatistics () {
        }

        public MemoryStatistics (string json) {
            var prefix = "// Memory=";
            if (json.StartsWith(prefix))
                json = json.Substring(prefix.Length);

            var jss = new JavaScriptSerializer();
            var dict = jss.Deserialize<Dictionary<string, object>>(json);

            var cn = Mapper<MemoryStatistics>.ColumnNames;
            var t = this.GetType();

            foreach (var name in cn)
                BoundMember.New(this, t.GetField(name)).Value = dict[name];
        }

        public MemoryStatistics (Process process) {
            process.Refresh();

            NonpagedSystem = process.NonpagedSystemMemorySize64;
            Paged = process.PagedMemorySize64;
            PagedSystem = process.PagedSystemMemorySize64;
            Private = process.PrivateMemorySize64;
            Virtual = process.VirtualMemorySize64;
            WorkingSet = process.WorkingSet64;
            PeakPaged = process.PeakPagedMemorySize64;
            PeakVirtual = process.PeakVirtualMemorySize64;
            PeakWorking = process.PeakWorkingSet64;
        }

        public string GetFileText () {
            var sb = new StringBuilder();

            var cn = Mapper<MemoryStatistics>.ColumnNames;
            var cv = Mapper<MemoryStatistics>.GetColumnValues(this);

            var dict = new Dictionary<string, object>();
            var jss = new JavaScriptSerializer();

            for (int i = 0; i < cn.Length; i++) {
                dict[cn[i]] = cv[i];
            }

            sb.Append("// Memory=");
            jss.Serialize(dict, sb);

            return sb.ToString();
        }
    }

    public class HeapSnapshot {
        public readonly int Index;
        public readonly DateTime When;
        public readonly string Filename;
        public readonly HashSet<string> Modules;
        public readonly MemoryStatistics Memory;

        public HeapSnapshot (int index, DateTime when, string filename) {
            Index = index;
            When = when;
            Filename = filename;
            Modules = new HashSet<string>();
            Memory = new MemoryStatistics();

            string line = null;
            bool scanningForStart = true, scanningForMemory = false;

            using (var f = File.OpenRead(filename))
            using (var sr = new StreamReader(f))
            while ((line = sr.ReadLine()) != null) {
                if (scanningForStart) {
                    if (line.Contains("Loaded modules"))
                        scanningForStart = false;
                    else if (line.Contains("Start of data for heap"))
                        break;
                    else
                        continue;
                } else if (scanningForMemory) {
                    if (line.StartsWith("// Memory=")) {
                        Memory = new MemoryStatistics(line);
                        scanningForMemory = false;
                        break;
                    }
                } else {
                    Match m;
                    if (!Regexes.SnapshotModule.TryMatch(line, out m)) {
                        if (line.Contains("Process modules enumerated"))
                            scanningForMemory = true;
                        else
                            continue;
                    } else {
                        Modules.Add(Path.GetFullPath(m.Groups["module"].Value).ToLowerInvariant());
                    }
                }
            }
        }

        public HeapSnapshot (string filename)
            : this(
            IndexFromFilename(filename), 
            DateTimeFromFilename(filename), 
            filename
        ) {
        }

        private HeapSnapshot (DateTime time) {
            When = time;
        }

        // Used for binary searching, not for creating a valid snapshot :/
        public static HeapSnapshot FromTime (DateTime time) {
            return new HeapSnapshot(time);
        }

        static int IndexFromFilename (string filename) {
            var parts = Path.GetFileNameWithoutExtension(filename)
                .Split(new[] { '_' }, 2);
            return int.Parse(parts[0]);
        }

        static DateTime DateTimeFromFilename (string filename) {
            var parts = Path.GetFileNameWithoutExtension(filename)
                .Split(new[] { '_' }, 2);

            return DateTime.ParseExact(
                parts[1].Replace("_", ":"), "u",
                System.Globalization.DateTimeFormatInfo.InvariantInfo
            );
        }

        public override string ToString () {
            return String.Format("#{0} - {1}", Index, When.ToLongTimeString());
        }
    }
}
