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

        protected bool _Updating = false;
        protected Dictionary<string, ModuleInfo> _Items = new Dictionary<string, ModuleInfo>();

        public ModuleSelector () {
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Dictionary<string, ModuleInfo> Items {
            get {
                return _Items;
            }
            set {
                _Items = value;

                List.BeginUpdate();
                _Updating = true;

                List.Items.Clear();
                foreach (var key in value.Keys.OrderBy((s) => s))
                    List.Items.Add(value[key]);
                for (int i = 0; i < List.Items.Count; i++)
                    List.SetItemChecked(i, true);

                _Updating = false;
                List.EndUpdate();
            }
        }

        private void List_ItemCheck (object sender, ItemCheckEventArgs e) {
            var m = (ModuleInfo)List.Items[e.Index];
            m.Filtered = (e.NewValue == CheckState.Unchecked);

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
