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
using Squared.Data.Mangler;
using System.Runtime.InteropServices;

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

    public class MemoryStatistics {
        [Column]
        public long NonpagedSystem, Paged, PagedSystem, Private, Virtual, WorkingSet;
        [Column]
        public long PeakPaged, PeakVirtual, PeakWorking;

        public MemoryStatistics () {
        }

        public MemoryStatistics (string json) {
            const string prefix = "// Memory=";
            if (json.StartsWith(prefix))
                json = json.Substring(prefix.Length);

            var jss = new JavaScriptSerializer();
            var dict = jss.Deserialize<Dictionary<string, object>>(json);

            var cn = Mapper<MemoryStatistics>.ColumnNames;
            var t = this.GetType();

            foreach (var name in cn)
                BoundMember.New(this, t.GetField(name)).Value = dict[name];
        }

        protected MemoryStatistics (BinaryReader input) {
            var t = this.GetType();
            var cn = Mapper<MemoryStatistics>.ColumnNames;

            var valueCount = input.ReadInt32();
            if (valueCount != cn.Length)
                throw new InvalidDataException();

            for (int i = 0; i < cn.Length; i++) {
                var value = input.ReadInt64();
                BoundMember.New(this, t.GetField(cn[i])).Value = value;
            }
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

        public IEnumerator<object> SaveToDatabase (DatabaseFile db, int Snapshots_ID) {
            yield return db.MemoryStatistics.Set(Snapshots_ID, this);
        }

        [TangleSerializer]
        static void Serialize (ref SerializationContext<MemoryStatistics> context, ref MemoryStatistics input) {
            var cv = Mapper<MemoryStatistics>.GetColumnValues(input);

            var bw = new BinaryWriter(context.Stream, Encoding.UTF8);
            bw.Write(cv.Length);
            for (int i = 0; i < cv.Length; i++)
                bw.Write((long)cv[i]);
            bw.Flush();
        }

        [TangleDeserializer]
        static void Deserialize (ref DeserializationContext<MemoryStatistics> context, out MemoryStatistics output) {
            var br = new BinaryReader(context.Stream, Encoding.UTF8);

            output = new MemoryStatistics(br);
        }
    }

    public class HeapSnapshotInfo {
        public readonly int Index;
        public readonly DateTime Timestamp;
        public readonly string Filename;
        public readonly MemoryStatistics Memory;

        public readonly float HeapFragmentation;
        public readonly long LargestFreeHeapBlock, LargestOccupiedHeapBlock;
        public readonly long AverageFreeBlockSize, AverageOccupiedBlockSize;

        private HeapSnapshot _StrongRef;
        private WeakReference _WeakRef;

        public HeapSnapshotInfo (int index, DateTime timestamp, string filename, MemoryStatistics memory, HeapSnapshot snapshot) {
            Index = index;
            Timestamp = timestamp;
            Filename = filename;
            Memory = memory;
            _StrongRef = snapshot;

            LargestFreeHeapBlock = (from heap in snapshot.Heaps select heap.Info.LargestFreeSpan).Max();
            LargestOccupiedHeapBlock = (from heap in snapshot.Heaps select heap.Info.LargestOccupiedSpan).Max();
            AverageFreeBlockSize = (long)(from heap in snapshot.Heaps select (heap.Info.EstimatedFree) / Math.Max(heap.Info.EmptySpans, 1)).Average();
            AverageOccupiedBlockSize = (long)(from heap in snapshot.Heaps select (heap.Info.TotalOverhead + heap.Info.TotalRequested) / Math.Max(heap.Info.OccupiedSpans, 1)).Average();
            HeapFragmentation = (from heap in snapshot.Heaps select heap.Info.EmptySpans).Sum() / (float)Math.Max(1, (from heap in snapshot.Heaps select heap.Allocations.Count).Sum());
        }

        public HeapSnapshot Snapshot {
            get {
                if (_StrongRef != null)
                    return _StrongRef;
                else if (_WeakRef != null)
                    return _WeakRef.Target as HeapSnapshot;
                else
                    return null;
            }
        }

        public void ReleaseStrongReference () {
            if (_StrongRef != null) {
                _WeakRef = new WeakReference(_StrongRef);
            }
            _StrongRef = null;
        }

        internal void SetSnapshot (HeapSnapshot heapSnapshot) {
            var oldSnapshot = Snapshot;
            if (oldSnapshot != null)
                throw new InvalidOperationException();

            _StrongRef = heapSnapshot;
            _WeakRef = null;
        }
    }

    public class HeapSnapshot {
        public class Module {
            public readonly string Filename;
            public readonly string ShortFilename;
            public readonly UInt32 BaseAddress;
            public readonly UInt32 Size;

            public Module (string filename, UInt32 baseAddress, UInt32 size) {
                Filename = filename;
                ShortFilename = Path.GetFileName(Filename);
                BaseAddress = baseAddress;
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
                return String.Format("{0} @ {1:x8}", ShortFilename, BaseAddress);
            }

            [TangleSerializer]
            static void Serialize (ref SerializationContext<Module> context, ref Module input) {
                var bw = new BinaryWriter(context.Stream, Encoding.UTF8);
                bw.Write(input.BaseAddress);
                bw.Write(input.Size);
                bw.Write(input.Filename);
                bw.Flush();
            }

            [TangleDeserializer]
            static void Deserialize (ref SerializationContext<Module> context, out Module output) {
                var br = new BinaryReader(context.Stream, Encoding.UTF8);

                var baseAddress = br.ReadUInt32();
                var size = br.ReadUInt32();
                var filename = br.ReadString();

                output = new Module(filename, baseAddress, size);
            }
        }

        public class ModuleCollection : KeyedCollection2<string, Module> {
            protected override string GetKeyForItem (Module item) {
                return item.Filename;
            }
        }

        public struct HeapInfo {
            public readonly int SnapshotID;
            public readonly UInt32 HeapID;

            public long EstimatedStart, EstimatedSize, EstimatedFree;
            public long TotalOverhead, TotalRequested;
            public long LargestFreeSpan, LargestOccupiedSpan;
            public long OccupiedSpans, EmptySpans;

            public HeapInfo (int snapshotID, UInt32 heapID) {
                SnapshotID = snapshotID;
                HeapID = heapID;

                EstimatedStart = EstimatedSize = EstimatedFree = 0;
                TotalOverhead = TotalRequested = 0;
                LargestFreeSpan = LargestOccupiedSpan = 0;
                OccupiedSpans = EmptySpans = 0;
            }
        }

        public class Heap {
            public readonly UInt32 ID;
            public readonly List<Allocation> Allocations = new List<Allocation>();

            public UInt32 BaseAddress;

            public HeapInfo Info;

            public Heap (int snapshotID, UInt32 id) {
                BaseAddress = ID = id;
                Info = new HeapInfo(snapshotID, id);
            }

            internal void ComputeStatistics () {
                Info.EstimatedStart = BaseAddress = ID;
                Info.OccupiedSpans = Info.EmptySpans = 0;
                Info.EstimatedSize = Info.EstimatedFree = 0;
                Info.LargestFreeSpan = Info.LargestOccupiedSpan = 0;
                Info.TotalOverhead = Info.TotalRequested = 0;

                if (Allocations.Count == 0)
                    return;

                Info.EstimatedStart = Allocations[0].Address;

                var currentPair = new Pair<UInt32>(Allocations[0].Address, Allocations[0].NextOffset);
                // Detect free space at the front of the heap
                if (currentPair.First > ID) {
                    Info.EmptySpans += 1;
                    Info.EstimatedFree += currentPair.First - ID;
                    Info.LargestFreeSpan = Math.Max(Info.LargestFreeSpan, currentPair.First - ID);
                }

                var a = Allocations[0];
                Info.TotalRequested += a.Size;
                Info.TotalOverhead += a.Overhead;

                for (int i = 1; i < Allocations.Count; i++) {
                    a = Allocations[i];

                    if (a.Address > currentPair.Second) {
                        // There's empty space between this allocation and the last one, so begin tracking a new occupied span
                        //  and update the statistics
                        var emptySize = a.Address - currentPair.Second;

                        Info.OccupiedSpans += 1;
                        Info.EmptySpans += 1;

                        Info.EstimatedFree += emptySize;

                        Info.LargestFreeSpan = Math.Max(Info.LargestFreeSpan, emptySize);
                        Info.LargestOccupiedSpan = Math.Max(Info.LargestOccupiedSpan, currentPair.Second - currentPair.First);

                        currentPair.First = a.Address;
                        currentPair.Second = a.NextOffset;
                    } else {
                        currentPair.Second = a.NextOffset;
                    }

                    Info.TotalRequested += a.Size;
                    Info.TotalOverhead += a.Overhead;
                }

                // We aren't given the size of the heap, so we treat the end of the last allocation as the end of the heap.
                Info.OccupiedSpans += 1;
                Info.LargestOccupiedSpan = Math.Max(Info.LargestOccupiedSpan, currentPair.Second - currentPair.First);
                Info.EstimatedSize = currentPair.Second - BaseAddress;
            }

            public float Fragmentation {
                get {
                    return Info.EmptySpans / (float)Math.Max(1, Allocations.Count);
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

        public struct AllocationRanges {
            public struct Range {
                public readonly UInt16 First, Last;

                public Range (UInt16 id) {
                    First = Last = id;
                }

                public Range (UInt16 first, UInt16 last) {
                    First = first;
                    Last = last;
                }
            }

            public readonly ArraySegment<Range> Ranges;

            public AllocationRanges (params Range[] ranges)
                : this(new ArraySegment<Range>(ranges)) {
            }

            public AllocationRanges (ArraySegment<Range> ranges) {
                Ranges = ranges;
            }

            [TangleSerializer]
            static unsafe void Serialize (ref SerializationContext<AllocationRanges> context, ref AllocationRanges input) {
                var count = input.Ranges.Count;

                var buffer = new byte[4 + (count * 3)];
                fixed (byte* pBuffer = buffer) {
                    *(int*)(pBuffer + 0) = count;

                    for (int i = 0; i < count; i++) {
                        int offset = 4 + (3 * i);
                        var range = input.Ranges.Array[i + input.Ranges.Offset];
                        *(ushort*)(pBuffer + offset) = range.First;
                        byte delta = (byte)(range.Last - range.First);
                        *(byte*)(pBuffer + offset + 2) = delta;
                    }
                }

                context.SetResult(buffer);
            }

            [TangleDeserializer]
            static unsafe void Deserialize (ref DeserializationContext<AllocationRanges> context, out AllocationRanges output) {
                if (context.SourceLength < 4)
                    throw new InvalidDataException();

                int count = *(int *)(context.Source + 0);
                var array = new Range[count];

                if (context.SourceLength < (4 + (3 * count)))
                    throw new InvalidDataException();

                for (int i = 0; i < count; i++) {
                    var first = *(ushort*)(context.Source + 4 + (i * 3));
                    var delta = *(context.Source + 4 + (i * 3) + 2);
                    array[i] = new Range(first, (ushort)(first + delta));
                }

                output = new AllocationRanges(array);
            }

            public static AllocationRanges New (UInt16 index) {
                return new AllocationRanges(new Range(index));
            }

            public AllocationRanges Update (UInt16 index) {
                Range[] result;

                for (int i = 0; i < Ranges.Count; i++) {
                    var range = Ranges.Array[i + Ranges.Offset];
                    if (range.First <= index && range.Last >= index)
                        return this;

                    if (range.Last == index - 1) {
                        result = new Range[Ranges.Count];
                        Array.Copy(Ranges.Array, Ranges.Offset, result, 0, Ranges.Count);
                        result[i] = new Range(range.First, index);
                        return new AllocationRanges(result);
                    }
                }

                result = new Range[Ranges.Count + 1];
                Array.Copy(Ranges.Array, Ranges.Offset, result, 0, Ranges.Count);
                result[result.Length - 1] = new Range(index);
                return new AllocationRanges(result);
            }
        }

        public struct Allocation {
            public readonly UInt32 Address;
            public readonly UInt32 Size;
            public readonly UInt32 Overhead;
            public readonly Traceback Traceback;

            public Allocation (UInt32 offset, UInt32 size, UInt32 overhead, Traceback traceback) {
                Address = offset;
                Size = size;
                Overhead = overhead;
                Traceback = traceback;
            }

            public UInt32 NextOffset {
                get {
                    return Address + Size + Overhead;
                }
            }
        }

        public class Traceback : IEnumerable<UInt32> {
            public readonly UInt32 ID;
            public readonly ArraySegment<UInt32> Frames;

            public Traceback (UInt32 id, ArraySegment<UInt32> frames) {
                ID = id;
                Frames = frames;
            }

            public IEnumerator<uint> GetEnumerator () {
                for (int i = 0; i < Frames.Count; i++)
                    yield return Frames.Array[i + Frames.Offset];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () {
                return this.GetEnumerator();
            }

            public override string ToString () {
                return String.Format(
                    "Traceback {0:X8}{1}{2}", 
                    ID, Environment.NewLine, 
                    String.Join(
                        Environment.NewLine,
                        (from f in this select String.Format("  {0:X8}", f)).ToArray()
                    )
                );
            }

            [TangleSerializer]
            static unsafe void Serialize (ref SerializationContext<Traceback> context, ref Traceback input) {
                var buffer = new byte[8 + input.Frames.Count * 4];

                fixed (uint * pFrames = &input.Frames.Array[input.Frames.Offset])
                fixed (byte * pBuffer = buffer) {
                    *(UInt32 *)(pBuffer + 0) = input.ID;
                    *(int *)(pBuffer + 4) = input.Frames.Count;

                    Squared.Data.Mangler.Internal.Native.memmove(
                        pBuffer + 8, (byte *)pFrames, new UIntPtr((uint)(input.Frames.Count * 4))
                    );
                }

                context.SetResult(buffer);
            }

            [TangleDeserializer]
            static unsafe void Deserialize (ref DeserializationContext<Traceback> context, out Traceback output) {
                if (context.SourceLength < 8)
                    throw new InvalidDataException();

                var id = *(UInt32 *)(context.Source + 0);
                var count = *(int*)(context.Source + 4);

                if (context.SourceLength < 8 + (count * 4))
                    throw new InvalidDataException();

                var array = new UInt32[count];
                fixed (uint * pFrames = array)
                    Squared.Data.Mangler.Internal.Native.memmove(
                        (byte*)pFrames, context.Source + 8, new UIntPtr((uint)(count * 4))
                    );

                output = new Traceback(id, new ArraySegment<uint>(array));
            }
        }

        public class TracebackCollection : KeyedCollection2<UInt32, Traceback> {
            protected override UInt32 GetKeyForItem (Traceback item) {
                return item.ID;
            }
        }

        public readonly HeapSnapshotInfo Info;
        public readonly ModuleCollection Modules = new ModuleCollection();
        public readonly HeapCollection Heaps = new HeapCollection();
        public readonly TracebackCollection Tracebacks = new TracebackCollection();

        public bool SavedToDatabase = false;

        public const int FrameBufferSize = 16 * 1024;
        public const int MaxTracebackLength = 32;

        public HeapSnapshot (int index, DateTime when, string filename, string text) {
            MemoryStatistics memory = new MemoryStatistics();
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
            
            // Instead of allocating a tiny new UInt32[] for every traceback we read in,
            //  we store groups of tracebacks into fixed-size buffers so that the GC has
            //  less work to do when performing collections. Tracebacks are read-only after 
            //  being constructed, and all the tracebacks from a snapshot have the same
            //  lifetime, so this works out well.
            var frameBuffer = new UInt32[FrameBufferSize];
            int frameBufferCount = 0;

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
                            // If the frame buffer could fill up while we're building our traceback,
                            //  let's allocate a new one.
                            if (frameBufferCount >= frameBuffer.Length - MaxTracebackLength) {
                                frameBuffer = new UInt32[frameBuffer.Length];
                                frameBufferCount = 0;
                            }

                            int firstFrame = frameBufferCount;

                            // This is only valid if every allocation is followed by an empty line
                            while (lr.ReadLine(out line)) {
                                if (line.StartsWith("\t"))
                                    frameBuffer[frameBufferCount++] = UInt32.Parse(
                                        line.ToString(), NumberStyles.HexNumber | NumberStyles.AllowLeadingWhite
                                    );
                                else {
                                    lr.Rewind(ref line);
                                    break;
                                }
                            }

                            Tracebacks.Add(traceback = new Traceback(
                                tracebackId, new ArraySegment<UInt32>(frameBuffer, firstFrame, frameBufferCount - firstFrame)
                            ));
                        }

                        scanningHeap.Allocations.Add(new Allocation(
                            UInt32.Parse(m.Groups[groupAllocOffset].Value, NumberStyles.HexNumber),
                            UInt32.Parse(m.Groups[groupAllocSize].Value, NumberStyles.HexNumber),
                            UInt32.Parse(m.Groups[groupAllocOverhead].Value, NumberStyles.HexNumber),
                            traceback
                        ));
                    }
                } else if (scanningForMemory) {
                    if (regexes.HeapHeader.TryMatch(ref line, out m)) {
                        scanningHeap = new Heap(index, UInt32.Parse(m.Groups[groupHeaderId].Value, NumberStyles.HexNumber));
                        Heaps.Add(scanningHeap);
                    } else if (line.StartsWith("// Memory=")) {
                        memory = new MemoryStatistics(line.ToString());
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
                    (lhs, rhs) => lhs.Address.CompareTo(rhs.Address)
                );
                heap.ComputeStatistics();
            }

            Info = new HeapSnapshotInfo(index, when, filename, memory, this);
        }

        public HeapSnapshot (string filename, string text)
            : this(
            IndexFromFilename(filename),
            DateTimeFromFilename(filename),
            filename, text
        ) {
        }

        internal HeapSnapshot (HeapSnapshotInfo info) {
            Info = info;
            info.SetSnapshot(this);
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

        protected unsafe void MakeKeyForAllocation (Allocation allocation, out TangleKey key) {
            var buffer = new byte[12];

            fixed (byte* pBuffer = buffer) {
                *(uint*)(pBuffer + 0) = allocation.Traceback.ID;
                *(uint*)(pBuffer + 4) = allocation.Address;
                *(uint*)(pBuffer + 8) = allocation.Size + allocation.Overhead;
            }

            key = new TangleKey(buffer);
        }

        public static IEnumerator<object> LoadFromDatabase (DatabaseFile db, HeapSnapshotInfo info) {
            var fResult = db.Snapshots.Get(info.Index);

            yield return fResult;
        }

        public IEnumerator<object> SaveToDatabase (DatabaseFile db) {
            SavedToDatabase = true;

            yield return db.Snapshots.Set(Index, this);

            yield return Memory.SaveToDatabase(db, Index);

            {
                var batch = db.Modules.CreateBatch(Modules.Count);

                foreach (var module in Modules)
                    batch.Add(module.Filename, module);

                yield return batch.Execute(db.Modules);
            }

            yield return db.SnapshotModules.Set(Index, Modules.Keys.ToArray());

            {
                var batch = db.Tracebacks.CreateBatch(Tracebacks.Count);

                foreach (var traceback in Tracebacks)
                    batch.Add(traceback.ID, traceback);

                yield return batch.Execute(db.Tracebacks);
            }

            UInt16 uIndex = (UInt16)Index;
            var nullRange = AllocationRanges.New(uIndex);

            Tangle<AllocationRanges>.UpdateCallback updateRanges =
                (value) => value.Update(uIndex);

            TangleKey key;

            foreach (var heap in Heaps) {
                var batch = db.Allocations.CreateBatch(heap.Allocations.Count);

                foreach (var allocation in heap.Allocations) {
                    MakeKeyForAllocation(allocation, out key);

                    batch.AddOrUpdate(
                        key, nullRange, updateRanges
                    );
                }

                yield return batch.Execute(db.Allocations);
            }

            yield return db.SnapshotHeaps.Set(Index, (from heap in Heaps select heap.Info).ToArray());
        }

        public int Index {
            get {
                return Info.Index;
            }
        }

        public DateTime Timestamp {
            get {
                return Info.Timestamp;
            }
        }

        public string Filename {
            get {
                return Info.Filename;
            }
        }

        public MemoryStatistics Memory {
            get {
                return Info.Memory;
            }
        }

        public override string ToString () {
            return String.Format("#{0} - {1}", Index, Timestamp.ToLongTimeString());
        }

        [TangleSerializer]
        static void Serialize (ref SerializationContext<HeapSnapshot> context, ref HeapSnapshot input) {
            var bw = new BinaryWriter(context.Stream, Encoding.UTF8);
            bw.Write(input.Index);
            bw.Write(input.Timestamp.ToString("O"));
            bw.Write(input.Filename);

            bw.Write(input.Heaps.Count);

            foreach (var heap in input.Heaps) {
                bw.Write(heap.ID);
                bw.Write(heap.Allocations.Count);
            }

            bw.Flush();
        }

        public static void Deserialize (DatabaseFile db, ref DeserializationContext<HeapSnapshot> context, out HeapSnapshot output) {
            var br = new BinaryReader(context.Stream, Encoding.UTF8);
            var index = br.ReadInt32();
            var timestamp = DateTime.Parse(br.ReadString());
            var filename = br.ReadString();

            output = new HeapSnapshot(index, timestamp, filename, "");

            // TODO

            var ids = new List<UInt32>();

            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
                ids.Add(br.ReadUInt32());

            Console.WriteLine(String.Join(", ", (from id in ids select id.ToString()).ToArray()));
            ids.Clear();

            count = br.ReadInt32();
            for (int i = 0; i < count; i++)
                ids.Add(br.ReadUInt32());

            Console.WriteLine(String.Join(", ", (from id in ids select id.ToString()).ToArray()));
            ids.Clear();

            throw new NotImplementedException();
        }
    }
}
