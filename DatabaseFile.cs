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
        public readonly TaskScheduler Scheduler;

        public FolderStreamSource Storage;

        public Tangle<HeapSnapshotInfo> Snapshots;
        public Tangle<HeapSnapshot.Module> Modules;
        public Tangle<HeapSnapshot.Traceback> Tracebacks;
        public Tangle<HeapSnapshot.AllocationRanges> Allocations;
        public Tangle<HashSet<TangleKey>> HeapAllocations;
        public Tangle<TracebackFrame> SymbolCache;
        public Tangle<string[]> SnapshotModules;
        public Tangle<HeapSnapshot.HeapInfo[]> SnapshotHeaps;

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
                BoundMember.New(() => SymbolCache)
            };

            Deserializers["SnapshotModules"] = (Deserializer<string[]>)DeserializeModuleList;
            Serializers["SnapshotModules"] = (Serializer<string[]>)SerializeModuleList;
            Deserializers["SnapshotHeaps"] = (Deserializer<HeapSnapshot.HeapInfo[]>)DeserializeHeapList;
            Serializers["SnapshotHeaps"] = (Serializer<HeapSnapshot.HeapInfo[]>)SerializeHeapList;
            Deserializers["HeapAllocations"] = (Deserializer<HashSet<TangleKey>>)DeserializeKeySet;
            Serializers["HeapAllocations"] = (Serializer<HashSet<TangleKey>>)SerializeKeySet;
        }

        public DatabaseFile (TaskScheduler scheduler, string filename)
            : this(scheduler) {
            _Filename = filename;
            if (File.Exists(_Filename))
                File.Delete(_Filename);

            Directory.CreateDirectory(_Filename);
            Storage = new FolderStreamSource(_Filename);

            MakeTokenFile(filename);

            CreateTangles();
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

        static unsafe void SerializeKeySet (ref SerializationContext context, ref HashSet<TangleKey> input) {
            var stream = context.Stream;
            var buffer = new byte[4];

            fixed (byte * pBuffer = buffer) {
                *(int *)pBuffer = input.Count;
                stream.Write(buffer, 0, 4);

                foreach (var key in input) {
                    var data = key.Data;

                    *(ushort *)pBuffer = key.OriginalTypeId;
                    stream.Write(buffer, 0, 2);

                    *(ushort *)pBuffer = (ushort)data.Count;
                    stream.Write(buffer, 0, 2);

                    stream.Write(data.Array, data.Offset, data.Count);
                }
            }
        }

        static unsafe void DeserializeKeySet (ref DeserializationContext context, out HashSet<TangleKey> output) {
            var stream = context.Stream;

            var pointer = context.Source;
            var count = *(int *)pointer;
            var keys = new TangleKey[count];
            var buffer = new byte[context.SourceLength - 4];
            int offset = 0;

            fixed (byte * pBuffer = buffer)
                Native.memmove(pBuffer, context.Source + 4, new UIntPtr((uint)buffer.Length));

            pointer += 4;

            for (int i = 0; i < count; i++) {
                var keyType = *(ushort *)(pointer + offset);
                offset += 2;

                var keyLength = *(ushort *)(pointer + offset);
                offset += 2;

                keys[i] = new TangleKey(new ArraySegment<byte>(buffer, offset, keyLength), keyType);
                offset += keyLength;
            }

            output = new HashSet<TangleKey>(keys);
        }

        protected void MakeTokenFile (string filename) {
            _TokenFilePath = Path.Combine(filename, Path.GetFileNameWithoutExtension(filename) + ".heaprecording");
            File.WriteAllText(_TokenFilePath, "");
        }

        protected void CreateTangles () {
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

            CreateTangles();

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
