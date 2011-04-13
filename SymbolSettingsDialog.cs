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
    public partial class SymbolSettingsDialog : Form {
        TanglePropertySerializer PreferenceSerializer;
        Dictionary<IBoundMember, object> Defaults = new Dictionary<IBoundMember, object>();

        public SymbolSettingsDialog () {
            InitializeComponent();

            PreferenceSerializer = new TanglePropertySerializer(
                Program.Preferences, ChooseName
            );

            PreferenceSerializer.Bind(() => SymbolServers.Text);
            PreferenceSerializer.Bind(() => SymbolPath.Text);

            foreach (var binding in PreferenceSerializer.Bindings)
                Defaults[binding] = binding.Value;

            Program.Scheduler.WaitFor(PreferenceSerializer.Load());
        }

        protected string ChooseName (IBoundMember bm) {
            return String.Format("{0}", (bm.Target as Control).Name);
        }

        private void OK_Click (object sender, EventArgs e) {
            Program.Scheduler.WaitFor(PreferenceSerializer.Save());
        }

        private void ResetToDefault_Click (object sender, EventArgs e) {
            foreach (var preference in PreferenceSerializer.Bindings)
                preference.Value = Defaults[preference];
        }
    }
}
