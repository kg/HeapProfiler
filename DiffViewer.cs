using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Squared.Task;
using System.Diagnostics;
using System.IO;
using Squared.Task.IO;
using System.Text.RegularExpressions;
using Squared.Util.RegexExtensions;
using System.Globalization;

namespace HeapProfiler {
    public partial class DiffViewer : TaskForm {
        public struct ModuleInfo {
            public string ModuleName;
            public string SymbolType;
            public string SymbolPath;
        }

        public struct DeltaInfo {
            public bool Added;
            public int BytesDelta, OldBytes, NewBytes, NewCount;
            public int? CountDelta, OldCount;
            public string TraceId;
        }

        public struct TracebackInfo {
            public string TraceId;
            public TracebackFrame[] Frames;
        }

        public struct TracebackFrame {
            public string Module;
            public string Function;
            public UInt32 Offset;
            public UInt32? Offset2;
        }

        public static Regex ModuleRegex = new Regex(
            @"DBGHELP: (?'module'\w*) - (?'symboltype'[^\n\r]*)", 
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex BytesDeltaRegex = new Regex(
            @"(?'type'\+|\-)(\s+)(?'delta_bytes'[\da-fA-F]+)(\s+)\((\s*)(?'old_bytes'[\da-fA-F]*)(\s*)-(\s*)(?'new_bytes'[\da-fA-F]*)\)(\s*)(?'new_count'[\da-fA-F]+) allocs\t(BackTrace(?'trace_id'\w*))",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex CountDeltaRegex = new Regex(
            @"(?'type'\+|\-)(\s+)(?'delta'[\da-fA-F]+)(\s+)\((\s*)(?'old_count'[\da-fA-F]*)(\s*)-(\s*)(?'new_count'[\da-fA-F]*)\)\t(BackTrace(?'trace_id'\w*))\tallocations",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        public static Regex TracebackRegex = new Regex(
            @"\t(?'module'[^!]+)!(?'function'[^+]+)\+(?'offset'[\dA-Fa-f]+)(\s*:\s*(?'offset2'[\dA-Fa-f]+))?",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        public List<ModuleInfo> Modules = new List<ModuleInfo>();
        public List<DeltaInfo> Deltas = new List<DeltaInfo>();
        public Dictionary<string, TracebackInfo> Tracebacks = new Dictionary<string, TracebackInfo>();

        public DiffViewer (TaskScheduler scheduler)
            : base (scheduler) {
            InitializeComponent();
        }

        public IEnumerator<object> LoadDiff (string filename) {
            using (var fda = new FileDataAdapter(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var input = new AsyncTextReader(fda)) {
                Future<string> nextLine;
                string line = null;

                try {
                    LoadingProgress.Style = ProgressBarStyle.Continuous;
                    LoadingProgress.Maximum = (int)fda.BaseStream.Length;
                } catch {
                }

                int i = 0;
                while (true) {
                    i += 1;
                    if ((i % 5 == 0) && (LoadingProgress.Style == ProgressBarStyle.Continuous)) {
                        int v = (int)fda.BaseStream.Position;
                        LoadingProgress.Value = Math.Min(v + 1, LoadingProgress.Maximum);
                        LoadingProgress.Value = v;
                    }

                    if (line == null) {
                        yield return nextLine = input.ReadLine();
                        line = nextLine.Result;

                        if (line == null)
                            break;
                    }

                    Match m;
                    if (ModuleRegex.TryMatch(line, out m)) {
                        yield return nextLine = input.ReadLine();
                        line = nextLine.Result;

                        Modules.Add(new ModuleInfo {
                            ModuleName = String.Intern(m.Groups["module"].Value),
                            SymbolType = String.Intern(m.Groups["symboltype"].Value),
                            SymbolPath = line.Trim()
                        });
                    } else if (BytesDeltaRegex.TryMatch(line, out m)) {
                        var info = new DeltaInfo {
                            Added = (m.Groups["type"].Value == "+"),
                            BytesDelta = int.Parse(m.Groups["delta_bytes"].Value),
                            NewBytes = int.Parse(m.Groups["new_bytes"].Value),
                            OldBytes = int.Parse(m.Groups["old_bytes"].Value),
                            NewCount = int.Parse(m.Groups["new_count"].Value),
                            TraceId = String.Intern(m.Groups["trace_id"].Value)
                        };

                        yield return nextLine = input.ReadLine();
                        line = nextLine.Result;
                        if (CountDeltaRegex.TryMatch(line, out m)) {
                            info.OldCount = int.Parse(m.Groups["old_count"].Value);
                            info.CountDelta = int.Parse(m.Groups["delta"].Value);
                        }

                        bool readingLeadingWhitespace = true;

                        var frames = new List<TracebackFrame>();

                        while (true) {
                            yield return nextLine = input.ReadLine();
                            line = nextLine.Result;

                            if (line == null)
                                break;
                            else if (line.Trim().Length == 0) {
                                if (readingLeadingWhitespace)
                                    continue;
                                else
                                    break;
                            } else if (TracebackRegex.TryMatch(line, out m)) {
                                readingLeadingWhitespace = false;
                                var frame = new TracebackFrame {
                                    Module = String.Intern(m.Groups["module"].Value),
                                    Function = String.Intern(m.Groups["function"].Value),
                                    Offset = UInt32.Parse(m.Groups["offset"].Value, NumberStyles.HexNumber)
                                };
                                if (m.Groups["offset2"].Success)
                                    frame.Offset2 = UInt32.Parse(m.Groups["offset2"].Value, NumberStyles.HexNumber);

                                frames.Add(frame);
                            }
                        }

                        if (Tracebacks.ContainsKey(info.TraceId)) {
                            Console.WriteLine("Duplicate traceback for id {0}!", info.TraceId);
                        } else {
                            Tracebacks[info.TraceId] = new TracebackInfo {
                                TraceId = info.TraceId,
                                Frames = frames.ToArray()
                            };
                        }

                        Deltas.Add(info);
                    }

                    if (line == null)
                        break;
                    line = null;
                }
            }

            LoadingPanel.Visible = false;
            UseWaitCursor = false;
            try {
                File.Delete(filename);
            } catch {
            }
        }

        private void DiffViewer_Shown (object sender, EventArgs e) {
            UseWaitCursor = true;
        }

        private void DiffViewer_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();
        }
    }
}
