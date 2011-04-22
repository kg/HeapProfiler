using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squared.Task;

namespace HeapProfiler {
    public struct FunctionKey {
        public readonly string ModuleName;
        public readonly string FunctionName;
        public readonly string SourceFile;

        public FunctionKey (string module, string function, string sourceFile) {
            ModuleName = module;
            FunctionName = function;
            SourceFile = sourceFile;
        }

        public bool Equals (FunctionKey rhs) {
            return ModuleName.Equals(rhs.ModuleName) &&
                FunctionName.Equals(rhs.FunctionName);
        }

        public override bool Equals (object obj) {
            if (obj is FunctionKey)
                return Equals((FunctionKey)obj);
            else
                return base.Equals(obj);
        }

        public override int GetHashCode () {
            return ModuleName.GetHashCode() ^ 
                FunctionName.GetHashCode();
        }

        public override string ToString () {
            if (SourceFile != null)
                return String.Format("{0}!{1} ({2})", ModuleName, FunctionName, SourceFile);
            else
                return String.Format("{0}!{1}", ModuleName, FunctionName);
        }
    }

    public class StackGraphNode {
        public readonly FunctionKey Key;

        public readonly HashSet<UInt32> VisitedTracebacks = new HashSet<UInt32>();
        public readonly HashSet<StackGraphNode> Parents = new HashSet<StackGraphNode>();
        public readonly HashSet<StackGraphNode> Children = new HashSet<StackGraphNode>();

        public bool Ignored = false;
        public int Allocations = 0;
        public int BytesRequested = 0;

        public StackGraphNode (FunctionKey key) {
            Key = key;
        }

        public void Visit (DeltaInfo delta) {
            lock (this) {
                if (VisitedTracebacks.Contains(delta.TracebackID))
                    return;
                else
                    VisitedTracebacks.Add(delta.TracebackID);

                Allocations += delta.CountDelta.GetValueOrDefault(0);
                BytesRequested += delta.BytesDelta;
            }
        }

        public void Visit (HeapSnapshot.Allocation allocation) {
            lock (this) {
                if (VisitedTracebacks.Contains(allocation.TracebackID))
                    return;
                else
                    VisitedTracebacks.Add(allocation.TracebackID);

                Allocations += 1;
                BytesRequested += (int)allocation.Size;
            }
        }

        public void AddChild (StackGraphNode child) {
            lock (Children)
                Children.Add(child);

            lock (child.Parents)
                child.Parents.Add(this);
        }

        public void RemoveChild (StackGraphNode child) {
            lock (Children)
                Children.Remove(child);

            lock (child.Parents)
                child.Parents.Remove(this);
        }

        public override string ToString () {
            return String.Format(
                "{0}\r\n{1}{2} allocation(s), {3}{4}\r\nCalls {5} different function(s)\r\nCalled by {6} different function(s)", 
                Key.ToString(), 
                (Allocations > 0) ? "+" : "-",
                Math.Abs(Allocations), 
                (BytesRequested > 0) ? "+" : "-",
                FileSize.Format(Math.Abs(BytesRequested)),
                Children.Count,
                Parents.Count
            );
        }

        public override bool Equals (object obj) {
            return Object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode () {
            return Key.GetHashCode();
        }
    }

    public class StackGraph : KeyedCollection2<FunctionKey, StackGraphNode> {
        private readonly object Lock = new object();

        protected override FunctionKey GetKeyForItem (StackGraphNode item) {
            return item.Key;
        }

        protected StackGraphNode GetNodeForFrame (TracebackFrame frameInfo) {
            StackGraphNode result;

            if ((frameInfo.Module == null) || (frameInfo.Function == null))
                return null;

            var key = new FunctionKey(frameInfo.Module, frameInfo.Function, frameInfo.SourceFile);

            lock (Lock) {
                if (!this.TryGetValue(key, out result))
                    this.Add(result = new StackGraphNode(key));
            }

            return result;
        }

        protected IEnumerator<object> FinalizeBuild () {
            yield return Future.RunInThread(() => {
                lock (Lock)
                    Parallel.ForEach(
                        this.Values,
                        (node) => node.VisitedTracebacks.Clear()
                    );
            });

            lock (Lock) {
                int numIgnored = 0;
                StackGraphNode child;

                foreach (var node in Values) {
                    while ((node.Children.Count == 1) &&
                        ((child = node.Children.First()).Parents.Count == 1)) {

                            numIgnored += 1;
                        child.Ignored = true;
                        node.Children.Remove(child);

                        foreach (var subchild in child.Children.ToArray()) {
                            node.AddChild(subchild);
                            child.RemoveChild(subchild);
                        }
                    }
                }
            }
        }

        public IEnumerator<object> Build (HeapRecording instance, IEnumerable<DeltaInfo> deltas) {
            yield return Future.RunInThread(() => {
                Parallel.ForEach(
                    deltas, (delta) => {
                        StackGraphNode parent = null;

                        foreach (var frame in delta.Traceback) {
                            var node = GetNodeForFrame(frame);

                            if (node != null) {
                                node.Visit(delta);

                                if (parent != null)
                                    parent.AddChild(node);
                            }

                            if (node != null)
                                parent = node;
                        }
                    }
                );
            });

            yield return FinalizeBuild();
        }

        public IEnumerator<object> Build (
            HeapSnapshot snapshot, 
            Dictionary<uint, HeapSnapshot.Traceback> tracebacks,
            Dictionary<uint, TracebackFrame> symbols
        ) {
            var allocations = from heap in snapshot.Heaps
                              from allocation in heap.Allocations
                              select allocation;

            yield return Future.RunInThread(() => {
                Parallel.ForEach(
                    allocations, (alloc) => {
                        StackGraphNode parent = null;
                        var traceback = tracebacks[alloc.TracebackID];

                        foreach (var frameId in traceback) {
                            var frame = symbols[frameId];
                            var node = GetNodeForFrame(frame);

                            if (node != null) {
                                node.Visit(alloc);

                                if (parent != null)
                                    parent.AddChild(node);
                            }

                            if (node != null)
                                parent = node;
                        }
                    }
                );
            });

            yield return FinalizeBuild();
        }

        public IEnumerable<StackGraphNode> TopItems {
            get {
                if (this.Dictionary != null)
                    return from kvp in this.Dictionary
                           orderby kvp.Value.BytesRequested descending
                           where !kvp.Value.Ignored
                           select kvp.Value;
                else
                    return from item in this.Items
                           orderby item.BytesRequested descending
                           where !item.Ignored
                           select item;
            }
        }
    }

    public class StackGraphTooltipContent : TooltipContentBase {
        public readonly StackGraphNode Node;
        public readonly StringFormat StringFormat;

        public StackGraphTooltipContent (StackGraphNode node, StringFormat sf) {
            Node = node;
            StringFormat = sf;
        }

        public override void Render (Graphics g) {
            using (var brush = new SolidBrush(SystemColors.WindowText))
                g.DrawString(
                    Node.ToString(), Font, brush, new PointF(0, 0), StringFormat
                );
        }

        public override Size Measure (Graphics g) {
            var size = g.MeasureString(
                Node.ToString(), Font, 99999, StringFormat
            );

            return new Size(
                (int)Math.Ceiling(size.Width),
                (int)Math.Ceiling(size.Height)
            );
        }
    }
}
