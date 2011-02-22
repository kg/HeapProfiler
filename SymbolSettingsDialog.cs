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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Squared.Util.Bind;
using Microsoft.Win32;

namespace HeapProfiler {
    public partial class SymbolSettingsDialog : Form {
        IBoundMember[] PersistedControls;

        public SymbolSettingsDialog () {
            InitializeComponent();

            PersistedControls = new[] {
                BoundMember.New(() => SymbolServers.Text),
                BoundMember.New(() => SymbolPath.Text),
            };

            LoadPersistedValues();
        }

        protected string ChooseName (IBoundMember bm) {
            return String.Format("{0}_{1}", (bm.Target as Control).Name, bm.Name);
        }

        protected void LoadPersistedValues () {
            if (!Registry.CurrentUser.SubKeyExists("Software\\HeapProfiler\\Symbols"))
                return;

            using (var key = Registry.CurrentUser.OpenSubKey("Software\\HeapProfiler\\Symbols"))
            foreach (var pc in PersistedControls)
                pc.Value = key.GetValue(ChooseName(pc), pc.Value);
        }

        protected void SavePersistedValues () {
            using (var key = Registry.CurrentUser.OpenOrCreateSubKey("Software\\HeapProfiler\\Symbols"))
            foreach (var pc in PersistedControls)
                key.SetValue(ChooseName(pc), pc.Value);
        }

        private void OK_Click (object sender, EventArgs e) {
            SavePersistedValues();
        }

        private void ResetToDefault_Click (object sender, EventArgs e) {
            if (!Registry.CurrentUser.SubKeyExists("Software\\HeapProfiler\\Symbols"))
                return;

            Registry.CurrentUser.DeleteSubKey("Software\\HeapProfiler\\Symbols", false);
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }
    }
}
