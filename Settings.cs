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
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace HeapProfiler {
    public static class Settings {
        [DllImport("msi.dll", SetLastError=true, CharSet=CharSet.Unicode)]
        static unsafe extern Int32 MsiGetProductInfo (
            string product, string property, char * buf, ref int length
        );
        [DllImport("msi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static unsafe extern Int32 MsiGetComponentPath (
            string product, string component, char* buf, ref int length
        );

        public static string GflagsPath {
            get {
                return GetMsiComponentPath(
                    // dbg_x86.msi
                    "{D09605BE-5587-4B0C-86C8-69B5092CB80F}", 
                    // gflags.exe
                    "{B714F174-59EA-4C1E-BCF8-5989FA7D90B2}"
                );
            }
        }

        public static string UmdhPath {
            get {
                return GetMsiComponentPath(
                    // dbg_x86.msi
                    "{D09605BE-5587-4B0C-86C8-69B5092CB80F}",
                    // umdh.exe
                    "{FBC70C26-11E0-4DC7-816D-10D88032F0B3}"
                );
            }
        }

        static unsafe string GetMsiComponentPath (string product, string component) {
            int length = 0;

            var rc = MsiGetComponentPath(product, component, null, ref length);
            if (rc <= 0)
                throw new Exception(String.Format("MsiGetComponentPath failed with error code {0}", rc));

            length += 1;
            char[] buffer = new char[length];

            fixed (char* pBuffer = buffer) {
                rc = MsiGetComponentPath(product, component, pBuffer, ref length);
                if (rc <= 0)
                    throw new Exception(String.Format("MsiGetComponentPath failed with error code {0}", rc));
            }

            return new String(buffer, 0, length);
        }

        static unsafe string GetMsiProperty (string product, string property) {
            int length = 0;

            var rc = MsiGetProductInfo(product, property, null, ref length);
            if (rc != 0)
                throw new Exception(String.Format("MsiGetProductInfo failed with error code {0}", rc));

            length += 1;
            char[] buffer = new char[length];

            fixed (char* pBuffer = buffer) {
                rc = MsiGetProductInfo(product, property, pBuffer, ref length);
                if (rc != 0)
                    throw new Exception(String.Format("MsiGetProductInfo failed with error code {0}", rc));
            }

            return new String(buffer, 0, length);
        }
    }
}
