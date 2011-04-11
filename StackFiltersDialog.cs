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

Original Author: Kevin Gadd (kevin.gadd@gmail.com)
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
        }

        private void OK_Click (object sender, EventArgs e) {
        }
    }
}
