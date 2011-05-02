using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squared.Task;

namespace HeapProfiler {
    public enum GraphKeyType {
        None = 0,
        Function,
        Module,
        SourceFile,
        SourceFolder
    }

    public struct StackGraphKey {
        public readonly GraphKeyType KeyType;

        public readonly string ModuleName;
        public readonly string FunctionName;
        public readonly string SourceFile;
        public readonly string SourceFolder;

        private readonly int HashCode;

        public StackGraphKey (GraphKeyType keyType, string module, string function, string sourceFile) {
            KeyType = keyType;
            ModuleName = module;
            FunctionName = function;
            SourceFile = sourceFile;
            SourceFolder = Path.GetDirectoryName(sourceFile);

            switch (keyType) {
                case GraphKeyType.Function:
                    HashCode = ModuleName.GetHashCode() ^
                        FunctionName.GetHashCode();
                break;
                case GraphKeyType.Module:
                    FunctionName = null;
                    HashCode = ModuleName.GetHashCode();
                break;
                case GraphKeyType.SourceFile:
                    FunctionName = null;
                    ModuleName = null;
                    HashCode = (SourceFile ?? "").GetHashCode();
                break;
                case GraphKeyType.SourceFolder:
                    FunctionName = null;
                    ModuleName = null;
                    SourceFile = null;
                    HashCode = (SourceFolder ?? "").GetHashCode();
                break;
                default:
                    throw new InvalidDataException();
            }
        }

        public bool Equals (StackGraphKey rhs) {
            switch (KeyType) {
                case GraphKeyType.Function:
                    return ModuleName.Equals(rhs.ModuleName) &&
                        FunctionName.Equals(rhs.FunctionName);
                case GraphKeyType.Module:
                    return ModuleName.Equals(rhs.ModuleName);
                case GraphKeyType.SourceFile:
                    return String.Equals(SourceFile, rhs.SourceFile);
                case GraphKeyType.SourceFolder:
                    return String.Equals(SourceFolder, rhs.SourceFolder);
                default:
                    throw new InvalidDataException();
            }
        }

        public override bool Equals (object obj) {
            if (obj is StackGraphKey)
                return Equals((StackGraphKey)obj);
            else
                return base.Equals(obj);
        }

        public override int GetHashCode () {
            return HashCode;
        }

        public override string ToString () {
            switch (KeyType) {
                case GraphKeyType.Function:
                    return String.Format("{0}!{1}", ModuleName, FunctionName);
                case GraphKeyType.Module:
                    return ModuleName;
                case GraphKeyType.SourceFile:
                    return SourceFile ?? "<no symbols>";
                case GraphKeyType.SourceFolder:
                    return SourceFolder ?? "<no symbols>";
                default:
                    throw new InvalidDataException();
            }
        }
    }

    public class StackGraphNode : IEnumerable<StackGraphNode> {
        public readonly StackGraphKey Key;

        public readonly HashSet<UInt32> VisitedTracebacks = new HashSet<UInt32>();
        public readonly HashSet<StackGraphNode> Parents = new HashSet<StackGraphNode>();
        public readonly StackGraphNodeCollection Children;

        public int CulledFrames = 0;
        public int Allocations = 0;
        public int BytesRequested = 0;

