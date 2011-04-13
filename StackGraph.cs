using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squared.Task;

namespace HeapProfiler {
    public class StackGraphNode {
        public readonly UInt32 FrameID;
        public readonly TracebackFrame FrameInfo;

        public int Allocations = 0;
        public int BytesRequested = 0;

        public StackGraphNode (UInt32 frameId, TracebackFrame frameInfo) {
            FrameID = frameId;
            FrameInfo = frameInfo;
        }

        public void Visit (DeltaInfo delta) {
            Allocations += delta.CountDelta.GetValueOrDefault(0);
            BytesRequested += delta.BytesDelta;
        }
    }

    public class StackGraph : KeyedCollection2<UInt32, StackGraphNode> {
        private readonly object Lock = new object();

        protected override uint GetKeyForItem (StackGraphNode item) {
            return item.FrameID;
        }

        protected StackGraphNode GetNodeForFrame (UInt32 frameId, TracebackFrame frameInfo) {
            StackGraphNode result;

            lock (Lock) {
                if (!this.TryGetValue(frameId, out result))
                    this.Add(result = new StackGraphNode(frameId, frameInfo));
            }

            return result;
        }

        public IEnumerator<object> Build (HeapRecording instance, IEnumerable<DeltaInfo> deltas) {
            yield return Future.RunInThread(() => {
                Parallel.ForEach(
                    deltas, (delta) => {
                        foreach (var frame in delta.Traceback)
                            GetNodeForFrame(frame.RawOffset, frame).Visit(delta);
                    }
                );
            });

            var top100 = (from kvp in this.Dictionary
                         orderby kvp.Value.BytesRequested descending
                         select kvp).Take(100);

            foreach (var kvp in top100)
                Console.WriteLine("{0}: {1} alloc(s), {2} byte(s)", kvp.Value.FrameInfo.ToString(), kvp.Value.Allocations, kvp.Value.BytesRequested);
        }
    }
}
