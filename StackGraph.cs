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

        public readonly HashSet<DeltaInfo> VisitedDeltas = new HashSet<DeltaInfo>();

        public int Allocations = 0;
        public int BytesRequested = 0;

        public StackGraphNode (FunctionKey key) {
            Key = key;
        }

        public void Visit (DeltaInfo delta) {
            lock (this) {
                if (VisitedDeltas.Contains(delta))
                    return;
                else
                    VisitedDeltas.Add(delta);

                Allocations += delta.CountDelta.GetValueOrDefault(0);
                BytesRequested += delta.BytesDelta;
            }
        }

        public override string ToString () {
            return String.Format(
                "{0}\r\n{1}{2} allocation(s), {3}{4}", 
                Key.ToString(), 
                (Allocations > 0) ? "+" : "-",
                Math.Abs(Allocations), 
                (BytesRequested > 0) ? "+" : "-",
                FileSize.Format(Math.Abs(BytesRequested))
            );
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

        public IEnumerator<object> Build (HeapRecording instance, IEnumerable<DeltaInfo> deltas) {
            yield return Future.RunInThread(() => {
                Parallel.ForEach(
                    deltas, (delta) => {
                        foreach (var frame in delta.Traceback) {
                            var node = GetNodeForFrame(frame);

                            if (node != null)
                                node.Visit(delta);
                        }
                    }
                );
            });

            lock (Lock)
                foreach (StackGraphNode node in this.Values)
                    node.VisitedDeltas.Clear();
        }

        public IEnumerable<StackGraphNode> TopItems {
            get {
                if (this.Dictionary != null)
                    return from kvp in this.Dictionary
                           orderby kvp.Value.BytesRequested descending
                           select kvp.Value;
                else
                    return from item in this.Items
                           orderby item.BytesRequested descending
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
