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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiffViewer));
            this.LoadingPanel = new System.Windows.Forms.GroupBox();
            this.LoadingProgress = new System.Windows.Forms.ProgressBar();
            this.MainSplit = new System.Windows.Forms.SplitContainer();
            this.ModuleList = new HeapProfiler.ModuleSelector();
            this.ViewSplit = new System.Windows.Forms.SplitContainer();
            this.TracebackFilter = new HeapProfiler.FilterControl();
            this.GraphTreemap = new HeapProfiler.GraphTreemap();
            this.DeltaHistogram = new HeapProfiler.DeltaHistogram();
            this.DeltaList = new HeapProfiler.DeltaList();
            this.GraphHistogram = new HeapProfiler.GraphHistogram();
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SaveDiffMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.CloseMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewListMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewHistogramMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewFunctionHistogramMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewFunctionTreemapMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.StatusBar = new System.Windows.Forms.StatusStrip();
            this.StatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.AllocationTotals = new System.Windows.Forms.ToolStripStatusLabel();
            this.Timeline = new HeapProfiler.SnapshotTimeline();
            this.LoadingPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainSplit)).BeginInit();
            this.MainSplit.Panel1.SuspendLayout();
            this.MainSplit.Panel2.SuspendLayout();
            this.MainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ViewSplit)).BeginInit();
            this.ViewSplit.Panel1.SuspendLayout();
            this.ViewSplit.Panel2.SuspendLayout();
            this.ViewSplit.SuspendLayout();
            this.MainMenu.SuspendLayout();
            this.StatusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // LoadingPanel
            // 
            this.LoadingPanel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.LoadingPanel.Controls.Add(this.LoadingProgress);
            this.LoadingPanel.Location = new System.Drawing.Point(59, 179);
            this.LoadingPanel.Margin = new System.Windows.Forms.Padding(4);
            this.LoadingPanel.Name = "LoadingPanel";
            this.LoadingPanel.Padding = new System.Windows.Forms.Padding(4);
            this.LoadingPanel.Size = new System.Drawing.Size(467, 54);
            this.LoadingPanel.TabIndex = 0;
            this.LoadingPanel.TabStop = false;
            this.LoadingPanel.Text = "Generating Diff...";
            // 
            // LoadingProgress
            // 
            this.LoadingProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LoadingProgress.Location = new System.Drawing.Point(8, 21);
            this.LoadingProgress.Margin = new System.Windows.Forms.Padding(4);
            this.LoadingProgress.MarqueeAnimationSpeed = 25;
            this.LoadingProgress.Name = "LoadingProgress";
            this.LoadingProgress.Size = new System.Drawing.Size(451, 25);
            this.LoadingProgress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.LoadingProgress.TabIndex = 0;
            // 
            // MainSplit
            // 
            this.MainSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MainSplit.Location = new System.Drawing.Point(0, 26);
            this.MainSplit.Margin = new System.Windows.Forms.Padding(4);
            this.MainSplit.Name = "MainSplit";
            // 
            // MainSplit.Panel1
            // 
            this.MainSplit.Panel1.Controls.Add(this.ModuleList);
            this.MainSplit.Panel1MinSize = 75;
            // 
            // MainSplit.Panel2
            // 
            this.MainSplit.Panel2.Controls.Add(this.ViewSplit);
            this.MainSplit.Size = new System.Drawing.Size(584, 328);
            this.MainSplit.SplitterDistance = 120;
            this.MainSplit.SplitterWidth = 5;
            this.MainSplit.TabIndex = 1;
            this.MainSplit.Visible = false;
            // 
            // ModuleList
            // 
            this.ModuleList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ModuleList.Location = new System.Drawing.Point(0, 0);
            this.ModuleList.Margin = new System.Windows.Forms.Padding(2);
            this.ModuleList.Name = "ModuleList";
            this.ModuleList.Size = new System.Drawing.Size(120, 328);
            this.ModuleList.TabIndex = 0;
            this.ModuleList.FilterChanged += new System.EventHandler(this.ModuleList_FilterChanged);
            // 
            // ViewSplit
            // 
            this.ViewSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ViewSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.ViewSplit.IsSplitterFixed = true;
            this.ViewSplit.Location = new System.Drawing.Point(0, 0);
            this.ViewSplit.Margin = new System.Windows.Forms.Padding(2);
            this.ViewSplit.Name = "ViewSplit";
            this.ViewSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // ViewSplit.Panel1
            // 
            this.ViewSplit.Panel1.Controls.Add(this.TracebackFilter);
            // 
            // ViewSplit.Panel2
            // 
            this.ViewSplit.Panel2.Controls.Add(this.GraphTreemap);
            this.ViewSplit.Panel2.Controls.Add(this.DeltaHistogram);
            this.ViewSplit.Panel2.Controls.Add(this.DeltaList);
            this.ViewSplit.Panel2.Controls.Add(this.GraphHistogram);
            this.ViewSplit.Size = new System.Drawing.Size(459, 328);
            this.ViewSplit.SplitterDistance = 30;
            this.ViewSplit.SplitterWidth = 1;
            this.ViewSplit.TabIndex = 2;
            // 
            // TracebackFilter
            // 
            this.TracebackFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TracebackFilter.Location = new System.Drawing.Point(0, 0);
            this.TracebackFilter.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.TracebackFilter.Name = "TracebackFilter";
            this.TracebackFilter.Size = new System.Drawing.Size(459, 30);
            this.TracebackFilter.TabIndex = 0;
            this.TracebackFilter.FilterChanging += new HeapProfiler.FilterChangingEventHandler(this.TracebackFilter_FilterChanging);
            this.TracebackFilter.FilterChanged += new System.EventHandler(this.TracebackFilter_FilterChanged);
            // 
            // GraphTreemap
            // 
            this.GraphTreemap.BackColor = System.Drawing.SystemColors.Window;
            this.GraphTreemap.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GraphTreemap.ForeColor = System.Drawing.SystemColors.WindowText;
            this.GraphTreemap.Location = new System.Drawing.Point(0, 0);
            this.GraphTreemap.Name = "GraphTreemap";
            this.GraphTreemap.Size = new System.Drawing.Size(459, 297);
            this.GraphTreemap.TabIndex = 5;
            this.GraphTreemap.Visible = false;
            // 
            // DeltaHistogram
            // 
            this.DeltaHistogram.BackColor = System.Drawing.SystemColors.Window;
            this.DeltaHistogram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DeltaHistogram.Font = new System.Drawing.Font("Consolas", 11.25F);
            this.DeltaHistogram.ForeColor = System.Drawing.SystemColors.WindowText;
            this.DeltaHistogram.Location = new System.Drawing.Point(0, 0);
            this.DeltaHistogram.Margin = new System.Windows.Forms.Padding(4);
            this.DeltaHistogram.Name = "DeltaHistogram";
            this.DeltaHistogram.Size = new System.Drawing.Size(459, 297);
            this.DeltaHistogram.TabIndex = 4;
            this.DeltaHistogram.Visible = false;
            // 
            // DeltaList
            // 
            this.DeltaList.BackColor = System.Drawing.SystemColors.Window;
            this.DeltaList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DeltaList.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeltaList.ForeColor = System.Drawing.SystemColors.WindowText;
            this.DeltaList.Location = new System.Drawing.Point(0, 0);
            this.DeltaList.Margin = new System.Windows.Forms.Padding(4);
            this.DeltaList.Name = "DeltaList";
            this.DeltaList.Size = new System.Drawing.Size(459, 297);
            this.DeltaList.TabIndex = 3;
            // 
            // GraphHistogram
            // 
            this.GraphHistogram.BackColor = System.Drawing.SystemColors.Window;
            this.GraphHistogram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GraphHistogram.Font = new System.Drawing.Font("Consolas", 11.25F);
            this.GraphHistogram.ForeColor = System.Drawing.SystemColors.WindowText;
            this.GraphHistogram.Location = new System.Drawing.Point(0, 0);
            this.GraphHistogram.Margin = new System.Windows.Forms.Padding(5);
            this.GraphHistogram.Name = "GraphHistogram";
            this.GraphHistogram.Size = new System.Drawing.Size(459, 297);
            this.GraphHistogram.TabIndex = 4;
            this.GraphHistogram.Visible = false;
            // 
            // MainMenu
            // 
            this.MainMenu.Enabled = false;
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(584, 24);
            this.MainMenu.TabIndex = 2;
            this.MainMenu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SaveDiffMenu,
            this.toolStripMenuItem1,
            this.CloseMenu});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // SaveDiffMenu
            // 
            this.SaveDiffMenu.Name = "SaveDiffMenu";
            this.SaveDiffMenu.Size = new System.Drawing.Size(123, 22);
            this.SaveDiffMenu.Text = "&Save As...";
            this.SaveDiffMenu.Click += new System.EventHandler(this.SaveDiffMenu_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(120, 6);
            // 
            // CloseMenu
            // 
            this.CloseMenu.Name = "CloseMenu";
            this.CloseMenu.Size = new System.Drawing.Size(123, 22);
            this.CloseMenu.Text = "&Close";
            this.CloseMenu.Click += new System.EventHandler(this.CloseMenu_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ViewListMenu,
            this.ViewHistogramMenu,
            this.ViewFunctionHistogramMenu,
            this.ViewFunctionTreemapMenu});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "&View";
            // 
            // ViewListMenu
            // 
            this.ViewListMenu.Checked = true;
            this.ViewListMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ViewListMenu.Name = "ViewListMenu";
            this.ViewListMenu.Size = new System.Drawing.Size(180, 22);
            this.ViewListMenu.Text = "Traceback &List";
            this.ViewListMenu.Click += new System.EventHandler(this.ViewListMenu_Click);
            // 
            // ViewHistogramMenu
            // 
            this.ViewHistogramMenu.Name = "ViewHistogramMenu";
            this.ViewHistogramMenu.Size = new System.Drawing.Size(180, 22);
            this.ViewHistogramMenu.Text = "&Stack Histogram";
            this.ViewHistogramMenu.Click += new System.EventHandler(this.ViewHistogramMenu_Click);
            // 
            // ViewFunctionHistogramMenu
            // 
            this.ViewFunctionHistogramMenu.Enabled = false;
            this.ViewFunctionHistogramMenu.Name = "ViewFunctionHistogramMenu";
            this.ViewFunctionHistogramMenu.Size = new System.Drawing.Size(180, 22);
            this.ViewFunctionHistogramMenu.Text = "&Function Histogram";
            this.ViewFunctionHistogramMenu.Click += new System.EventHandler(this.ViewFunctionHistogramMenu_Click);
            // 
            // ViewFunctionTreemapMenu
            // 
            this.ViewFunctionTreemapMenu.Enabled = false;
            this.ViewFunctionTreemapMenu.Name = "ViewFunctionTreemapMenu";
            this.ViewFunctionTreemapMenu.Size = new System.Drawing.Size(180, 22);
            this.ViewFunctionTreemapMenu.Text = "Function &Treemap";
            this.ViewFunctionTreemapMenu.Click += new System.EventHandler(this.ViewFunctionTreemapMenu_Click);
            // 
            // StatusBar
            // 
            this.StatusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StatusLabel,
            this.AllocationTotals});
            this.StatusBar.Location = new System.Drawing.Point(0, 390);
            this.StatusBar.Name = "StatusBar";
            this.StatusBar.Size = new System.Drawing.Size(584, 22);
            this.StatusBar.TabIndex = 3;
            this.StatusBar.Text = "statusStrip1";
            // 
            // StatusLabel
            // 
            this.StatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.StatusLabel.Size = new System.Drawing.Size(0, 17);
            this.StatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AllocationTotals
            // 
            this.AllocationTotals.AutoSize = false;
            this.AllocationTotals.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.AllocationTotals.Name = "AllocationTotals";
            this.AllocationTotals.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.AllocationTotals.Size = new System.Drawing.Size(569, 17);
            this.AllocationTotals.Spring = true;
            this.AllocationTotals.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Timeline
            // 
            this.Timeline.AllowMultiselect = true;
            this.Timeline.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Timeline.BackColor = System.Drawing.SystemColors.Control;
            this.Timeline.Enabled = false;
            this.Timeline.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Timeline.Location = new System.Drawing.Point(0, 356);
            this.Timeline.Margin = new System.Windows.Forms.Padding(4);
            this.Timeline.Name = "Timeline";
            this.Timeline.RequireMultiselect = true;
            this.Timeline.Scrollable = false;
            this.Timeline.Size = new System.Drawing.Size(584, 32);
            this.Timeline.TabIndex = 4;
            this.Timeline.SelectionChanged += new System.EventHandler(this.Timeline_RangeChanged);
            // 
            // DiffViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 412);
            this.Controls.Add(this.Timeline);
            this.Controls.Add(this.StatusBar);
            this.Controls.Add(this.LoadingPanel);
            this.Controls.Add(this.MainMenu);
            this.Controls.Add(this.MainSplit);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "DiffViewer";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Diff Viewer";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.DiffViewer_FormClosed);
            this.Shown += new System.EventHandler(this.DiffViewer_Shown);
            this.LoadingPanel.ResumeLayout(false);
            this.MainSplit.Panel1.ResumeLayout(false);
            this.MainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MainSplit)).EndInit();
            this.MainSplit.ResumeLayout(false);
            this.ViewSplit.Panel1.ResumeLayout(false);
            this.ViewSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ViewSplit)).EndInit();
            this.ViewSplit.ResumeLayout(false);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.StatusBar.ResumeLayout(false);
            this.StatusBar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar LoadingProgress;
        public System.Windows.Forms.GroupBox LoadingPanel;
        private System.Windows.Forms.SplitContainer MainSplit;
        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem SaveDiffMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem CloseMenu;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ViewListMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewHistogramMenu;
        private System.Windows.Forms.ToolTip ToolTips;
        private System.Windows.Forms.StatusStrip StatusBar;
        private System.Windows.Forms.ToolStripStatusLabel StatusLabel;
        private SnapshotTimeline Timeline;
        private System.Windows.Forms.ToolStripStatusLabel AllocationTotals;
        private ModuleSelector ModuleList;
        private System.Windows.Forms.SplitContainer ViewSplit;
        private GraphHistogram GraphHistogram;
        private DeltaHistogram DeltaHistogram;
        private DeltaList DeltaList;
        private FilterControl TracebackFilter;
        private System.Windows.Forms.ToolStripMenuItem ViewFunctionHistogramMenu;
        private GraphTreemap GraphTreemap;
        private System.Windows.Forms.ToolStripMenuItem ViewFunctionTreemapMenu;
    }
}