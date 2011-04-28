namespace HeapProfiler {
    partial class FilterControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilterControl));
            this.FindIcon = new System.Windows.Forms.PictureBox();
            this.FilterText = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.FindIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // FindIcon
            // 
            this.FindIcon.BackColor = System.Drawing.Color.Transparent;
            this.FindIcon.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("FindIcon.BackgroundImage")));
            this.FindIcon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.FindIcon.Dock = System.Windows.Forms.DockStyle.Left;
            this.FindIcon.Location = new System.Drawing.Point(0, 0);
            this.FindIcon.Margin = new System.Windows.Forms.Padding(4);
            this.FindIcon.Name = "FindIcon";
            this.FindIcon.Size = new System.Drawing.Size(22, 24);
            this.FindIcon.TabIndex = 7;
            this.FindIcon.TabStop = false;
            // 
            // FilterText
            // 
            this.FilterText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FilterText.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.FilterText.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.FilterText.Font = new System.Drawing.Font("Consolas", 11.25F);
            this.FilterText.Location = new System.Drawing.Point(21, 0);
            this.FilterText.Margin = new System.Windows.Forms.Padding(4);
            this.FilterText.Name = "FilterText";
            this.FilterText.Size = new System.Drawing.Size(241, 25);
            this.FilterText.TabIndex = 6;
            this.FilterText.TextChanged += new System.EventHandler(this.FilterText_TextChanged);
            // 
            // FilterControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.FilterText);
            this.Controls.Add(this.FindIcon);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Font = new System.Drawing.Font("Consolas", 11.25F);
            this.Name = "FilterControl";
            this.Size = new System.Drawing.Size(262, 24);
            this.SizeChanged += new System.EventHandler(this.FilterControl_SizeChanged);
            ((System.ComponentModel.ISupportInitialize)(this.FindIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox FindIcon;
        private System.Windows.Forms.TextBox FilterText;
    }
}
