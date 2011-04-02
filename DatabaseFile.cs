using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Squared.Data.Mangler;
using Squared.Data.Mangler.Internal;
using Squared.Data.Mangler.Serialization;
using Squared.Task;
using System.IO;
using Squared.Util.Bind;

namespace HeapProfiler {
    public class DatabaseFile : IDisposable {
        public const int FileVersion = 1;

        public readonly TaskScheduler Scheduler;

        public FolderStreamSource Storage;

        public Tangle<HeapSnapshotInfo> Snapshots;
        public Tangle<HeapSnapshot.Module> Modules;
        public Tangle<HeapSnapshot.Traceback> Tracebacks;
        public Tangle<HeapSnapshot.AllocationRanges> Allocations;
        public Tangle<UInt32[]> HeapAllocations;
        public Tangle<TracebackFrame> SymbolCache;
        public Tangle<string[]> SnapshotModules;
        public Tangle<HeapSnapshot.HeapInfo[]> SnapshotHeaps;

        public Index<string, TracebackFrame> SymbolsByFunction;

        private readonly Dictionary<string, Delegate> Deserializers = new Dictionary<string, Delegate>();
        private readonly Dictionary<string, Delegate> Serializers = new Dictionary<string, Delegate>();

        private readonly IBoundMember[] TangleFields;
        private HashSet<ITangle> Tangles = new HashSet<ITangle>();
        private string _TokenFilePath;
        private string _Filename;

        protected DatabaseFile (TaskScheduler scheduler) {
            Scheduler = scheduler;

            TangleFields = new IBoundMember[] { 
                BoundMember.New(() => Snapshots),
                BoundMember.New(() => Modules),
                BoundMember.New(() => Tracebacks),
                BoundMember.New(() => Allocations),
                BoundMember.New(() => HeapAllocations),
                BoundMember.New(() => SnapshotModules),
                BoundMember.New(() => SnapshotHeaps),
                BoundMember.New(() => SymbolCache),
            };

            Deserializers["SnapshotModules"] = (Deserializer<string[]>)DeserializeModuleList;
            Serializers["SnapshotModules"] = (Serializer<string[]>)SerializeModuleList;
            Deserializers["SnapshotHeaps"] = (Deserializer<HeapSnapshot.HeapInfo[]>)DeserializeHeapList;
            Serializers["SnapshotHeaps"] = (Serializer<HeapSnapshot.HeapInfo[]>)SerializeHeapList;
            Deserializers["HeapAllocations"] = (Deserializer<UInt32[]>)DeserializeAddresses;
            Serializers["HeapAllocations"] = (Serializer<UInt32[]>)SerializeAddresses;
        }

        public DatabaseFile (TaskScheduler scheduler, string filename)
            : this(scheduler) {
            _Filename = filename;
            if (File.Exists(_Filename))
                File.Delete(_Filename);

            Directory.CreateDirectory(_Filename);
            Storage = new FolderStreamSource(_Filename);

            MakeTokenFile(filename);

            Scheduler.Start(CreateTangles(), TaskExecutionPolicy.RunAsBackgroundTask);
        }

        static void SerializeModuleList (ref SerializationContext context, ref string[] input) {
            var bw = new BinaryWriter(context.Stream);

            bw.Write(input.Length);

            foreach (var name in input)
                bw.Write(name);

            bw.Flush();
        }

        static void DeserializeModuleList (ref DeserializationContext context, out string[] output) {
            var br = new BinaryReader(context.Stream);

            int count = br.ReadInt32();
            output = new string[count];

            for (int i = 0; i < count; i++)
                output[i] = br.ReadString();
        }

        static void SerializeHeapList (ref SerializationContext context, ref HeapSnapshot.HeapInfo[] input) {
            var bw = new BinaryWriter(context.Stream);

            bw.Write(input.Length);

            foreach (var heap in input)
                context.SerializeValue(BlittableSerializer<HeapSnapshot.HeapInfo>.Serialize, heap);

            bw.Flush();
        }

