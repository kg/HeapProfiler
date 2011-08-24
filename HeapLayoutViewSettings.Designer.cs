namespace HeapProfiler {
    partial class HeapLayoutViewSettings {
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
            this.label1 = new System.Windows.Forms.Label();
            this.StartOffset = new System.Windows.Forms.NumericUpDown();
            this.EndOffset = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.Zoom = new System.Windows.Forms.TrackBar();
            this.JumpToNext = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.StartOffset)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.EndOffset)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Zoom)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Viewing:";
            // 
            // StartOffset
            // 
            this.StartOffset.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.StartOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartOffset.Location = new System.Drawing.Point(3, 18);
            this.StartOffset.Name = "StartOffset";
            this.StartOffset.ReadOnly = true;
            this.StartOffset.Size = new System.Drawing.Size(144, 25);
            this.StartOffset.TabIndex = 1;
            // 
            // EndOffset
            // 
            this.EndOffset.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.EndOffset.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndOffset.Location = new System.Drawing.Point(3, 65);
            this.EndOffset.Name = "EndOffset";
            this.EndOffset.ReadOnly = true;
            this.EndOffset.Size = new System.Drawing.Size(144, 25);
            this.EndOffset.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(3, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(144, 16);
            this.label2.TabIndex = 3;
            this.label2.Text = "to";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 96);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(42, 15);
            this.label3.TabIndex = 4;
            this.label3.Text = "Zoom:";
            // 
            // Zoom
            // 
            this.Zoom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Zoom.LargeChange = 4;
            this.Zoom.Location = new System.Drawing.Point(3, 114);
            this.Zoom.Maximum = 32;
            this.Zoom.Minimum = 1;
            this.Zoom.Name = "Zoom";
            this.Zoom.Size = new System.Drawing.Size(144, 45);
            this.Zoom.TabIndex = 5;
            this.Zoom.TickFrequency = 8;
            this.Zoom.TickStyle = System.Windows.Forms.TickStyle.Both;
            this.Zoom.Value = 32;
            this.Zoom.Scroll += new System.EventHandler(this.Zoom_Scroll);
            // 
            // JumpToNext
            // 
            this.JumpToNext.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.JumpToNext.Location = new System.Drawing.Point(3, 165);
            this.JumpToNext.Name = "JumpToNext";
            this.JumpToNext.Size = new System.Drawing.Size(144, 27);
            this.JumpToNext.TabIndex = 6;
            this.JumpToNext.Text = "Find Next Allocation";
            this.JumpToNext.UseVisualStyleBackColor = true;
            this.JumpToNext.Click += new System.EventHandler(this.JumpToNext_Click);
            // 
            // HeapLayoutViewSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.JumpToNext);
            this.Controls.Add(this.Zoom);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.EndOffset);
            this.Controls.Add(this.StartOffset);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "HeapLayoutViewSettings";
            this.Size = new System.Drawing.Size(150, 300);
            ((System.ComponentModel.ISupportInitialize)(this.StartOffset)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.EndOffset)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Zoom)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown StartOffset;
        private System.Windows.Forms.NumericUpDown EndOffset;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TrackBar Zoom;
        private System.Windows.Forms.Button JumpToNext;
    }
}
