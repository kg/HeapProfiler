﻿/*
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
    public class Regexes {
        public Regex DiffModule = new Regex(
            @"^DBGHELP: (?'module'.*?)( - )(?'symbol_type'[^\n\r]*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex SnapshotModule = new Regex(
            "^//\\s*(?'offset'[A-Fa-f0-9]+)\\s+(?'size'[A-Fa-f0-9]+)\\s+(?'module'[^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex BytesDelta = new Regex(
            @"^(?'type'\+|\-)(\s+)(?'delta_bytes'[0-9A-Fa-f]+)(\s+)\((\s*)(?'new_bytes'[0-9A-Fa-f]*)(\s*)-(\s*)(?'old_bytes'[0-9A-Fa-f]*)\)(\s*)(?'new_count'[0-9A-Fa-f]+) allocs\t(BackTrace(?'trace_id'\w*))",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex CountDelta = new Regex(
            @"^(?'type'\+|\-)(\s+)(?'delta_count'[0-9A-Fa-f]+)(\s+)\((\s*)(?'new_count'[0-9A-Fa-f]*)(\s*)-(\s*)(?'old_count'[0-9A-Fa-f]*)\)\t(BackTrace(?'trace_id'\w*))\tallocations",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex TracebackFrame = new Regex(
            @"^\t(?'module'[^!]+)!(?'function'.+)\+(?'offset'[0-9A-Fa-f]+)(\s*:\s*(?'offset2'[0-9A-Fa-f]+))?(\s*\(\s*(?'path'[^,]+),\s*(?'line'[0-9]*)\))?",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex Allocation = new Regex(
            @"^(?'size'[0-9A-Fa-f]+)(\s*bytes\s*\+\s*)(?'overhead'[0-9A-Fa-f]+)(\s*at\s*)(?'offset'[0-9A-Fa-f]+)(\s*by\s*BackTrace)(?'id'[A-Fa-f0-9]*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public Regex HeapHeader = new Regex(
            @"^\*([- ]+)Start of data for heap \@ (?'id'[0-9A-Fa-f]+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
    }

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
                fBytes = null;
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

    public class HeapSnapshot {
        public class Module {
            public readonly string Filename;
            public readonly string ShortFilename;
            public readonly UInt32 Offset;
            public readonly UInt32 Size;

            public Module (string filename, UInt32 offset, UInt32 size) {
                Filename = filename;
                ShortFilename = Path.GetFileName(Filename);
                Offset = offset;
                Size = size;
            }

            public override bool Equals (object obj) {
                var rhs = obj as Module;
                if (rhs != null) {
                    return Filename == rhs.Filename;
                } else {
                    return base.Equals(obj);
                }
            }

            public override int GetHashCode () {
                return Filename.GetHashCode();
            }

            public override string ToString () {
                return String.Format("{0} @ {1:x8}", ShortFilename, Offset);
            }
        }

        public class ModuleCollection : KeyedCollection2<string, Module> {
            protected override string GetKeyForItem (Module item) {
                return item.Filename;
            }
        }

        public class Heap {
            public readonly UInt32 ID;
            public readonly List<Allocation> Allocations = new List<Allocation>();

            public UInt32 Offset, EstimatedSize, EstimatedFree;
            public UInt32 TotalOverhead, TotalRequested;
            public UInt32 LargestFreeSpan, LargestOccupiedSpan;
            public int OccupiedSpans;
            public int EmptySpans;

            public Heap (UInt32 id) {
                ID = id;
            }

            internal void ComputeStatistics () {
                Offset = ID;
                OccupiedSpans = EmptySpans = 0;
                EstimatedSize = EstimatedFree = 0;
                LargestFreeSpan = LargestOccupiedSpan = 0;
                TotalOverhead = TotalRequested = 0;

                if (Allocations.Count == 0)
                    return;

                Offset = Allocations[0].Offset;

                var currentPair = new Pair<UInt32>(Allocations[0].Offset, Allocations[0].NextOffset);
                // Detect free space at the front of the heap
                if (currentPair.First > ID) {
                    EmptySpans += 1;
                    EstimatedFree += currentPair.First - ID;
                    LargestFreeSpan = Math.Max(LargestFreeSpan, currentPair.First - ID);
                }

                var a = Allocations[0];
                TotalRequested += a.Size;
                TotalOverhead += a.Overhead;

                for (int i = 1; i < Allocations.Count; i++) {
                    a = Allocations[i];

                    if (a.Offset > currentPair.Second) {
                        // There's empty space between this allocation and the last one, so begin tracking a new occupied span
                        //  and update the statistics
                        var emptySize = a.Offset - currentPair.Second;

                        OccupiedSpans += 1;
                        EmptySpans += 1;

                        EstimatedFree += emptySize;

                        LargestFreeSpan = Math.Max(LargestFreeSpan, emptySize);
                        LargestOccupiedSpan = Math.Max(LargestOccupiedSpan, currentPair.Second - currentPair.First);

                        currentPair.First = a.Offset;
                        currentPair.Second = a.NextOffset;
                    } else {
                        currentPair.Second = a.NextOffset;
                    }

                    TotalRequested += a.Size;
                    TotalOverhead += a.Overhead;
                }

                // We aren't given the size of the heap, so we treat the end of the last allocation as the end of the heap.
                OccupiedSpans += 1;
                LargestOccupiedSpan = Math.Max(LargestOccupiedSpan, currentPair.Second - currentPair.First);
                EstimatedSize = currentPair.Second - Offset;
            }

            public float Fragmentation {
                get {
                    return EmptySpans / (float)Math.Max(1, Allocations.Count);
                }
            }
        }

        public class HeapCollection : KeyedCollection2<UInt32, Heap> {
            protected readonly List<UInt32> _Keys = new List<uint>();

            public override IEnumerable<UInt32> Keys {
                get {
                    return _Keys;
                }
            }

            protected override void InsertItem (int index, Heap item) {
                base.InsertItem(index, item);

                UpdateKeys();
            }

            protected override void SetItem (int index, Heap item) {
                base.SetItem(index, item);

                UpdateKeys();
            }

            protected override void RemoveItem (int index) {
                base.RemoveItem(index);

                UpdateKeys();
            }

            protected override void ClearItems () {
                base.ClearItems();

                UpdateKeys();
            }

            protected void UpdateKeys () {
                _Keys.Clear();
                foreach (var item in base.Items)
                    _Keys.Add(GetKeyForItem(item));
                _Keys.Sort();
            }

            protected override UInt32 GetKeyForItem (Heap item) {
                return item.ID;
            }
        }

        public struct Allocation {
            public readonly UInt32 Offset;
            public readonly UInt32 Size;
            public readonly UInt32 Overhead;
            public readonly UInt32 TracebackID;

            public Allocation (UInt32 offset, UInt32 size, UInt32 overhead, UInt32 tracebackID) {
                Offset = offset;
                Size = size;
                Overhead = overhead;
                TracebackID = tracebackID;
            }

            public UInt32 NextOffset {
                get {
                    return Offset + Size + Overhead;
                }
            }
        }

        public class Traceback {
            public readonly UInt32 ID;
            public readonly UInt32[] Frames;

            public Traceback (UInt32 id, UInt32[] frames) {
                ID = id;
                Frames = frames;
            }
        }

        public class TracebackCollection : KeyedCollection2<UInt32, Traceback> {
            protected override UInt32 GetKeyForItem (Traceback item) {
                return item.ID;
            }
        }

        public readonly int Index;
        public readonly DateTime When;
        public readonly string Filename;
        public readonly ModuleCollection Modules = new ModuleCollection();
        public readonly HeapCollection Heaps = new HeapCollection();
        public readonly TracebackCollection Tracebacks = new TracebackCollection();
        public readonly MemoryStatistics Memory;

        public HeapSnapshot (int index, DateTime when, string filename, string text) {
            Index = index;
            When = when;
            Filename = filename;
            Memory = new MemoryStatistics();

            bool scanningForStart = true, scanningForMemory = false;
            Heap scanningHeap = null;

            var regexes = new Regexes();

            int groupModule = regexes.SnapshotModule.GroupNumberFromName("module");
            int groupModuleOffset = regexes.SnapshotModule.GroupNumberFromName("offset");
            int groupModuleSize = regexes.SnapshotModule.GroupNumberFromName("size");
            int groupHeaderId = regexes.HeapHeader.GroupNumberFromName("id");
            int groupAllocOffset = regexes.Allocation.GroupNumberFromName("offset");
            int groupAllocSize = regexes.Allocation.GroupNumberFromName("size");
            int groupAllocOverhead = regexes.Allocation.GroupNumberFromName("overhead");
            int groupAllocId = regexes.Allocation.GroupNumberFromName("id");
            
            Match m;
            var frameList = new List<UInt32>();

            var lr = new LineReader(text);
            LineReader.Line line;

            while (lr.ReadLine(out line)) {
                if (scanningHeap != null) {
                    if (line.StartsWith("*-") && line.Contains("End of data for heap")) {
                        scanningHeap.Allocations.TrimExcess();
                        scanningHeap = null;
                    } else if (regexes.Allocation.TryMatch(ref line, out m)) {
                        var tracebackId = UInt32.Parse(m.Groups[groupAllocId].Value, NumberStyles.HexNumber);
                        Traceback traceback;

                        if (!Tracebacks.TryGetValue(tracebackId, out traceback)) {
                            // This is only valid if every allocation is followed by an empty line
                            frameList.Clear();
                            while (lr.ReadLine(out line)) {
                                if (line.StartsWith("\t"))
                                    frameList.Add(UInt32.Parse(
                                        line.ToString(), NumberStyles.HexNumber | NumberStyles.AllowLeadingWhite
                                    ));
                                else {
                                    lr.Rewind(ref line);
                                    break;
                                }
                            }

                            Tracebacks.Add(traceback = new Traceback(
                                tracebackId, frameList.ToArray()
                            ));
                        }

                        scanningHeap.Allocations.Add(new Allocation(
                            UInt32.Parse(m.Groups[groupAllocOffset].Value, NumberStyles.HexNumber),
                            UInt32.Parse(m.Groups[groupAllocSize].Value, NumberStyles.HexNumber),
                            UInt32.Parse(m.Groups[groupAllocOverhead].Value, NumberStyles.HexNumber),
                            tracebackId
                        ));
                    }
                } else if (scanningForMemory) {
                    if (regexes.HeapHeader.TryMatch(ref line, out m)) {
                        scanningHeap = new Heap(UInt32.Parse(m.Groups[groupHeaderId].Value, NumberStyles.HexNumber));
                        Heaps.Add(scanningHeap);
                    } else if (line.StartsWith("// Memory=")) {
                        Memory = new MemoryStatistics(line.ToString());
                        scanningForMemory = false;
                        break;
                    } else {
                        continue;
                    }
                } else if (scanningForStart) {
                    if (line.Contains("Loaded modules"))
                        scanningForStart = false;
                    else if (line.Contains("Start of data for heap"))
                        break;
                    else
                        continue;
                } else {
                    if (!regexes.SnapshotModule.TryMatch(ref line, out m)) {
                        if (line.Contains("Process modules enumerated"))
                            scanningForMemory = true;
                        else
                            continue;
                    } else {
                        var modulePath = Path.GetFullPath(m.Groups[groupModule].Value).ToLowerInvariant();
                        Modules.Add(new Module(
                            modulePath, 
                            UInt32.Parse(m.Groups[groupModuleOffset].Value, System.Globalization.NumberStyles.HexNumber),
                            UInt32.Parse(m.Groups[groupModuleSize].Value, System.Globalization.NumberStyles.HexNumber)
                        ));
                    }
                }
            }

            foreach (var heap in Heaps) {
                heap.Allocations.Sort(
                    (lhs, rhs) => lhs.Offset.CompareTo(rhs.Offset)
                );
                heap.ComputeStatistics();
            }
        }

        public HeapSnapshot (string filename, string text)
            : this(
            IndexFromFilename(filename),
            DateTimeFromFilename(filename),
            filename, text
        ) {
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

        public float HeapFragmentation {
            get {
                if (Heaps.Count == 0)
                    return 0.0f;

                return (from heap in Heaps select heap.EmptySpans).Sum() / (float)Math.Max(1, (from heap in Heaps select heap.Allocations.Count).Sum());
            }
        }

        public override string ToString () {
            return String.Format("#{0} - {1}", Index, When.ToLongTimeString());
        }
    }
}
