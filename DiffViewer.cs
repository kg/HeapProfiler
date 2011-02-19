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
        public class ModuleInfo {
            public bool Filtered = false;
            public string ModuleName;
            public string SymbolType;
            public string SymbolPath;
            public int References = 0;

            public override string ToString () {
                return ModuleName;
            }
        }

        public class DeltaInfo {
            public bool Added;
            public int BytesDelta, OldBytes, NewBytes, NewCount;
            public int? CountDelta, OldCount;
            public TracebackInfo Traceback;

            public string ToString (bool includeTraceback) {
                var result = String.Format(
                    "{0} {1} byte(s) ({2} - {3})",
                    Added ? "+" : "-",
                    BytesDelta, NewBytes, OldBytes
                );

                if (includeTraceback)
                    result += Environment.NewLine + Traceback.ToString();

                return result;
            }

            public override string ToString () {
                return ToString(true);
            }
        }

        public class TracebackInfo {
            public string TraceId;
            public TracebackFrame[] Frames;
            public HashSet<string> Functions;
            public HashSet<string> Modules;

            public override string ToString () {
                var sb = new StringBuilder();

                foreach (var frame in Frames) {
                    if (sb.Length > 0)
                        sb.AppendLine();

                    sb.Append(frame.ToString());
                }

                return sb.ToString();
            }
        }

        public struct TracebackFrame {
            public string Module;
            public string Function;
            public UInt32 Offset;
            public UInt32? Offset2;

            public override string ToString () {
                if (Offset2.HasValue)
                    return String.Format("{0}!{1}@{2:x8}", Module, Function, Offset2.Value);
                else
                    return String.Format("{0}!{1}+{2:x8}", Module, Function, Offset);
            }
        }

        public static Regex ModuleRegex = new Regex(
            @"DBGHELP: (?'module'.*?)( - )(?'symboltype'[^\n\r]*)", 
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

        public Dictionary<string, ModuleInfo> Modules = new Dictionary<string, ModuleInfo>();
        public HashSet<string> FunctionNames = new HashSet<string>();
        public List<DeltaInfo> Deltas = new List<DeltaInfo>();
        public Dictionary<string, TracebackInfo> Tracebacks = new Dictionary<string, TracebackInfo>();
        public List<DeltaInfo> ListItems = new List<DeltaInfo>();

        protected string Filename;
        protected string FunctionFilter = null;
        protected StringFormat DeltaListFormat;
        protected bool Updating = false;

        public DiffViewer (TaskScheduler scheduler)
            : base (scheduler) {
            InitializeComponent();

            DeltaListFormat = new StringFormat();
            DeltaListFormat.Trimming = StringTrimming.None;
            DeltaListFormat.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
        }

        protected void SetBusy (bool busy) {
            UseWaitCursor = Updating = busy;
        }

        public IEnumerator<object> LoadDiff (string filename) {
            LoadingPanel.Text = "Loading diff...";
            Text = "Diff Viewer - " + filename;

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
                    if ((i % 15 == 0) && (LoadingProgress.Style == ProgressBarStyle.Continuous)) {
                        int v = (int)fda.BaseStream.Position;
                        // Setting the progress higher and then lower bypasses the slow animation baked into
                        //  the windows theme engine's progress bar implementation
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
                        var moduleName = String.Intern(m.Groups["module"].Value);

                        var info = new ModuleInfo {
                            ModuleName = moduleName,
                            SymbolType = String.Intern(m.Groups["symboltype"].Value),
                        };

                        yield return nextLine = input.ReadLine();
                        line = nextLine.Result;
                        if (!ModuleRegex.IsMatch(line)) {
                            info.SymbolPath = line.Trim();
                            line = null;
                        }

                        Modules[moduleName] = info;

                        continue;
                    } else if (BytesDeltaRegex.TryMatch(line, out m)) {
                        var traceId = String.Intern(m.Groups["trace_id"].Value);
                        var info = new DeltaInfo {
                            Added = (m.Groups["type"].Value == "+"),
                            BytesDelta = int.Parse(m.Groups["delta_bytes"].Value),
                            NewBytes = int.Parse(m.Groups["new_bytes"].Value),
                            OldBytes = int.Parse(m.Groups["old_bytes"].Value),
                            NewCount = int.Parse(m.Groups["new_count"].Value),
                        };

                        yield return nextLine = input.ReadLine();
                        line = nextLine.Result;
                        if (CountDeltaRegex.TryMatch(line, out m)) {
                            info.OldCount = int.Parse(m.Groups["old_count"].Value);
                            info.CountDelta = int.Parse(m.Groups["delta"].Value);
                        }

                        bool readingLeadingWhitespace = true;

                        var frames = new List<TracebackFrame>();
                        var modules = new HashSet<string>();
                        var functions = new HashSet<string>();

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

                                var moduleName = String.Intern(m.Groups["module"].Value);
                                modules.Add(moduleName);

                                var functionName = String.Intern(m.Groups["function"].Value);
                                functions.Add(functionName);
                                FunctionNames.Add(functionName);

                                if (!Modules.ContainsKey(moduleName)) {
                                    Modules[moduleName] = new ModuleInfo {
                                        ModuleName = moduleName,
                                        SymbolType = "Unknown",
                                        References = 1
                                    };
                                } else {
                                    Modules[moduleName].References += 1;
                                }

                                var frame = new TracebackFrame {
                                    Module = moduleName,
                                    Function = functionName,
                                    Offset = UInt32.Parse(m.Groups["offset"].Value, NumberStyles.HexNumber)
                                };
                                if (m.Groups["offset2"].Success)
                                    frame.Offset2 = UInt32.Parse(m.Groups["offset2"].Value, NumberStyles.HexNumber);

                                frames.Add(frame);
                            }
                        }

                        if (Tracebacks.ContainsKey(traceId)) {
                            info.Traceback = Tracebacks[traceId];
                            Console.WriteLine("Duplicate traceback for id {0}!", traceId);
                        } else {
                            info.Traceback = Tracebacks[traceId] = new TracebackInfo {
                                TraceId = traceId,
                                Frames = frames.ToArray(),
                                Modules = modules,
                                Functions = functions
                            };
                        }

                        Deltas.Add(info);
                    }

                    if (line == null)
                        break;
                    line = null;
                }
            }

            foreach (var key in Modules.Keys.ToArray()) {
                if (Modules[key].References == 0)
                    Modules.Remove(key);
            }

            TracebackFilter.AutoCompleteCustomSource.Clear();
            TracebackFilter.AutoCompleteCustomSource.AddRange(FunctionNames.ToArray());

            Filename = filename;
            RefreshModules();
            RefreshDeltas();

            MainMenuStrip.Enabled = true;
            LoadingPanel.Visible = false;
            MainSplit.Visible = true;
            UseWaitCursor = false;
        }

        public void RefreshModules () {
            if (Updating)
                return;

            SetBusy(true);

            ModuleList.BeginUpdate();
            ModuleList.Items.Clear();
            foreach (var key in Modules.Keys.OrderBy((s) => s))
                ModuleList.Items.Add(Modules[key]);
            for (int i = 0; i < ModuleList.Items.Count; i++)
                ModuleList.SetItemChecked(i, true);
            ModuleList.EndUpdate();

            SetBusy(false);
        }

        public void RefreshDeltas () {
            if (Updating)
                return;

            SetBusy(true);

            ListItems.Clear();
            foreach (var delta in Deltas) {
                if (FunctionFilter != null) {
                    if (!delta.Traceback.Functions.Contains(FunctionFilter))
                        continue;
                }

                bool filteredOut = true;
                foreach (var module in delta.Traceback.Modules) {
                    filteredOut &= Modules[module].Filtered;

                    if (!filteredOut)
                        break;
                }

                if (!filteredOut)
                    ListItems.Add(delta);
            }

            StatusLabel.Text = String.Format("Showing {0} out of {1} item(s)", ListItems.Count, Deltas.Count);
            DeltaList.Items = ListItems;
            DeltaList.Invalidate();

            SetBusy(false);
        }

        private void DiffViewer_Shown (object sender, EventArgs e) {
            UseWaitCursor = true;
        }

        private void DiffViewer_FormClosed (object sender, FormClosedEventArgs e) {
            Dispose();
        }

        private void ModuleList_ItemCheck (object sender, ItemCheckEventArgs e) {
            var m = (ModuleInfo)ModuleList.Items[e.Index];
            m.Filtered = (e.NewValue == CheckState.Unchecked);
            RefreshDeltas();
        }

        private void SelectAllModules_Click (object sender, EventArgs e) {
            SetBusy(true);
            ModuleList.BeginUpdate();

            for (int i = 0; i < ModuleList.Items.Count; i++)
                ModuleList.SetItemChecked(i, true);

            ModuleList.EndUpdate();
            SetBusy(false);

            RefreshDeltas();
        }

        private void SelectNoModules_Click (object sender, EventArgs e) {
            SetBusy(true);
            ModuleList.BeginUpdate();

            for (int i = 0; i < ModuleList.Items.Count; i++)
                ModuleList.SetItemChecked(i, false);

            ModuleList.EndUpdate();
            SetBusy(false);

            RefreshDeltas();
        }

        private void InvertModuleSelection_Click (object sender, EventArgs e) {
            SetBusy(true);
            ModuleList.BeginUpdate();

            for (int i = 0; i < ModuleList.Items.Count; i++)
                ModuleList.SetItemChecked(i, !ModuleList.GetItemChecked(i));

            ModuleList.EndUpdate();
            SetBusy(false);

            RefreshDeltas();
        }

        private void SaveDiffMenu_Click (object sender, EventArgs e) {
            using (var dialog = new SaveFileDialog()) {
                dialog.Title = "Save Diff";
                dialog.Filter = "Heap Diffs|*.heapdiff";
                dialog.AddExtension = true;
                dialog.CheckPathExists = true;
                dialog.DefaultExt = ".heapdiff";

                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                File.Copy(Filename, dialog.FileName, true);
                Filename = dialog.FileName;
                Text = "Diff Viewer - " + Filename;
            }
        }

        private void CloseMenu_Click (object sender, EventArgs e) {
            Close();
        }

        private void TracebackFilter_TextChanged (object sender, EventArgs e) {
            string newFilter = null;
            if (FunctionNames.Contains(TracebackFilter.Text))
                newFilter = String.Intern(TracebackFilter.Text);

            var newColor =
                (TracebackFilter.Text.Length > 0) ?
                    ((newFilter == null) ?
                        Color.LightPink : Color.LightGoldenrodYellow)
                    : SystemColors.Window;

            if (newColor != TracebackFilter.BackColor)
                TracebackFilter.BackColor = newColor;

            if (newFilter != FunctionFilter) {
                DeltaList.FunctionFilter = FunctionFilter = newFilter;
                RefreshDeltas();
            }
        }
    }
}