        static void DeserializeHeapList (ref DeserializationContext context, out HeapSnapshot.HeapInfo[] output) {
            var br = new BinaryReader(context.Stream);

            int count = br.ReadInt32();
            output = new HeapSnapshot.HeapInfo[count];

            uint offset = 4;
            uint size = BlittableSerializer<HeapSnapshot.HeapInfo>.Size;
            for (int i = 0; i < count; i++) {
                context.DeserializeValue(BlittableSerializer<HeapSnapshot.HeapInfo>.Deserialize, offset, size, out output[i]);
                offset += size;
            }
        }

        static unsafe void SerializeAddresses (ref SerializationContext context, ref UInt32[] input) {
            var stream = context.Stream;
            var buffer = new byte[input.Length * 4];

            var lengthBytes = ImmutableBufferPool.GetBytes(input.Length);
            stream.Write(lengthBytes.Array, lengthBytes.Offset, lengthBytes.Count);

            fixed (byte * pBuffer = buffer)
            fixed (UInt32 * pInput = input)
                Native.memmove(pBuffer, (byte *)pInput, new UIntPtr((uint)buffer.Length));
                
            stream.Write(buffer, 0, buffer.Length);
        }

        static unsafe void DeserializeAddresses (ref DeserializationContext context, out UInt32[] output) {
            var stream = context.Stream;

            var pointer = context.Source;
            var count = *(int *)pointer;
            var addresses = new UInt32[count];

            if ((count * 4) + 4 > context.SourceLength)
                throw new InvalidDataException();

            fixed (UInt32 * pAddresses = addresses)
                Native.memmove((byte *)pAddresses, pointer + 4, new UIntPtr((uint)count * 4));

            output = addresses;
        }

        public static bool CheckTokenFileVersion (string filename) {
            int version;
            if (!int.TryParse(File.ReadAllText(filename), out version) || version != FileVersion)
                return false;

            return true;
        }

        protected void MakeTokenFile (string filename) {
            _TokenFilePath = Path.Combine(filename, Path.GetFileNameWithoutExtension(filename) + ".heaprecording");
            File.WriteAllText(_TokenFilePath, FileVersion.ToString());
        }

        protected IEnumerator<object> CreateTangles () {
            Delegate deserializer = null, serializer = null;

            foreach (var tf in TangleFields) {
                var constructor = tf.Type.GetConstructors()[0];
                var subStorage = new SubStreamSource(Storage, tf.Name + "_");

                Deserializers.TryGetValue(tf.Name, out deserializer);
                Serializers.TryGetValue(tf.Name, out serializer);

                var theTangle = (ITangle)constructor.Invoke(new object[] { 
                    Scheduler, subStorage, serializer, deserializer, false 
                });
                tf.Value = theTangle;
                Tangles.Add(theTangle);
            }

            yield return SymbolCache.CreateIndex(
                "ByFunction", 
                IndexSymbolByFunction
            ).Bind(() => SymbolsByFunction);

            /*
            yield return Tracebacks.CreateIndex(
                "ByFramePrefix",
                IndexTracebackByFrames
            );
             */
        }

        protected static IEnumerable<UInt32> IndexTracebackByFrames (HeapSnapshot.Traceback traceback) {
            var a = traceback.Frames.Array;
            for (int i = 0, c = traceback.Frames.Count, o = traceback.Frames.Offset; i < c; i++)
                yield return a[i + o];
        }

        protected static string IndexSymbolByFunction (ref TracebackFrame frame) {
            var fn = (frame.Function ?? "???");
            return fn;
        }

        public IEnumerator<object> Move (string targetFilename, ActivityIndicator activities) {
            // Wait for any pending operations running against the tangles
            var cb = new BarrierCollection(true, Tangles);
            using (activities.AddItem("Waiting for database to be idle"))
                yield return cb;

            foreach (var tangle in Tangles)
                tangle.Dispose();

            Tangles.Clear();

            var f = Future.RunInThread(() => {
                File.Delete(_TokenFilePath);

                if (File.Exists(targetFilename))
                    File.Delete(targetFilename);

                Storage.Folder = targetFilename;

                MakeTokenFile(targetFilename);

                _Filename = targetFilename;
            });

            using (activities.AddItem("Moving database"))
                yield return f;

            var failed = f.Failed;

            yield return CreateTangles();

            if (failed)
                throw f.Error;
        }

        public void Dispose () {
            foreach (var id in Tangles)
                id.Dispose();
            Tangles.Clear();

            Storage.Dispose();
        }
    }
}
