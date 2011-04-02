using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeapProfiler {
    public partial class FilterControl : UserControl {
        public event FilterChangingEventHandler FilterChanging;
        public event EventHandler FilterChanged;

        private string _Filter;
        private bool? _FilterTextIsValid = null;

        public FilterControl () {
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Filter {
            get {
                return _Filter;
            }
            set {
                if (value == _Filter)
                    return;

                FilterText.Text = _Filter;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public IEnumerable<string> AutoCompleteItems {
            set {
                if (value == null) {
                    FilterText.AutoCompleteMode = AutoCompleteMode.None;
                    FilterText.AutoCompleteSource = AutoCompleteSource.None;
                    FilterText.AutoCompleteCustomSource = null;
                } else {
                    FilterText.AutoCompleteCustomSource = new AutoCompleteStringCollection();
                    FilterText.AutoCompleteCustomSource.AddRange(value.ToArray());
                    FilterText.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    FilterText.AutoCompleteMode = AutoCompleteMode.Suggest;
                }
            }
        }

        private void FilterControl_SizeChanged (object sender, EventArgs e) {
            FilterText.Width = this.ClientSize.Width - FindIcon.Width;
        }

        private void FilterText_TextChanged (object sender, EventArgs e) {
            if (FilterText.Text != _Filter) {
                if (FilterText.Text.Trim().Length == 0) {
                    if (SystemColors.Window != FilterText.BackColor)
                        FilterText.BackColor = SystemColors.Window;

                    _Filter = null;

                    if (FilterChanged != null)
                        FilterChanged(this, EventArgs.Empty);

                    return;
                }

                var ea = new FilterChangingEventArgs(FilterText.Text);

                if (FilterChanging != null)
                    FilterChanging(this, ea);
                else
                    ea.SetValid(true);

                var newColor =
                    (ea.IsValid.HasValue) ?
                        ((ea.IsValid.GetValueOrDefault(false)) ?
                            Color.LightGoldenrodYellow : Color.LightPink)
                        : SystemColors.Window;

                if (newColor != FilterText.BackColor)
                    FilterText.BackColor = newColor;

                if (ea.IsValid.GetValueOrDefault(false))
                    _Filter = FilterText.Text;
                else
                    _Filter = null;

                if (FilterChanged != null)
                    FilterChanged(this, EventArgs.Empty);
            }
        }
    }

    public class FilterChangingEventArgs : EventArgs {
        public readonly string Filter;
        private bool? FilterIsValid;

        public FilterChangingEventArgs (string filter) {
            Filter = filter;
            FilterIsValid = null;
        }

        public void SetValid (bool isValid) {
            FilterIsValid = isValid;
        }

        public bool? IsValid {
            get {
                return FilterIsValid;
            }
        }
    }

    public delegate void FilterChangingEventHandler (object sender, FilterChangingEventArgs args);
}
