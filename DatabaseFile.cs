using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Squared.Data.Mangler;
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
                BoundMember.New(() => Allocations)
            };
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

        protected void CreateTangles () {
            foreach (var tf in TangleFields) {
                var constructor = tf.Type.GetConstructors()[0];
                var subStorage = new SubStreamSource(Storage, tf.Name + "_");
                var theTangle = (IDisposable)constructor.Invoke(new object[] { Scheduler, subStorage, null, null, false });
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
            var temp = f.Failed;

            CreateTangles();
        }

        public void Dispose () {
            foreach (var id in Tangles)
                id.Dispose();
            Tangles.Clear();

            Storage.Dispose();
        }
    }
}
