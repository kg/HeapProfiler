namespace HeapProfiler {
    partial class ModuleSelector {
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent () {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModuleSelector));
            this.Toolbar = new System.Windows.Forms.ToolStrip();
            this.SelectAllModules = new System.Windows.Forms.ToolStripButton();
            this.SelectNoModules = new System.Windows.Forms.ToolStripButton();
            this.InvertModuleSelection = new System.Windows.Forms.ToolStripButton();
            this.List = new System.Windows.Forms.CheckedListBox();
            this.Toolbar.SuspendLayout();
            this.SuspendLayout();
            // 
            // Toolbar
            // 
            this.Toolbar.CanOverflow = false;
            this.Toolbar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.Toolbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SelectAllModules,
            this.SelectNoModules,
            this.InvertModuleSelection});
            this.Toolbar.Location = new System.Drawing.Point(0, 0);
            this.Toolbar.Name = "Toolbar";
            this.Toolbar.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.Toolbar.Size = new System.Drawing.Size(150, 27);
            this.Toolbar.TabIndex = 5;
            this.Toolbar.Text = "toolStrip1";
            // 
            // SelectAllModules
            // 
            this.SelectAllModules.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.SelectAllModules.Image = ((System.Drawing.Image)(resources.GetObject("SelectAllModules.Image")));
            this.SelectAllModules.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.SelectAllModules.Name = "SelectAllModules";
            this.SelectAllModules.Size = new System.Drawing.Size(31, 24);
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
            this.SelectNoModules.Size = new System.Drawing.Size(49, 24);
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
            this.InvertModuleSelection.Size = new System.Drawing.Size(50, 24);
            this.InvertModuleSelection.Text = "Invert";
            this.InvertModuleSelection.ToolTipText = "Invert Selection";
            this.InvertModuleSelection.Click += new System.EventHandler(this.InvertModuleSelection_Click);
            // 
            // List
            // 
            this.List.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.List.CheckOnClick = true;
            this.List.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.List.FormattingEnabled = true;
            this.List.IntegralHeight = false;
            this.List.Location = new System.Drawing.Point(0, 30);
            this.List.Margin = new System.Windows.Forms.Padding(5);
            this.List.Name = "List";
            this.List.Size = new System.Drawing.Size(150, 120);
            this.List.TabIndex = 4;
            this.List.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.List_ItemCheck);
            // 
            // ModuleSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Toolbar);
            this.Controls.Add(this.List);
            this.Name = "ModuleSelector";
            this.Toolbar.ResumeLayout(false);
            this.Toolbar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip Toolbar;
        private System.Windows.Forms.ToolStripButton SelectAllModules;
        private System.Windows.Forms.ToolStripButton SelectNoModules;
        private System.Windows.Forms.ToolStripButton InvertModuleSelection;
        private System.Windows.Forms.CheckedListBox List;
    }
}
