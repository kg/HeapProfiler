namespace HeapProfiler {
    partial class MainWindow {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent () {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.ToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.SelectExecutable = new System.Windows.Forms.Button();
            this.SelectWorkingDirectory = new System.Windows.Forms.Button();
            this.GroupExecutable = new System.Windows.Forms.GroupBox();
            this.WorkingDirectory = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.Arguments = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.ExecutableStatus = new System.Windows.Forms.Label();
            this.ExecutablePath = new System.Windows.Forms.TextBox();
            this.LaunchProcess = new System.Windows.Forms.Button();
            this.GroupSnapshots = new System.Windows.Forms.GroupBox();
            this.ViewSelection = new System.Windows.Forms.Button();
            this.AutoCapture = new System.Windows.Forms.CheckBox();
            this.DiffSelection = new System.Windows.Forms.Button();
            this.CaptureSnapshot = new System.Windows.Forms.Button();
            this.SnapshotTimeline = new HeapProfiler.SnapshotTimeline();
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenFilesMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.SaveAsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.snapshotsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImportSnapshotsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ExportSnapshotsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.OptionsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.SymbolPathMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.AssociateRecordingsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.AssociateSnapshotsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.AssociateDiffsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewPagedMemoryMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewVirtualMemoryMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewWorkingSetMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewLargestFreeHeapMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewAverageFreeBlockSizeMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewLargestOccupiedHeapMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewAverageHeapBlockSizeMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewHeapFragmentationMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.Activities = new HeapProfiler.ActivityIndicator();
            this.VerifyCache = new System.Windows.Forms.Button();
            this.GroupExecutable.SuspendLayout();
            this.GroupSnapshots.SuspendLayout();
            this.MainMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // SelectExecutable
            // 
            this.SelectExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectExecutable.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SelectExecutable.Location = new System.Drawing.Point(406, 14);
            this.SelectExecutable.Margin = new System.Windows.Forms.Padding(2);
            this.SelectExecutable.Name = "SelectExecutable";
            this.SelectExecutable.Size = new System.Drawing.Size(30, 22);
            this.SelectExecutable.TabIndex = 1;
            this.SelectExecutable.Text = "...";
            this.ToolTips.SetToolTip(this.SelectExecutable, "Select Executable");
            this.SelectExecutable.UseVisualStyleBackColor = true;
            this.SelectExecutable.Click += new System.EventHandler(this.SelectExecutable_Click);
            // 
            // SelectWorkingDirectory
            // 
            this.SelectWorkingDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectWorkingDirectory.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SelectWorkingDirectory.Location = new System.Drawing.Point(484, 62);
            this.SelectWorkingDirectory.Margin = new System.Windows.Forms.Padding(2);
            this.SelectWorkingDirectory.Name = "SelectWorkingDirectory";
            this.SelectWorkingDirectory.Size = new System.Drawing.Size(30, 22);
            this.SelectWorkingDirectory.TabIndex = 7;
            this.SelectWorkingDirectory.Text = "...";
            this.ToolTips.SetToolTip(this.SelectWorkingDirectory, "Select Executable");
            this.SelectWorkingDirectory.UseVisualStyleBackColor = true;
            this.SelectWorkingDirectory.Click += new System.EventHandler(this.SelectWorkingDirectory_Click);
            // 
            // GroupExecutable
            // 
            this.GroupExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.GroupExecutable.Controls.Add(this.SelectWorkingDirectory);
            this.GroupExecutable.Controls.Add(this.WorkingDirectory);
            this.GroupExecutable.Controls.Add(this.label2);
            this.GroupExecutable.Controls.Add(this.Arguments);
            this.GroupExecutable.Controls.Add(this.label1);
            this.GroupExecutable.Controls.Add(this.ExecutableStatus);
            this.GroupExecutable.Controls.Add(this.SelectExecutable);
            this.GroupExecutable.Controls.Add(this.ExecutablePath);
            this.GroupExecutable.Controls.Add(this.LaunchProcess);
            this.GroupExecutable.Location = new System.Drawing.Point(8, 32);
            this.GroupExecutable.Margin = new System.Windows.Forms.Padding(2);
            this.GroupExecutable.Name = "GroupExecutable";
            this.GroupExecutable.Padding = new System.Windows.Forms.Padding(2);
            this.GroupExecutable.Size = new System.Drawing.Size(519, 110);
            this.GroupExecutable.TabIndex = 1;
            this.GroupExecutable.TabStop = false;
            this.GroupExecutable.Text = "Executable";
            // 
            // WorkingDirectory
            // 
            this.WorkingDirectory.AllowDrop = true;
            this.WorkingDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.WorkingDirectory.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.WorkingDirectory.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.WorkingDirectory.Location = new System.Drawing.Point(104, 64);
            this.WorkingDirectory.Margin = new System.Windows.Forms.Padding(2);
            this.WorkingDirectory.Name = "WorkingDirectory";
            this.WorkingDirectory.Size = new System.Drawing.Size(376, 20);
            this.WorkingDirectory.TabIndex = 6;
            this.WorkingDirectory.DragDrop += new System.Windows.Forms.DragEventHandler(this.WorkingDirectory_DragDrop);
            this.WorkingDirectory.DragOver += new System.Windows.Forms.DragEventHandler(this.WorkingDirectory_DragOver);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(4, 67);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "&Working Directory:";
            // 
            // Arguments
            // 
            this.Arguments.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Arguments.Location = new System.Drawing.Point(104, 40);
            this.Arguments.Margin = new System.Windows.Forms.Padding(2);
            this.Arguments.Name = "Arguments";
            this.Arguments.Size = new System.Drawing.Size(410, 20);
            this.Arguments.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(4, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "A&rguments:";
            // 
            // ExecutableStatus
            // 
            this.ExecutableStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ExecutableStatus.AutoSize = true;
            this.ExecutableStatus.Location = new System.Drawing.Point(4, 90);
            this.ExecutableStatus.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.ExecutableStatus.Name = "ExecutableStatus";
            this.ExecutableStatus.Size = new System.Drawing.Size(97, 13);
            this.ExecutableStatus.TabIndex = 8;
            this.ExecutableStatus.Text = "Status: Not Started";
            // 
            // ExecutablePath
            // 
            this.ExecutablePath.AllowDrop = true;
            this.ExecutablePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ExecutablePath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.ExecutablePath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.ExecutablePath.Location = new System.Drawing.Point(7, 16);
            this.ExecutablePath.Margin = new System.Windows.Forms.Padding(2);
            this.ExecutablePath.Name = "ExecutablePath";
            this.ExecutablePath.Size = new System.Drawing.Size(395, 20);
            this.ExecutablePath.TabIndex = 0;
            this.ExecutablePath.TextChanged += new System.EventHandler(this.ExecutablePath_TextChanged);
            this.ExecutablePath.DragDrop += new System.Windows.Forms.DragEventHandler(this.ExecutablePath_DragDrop);
            this.ExecutablePath.DragOver += new System.Windows.Forms.DragEventHandler(this.ExecutablePath_DragOver);
            // 
            // LaunchProcess
            // 
            this.LaunchProcess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.LaunchProcess.Enabled = false;
            this.LaunchProcess.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LaunchProcess.Location = new System.Drawing.Point(439, 14);
            this.LaunchProcess.Margin = new System.Windows.Forms.Padding(2);
            this.LaunchProcess.Name = "LaunchProcess";
            this.LaunchProcess.Size = new System.Drawing.Size(75, 22);
            this.LaunchProcess.TabIndex = 2;
            this.LaunchProcess.Text = "&Launch";
            this.LaunchProcess.UseVisualStyleBackColor = true;
            this.LaunchProcess.Click += new System.EventHandler(this.LaunchProcess_Click);
            // 
            // GroupSnapshots
            // 
            this.GroupSnapshots.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.GroupSnapshots.Controls.Add(this.VerifyCache);
            this.GroupSnapshots.Controls.Add(this.ViewSelection);
            this.GroupSnapshots.Controls.Add(this.AutoCapture);
            this.GroupSnapshots.Controls.Add(this.DiffSelection);
            this.GroupSnapshots.Controls.Add(this.CaptureSnapshot);
            this.GroupSnapshots.Controls.Add(this.SnapshotTimeline);
            this.GroupSnapshots.Location = new System.Drawing.Point(8, 146);
            this.GroupSnapshots.Margin = new System.Windows.Forms.Padding(2);
            this.GroupSnapshots.Name = "GroupSnapshots";
            this.GroupSnapshots.Padding = new System.Windows.Forms.Padding(2);
            this.GroupSnapshots.Size = new System.Drawing.Size(519, 259);
            this.GroupSnapshots.TabIndex = 2;
            this.GroupSnapshots.TabStop = false;
            this.GroupSnapshots.Text = "Snapshots";
            // 
            // ViewSelection
            // 
            this.ViewSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ViewSelection.Enabled = false;
            this.ViewSelection.Location = new System.Drawing.Point(415, 93);
            this.ViewSelection.Margin = new System.Windows.Forms.Padding(2);
            this.ViewSelection.Name = "ViewSelection";
            this.ViewSelection.Size = new System.Drawing.Size(100, 22);
            this.ViewSelection.TabIndex = 7;
            this.ViewSelection.Text = "&View Selection";
            this.ViewSelection.UseVisualStyleBackColor = true;
            this.ViewSelection.Click += new System.EventHandler(this.ViewSelection_Click);
            // 
            // AutoCapture
            // 
            this.AutoCapture.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.AutoCapture.Appearance = System.Windows.Forms.Appearance.Button;
            this.AutoCapture.Location = new System.Drawing.Point(415, 42);
            this.AutoCapture.Name = "AutoCapture";
            this.AutoCapture.Size = new System.Drawing.Size(100, 22);
            this.AutoCapture.TabIndex = 4;
            this.AutoCapture.Text = "&Auto Capture";
            this.AutoCapture.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.AutoCapture.UseVisualStyleBackColor = true;
            this.AutoCapture.CheckedChanged += new System.EventHandler(this.AutoCapture_CheckedChanged);
            // 
            // DiffSelection
            // 
            this.DiffSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.DiffSelection.Enabled = false;
            this.DiffSelection.Location = new System.Drawing.Point(415, 67);
            this.DiffSelection.Margin = new System.Windows.Forms.Padding(2);
            this.DiffSelection.Name = "DiffSelection";
            this.DiffSelection.Size = new System.Drawing.Size(100, 22);
            this.DiffSelection.TabIndex = 2;
            this.DiffSelection.Text = "&Diff Selection";
            this.DiffSelection.UseVisualStyleBackColor = true;
            this.DiffSelection.Click += new System.EventHandler(this.DiffSelection_Click);
            // 
            // CaptureSnapshot
            // 
            this.CaptureSnapshot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.CaptureSnapshot.Enabled = false;
            this.CaptureSnapshot.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CaptureSnapshot.Location = new System.Drawing.Point(415, 17);
            this.CaptureSnapshot.Margin = new System.Windows.Forms.Padding(2);
            this.CaptureSnapshot.Name = "CaptureSnapshot";
            this.CaptureSnapshot.Size = new System.Drawing.Size(100, 22);
            this.CaptureSnapshot.TabIndex = 1;
            this.CaptureSnapshot.Text = "&Capture Now";
            this.CaptureSnapshot.UseVisualStyleBackColor = true;
            this.CaptureSnapshot.Click += new System.EventHandler(this.CaptureSnapshot_Click);
            // 
            // SnapshotTimeline
            // 
            this.SnapshotTimeline.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.SnapshotTimeline.BackColor = System.Drawing.SystemColors.Control;
            this.SnapshotTimeline.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SnapshotTimeline.ForeColor = System.Drawing.SystemColors.WindowText;
            this.SnapshotTimeline.Location = new System.Drawing.Point(4, 17);
            this.SnapshotTimeline.Margin = new System.Windows.Forms.Padding(2);
            this.SnapshotTimeline.Name = "SnapshotTimeline";
            this.SnapshotTimeline.Size = new System.Drawing.Size(406, 237);
            this.SnapshotTimeline.TabIndex = 6;
            this.SnapshotTimeline.SelectionChanged += new System.EventHandler(this.SnapshotTimeline_SelectionChanged);
            this.SnapshotTimeline.ItemValueGetterChanged += new System.EventHandler(this.SnapshotTimeline_ItemValueGetterChanged);
            // 
            // MainMenu
            // 
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.snapshotsToolStripMenuItem,
            this.OptionsMenu,
            this.viewToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(534, 24);
            this.MainMenu.TabIndex = 0;
            this.MainMenu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OpenFilesMenu,
            this.SaveAsMenu,
            this.toolStripMenuItem1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // OpenFilesMenu
            // 
            this.OpenFilesMenu.Name = "OpenFilesMenu";
            this.OpenFilesMenu.Size = new System.Drawing.Size(123, 22);
            this.OpenFilesMenu.Text = "&Open...";
            this.OpenFilesMenu.Click += new System.EventHandler(this.OpenFilesMenu_Click);
            // 
            // SaveAsMenu
            // 
            this.SaveAsMenu.Name = "SaveAsMenu";
            this.SaveAsMenu.Size = new System.Drawing.Size(123, 22);
            this.SaveAsMenu.Text = "&Save As...";
            this.SaveAsMenu.Click += new System.EventHandler(this.SaveAsMenu_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(120, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitMenu_Click);
            // 
            // snapshotsToolStripMenuItem
            // 
            this.snapshotsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ImportSnapshotsMenu,
            this.ExportSnapshotsMenu});
            this.snapshotsToolStripMenuItem.Name = "snapshotsToolStripMenuItem";
            this.snapshotsToolStripMenuItem.Size = new System.Drawing.Size(73, 20);
            this.snapshotsToolStripMenuItem.Text = "&Snapshots";
            // 
            // ImportSnapshotsMenu
            // 
            this.ImportSnapshotsMenu.Name = "ImportSnapshotsMenu";
            this.ImportSnapshotsMenu.Size = new System.Drawing.Size(119, 22);
            this.ImportSnapshotsMenu.Text = "&Import...";
            this.ImportSnapshotsMenu.Click += new System.EventHandler(this.OpenSnapshotsMenu_Click);
            // 
            // ExportSnapshotsMenu
            // 
            this.ExportSnapshotsMenu.Name = "ExportSnapshotsMenu";
            this.ExportSnapshotsMenu.Size = new System.Drawing.Size(119, 22);
            this.ExportSnapshotsMenu.Text = "&Export...";
            this.ExportSnapshotsMenu.Click += new System.EventHandler(this.SaveAllSnapshots_Click);
            // 
            // OptionsMenu
            // 
            this.OptionsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SymbolPathMenu,
            this.AssociateRecordingsMenu,
            this.AssociateSnapshotsMenu,
            this.AssociateDiffsMenu});
            this.OptionsMenu.Name = "OptionsMenu";
            this.OptionsMenu.Size = new System.Drawing.Size(61, 20);
            this.OptionsMenu.Text = "&Options";
            this.OptionsMenu.DropDownOpening += new System.EventHandler(this.OptionsMenu_DropDownOpening);
            // 
            // SymbolPathMenu
            // 
            this.SymbolPathMenu.Name = "SymbolPathMenu";
            this.SymbolPathMenu.Size = new System.Drawing.Size(281, 22);
            this.SymbolPathMenu.Text = "Sy&mbols...";
            this.SymbolPathMenu.Click += new System.EventHandler(this.SymbolPathMenu_Click);
            // 
            // AssociateRecordingsMenu
            // 
            this.AssociateRecordingsMenu.Name = "AssociateRecordingsMenu";
            this.AssociateRecordingsMenu.Size = new System.Drawing.Size(281, 22);
            this.AssociateRecordingsMenu.Text = "Associate &recordings with Heap Profiler";
            this.AssociateRecordingsMenu.Click += new System.EventHandler(this.AssociateRecordingsMenu_Click);
            // 
            // AssociateSnapshotsMenu
            // 
            this.AssociateSnapshotsMenu.Name = "AssociateSnapshotsMenu";
            this.AssociateSnapshotsMenu.Size = new System.Drawing.Size(281, 22);
            this.AssociateSnapshotsMenu.Text = "Associate &snapshots with Heap Profiler";
            this.AssociateSnapshotsMenu.Click += new System.EventHandler(this.AssociateSnapshotsMenu_Click);
            // 
            // AssociateDiffsMenu
            // 
            this.AssociateDiffsMenu.Name = "AssociateDiffsMenu";
            this.AssociateDiffsMenu.Size = new System.Drawing.Size(281, 22);
            this.AssociateDiffsMenu.Text = "&Associate &diffs with Heap Profiler";
            this.AssociateDiffsMenu.Click += new System.EventHandler(this.AssociateDiffsMenu_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ViewPagedMemoryMenu,
            this.ViewVirtualMemoryMenu,
            this.ViewWorkingSetMenu,
            this.ViewLargestFreeHeapMenu,
            this.ViewAverageFreeBlockSizeMenu,
            this.ViewLargestOccupiedHeapMenu,
            this.ViewAverageHeapBlockSizeMenu,
            this.ViewHeapFragmentationMenu});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "&View";
            // 
            // ViewPagedMemoryMenu
            // 
            this.ViewPagedMemoryMenu.Checked = true;
            this.ViewPagedMemoryMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ViewPagedMemoryMenu.Name = "ViewPagedMemoryMenu";
            this.ViewPagedMemoryMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewPagedMemoryMenu.Text = "Paged Memory";
            this.ViewPagedMemoryMenu.Click += new System.EventHandler(this.ViewPagedMemoryMenu_Click);
            // 
            // ViewVirtualMemoryMenu
            // 
            this.ViewVirtualMemoryMenu.Name = "ViewVirtualMemoryMenu";
            this.ViewVirtualMemoryMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewVirtualMemoryMenu.Text = "Virtual Memory";
            this.ViewVirtualMemoryMenu.Click += new System.EventHandler(this.ViewVirtualMemoryMenu_Click);
            // 
            // ViewWorkingSetMenu
            // 
            this.ViewWorkingSetMenu.Name = "ViewWorkingSetMenu";
            this.ViewWorkingSetMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewWorkingSetMenu.Text = "Working Set";
            this.ViewWorkingSetMenu.Click += new System.EventHandler(this.ViewWorkingSetMenu_Click);
            // 
            // ViewLargestFreeHeapMenu
            // 
            this.ViewLargestFreeHeapMenu.Name = "ViewLargestFreeHeapMenu";
            this.ViewLargestFreeHeapMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewLargestFreeHeapMenu.Text = "Largest Free Block";
            this.ViewLargestFreeHeapMenu.Click += new System.EventHandler(this.ViewLargestFreeHeapMenu_Click);
            // 
            // ViewAverageFreeBlockSizeMenu
            // 
            this.ViewAverageFreeBlockSizeMenu.Name = "ViewAverageFreeBlockSizeMenu";
            this.ViewAverageFreeBlockSizeMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewAverageFreeBlockSizeMenu.Text = "Average Free Block Size";
            this.ViewAverageFreeBlockSizeMenu.Click += new System.EventHandler(this.ViewAverageFreeBlockSizeMenu_Click);
            // 
            // ViewLargestOccupiedHeapMenu
            // 
            this.ViewLargestOccupiedHeapMenu.Name = "ViewLargestOccupiedHeapMenu";
            this.ViewLargestOccupiedHeapMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewLargestOccupiedHeapMenu.Text = "Largest Occupied Block";
            this.ViewLargestOccupiedHeapMenu.Click += new System.EventHandler(this.ViewLargestOccupiedHeapMenu_Click);
            // 
            // ViewAverageHeapBlockSizeMenu
            // 
            this.ViewAverageHeapBlockSizeMenu.Name = "ViewAverageHeapBlockSizeMenu";
            this.ViewAverageHeapBlockSizeMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewAverageHeapBlockSizeMenu.Text = "Average Occupied Block Size";
            this.ViewAverageHeapBlockSizeMenu.Click += new System.EventHandler(this.ViewAverageHeapBlockSizeMenu_Click);
            // 
            // ViewHeapFragmentationMenu
            // 
            this.ViewHeapFragmentationMenu.Name = "ViewHeapFragmentationMenu";
            this.ViewHeapFragmentationMenu.Size = new System.Drawing.Size(226, 22);
            this.ViewHeapFragmentationMenu.Text = "Heap Fragmentation";
            this.ViewHeapFragmentationMenu.Click += new System.EventHandler(this.ViewHeapFragmentationMenu_Click);
            // 
            // Activities
            // 
            this.Activities.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Activities.Location = new System.Drawing.Point(8, 410);
            this.Activities.Name = "Activities";
            this.Activities.Size = new System.Drawing.Size(519, 10);
            this.Activities.TabIndex = 3;
            this.Activities.PreferredSizeChanged += new System.EventHandler(this.Activities_PreferredSizeChanged);
            // 
            // VerifyCache
            // 
            this.VerifyCache.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.VerifyCache.Location = new System.Drawing.Point(415, 231);
            this.VerifyCache.Name = "VerifyCache";
            this.VerifyCache.Size = new System.Drawing.Size(99, 23);
            this.VerifyCache.TabIndex = 8;
            this.VerifyCache.Text = "Verify Cache";
            this.VerifyCache.UseVisualStyleBackColor = true;
            this.VerifyCache.Click += new System.EventHandler(this.VerifyCache_Click);
            // 
            // MainWindow
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(534, 432);
            this.Controls.Add(this.Activities);
            this.Controls.Add(this.GroupSnapshots);
            this.Controls.Add(this.GroupExecutable);
            this.Controls.Add(this.MainMenu);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Heap Profiler";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainWindow_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainWindow_FormClosed);
            this.SizeChanged += new System.EventHandler(this.MainWindow_SizeChanged);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainWindow_DragDrop);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.MainWindow_DragOver);
            this.GroupExecutable.ResumeLayout(false);
            this.GroupExecutable.PerformLayout();
            this.GroupSnapshots.ResumeLayout(false);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolTip ToolTips;
        private System.Windows.Forms.GroupBox GroupExecutable;
        private System.Windows.Forms.Button SelectExecutable;
        private System.Windows.Forms.TextBox ExecutablePath;
        private System.Windows.Forms.Button LaunchProcess;
        private System.Windows.Forms.Label ExecutableStatus;
        private System.Windows.Forms.GroupBox GroupSnapshots;
        private System.Windows.Forms.Button DiffSelection;
        private System.Windows.Forms.Button CaptureSnapshot;
        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem OptionsMenu;
        private System.Windows.Forms.ToolStripMenuItem SymbolPathMenu;
        private System.Windows.Forms.ToolStripMenuItem OpenFilesMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.Button SelectWorkingDirectory;
        private System.Windows.Forms.TextBox WorkingDirectory;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox Arguments;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox AutoCapture;
        private System.Windows.Forms.ToolStripMenuItem AssociateDiffsMenu;
        private ActivityIndicator Activities;
        private SnapshotTimeline SnapshotTimeline;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ViewPagedMemoryMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewVirtualMemoryMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewWorkingSetMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewLargestFreeHeapMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewLargestOccupiedHeapMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewAverageHeapBlockSizeMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewHeapFragmentationMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewAverageFreeBlockSizeMenu;
        private System.Windows.Forms.Button ViewSelection;
        private System.Windows.Forms.ToolStripMenuItem SaveAsMenu;
        private System.Windows.Forms.ToolStripMenuItem snapshotsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ImportSnapshotsMenu;
        private System.Windows.Forms.ToolStripMenuItem ExportSnapshotsMenu;
        private System.Windows.Forms.ToolStripMenuItem AssociateRecordingsMenu;
        private System.Windows.Forms.ToolStripMenuItem AssociateSnapshotsMenu;
        private System.Windows.Forms.Button VerifyCache;
    }
}

