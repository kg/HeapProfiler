namespace HeapProfiler {
    partial class HeapViewer {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HeapViewer));
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.CloseMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.Timeline = new HeapProfiler.TimelineRangeSelector();
            this.LayoutView = new HeapProfiler.HeapLayoutView();
            this.MainMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainMenu
            // 
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(584, 24);
            this.MainMenu.TabIndex = 2;
            this.MainMenu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1,
            this.CloseMenu});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(100, 6);
            // 
            // CloseMenu
            // 
            this.CloseMenu.Name = "CloseMenu";
            this.CloseMenu.Size = new System.Drawing.Size(103, 22);
            this.CloseMenu.Text = "&Close";
            this.CloseMenu.Click += new System.EventHandler(this.CloseMenu_Click);
            // 
            // Timeline
            // 
            this.Timeline.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Timeline.BackColor = System.Drawing.SystemColors.Control;
            this.Timeline.Enabled = false;
            this.Timeline.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Timeline.Location = new System.Drawing.Point(0, 379);
            this.Timeline.Margin = new System.Windows.Forms.Padding(4);
            this.Timeline.Name = "Timeline";
            this.Timeline.Size = new System.Drawing.Size(584, 32);
            this.Timeline.TabIndex = 4;
            this.Timeline.RangeChanged += new System.EventHandler(this.Timeline_RangeChanged);
            // 
            // LayoutView
            // 
            this.LayoutView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LayoutView.BackColor = System.Drawing.SystemColors.Window;
            this.LayoutView.ForeColor = System.Drawing.SystemColors.WindowText;
            this.LayoutView.Location = new System.Drawing.Point(0, 26);
            this.LayoutView.Margin = new System.Windows.Forms.Padding(2);
            this.LayoutView.Name = "LayoutView";
            this.LayoutView.Size = new System.Drawing.Size(584, 350);
            this.LayoutView.TabIndex = 5;
            // 
            // HeapViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 412);
            this.Controls.Add(this.LayoutView);
            this.Controls.Add(this.Timeline);
            this.Controls.Add(this.MainMenu);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "HeapViewer";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Heap Viewer";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.HeapViewer_FormClosed);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem CloseMenu;
        private System.Windows.Forms.ToolTip ToolTips;
        private TimelineRangeSelector Timeline;
        private HeapLayoutView LayoutView;
    }
}