/*
The contents of this file are subject to the Mozilla Public License
Version 1.1 (the "License"); you may not use this file except in
compliance with the License. You may obtain a copy of the License at
http://www.mozilla.org/MPL/

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
License for the specific language governing rights and limitations
under the License.

The Original Code is Windows Heap Profiler Frontend.

The Initial Developer of the Original Code is Mozilla Corporation.

Original Author: K. Gadd (kg@luminance.org)
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Squared.Data.Mangler;
using Squared.Util.Bind;
using Microsoft.Win32;

namespace HeapProfiler {
    public partial class StackFiltersDialog : Form {
        public StackFiltersDialog () {
            InitializeComponent();

            Program.Scheduler.WaitFor(LoadFilters());
        }

        private void OK_Click (object sender, EventArgs e) {
            Program.Scheduler.WaitFor(SaveFilters());
        }

        protected IEnumerator<object> LoadFilters () {
            var fFilters = Program.Preferences.Get("StackFilters");
            yield return fFilters;

            if (fFilters.Failed) {
                FilterList.RowCount = 1;
                yield break;
            }
            
            var filters = fFilters.Result as StackFilter[];

            FilterList.RowCount = filters.Length + 1;
            for (int i = 0, l = filters.Length; i < l; i++) {
                var row = FilterList.Rows[i];
                var filter = filters[i];
                row.Cells[0].Value = filter.ModuleGlob;
                row.Cells[1].Value = filter.FunctionGlob;
            }
        }

        protected IEnumerator<object> SaveFilters () {
            var filters = new List<StackFilter>();

            foreach (DataGridViewRow row in FilterList.Rows) {
                var module = row.Cells[0].Value as string;
                var function = row.Cells[1].Value as string;
                if (String.IsNullOrWhiteSpace(module))
                    module = null;
                if (String.IsNullOrWhiteSpace(function))
                    function = null;

                if ((module == null) && (function == null))
                    continue;

                filters.Add(new StackFilter(module, function));
            }

            yield return Program.Preferences.Set("StackFilters", filters.ToArray());
        }
    }
}
