using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeapProfiler {
    public partial class ErrorListDialog : Form {
        public event EventHandler ErrorReported;
        public event EventHandler ListCleared;

        public ErrorListDialog () {
            InitializeComponent();
        }

        private void ClearList_Click (object sender, EventArgs e) {
            ErrorList.Items.Clear();
            ErrorText.Text = "";

            if (ListCleared != null)
                ListCleared(this, EventArgs.Empty);
        }

        private void ErrorList_SelectedIndexChanged (object sender, EventArgs e) {
            ErrorText.Text = (ErrorList.SelectedItem as string) ?? "";
        }

        public void ReportError (string format, params object[] values) {
            bool selectNew = (ErrorList.SelectedIndex == ErrorList.Items.Count - 1);

            ErrorList.Items.Add(String.Format(format, values));

            if (ErrorReported != null)
                ErrorReported(this, EventArgs.Empty);

            if (selectNew)
                ErrorList.SelectedIndex = ErrorList.Items.Count - 1;
        }

        private void ErrorListDialog_Shown (object sender, EventArgs e) {
            ErrorList.SelectedIndex = ErrorList.Items.Count - 1;
        }

        private void ErrorListDialog_FormClosing (object sender, FormClosingEventArgs e) {
            e.Cancel = true;
            this.Hide();
        }

        public int Count {
            get {
                return ErrorList.Items.Count;
            }
        }
    }
}
