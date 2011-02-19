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
            this.SaveSelection = new System.Windows.Forms.Button();
            this.DiffSelection = new System.Windows.Forms.Button();
            this.CaptureSnapshot = new System.Windows.Forms.Button();
            this.SnapshotList = new System.Windows.Forms.ListBox();
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenDiffMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SymbolPathMenu = new System.Windows.Forms.ToolStripMenuItem();
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
            this.label2.Text = "Working &Directory:";
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
            this.label1.Text = "&Arguments:";
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
            this.GroupSnapshots.Controls.Add(this.SaveSelection);
            this.GroupSnapshots.Controls.Add(this.DiffSelection);
            this.GroupSnapshots.Controls.Add(this.CaptureSnapshot);
            this.GroupSnapshots.Controls.Add(this.SnapshotList);
            this.GroupSnapshots.Location = new System.Drawing.Point(8, 146);
            this.GroupSnapshots.Margin = new System.Windows.Forms.Padding(2);
            this.GroupSnapshots.Name = "GroupSnapshots";
            this.GroupSnapshots.Padding = new System.Windows.Forms.Padding(2);
            this.GroupSnapshots.Size = new System.Drawing.Size(519, 278);
            this.GroupSnapshots.TabIndex = 2;
            this.GroupSnapshots.TabStop = false;
            this.GroupSnapshots.Text = "Snapshots";
            // 
            // SaveSelection
            // 
            this.SaveSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SaveSelection.Enabled = false;
            this.SaveSelection.Location = new System.Drawing.Point(415, 69);
            this.SaveSelection.Margin = new System.Windows.Forms.Padding(2);
            this.SaveSelection.Name = "SaveSelection";
            this.SaveSelection.Size = new System.Drawing.Size(100, 22);
            this.SaveSelection.TabIndex = 3;
            this.SaveSelection.Text = "&Save Selection";
            this.SaveSelection.UseVisualStyleBackColor = true;
            // 
            // DiffSelection
            // 
            this.DiffSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.DiffSelection.Enabled = false;
            this.DiffSelection.Location = new System.Drawing.Point(415, 43);
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
            // SnapshotList
            // 
            this.SnapshotList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.SnapshotList.Font = new System.Drawing.Font("Consolas", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SnapshotList.IntegralHeight = false;
            this.SnapshotList.ItemHeight = 17;
            this.SnapshotList.Location = new System.Drawing.Point(4, 17);
            this.SnapshotList.Margin = new System.Windows.Forms.Padding(2);
            this.SnapshotList.Name = "SnapshotList";
            this.SnapshotList.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.SnapshotList.Size = new System.Drawing.Size(407, 257);
            this.SnapshotList.TabIndex = 0;
            this.SnapshotList.SelectedIndexChanged += new System.EventHandler(this.SnapshotList_SelectedIndexChanged);
            // 
            // MainMenu
            // 
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.optionsToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(534, 24);
            this.MainMenu.TabIndex = 0;
            this.MainMenu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OpenDiffMenu,
            this.toolStripMenuItem1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // OpenDiffMenu
            // 
            this.OpenDiffMenu.Name = "OpenDiffMenu";
            this.OpenDiffMenu.Size = new System.Drawing.Size(134, 22);
            this.OpenDiffMenu.Text = "Open &Diff...";
            this.OpenDiffMenu.Click += new System.EventHandler(this.OpenDiffMenu_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(131, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(134, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitMenu_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SymbolPathMenu});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.optionsToolStripMenuItem.Text = "&Options";
            // 
            // SymbolPathMenu
            // 
            this.SymbolPathMenu.Enabled = false;
            this.SymbolPathMenu.Name = "SymbolPathMenu";
            this.SymbolPathMenu.Size = new System.Drawing.Size(150, 22);
            this.SymbolPathMenu.Text = "&Symbol Path...";
            this.SymbolPathMenu.Click += new System.EventHandler(this.SymbolPathMenu_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(534, 432);
            this.Controls.Add(this.GroupSnapshots);
            this.Controls.Add(this.GroupExecutable);
            this.Controls.Add(this.MainMenu);
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Heap Profiler";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainWindow_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainWindow_FormClosed);
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
        private System.Windows.Forms.ListBox SnapshotList;
        private System.Windows.Forms.Button DiffSelection;
        private System.Windows.Forms.Button CaptureSnapshot;
        private System.Windows.Forms.Button SaveSelection;
        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem SymbolPathMenu;
        private System.Windows.Forms.ToolStripMenuItem OpenDiffMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.Button SelectWorkingDirectory;
        private System.Windows.Forms.TextBox WorkingDirectory;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox Arguments;
        private System.Windows.Forms.Label label1;
    }
}