        public StackGraphNode (StackGraphKey key) {
            Key = key;
            Children = new StackGraphNodeCollection(key.KeyType);
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

        public StackGraphNode GetNodeForChildFrame (TracebackFrame frameInfo) {
            bool isNew;
            StackGraphNode result;

            lock (Children)
                result = Children.GetNodeForFrame(frameInfo, out isNew);

            if (isNew)
                lock (result.Parents)
                    result.Parents.Add(this);

            return result;
        }

        public void Finalize () {
            VisitedTracebacks.Clear();

            lock (Children) {
                StackGraphNode child;
                if (
                    (Children.Count == 1) && 
                    ((child = Children.First()).Parents.Count == 1)
                ) {
                    Children.Remove(child);

                    foreach (var subchild in child.Children) {
                        subchild.CulledFrames = child.CulledFrames + 1;
                        Children.Add(subchild);
                        subchild.Parents.Clear();
                        subchild.Parents.Add(this);
                    }
                }

                foreach (var c in Children)
                    c.Finalize();
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
                Key, 
                (Allocations > 0) ? "+" : "-",
                Math.Abs(Allocations), 
                (BytesRequested > 0) ? "+" : "-",
                FileSize.Format(Math.Abs(BytesRequested)),
                Children.Count,
                Parents.Count
            );
        }

        public override bool Equals (object obj) {
            var rhs = obj as StackGraphNode;

            if (rhs != null)
                return Key.Equals(rhs.Key);
            else
                return base.Equals(obj);
        }

        public override int GetHashCode () {
            return Key.GetHashCode();
        }

        IEnumerator<StackGraphNode> IEnumerable<StackGraphNode>.GetEnumerator () {
            return
                (from child in Children
                orderby child.BytesRequested descending
                select child).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () {
            return ((IEnumerable<StackGraphNode>)this).GetEnumerator();
        }
    }

    public class StackGraphNodeCollection : KeyedCollection2<StackGraphKey, StackGraphNode> {
        public readonly GraphKeyType KeyType;
        protected readonly object Lock = new object();

        public StackGraphNodeCollection (GraphKeyType keyType) {
            KeyType = keyType;
        }

        protected override StackGraphKey GetKeyForItem (StackGraphNode item) {
            return item.Key;
        }

        public StackGraphNode GetNodeForFrame (TracebackFrame frameInfo) {
            bool temp;
            return GetNodeForFrame(frameInfo, out temp);
        }

        public StackGraphNode GetNodeForFrame (TracebackFrame frameInfo, out bool isNew) {
            StackGraphNode result;

            if ((frameInfo.Module == null) || (frameInfo.Function == null)) {
                isNew = false;
                return null;
            }

            var key = new StackGraphKey(KeyType, frameInfo.Module, frameInfo.Function, frameInfo.SourceFile);

            lock (Lock) {
                if (!this.TryGetValue(key, out result)) {
                    isNew = true;
                    this.Add(result = new StackGraphNode(key));
                } else {
                    isNew = false;
                }
            }

            return result;
        }

        public IEnumerable<StackGraphNode> OrderedItems {
            get {
                return from item in this.Items
                       orderby item.BytesRequested descending
                       select item;
            }
        }
    }

    public class StackGraph : StackGraphNodeCollection {
        public readonly StackGraphNodeCollection Functions;

        public StackGraph (GraphKeyType keyType) 
            : base (keyType) {

            Functions = new StackGraphNodeCollection(KeyType);
        }

        protected IEnumerator<object> FinalizeBuild () {
            yield return Future.RunInThread(() => {
                lock (Lock)
                    Parallel.ForEach(
                        this.Items,
                        (node) => node.Finalize()
                    );
            });
        }

        public IEnumerator<object> Build (HeapRecording instance, IEnumerable<DeltaInfo> deltas) {
            yield return Future.RunInThread(() => {
                Parallel.ForEach(
                    deltas, (delta) => {
                        StackGraphNode parent = null, node;

                        foreach (var frame in delta.Traceback.Reverse()) {
                            if (parent != null)
                                node = parent.GetNodeForChildFrame(frame);
                            else
                                node = GetNodeForFrame(frame);

                            if (node != null)
                                node.Visit(delta);

                            if (node != null)
                                parent = node;

                            node = Functions.GetNodeForFrame(frame);
                            node.Visit(delta);
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
                        StackGraphNode parent = null, node;
                        var traceback = tracebacks[alloc.TracebackID];

                        foreach (var frameId in traceback.Reverse()) {
                            var frame = symbols[frameId];

                            if (parent != null)
                                node = parent.GetNodeForChildFrame(frame);
                            else
                                node = GetNodeForFrame(frame);

                            if (node != null)
                                node.Visit(alloc);

                            if (node != null)
                                parent = node;

                            node = Functions.GetNodeForFrame(frame);
                            node.Visit(alloc);
                        }
                    }
                );
            });

            yield return FinalizeBuild();
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
