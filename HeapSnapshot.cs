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
            using (var pi = Mapper<MemoryStatistics>.PrepareInsert(
                db, "data.MemoryStats", extraColumns: new[] { "Snapshots_ID" }
            ))
                yield return pi.Insert(this, Snapshots_ID);
        }
    }

    public class HeapSnapshot {
        public class Module {
            public readonly string Filename;
            public readonly string ShortFilename;
            public readonly UInt32 BaseAddress;
            public readonly UInt32 Size;

            public Module (string filename, UInt32 offset, UInt32 size) {
                Filename = filename;
                ShortFilename = Path.GetFileName(Filename);
                BaseAddress = offset;
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
        }

        public class ModuleCollection : KeyedCollection2<string, Module> {
            protected override string GetKeyForItem (Module item) {
                return item.Filename;
            }
        }

        [Mapper(Explicit=true)]
        public class Heap {
            public readonly UInt32 ID;
            public readonly List<Allocation> Allocations = new List<Allocation>();

            public UInt32 BaseAddress;

            [Column]
            public long EstimatedStart, EstimatedSize, EstimatedFree;
            [Column]
            public long TotalOverhead, TotalRequested;
            [Column]
            public long LargestFreeSpan, LargestOccupiedSpan;
            [Column]
            public int OccupiedSpans, EmptySpans;

            // Needed for Mapper :(
            public Heap () {
                throw new InvalidOperationException();
            }

            public Heap (UInt32 id) {
                ID = id;
            }

            internal void ComputeStatistics () {
                EstimatedStart = BaseAddress = ID;
                OccupiedSpans = EmptySpans = 0;
                EstimatedSize = EstimatedFree = 0;
                LargestFreeSpan = LargestOccupiedSpan = 0;
                TotalOverhead = TotalRequested = 0;

                if (Allocations.Count == 0)
                    return;

                EstimatedStart = Allocations[0].Address;

                var currentPair = new Pair<UInt32>(Allocations[0].Address, Allocations[0].NextOffset);
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

                    if (a.Address > currentPair.Second) {
                        // There's empty space between this allocation and the last one, so begin tracking a new occupied span
                        //  and update the statistics
                        var emptySize = a.Address - currentPair.Second;

                        OccupiedSpans += 1;
                        EmptySpans += 1;

                        EstimatedFree += emptySize;

                        LargestFreeSpan = Math.Max(LargestFreeSpan, emptySize);
                        LargestOccupiedSpan = Math.Max(LargestOccupiedSpan, currentPair.Second - currentPair.First);

                        currentPair.First = a.Address;
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
                EstimatedSize = currentPair.Second - BaseAddress;
            }

            public float Fragmentation {
                get {
                    return EmptySpans / (float)Math.Max(1, Allocations.Count);
                }
            }

            public IEnumerator<object> SaveToDatabase (DatabaseFile db, int Snapshots_ID) {
                var fHeapId = db.GetUniqueID(
                    "data.Heaps", new[] { "BaseAddress" },
                    (long)BaseAddress
                );
                yield return fHeapId;
                var heapId = fHeapId.Result;

                yield return db.ExecuteSQL(
                    "INSERT INTO data.SnapshotHeaps (Snapshots_ID, Heaps_ID) VALUES (?, ?)",
                    Snapshots_ID, heapId
                );

                using (var pi = Mapper<Heap>.PrepareInsert(
                    db, "data.HeapStats", extraColumns: new[] { "Heaps_ID", "Snapshots_ID" }
                ))
                    yield return pi.Insert(this, heapId, Snapshots_ID);

                IFuture lastFuture = null;

                using (var select = db.BuildQuery(
                    @"SELECT Allocations_ID FROM data.Allocations WHERE 
                        Heaps_ID = ? AND Tracebacks_ID = ? AND RelativeAddress = ? 
                        AND Size = ? AND Overhead = ? AND Last_Snapshots_ID = ?"
                ))
                using (var insert = db.BuildQuery(
                    @"INSERT INTO data.Allocations (
                        Heaps_ID, First_Snapshots_ID, Last_Snapshots_ID, Tracebacks_ID, RelativeAddress, Size, Overhead
                      ) VALUES (
                        ?, ?, ?, ?, ?, ?, ?
                      )"
                ))
                using (var update = db.BuildQuery(
                    @"UPDATE data.Allocations SET Last_Snapshots_ID = ? WHERE Allocations_ID = ?"
                )) {
                    foreach (var alloc in Allocations) {
                        var tracebackId = alloc.Traceback.DatabaseID.Value;
                        long relativeAddress = alloc.Address - BaseAddress;
                        long size = alloc.Size;
                        long overhead = alloc.Overhead;

                        var fExistingId = select.ExecuteScalar(
                            heapId, tracebackId, relativeAddress, size, overhead, Snapshots_ID - 1
                        );
                        yield return fExistingId;

                        if (fExistingId.Result == null)
                            lastFuture = insert.ExecuteNonQuery(
                                heapId, Snapshots_ID, Snapshots_ID, tracebackId,
                                relativeAddress, size, overhead
                            );
                        else
                            lastFuture = update.ExecuteNonQuery(
                                Snapshots_ID, (long)fExistingId.Result
                            );
                    }

                    // We need to wait for the last query we started to finish.
                    if (lastFuture != null)
                        yield return lastFuture;
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

            public long? DatabaseID = null;

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

            /*
            protected string GenerateFindSQL () {
                var sb = new StringBuilder();

                sb.AppendLine("SELECT f0.Tracebacks_ID FROM data.TracebackFrames f0 ");
                for (int i = 1; i < Frames.Count; i++) {
                    sb.AppendFormat("  INNER JOIN data.TracebackFrames f{0} USING (Tracebacks_ID) ", i);
                    sb.AppendLine();
                }

                sb.AppendLine("WHERE ");
                for (int i = 0; i < Frames.Count; i++) {
                    var frame = Frames.Array[i + Frames.Offset];
                    sb.AppendFormat("f{0}.Address = {1} AND f{0}.FrameIndex = {0} ", i, frame);

                    if (i < Frames.Count - 1)
                        sb.Append("AND ");

                    sb.AppendLine();
                }

                return sb.ToString();
            }
             */

            public IEnumerator<object> SaveToDatabase (DatabaseFile db) {
                var fExistingId = db.ExecuteScalar(
                    "SELECT Tracebacks_ID FROM data.Tracebacks WHERE RuntimeID = ?", 
                    (long)ID
                );
                yield return fExistingId;

                if (fExistingId.Result != null) {
                    DatabaseID = (long)fExistingId.Result;
                    yield break;
                }

                var fTracebackId = db.GetUniqueID(
                    "data.Tracebacks", new[] { "RuntimeID" }, 
                    (long)ID
                );
                yield return fTracebackId;

                DatabaseID = fTracebackId.Result;

                if (Frames.Count == 0)
                    yield break;

                IFuture lastFuture = null;

                using (var query = db.BuildQuery(
                    @"INSERT INTO data.TracebackFrames (
                        Tracebacks_ID, FrameIndex, Address
                      ) VALUES (
                        ?, ?, ?
                      )"
                )) {
                    for (int i = 0; i < Frames.Count; i++)
                        lastFuture = query.ExecuteNonQuery(
                            DatabaseID.Value, i,
                            (long)Frames.Array[i + Frames.Offset]
                        );

                    // We need to wait for the last insert we started to finish.
                    if (lastFuture != null)
                        yield return lastFuture;
                }
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
        }

        public class TracebackCollection : KeyedCollection2<UInt32, Traceback> {
            protected override UInt32 GetKeyForItem (Traceback item) {
                return item.ID;
            }
        }

        public readonly int Index;
        public readonly DateTime Timestamp;
        public readonly string Filename;
        public readonly ModuleCollection Modules = new ModuleCollection();
        public readonly HeapCollection Heaps = new HeapCollection();
        public readonly TracebackCollection Tracebacks = new TracebackCollection();
        public readonly MemoryStatistics Memory;

        public bool SavedToDatabase = false;

        public const int FrameBufferSize = 16 * 1024;
        public const int MaxTracebackLength = 32;

        public HeapSnapshot (int index, DateTime when, string filename, string text) {
            Index = index;
            Timestamp = when;
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
                    (lhs, rhs) => lhs.Address.CompareTo(rhs.Address)
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

        public IEnumerator<object> SaveToDatabase (DatabaseFile db) {
            SavedToDatabase = true;

            yield return db.ExecuteSQL(
                "INSERT INTO data.Snapshots (Snapshots_ID, Timestamp) VALUES (?, ?)",
                Index, Timestamp.ToUniversalTime().Ticks
            );

            yield return Memory.SaveToDatabase(db, Index);

            using (var xact = db.CreateTransaction()) {
                yield return xact;

                foreach (var traceback in Tracebacks)
                    yield return traceback.SaveToDatabase(db);

                yield return xact.Commit();
            }

            using (var xact = db.CreateTransaction()) {
                yield return xact;

                foreach (var heap in Heaps)
                    yield return heap.SaveToDatabase(db, Index);

                yield return xact.Commit();
            }
        }

        public override string ToString () {
            return String.Format("#{0} - {1}", Index, Timestamp.ToLongTimeString());
        }
    }
}
