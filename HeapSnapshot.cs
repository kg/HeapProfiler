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
using Squared.Data.Mangler.Serialization;
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

        [TangleSerializer]
        static void Serialize (ref SerializationContext context, ref MemoryStatistics input) {
            var cv = Mapper<MemoryStatistics>.GetColumnValues(input);

            var bw = new BinaryWriter(context.Stream, Encoding.UTF8);
            bw.Write(cv.Length);
            for (int i = 0; i < cv.Length; i++)
                bw.Write((long)cv[i]);
            bw.Flush();
        }

        [TangleDeserializer]
        static void Deserialize (ref DeserializationContext context, out MemoryStatistics output) {
            var br = new BinaryReader(context.Stream, Encoding.UTF8);

            output = new MemoryStatistics(br);
        }
    }

    public class FilteredHeapSnapshotInfo {
        public readonly long BytesAllocated, BytesOverhead, BytesTotal;
        public readonly int AllocationCount;

        public FilteredHeapSnapshotInfo (
            long bytesAllocated, long bytesOverhead, long bytesTotal, int allocationCount
        ) {
            BytesAllocated = bytesAllocated;
            BytesOverhead = bytesOverhead;
            BytesTotal = bytesTotal;
            AllocationCount = allocationCount;
        }
    }

    public class HeapSnapshotInfo : FilteredHeapSnapshotInfo {
        public readonly int Index;
        public readonly DateTime Timestamp;
        public readonly string Filename;
        public readonly MemoryStatistics Memory;

        public readonly float HeapFragmentation;
        public readonly long LargestFreeHeapBlock, LargestOccupiedHeapBlock;
        public readonly long AverageFreeBlockSize, AverageOccupiedBlockSize;

        private HeapSnapshot _StrongRef;
        private WeakReference _WeakRef;

        public HeapSnapshotInfo (
            int index, DateTime timestamp, string filename, 
            MemoryStatistics memory, HeapSnapshot snapshot
        ) : base (
            (from heap in snapshot.Heaps select (from alloc in heap.Allocations select (long)alloc.Size).Sum()).Sum(),
            (from heap in snapshot.Heaps select (from alloc in heap.Allocations select (long)alloc.Overhead).Sum()).Sum(),
            (from heap in snapshot.Heaps select (from alloc in heap.Allocations select (long)(alloc.Size + alloc.Overhead)).Sum()).Sum(),
            (from heap in snapshot.Heaps select heap.Allocations.Count).Sum()
        ) {
            Index = index;
            Timestamp = timestamp;
            Filename = filename;
            Memory = memory;
            _StrongRef = snapshot;

            HeapFragmentation = (from heap in snapshot.Heaps select heap.Info.EmptySpans).Sum() / (float)Math.Max(1, (from heap in snapshot.Heaps select heap.Allocations.Count).Sum());
            LargestFreeHeapBlock = (from heap in snapshot.Heaps select heap.Info.LargestFreeSpan).Max();
            LargestOccupiedHeapBlock = (from heap in snapshot.Heaps select heap.Info.LargestOccupiedSpan).Max();
            AverageFreeBlockSize = (long)(from heap in snapshot.Heaps select (heap.Info.EstimatedFree) / Math.Max(heap.Info.EmptySpans, 1)).Average();
            AverageOccupiedBlockSize = (long)(from heap in snapshot.Heaps select (heap.Info.TotalOverhead + heap.Info.TotalRequested) / Math.Max(heap.Info.OccupiedSpans, 1)).Average();
        }

        protected HeapSnapshotInfo (
            int index, DateTime timestamp, string filename, MemoryStatistics memory,
            float heapFragmentation, long largestFreeHeapBlock, long largestOccupiedHeapBlock,
            long averageFreeBlockSize, long averageOccupiedBlockSize, 
            long bytesAllocated, long bytesOverhead, long bytesTotal,
            int allocationCount
        ) : base (
            bytesAllocated, bytesOverhead, bytesTotal, allocationCount
        ) {
            Index = index;
            Timestamp = timestamp;
            Filename = filename;
            Memory = memory;

            HeapFragmentation = heapFragmentation;
            LargestFreeHeapBlock = largestFreeHeapBlock;
            LargestOccupiedHeapBlock = largestOccupiedHeapBlock;
            AverageFreeBlockSize = averageFreeBlockSize;
            AverageOccupiedBlockSize = averageOccupiedBlockSize;

            _StrongRef = null;
            _WeakRef = null;
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

        [TangleDeserializer]
        static void Deserialize (ref DeserializationContext context, out HeapSnapshotInfo output) {
            var br = new BinaryReader(context.Stream, Encoding.UTF8);

            var index = br.ReadInt32();
            var timestamp = DateTime.Parse(br.ReadString());
            var filename = br.ReadString();

            var heapFragmentation = br.ReadSingle();

            var largestFree = br.ReadInt64();
            var largestOccupied = br.ReadInt64();

            var averageFree = br.ReadInt64();
            var averageOccupied = br.ReadInt64();

            var bytesAllocated = br.ReadInt64();
            var bytesOverhead = br.ReadInt64();
            var bytesTotal = br.ReadInt64();

            var allocationCount = br.ReadInt32();

            var memoryOffset = br.ReadUInt32();

            MemoryStatistics memory;
            context.DeserializeValue(memoryOffset, out memory);

            output = new HeapSnapshotInfo(
                index, timestamp, filename, memory,
                heapFragmentation, largestFree, largestOccupied,
                averageFree, averageOccupied,
                bytesAllocated, bytesOverhead, bytesTotal,
                allocationCount
            );
        }

        [TangleSerializer]
        static void Serialize (ref SerializationContext context, ref HeapSnapshotInfo input) {
            var bw = new BinaryWriter(context.Stream, Encoding.UTF8);

            bw.Write(input.Index);
            bw.Write(input.Timestamp.ToString("O"));
            bw.Write(input.Filename);

            bw.Write(input.HeapFragmentation);

            bw.Write(input.LargestFreeHeapBlock);
            bw.Write(input.LargestOccupiedHeapBlock);

            bw.Write(input.AverageFreeBlockSize);
            bw.Write(input.AverageOccupiedBlockSize);

            bw.Write(input.BytesAllocated);
            bw.Write(input.BytesOverhead);
            bw.Write(input.BytesTotal);

            bw.Write(input.AllocationCount);

            bw.Flush();

            uint offset = (uint)context.Stream.Length;
            bw.Write(offset + 4);
            bw.Flush();

            context.SerializeValue(input.Memory);
        }

        internal void FlushCachedInstance () {
            ReleaseStrongReference();
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
                ShortFilename = Path.GetFileNameWithoutExtension(Filename);
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
            static void Serialize (ref SerializationContext context, ref Module input) {
                var bw = new BinaryWriter(context.Stream, Encoding.UTF8);
                bw.Write(input.BaseAddress);
                bw.Write(input.Size);
                bw.Flush();
            }

            [TangleDeserializer]
            static void Deserialize (ref DeserializationContext context, out Module output) {
                var br = new BinaryReader(context.Stream, Encoding.UTF8);

                var baseAddress = br.ReadUInt32();
                var size = br.ReadUInt32();
                var filename = context.Key.Value as string;

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

            public Heap (HeapInfo info) {
                Info = info;
                BaseAddress = ID = info.HeapID;
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

            public override string ToString () {
                return String.Format("Heap {0:x8}", ID);
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
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Range : IComparable<Range> {
                public readonly UInt16 First, Last;
                public readonly UInt32 TracebackID, Size, Overhead;

                public Range (UInt16 id, UInt32 tracebackId, UInt32 size, UInt32 overhead)
                    : this(id, id, tracebackId, size, overhead) {
                }

                public Range (UInt16 first, UInt16 last, UInt32 tracebackId, UInt32 size, UInt32 overhead) {
                    First = first;
                    Last = last;
                    TracebackID = tracebackId;
                    Size = size;
                    Overhead = overhead;
                }

                public int CompareTo (Range rhs) {
                    int result = First.CompareTo(rhs.First);
                    if (result == 0)
                        result = Last.CompareTo(rhs.Last);
                    if (result == 0)
                        result = TracebackID.CompareTo(rhs.TracebackID);
                    return result;
                }
            }

            public readonly ArraySegment<Range> Ranges;

            public AllocationRanges (Range[] ranges)
                : this(new ArraySegment<Range>(ranges)) {
            }

            public AllocationRanges (ArraySegment<Range> ranges) {
                Ranges = ranges;
            }

            [TangleSerializer]
            static unsafe void Serialize (ref SerializationContext context, ref AllocationRanges input) {
                var buffer = new byte[4];
                var count = input.Ranges.Count;

                fixed (byte * pBuffer = buffer) {
                    *(int *)(pBuffer) = count;
                    context.Stream.Write(buffer, 0, 4);
                }

                var array = input.Ranges.Array;
                for (int i = 0, o = input.Ranges.Offset; i < count; i++)
                    context.SerializeValue(
                        BlittableSerializer<Range>.Serialize, ref array[i + o]
                    );
            }

            [TangleDeserializer]
            static unsafe void Deserialize (ref DeserializationContext context, out AllocationRanges output) {
                if (context.SourceLength < 4)
                    throw new InvalidDataException();

                int count = *(int *)(context.Source + 0);
                var array = ImmutableArrayPool<Range>.Allocate(count);
                uint offset = 4;
                uint itemSize = BlittableSerializer<Range>.Size;

                if (context.SourceLength < (4 + (itemSize * count)))
                    throw new InvalidDataException();

                for (int i = 0; i < count; i++) {
                    context.DeserializeValue(
                        BlittableSerializer<Range>.Deserialize, 
                        offset, itemSize, out array.Array[array.Offset + i]
                    );

                    offset += itemSize;
                }

                output = new AllocationRanges(array);
            }

            public static AllocationRanges New (UInt16 snapshotId, UInt32 tracebackId, UInt32 size, UInt32 overhead) {
                var ranges = ImmutableArrayPool<Range>.Allocate(1);
                ranges.Array[ranges.Offset] = new Range(snapshotId, tracebackId, size, overhead);
                return new AllocationRanges(ranges);
            }

            public AllocationRanges Update (UInt16 snapshotId, UInt32 tracebackId, UInt32 size, UInt32 overhead) {
                var newRange = new Range(snapshotId, tracebackId, size, overhead);
                ArraySegment<Range> result;

                var a = Ranges.Array;
                for (int i = 0, c = Ranges.Count, o = Ranges.Offset; i < c; i++) {
                    var range = a[i + o];

                    if ((range.TracebackID != tracebackId) || (range.Size != size) || (range.Overhead != overhead))
                        continue;

                    if (range.First <= snapshotId && range.Last >= snapshotId)
                        return this;

                    if (range.Last == snapshotId - 1) {
                        result = ImmutableArrayPool<Range>.Allocate(Ranges.Count);
                        Array.Copy(Ranges.Array, Ranges.Offset, result.Array, result.Offset, Ranges.Count);
                        result.Array[result.Offset + i] = newRange;
                        return new AllocationRanges(result);
                    }
                }

                result = ImmutableArrayPool<Range>.Allocate(Ranges.Count + 1);
                Array.Copy(Ranges.Array, Ranges.Offset, result.Array, result.Offset, Ranges.Count);
                result.Array[result.Offset + result.Count - 1] = newRange;
                return new AllocationRanges(result);
            }

            public bool Get (int snapshotId, out Range range) {
                var a = Ranges.Array;

                for (int i = 0, c = Ranges.Count, o = Ranges.Offset; i < c; i++) {
                    var r = a[i + o];
                    if ((snapshotId >= r.First) && (snapshotId <= r.Last)) {
                        range = r;
                        return true;
                    }
                }

                range = default(Range);
                return false;
            }
        }

        public struct Allocation {
            public readonly UInt32 Address;
            public readonly UInt32 Size;
            public readonly UInt32 Overhead;
            public readonly UInt32 TracebackID;

            public Allocation (UInt32 offset, UInt32 size, UInt32 overhead, UInt32 tracebackId) {
                Address = offset;
                Size = size;
                Overhead = overhead;
                TracebackID = tracebackId;
            }

            public UInt32 NextOffset {
                get {
                    return Address + Size + Overhead;
                }
            }

            public override string ToString () {
                return String.Format(
                    "{0} ({1:x8}-{2:x8}) by {3:x8}", 
                        FileSize.Format(Size), 
                        Address, NextOffset, TracebackID
                    );
            }
        }

        public class AllocationTooltipContent : ITooltipContent {
            public readonly Allocation Allocation;
            public readonly TracebackInfo Traceback;
            public Point Location;
            public Size Size;
            public DeltaInfo.RenderParams RenderParams;

            public AllocationTooltipContent (ref Allocation allocation, ref TracebackInfo traceback, ref DeltaInfo.RenderParams renderParams) {
                Allocation = allocation;
                Traceback = traceback;
                RenderParams = renderParams;
            }

            public void Render (Graphics g) {
                RenderParams.ContentRegion = new Rectangle(
                    0, 0, Size.Width, Size.Height
                );
                var headerText = Allocation.ToString();
                Traceback.Render(g, ref RenderParams, headerText);
            }

            public Size Measure (Graphics g) {
                var font = RenderParams.Font;
                var sf = RenderParams.StringFormat;
                var headerText = Allocation.ToString();

                var width = (int)Math.Ceiling(g.MeasureString(
                    headerText + Environment.NewLine + Traceback.ToString(), 
                    font, 99999, sf
                ).Width);
                var lineHeight = g.MeasureString("AaBbYyZz", font, width, sf).Height;
                return new Size(
                    width, (int)Math.Ceiling(lineHeight * (Traceback.Frames.Count + 1))
                );                
            }

            Point ITooltipContent.Location {
                get {
                    return Location;
                }
            }

            Size ITooltipContent.Size {
                get {
                    return Size;
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
            static unsafe void Serialize (ref SerializationContext context, ref Traceback input) {
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
            static unsafe void Deserialize (ref DeserializationContext context, out Traceback output) {
                if (context.SourceLength < 8)
                    throw new InvalidDataException();

                var id = *(UInt32 *)(context.Source + 0);
                var count = *(int*)(context.Source + 4);

                if (context.SourceLength < 8 + (count * 4))
                    throw new InvalidDataException();

                var array = ImmutableArrayPool<UInt32>.Allocate(count);
                fixed (uint * pFrames = array.Array)
                    Squared.Data.Mangler.Internal.Native.memmove(
                        (byte *)(&pFrames[array.Offset]), context.Source + 8, new UIntPtr((uint)(count * 4))
                    );

                output = new Traceback(id, array);
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
                            traceback.ID
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

        public IEnumerator<object> SaveToRecording (HeapRecording recording) {
            var db = recording.Database;
            SavedToDatabase = true;

            yield return db.Snapshots.Set(Index, Info);

            {
                var batch = db.Modules.CreateBatch(Modules.Count);

                foreach (var module in Modules)
                    batch.Add(module.Filename, module);

                yield return batch.Execute();
            }

            yield return db.SnapshotModules.Set(Index, Modules.Keys.ToArray());

            {
                var tracebackBatch = db.Tracebacks.CreateBatch(Tracebacks.Count);

                foreach (var traceback in Tracebacks)
                    tracebackBatch.Add(traceback.ID, traceback);

                yield return tracebackBatch.Execute();

                yield return recording.UpdateFilteredTracebacks(
                    from traceback in Tracebacks select traceback.ID
                );
            }

            UInt16 uIndex = (UInt16)Index;

            HashSet<UInt32> addressSet;

            DecisionUpdateCallback<AllocationRanges> rangeUpdater =
                (ref AllocationRanges oldValue, ref AllocationRanges newValue) => {
                    var r = newValue.Ranges.Array[newValue.Ranges.Offset];
                    newValue = oldValue.Update(r.First, r.TracebackID, r.Size, r.Overhead);
                    return true;
                };

            foreach (var heap in Heaps) {
                var fAddressSet = db.HeapAllocations.Get(heap.ID);
                yield return fAddressSet;

                if (fAddressSet.Failed)
                    addressSet = new HashSet<UInt32>();
                else
                    addressSet = new HashSet<UInt32>(fAddressSet.Result);

                var batch = db.Allocations.CreateBatch(heap.Allocations.Count);

                foreach (var allocation in heap.Allocations) {
                    batch.AddOrUpdate(
                        allocation.Address, AllocationRanges.New(
                            uIndex, allocation.TracebackID, allocation.Size, allocation.Overhead
                        ), rangeUpdater
                    );

                    addressSet.Add(allocation.Address);
                }

                yield return batch.Execute();

                yield return db.HeapAllocations.Set(heap.ID, addressSet.ToArray());
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
    }

    [Serializable]
    public struct StackFilter {
        public readonly string ModuleGlob;
        public readonly string FunctionGlob;

        public StackFilter (string module, string function) {
            ModuleGlob = module;
            FunctionGlob = function;
        }

        public CompiledStackFilter Compile () {
            Regex module = null;
            Regex function = null;

            if (ModuleGlob != null)
                module = new Regex(MainWindow.EscapeFilter(ModuleGlob), RegexOptions.Compiled);

            if (FunctionGlob != null)
                function = new Regex(MainWindow.EscapeFilter(FunctionGlob), RegexOptions.Compiled);

            return new CompiledStackFilter(module, function);
        }
    }

    public struct CompiledStackFilter {
        public readonly Regex ModuleRegex;
        public readonly Regex FunctionRegex;

        public CompiledStackFilter (Regex module, Regex function) {
            ModuleRegex = module;
            FunctionRegex = function;
        }

        public bool IsMatch (TracebackFrame frame) {
            if ((ModuleRegex != null) && ((frame.Module == null) || !ModuleRegex.IsMatch(frame.Module)))
                return false;

            if ((FunctionRegex != null) && ((frame.Function == null) || !FunctionRegex.IsMatch(frame.Function)))
                return false;

            return true;
        }
    }
}
