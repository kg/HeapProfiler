namespace HeapProfiler {
    partial class StackFiltersDialog {
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
            this.Cancel = new System.Windows.Forms.Button();
            this.OK = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.FilterList = new System.Windows.Forms.DataGridView();
            this.Module = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Function = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FilterList)).BeginInit();
            this.SuspendLayout();
            // 
            // Cancel
            // 
            this.Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Cancel.Location = new System.Drawing.Point(360, 261);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(110, 32);
            this.Cancel.TabIndex = 3;
            this.Cancel.Text = "Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            // 
            // OK
            // 
            this.OK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.OK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.OK.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OK.Location = new System.Drawing.Point(244, 261);
            this.OK.Name = "OK";
            this.OK.Size = new System.Drawing.Size(110, 32);
            this.OK.TabIndex = 2;
            this.OK.Text = "OK";
            this.OK.UseVisualStyleBackColor = true;
            this.OK.Click += new System.EventHandler(this.OK_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.FilterList);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(458, 243);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Stack Frames To Ignore";
            // 
            // FilterList
            // 
            this.FilterList.AllowUserToResizeColumns = false;
            this.FilterList.AllowUserToResizeRows = false;
            this.FilterList.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.FilterList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FilterList.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.FilterList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.FilterList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Module,
            this.Function});
            this.FilterList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.FilterList.Location = new System.Drawing.Point(6, 23);
            this.FilterList.Name = "FilterList";
            this.FilterList.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.FilterList.RowHeadersVisible = false;
            this.FilterList.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.FilterList.RowTemplate.Height = 24;
            this.FilterList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.FilterList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.FilterList.ShowCellErrors = false;
            this.FilterList.ShowEditingIcon = false;
            this.FilterList.ShowRowErrors = false;
            this.FilterList.Size = new System.Drawing.Size(446, 214);
            this.FilterList.TabIndex = 0;
            // 
            // Module
            // 
            this.Module.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Module.FillWeight = 60F;
            this.Module.HeaderText = "Module";
            this.Module.Name = "Module";
            this.Module.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // Function
            // 
            this.Function.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Function.HeaderText = "Function";
            this.Function.Name = "Function";
            this.Function.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // StackFiltersDialog
            // 
            this.AcceptButton = this.OK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.Cancel;
            this.ClientSize = new System.Drawing.Size(482, 305);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.OK);
            this.Controls.Add(this.Cancel);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StackFiltersDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Stack Filter Settings";
            this.groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FilterList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.Button OK;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.DataGridView FilterList;
        private System.Windows.Forms.DataGridViewTextBoxColumn Module;
        private System.Windows.Forms.DataGridViewTextBoxColumn Function;
    }
}