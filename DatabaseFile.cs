using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Squared.Data.Mangler;
using Squared.Data.Mangler.Serialization;
using Squared.Task;
using System.IO;
using Squared.Util.Bind;

namespace HeapProfiler {
    public class DatabaseFile : IDisposable {
        public readonly TaskScheduler Scheduler;

        public FolderStreamSource Storage;

        public Tangle<HeapSnapshot> Snapshots;
        public Tangle<MemoryStatistics> MemoryStatistics;
        public Tangle<HeapSnapshot.Module> Modules;
        public Tangle<HeapSnapshot.Heap> Heaps;
        public Tangle<HeapSnapshot.Traceback> Tracebacks;
        public Tangle<HeapSnapshot.AllocationRanges> Allocations;
        public Tangle<TracebackFrame> SymbolCache;
        public Tangle<string[]> SnapshotModules;
        public Tangle<uint[]> SnapshotHeaps;

        private readonly Dictionary<string, Delegate> Deserializers = new Dictionary<string, Delegate>();
        private readonly Dictionary<string, Delegate> Serializers = new Dictionary<string, Delegate>();

        private readonly IBoundMember[] TangleFields;
        private HashSet<IDisposable> Tangles = new HashSet<IDisposable>();
        private string _Filename;

        protected DatabaseFile (TaskScheduler scheduler) {
            Scheduler = scheduler;

            TangleFields = new IBoundMember[] { 
                BoundMember.New(() => Snapshots),
                BoundMember.New(() => MemoryStatistics),
                BoundMember.New(() => Modules),
                BoundMember.New(() => Heaps),
                BoundMember.New(() => Tracebacks),
                BoundMember.New(() => Allocations),
                BoundMember.New(() => SnapshotModules),
                BoundMember.New(() => SnapshotHeaps),
                BoundMember.New(() => SymbolCache)
            };

            Deserializers["SnapshotModules"] = (Deserializer<string[]>)DeserializeModuleList;
            Serializers["SnapshotModules"] = (Serializer<string[]>)SerializeModuleList;
            Deserializers["SnapshotHeaps"] = (Deserializer<uint[]>)DeserializeHeapList;
            Serializers["SnapshotHeaps"] = (Serializer<uint[]>)SerializeHeapList;
        }

        public DatabaseFile (TaskScheduler scheduler, string filename)
            : this(scheduler) {
            _Filename = filename;
            if (File.Exists(_Filename))
                File.Delete(_Filename);

            Directory.CreateDirectory(_Filename);

            Storage = new FolderStreamSource(_Filename);

            CreateTangles();
        }

        protected void SerializeModuleList (ref string[] input, Stream output) {
            var bw = new BinaryWriter(output);

            bw.Write(input.Length);

            foreach (var name in input)
                bw.Write(name);

            bw.Flush();
        }

        protected void DeserializeModuleList (Stream input, out string[] output) {
            var br = new BinaryReader(input);

            int count = br.ReadInt32();
            output = new string[count];

            for (int i = 0; i < count; i++)
                output[i] = br.ReadString();
        }

        protected void SerializeHeapList (ref uint[] input, Stream output) {
            var bw = new BinaryWriter(output);

            bw.Write(input.Length);

            foreach (var id in input)
                bw.Write(id);

            bw.Flush();
        }

        protected void DeserializeHeapList (Stream input, out uint[] output) {
            var br = new BinaryReader(input);

            int count = br.ReadInt32();
            output = new uint[count];

            for (int i = 0; i < count; i++)
                output[i] = br.ReadUInt32();
        }

        protected void CreateTangles () {
            Delegate deserializer = null, serializer = null;

            foreach (var tf in TangleFields) {
                var constructor = tf.Type.GetConstructors()[0];
                var subStorage = new SubStreamSource(Storage, tf.Name + "_");

                Deserializers.TryGetValue(tf.Name, out deserializer);
                Serializers.TryGetValue(tf.Name, out serializer);

                var theTangle = (IDisposable)constructor.Invoke(new object[] { 
                    Scheduler, subStorage, serializer, deserializer, false 
                });
                tf.Value = theTangle;
                Tangles.Add(theTangle);
            }
        }

        public IEnumerator<object> Move (string targetFilename) {
            foreach (var id in Tangles)
                id.Dispose();
            Tangles.Clear();

            var f = Future.RunInThread(() => {
                if (File.Exists(targetFilename))
                    File.Delete(targetFilename);

                Storage.Folder = targetFilename;
            });
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
