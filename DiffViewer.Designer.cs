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
            this.ModuleSelectionToolbar = new System.Windows.Forms.ToolStrip();
            this.SelectAllModules = new System.Windows.Forms.ToolStripButton();
            this.SelectNoModules = new System.Windows.Forms.ToolStripButton();
            this.InvertModuleSelection = new System.Windows.Forms.ToolStripButton();
            this.ModuleList = new System.Windows.Forms.CheckedListBox();
            this.FilterPanel = new System.Windows.Forms.Panel();
            this.FindIcon = new System.Windows.Forms.PictureBox();
            this.TracebackFilter = new System.Windows.Forms.TextBox();
            this.DeltaList = new HeapProfiler.DeltaList();
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SaveDiffMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.CloseMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewListMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewHistogramMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.StatusBar = new System.Windows.Forms.StatusStrip();
            this.StatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.LoadingPanel.SuspendLayout();
            this.MainSplit.Panel1.SuspendLayout();
            this.MainSplit.Panel2.SuspendLayout();
            this.MainSplit.SuspendLayout();
            this.ModuleSelectionToolbar.SuspendLayout();
            this.FilterPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FindIcon)).BeginInit();
            this.MainMenu.SuspendLayout();
            this.StatusBar.SuspendLayout();
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
            this.MainSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.MainSplit.Location = new System.Drawing.Point(0, 27);
            this.MainSplit.Name = "MainSplit";
            // 
            // MainSplit.Panel1
            // 
            this.MainSplit.Panel1.Controls.Add(this.ModuleSelectionToolbar);
            this.MainSplit.Panel1.Controls.Add(this.ModuleList);
            this.MainSplit.Panel1MinSize = 75;
            // 
            // MainSplit.Panel2
            // 
            this.MainSplit.Panel2.Controls.Add(this.FilterPanel);
            this.MainSplit.Panel2.Controls.Add(this.DeltaList);
            this.MainSplit.Size = new System.Drawing.Size(484, 260);
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
            this.ModuleList.Size = new System.Drawing.Size(161, 232);
            this.ModuleList.TabIndex = 2;
            this.ToolTips.SetToolTip(this.ModuleList, "Filter Tracebacks By Module");
            this.ModuleList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ModuleList_ItemCheck);
            // 
            // FilterPanel
            // 
            this.FilterPanel.Controls.Add(this.FindIcon);
            this.FilterPanel.Controls.Add(this.TracebackFilter);
            this.FilterPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.FilterPanel.Location = new System.Drawing.Point(0, 0);
            this.FilterPanel.Name = "FilterPanel";
            this.FilterPanel.Size = new System.Drawing.Size(319, 31);
            this.FilterPanel.TabIndex = 1;
            // 
            // FindIcon
            // 
            this.FindIcon.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)));
            this.FindIcon.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("FindIcon.BackgroundImage")));
            this.FindIcon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.FindIcon.Location = new System.Drawing.Point(3, 3);
            this.FindIcon.Name = "FindIcon";
            this.FindIcon.Size = new System.Drawing.Size(18, 25);
            this.FindIcon.TabIndex = 1;
            this.FindIcon.TabStop = false;
            // 
            // TracebackFilter
            // 
            this.TracebackFilter.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.TracebackFilter.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.TracebackFilter.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.TracebackFilter.Font = new System.Drawing.Font("Consolas", 11.25F);
            this.TracebackFilter.Location = new System.Drawing.Point(23, 3);
            this.TracebackFilter.Name = "TracebackFilter";
            this.TracebackFilter.Size = new System.Drawing.Size(293, 25);
            this.TracebackFilter.TabIndex = 0;
            this.ToolTips.SetToolTip(this.TracebackFilter, "Filter Tracebacks By Function");
            this.TracebackFilter.TextChanged += new System.EventHandler(this.TracebackFilter_TextChanged);
            // 
            // DeltaList
            // 
            this.DeltaList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.DeltaList.BackColor = System.Drawing.SystemColors.Window;
            this.DeltaList.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeltaList.ForeColor = System.Drawing.SystemColors.WindowText;
            this.DeltaList.Location = new System.Drawing.Point(0, 31);
            this.DeltaList.Name = "DeltaList";
            this.DeltaList.ScrollOffset = 0;
            this.DeltaList.SelectedIndex = 0;
            this.DeltaList.Size = new System.Drawing.Size(319, 229);
            this.DeltaList.TabIndex = 0;
            // 
            // MainMenu
            // 
            this.MainMenu.Enabled = false;
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(484, 24);
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
            this.ViewHistogramMenu});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "&View";
            // 
            // ViewListMenu
            // 
            this.ViewListMenu.Checked = true;
            this.ViewListMenu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ViewListMenu.Name = "ViewListMenu";
            this.ViewListMenu.Size = new System.Drawing.Size(149, 22);
            this.ViewListMenu.Text = "Traceback &List";
            // 
            // ViewHistogramMenu
            // 
            this.ViewHistogramMenu.Enabled = false;
            this.ViewHistogramMenu.Name = "ViewHistogramMenu";
            this.ViewHistogramMenu.Size = new System.Drawing.Size(149, 22);
            this.ViewHistogramMenu.Text = "&Histogram";
            // 
            // StatusBar
            // 
            this.StatusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StatusLabel});
            this.StatusBar.Location = new System.Drawing.Point(0, 290);
            this.StatusBar.Name = "StatusBar";
            this.StatusBar.Size = new System.Drawing.Size(484, 22);
            this.StatusBar.TabIndex = 3;
            this.StatusBar.Text = "statusStrip1";
            // 
            // StatusLabel
            // 
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(438, 17);
            this.StatusLabel.Spring = true;
            this.StatusLabel.Text = "No Results";
            this.StatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DiffViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 312);
            this.Controls.Add(this.StatusBar);
            this.Controls.Add(this.MainSplit);
            this.Controls.Add(this.LoadingPanel);
            this.Controls.Add(this.MainMenu);
            this.MainMenuStrip = this.MainMenu;
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
            this.FilterPanel.ResumeLayout(false);
            this.FilterPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FindIcon)).EndInit();
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
        private System.Windows.Forms.CheckedListBox ModuleList;
        private DeltaList DeltaList;
        private System.Windows.Forms.ToolStrip ModuleSelectionToolbar;
        private System.Windows.Forms.ToolStripButton SelectAllModules;
        private System.Windows.Forms.ToolStripButton SelectNoModules;
        private System.Windows.Forms.ToolStripButton InvertModuleSelection;
        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem SaveDiffMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem CloseMenu;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ViewListMenu;
        private System.Windows.Forms.ToolStripMenuItem ViewHistogramMenu;
        private System.Windows.Forms.Panel FilterPanel;
        private System.Windows.Forms.PictureBox FindIcon;
        private System.Windows.Forms.TextBox TracebackFilter;
        private System.Windows.Forms.ToolTip ToolTips;
        private System.Windows.Forms.StatusStrip StatusBar;
        private System.Windows.Forms.ToolStripStatusLabel StatusLabel;
    }
}