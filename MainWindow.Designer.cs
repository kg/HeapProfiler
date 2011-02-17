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
            this.GroupExecutable = new System.Windows.Forms.GroupBox();
            this.ExecutableStatus = new System.Windows.Forms.Label();
            this.ExecutablePath = new System.Windows.Forms.TextBox();
            this.LaunchProcess = new System.Windows.Forms.Button();
            this.GroupSnapshots = new System.Windows.Forms.GroupBox();
            this.SaveSelection = new System.Windows.Forms.Button();
            this.DiffSelection = new System.Windows.Forms.Button();
            this.CaptureSnapshot = new System.Windows.Forms.Button();
            this.SnapshotList = new System.Windows.Forms.ListBox();
            this.GroupExecutable.SuspendLayout();
            this.GroupSnapshots.SuspendLayout();
            this.SuspendLayout();
            // 
            // SelectExecutable
            // 
            this.SelectExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectExecutable.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SelectExecutable.Location = new System.Drawing.Point(447, 20);
            this.SelectExecutable.Name = "SelectExecutable";
            this.SelectExecutable.Size = new System.Drawing.Size(40, 25);
            this.SelectExecutable.TabIndex = 1;
            this.SelectExecutable.Text = "...";
            this.ToolTips.SetToolTip(this.SelectExecutable, "Select Executable");
            this.SelectExecutable.UseVisualStyleBackColor = true;
            this.SelectExecutable.Click += new System.EventHandler(this.SelectExecutable_Click);
            // 
            // GroupExecutable
            // 
            this.GroupExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.GroupExecutable.Controls.Add(this.ExecutableStatus);
            this.GroupExecutable.Controls.Add(this.SelectExecutable);
            this.GroupExecutable.Controls.Add(this.ExecutablePath);
            this.GroupExecutable.Controls.Add(this.LaunchProcess);
            this.GroupExecutable.Location = new System.Drawing.Point(10, 10);
            this.GroupExecutable.Name = "GroupExecutable";
            this.GroupExecutable.Size = new System.Drawing.Size(597, 71);
            this.GroupExecutable.TabIndex = 0;
            this.GroupExecutable.TabStop = false;
            this.GroupExecutable.Text = "Executable";
            // 
            // ExecutableStatus
            // 
            this.ExecutableStatus.AutoSize = true;
            this.ExecutableStatus.Location = new System.Drawing.Point(6, 46);
            this.ExecutableStatus.Name = "ExecutableStatus";
            this.ExecutableStatus.Size = new System.Drawing.Size(128, 17);
            this.ExecutableStatus.TabIndex = 3;
            this.ExecutableStatus.Text = "Status: Not Started";
            // 
            // ExecutablePath
            // 
            this.ExecutablePath.AllowDrop = true;
            this.ExecutablePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ExecutablePath.Location = new System.Drawing.Point(6, 21);
            this.ExecutablePath.Name = "ExecutablePath";
            this.ExecutablePath.Size = new System.Drawing.Size(435, 22);
            this.ExecutablePath.TabIndex = 0;
            this.ExecutablePath.TextChanged += new System.EventHandler(this.ExecutablePath_TextChanged);
            this.ExecutablePath.DragDrop += new System.Windows.Forms.DragEventHandler(this.ExecutablePath_DragDrop);
            this.ExecutablePath.DragOver += new System.Windows.Forms.DragEventHandler(this.ExecutablePath_DragOver);
            // 
            // LaunchProcess
            // 
            this.LaunchProcess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.LaunchProcess.Enabled = false;
            this.LaunchProcess.Location = new System.Drawing.Point(491, 20);
            this.LaunchProcess.Name = "LaunchProcess";
            this.LaunchProcess.Size = new System.Drawing.Size(100, 25);
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
            this.GroupSnapshots.Location = new System.Drawing.Point(10, 87);
            this.GroupSnapshots.Name = "GroupSnapshots";
            this.GroupSnapshots.Size = new System.Drawing.Size(597, 294);
            this.GroupSnapshots.TabIndex = 1;
            this.GroupSnapshots.TabStop = false;
            this.GroupSnapshots.Text = "Snapshots";
            // 
            // SaveSelection
            // 
            this.SaveSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SaveSelection.Enabled = false;
            this.SaveSelection.Location = new System.Drawing.Point(481, 83);
            this.SaveSelection.Name = "SaveSelection";
            this.SaveSelection.Size = new System.Drawing.Size(110, 25);
            this.SaveSelection.TabIndex = 3;
            this.SaveSelection.Text = "&Save Selection";
            this.SaveSelection.UseVisualStyleBackColor = true;
            // 
            // DiffSelection
            // 
            this.DiffSelection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.DiffSelection.Enabled = false;
            this.DiffSelection.Location = new System.Drawing.Point(481, 52);
            this.DiffSelection.Name = "DiffSelection";
            this.DiffSelection.Size = new System.Drawing.Size(110, 25);
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
            this.CaptureSnapshot.Location = new System.Drawing.Point(481, 21);
            this.CaptureSnapshot.Name = "CaptureSnapshot";
            this.CaptureSnapshot.Size = new System.Drawing.Size(110, 25);
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
            this.SnapshotList.ItemHeight = 22;
            this.SnapshotList.Location = new System.Drawing.Point(6, 21);
            this.SnapshotList.Name = "SnapshotList";
            this.SnapshotList.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.SnapshotList.Size = new System.Drawing.Size(469, 267);
            this.SnapshotList.TabIndex = 0;
            this.SnapshotList.SelectedIndexChanged += new System.EventHandler(this.SnapshotList_SelectedIndexChanged);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(617, 393);
            this.Controls.Add(this.GroupSnapshots);
            this.Controls.Add(this.GroupExecutable);
            this.Name = "MainWindow";
            this.Text = "Heap Profiler";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainWindow_FormClosing);
            this.GroupExecutable.ResumeLayout(false);
            this.GroupExecutable.PerformLayout();
            this.GroupSnapshots.ResumeLayout(false);
            this.ResumeLayout(false);

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
    }
}

