namespace HeapProfiler {
    partial class DiffViewer {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiffViewer));
            this.LoadingPanel = new System.Windows.Forms.GroupBox();
            this.LoadingProgress = new System.Windows.Forms.ProgressBar();
            this.MainSplit = new System.Windows.Forms.SplitContainer();
            this.ModuleSelectionToolbar = new System.Windows.Forms.ToolStrip();
            this.SelectAllModules = new System.Windows.Forms.ToolStripButton();
            this.SelectNoModules = new System.Windows.Forms.ToolStripButton();
            this.InvertModuleSelection = new System.Windows.Forms.ToolStripButton();
            this.ModuleList = new System.Windows.Forms.CheckedListBox();
            this.DeltaList = new System.Windows.Forms.ListBox();
            this.LoadingPanel.SuspendLayout();
            this.MainSplit.Panel1.SuspendLayout();
            this.MainSplit.Panel2.SuspendLayout();
            this.MainSplit.SuspendLayout();
            this.ModuleSelectionToolbar.SuspendLayout();
            this.SuspendLayout();
            // 
            // LoadingPanel
            // 
            this.LoadingPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LoadingPanel.Controls.Add(this.LoadingProgress);
            this.LoadingPanel.Location = new System.Drawing.Point(117, 131);
            this.LoadingPanel.Name = "LoadingPanel";
            this.LoadingPanel.Size = new System.Drawing.Size(250, 50);
            this.LoadingPanel.TabIndex = 0;
            this.LoadingPanel.TabStop = false;
            this.LoadingPanel.Text = "Generating Diff...";
            // 
            // LoadingProgress
            // 
            this.LoadingProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LoadingProgress.Location = new System.Drawing.Point(6, 19);
            this.LoadingProgress.MarqueeAnimationSpeed = 25;
            this.LoadingProgress.Name = "LoadingProgress";
            this.LoadingProgress.Size = new System.Drawing.Size(238, 25);
            this.LoadingProgress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.LoadingProgress.TabIndex = 0;
            // 
            // MainSplit
            // 
            this.MainSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainSplit.Location = new System.Drawing.Point(0, 0);
            this.MainSplit.Name = "MainSplit";
            // 
            // MainSplit.Panel1
            // 
            this.MainSplit.Panel1.Controls.Add(this.ModuleSelectionToolbar);
            this.MainSplit.Panel1.Controls.Add(this.ModuleList);
            // 
            // MainSplit.Panel2
            // 
            this.MainSplit.Panel2.Controls.Add(this.DeltaList);
            this.MainSplit.Size = new System.Drawing.Size(484, 312);
            this.MainSplit.SplitterDistance = 161;
            this.MainSplit.TabIndex = 1;
            this.MainSplit.Visible = false;
            // 
            // ModuleSelectionToolbar
            // 
            this.ModuleSelectionToolbar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.ModuleSelectionToolbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SelectAllModules,
            this.SelectNoModules,
            this.InvertModuleSelection});
            this.ModuleSelectionToolbar.Location = new System.Drawing.Point(0, 0);
            this.ModuleSelectionToolbar.Name = "ModuleSelectionToolbar";
            this.ModuleSelectionToolbar.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.ModuleSelectionToolbar.Size = new System.Drawing.Size(161, 25);
            this.ModuleSelectionToolbar.TabIndex = 3;
            this.ModuleSelectionToolbar.Text = "toolStrip1";
            // 
            // SelectAllModules
            // 
            this.SelectAllModules.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.SelectAllModules.Image = ((System.Drawing.Image)(resources.GetObject("SelectAllModules.Image")));
            this.SelectAllModules.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.SelectAllModules.Name = "SelectAllModules";
            this.SelectAllModules.Size = new System.Drawing.Size(25, 22);
            this.SelectAllModules.Text = "All";
            this.SelectAllModules.ToolTipText = "Select All";
            this.SelectAllModules.Click += new System.EventHandler(this.SelectAllModules_Click);
            // 
            // SelectNoModules
            // 
            this.SelectNoModules.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.SelectNoModules.Image = ((System.Drawing.Image)(resources.GetObject("SelectNoModules.Image")));
            this.SelectNoModules.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.SelectNoModules.Name = "SelectNoModules";
            this.SelectNoModules.Size = new System.Drawing.Size(40, 22);
            this.SelectNoModules.Text = "None";
            this.SelectNoModules.ToolTipText = "Select None";
            this.SelectNoModules.Click += new System.EventHandler(this.SelectNoModules_Click);
            // 
            // InvertModuleSelection
            // 
            this.InvertModuleSelection.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.InvertModuleSelection.Image = ((System.Drawing.Image)(resources.GetObject("InvertModuleSelection.Image")));
            this.InvertModuleSelection.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.InvertModuleSelection.Name = "InvertModuleSelection";
            this.InvertModuleSelection.Size = new System.Drawing.Size(41, 22);
            this.InvertModuleSelection.Text = "Invert";
            this.InvertModuleSelection.ToolTipText = "Invert Selection";
            this.InvertModuleSelection.Click += new System.EventHandler(this.InvertModuleSelection_Click);
            // 
            // ModuleList
            // 
            this.ModuleList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ModuleList.CheckOnClick = true;
            this.ModuleList.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ModuleList.FormattingEnabled = true;
            this.ModuleList.IntegralHeight = false;
            this.ModuleList.Location = new System.Drawing.Point(0, 28);
            this.ModuleList.Name = "ModuleList";
            this.ModuleList.Size = new System.Drawing.Size(161, 284);
            this.ModuleList.TabIndex = 2;
            this.ModuleList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ModuleList_ItemCheck);
            // 
            // DeltaList
            // 
            this.DeltaList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DeltaList.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeltaList.FormattingEnabled = true;
            this.DeltaList.IntegralHeight = false;
            this.DeltaList.ItemHeight = 18;
            this.DeltaList.Location = new System.Drawing.Point(0, 0);
            this.DeltaList.Name = "DeltaList";
            this.DeltaList.Size = new System.Drawing.Size(319, 312);
            this.DeltaList.TabIndex = 0;
            // 
            // DiffViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 312);
            this.Controls.Add(this.MainSplit);
            this.Controls.Add(this.LoadingPanel);
            this.Name = "DiffViewer";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "View Diff";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.DiffViewer_FormClosed);
            this.Shown += new System.EventHandler(this.DiffViewer_Shown);
            this.LoadingPanel.ResumeLayout(false);
            this.MainSplit.Panel1.ResumeLayout(false);
            this.MainSplit.Panel1.PerformLayout();
            this.MainSplit.Panel2.ResumeLayout(false);
            this.MainSplit.ResumeLayout(false);
            this.ModuleSelectionToolbar.ResumeLayout(false);
            this.ModuleSelectionToolbar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar LoadingProgress;
        public System.Windows.Forms.GroupBox LoadingPanel;
        private System.Windows.Forms.SplitContainer MainSplit;
        private System.Windows.Forms.CheckedListBox ModuleList;
        private System.Windows.Forms.ListBox DeltaList;
        private System.Windows.Forms.ToolStrip ModuleSelectionToolbar;
        private System.Windows.Forms.ToolStripButton SelectAllModules;
        private System.Windows.Forms.ToolStripButton SelectNoModules;
        private System.Windows.Forms.ToolStripButton InvertModuleSelection;
    }
}