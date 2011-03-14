using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeapProfiler {
    public partial class ModuleSelector : UserControl {
        public event EventHandler FilterChanged;
        public readonly HashSet<string> SelectedItems = new HashSet<string>();

        protected bool _Updating = false;
        protected IEnumerable<string> _Items = new string[0];

        public ModuleSelector () {
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public IEnumerable<string> Items {
            get {
                return _Items;
            }
            set {
                _Items = value;

                List.BeginUpdate();
                _Updating = true;

                List.Items.Clear();
                SelectedItems.Clear();

                foreach (var key in value.OrderBy((s) => s))
                    List.Items.Add(key);
                for (int i = 0; i < List.Items.Count; i++)
                    List.SetItemChecked(i, true);

                _Updating = false;
                List.EndUpdate();
            }
        }

        private void List_ItemCheck (object sender, ItemCheckEventArgs e) {
            var m = (string)List.Items[e.Index];
            if (e.NewValue == CheckState.Unchecked) {
                SelectedItems.Remove(m);
            } else {
                SelectedItems.Add(m);
            }

            if (!_Updating)
                OnFilterChanged();
        }

        private void SelectAllModules_Click (object sender, EventArgs e) {
            List.BeginUpdate();
            _Updating = true;

            for (int i = 0; i < List.Items.Count; i++)
                List.SetItemChecked(i, true);

            _Updating = false;
            List.EndUpdate();
            OnFilterChanged();
        }

        private void SelectNoModules_Click (object sender, EventArgs e) {
            List.BeginUpdate();
            _Updating = true;

            for (int i = 0; i < List.Items.Count; i++)
                List.SetItemChecked(i, false);

            _Updating = false;
            List.EndUpdate();
            OnFilterChanged();
        }

        private void InvertModuleSelection_Click (object sender, EventArgs e) {
            List.BeginUpdate();
            _Updating = true;

            for (int i = 0; i < List.Items.Count; i++)
                List.SetItemChecked(i, !List.GetItemChecked(i));

            _Updating = false;
            List.EndUpdate();
            OnFilterChanged();
        }

        protected void OnFilterChanged () {
            if (FilterChanged != null)
                FilterChanged(this, EventArgs.Empty);
        }
    }
}
