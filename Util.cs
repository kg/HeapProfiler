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
using System.Drawing;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Squared.Util.RegexExtensions;

namespace HeapProfiler {
    class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class {

        public bool Equals (T x, T y) {
            return (x == y);
        }

        public int GetHashCode (T obj) {
            return obj.GetHashCode();
        }
    }

    public class ScratchBuffer : IDisposable {
        const uint SRCCOPY = 0xCC0020;

        [DllImport("gdi32.dll", SetLastError=true)]
        static extern bool BitBlt (
            IntPtr hDC, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hDCSrc, int nXSrc, int nYSrc, uint dwRop
        );

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr CreateCompatibleDC (IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool DeleteObject (IntPtr hGDIObj);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr SelectObject (IntPtr hDC, IntPtr hGDIObj);

        public struct Region : IDisposable {
            public readonly Graphics Graphics;
            public readonly Bitmap Bitmap;
            private readonly IntPtr HBitmap, HDC;

            public readonly Graphics DestinationGraphics;
            public readonly Rectangle DestinationRegion;

            bool Cancelled;

            public Region (ScratchBuffer sb, Graphics graphics, Rectangle region) {
                var minWidth = (int)Math.Ceiling(region.Width / 16.0f) * 16;
                var minHeight = (int)Math.Ceiling(region.Height / 16.0f) * 16;

                var needNewBitmap =
                    (sb._ScratchBuffer == null) || (sb._ScratchBuffer.Width < minWidth) ||
                    (sb._ScratchBuffer.Height < minHeight);

                if (needNewBitmap && sb._ScratchBuffer != null) {
                    DeleteObject(sb._ScratchHDC);
                    DeleteObject(sb._ScratchHBitmap);
                    sb._ScratchBuffer.Dispose();
                }
                if (needNewBitmap && sb._ScratchGraphics != null)
                    sb._ScratchGraphics.Dispose();

                if (needNewBitmap) {
                    Bitmap = sb._ScratchBuffer = new Bitmap(
                        minWidth, minHeight, System.Drawing.Imaging.PixelFormat.Format32bppRgb
                    );
                    HBitmap = sb._ScratchHBitmap = Bitmap.GetHbitmap();
                    var tempDC = graphics.GetHdc();
                    HDC = sb._ScratchHDC = CreateCompatibleDC(tempDC);
                    graphics.ReleaseHdc(tempDC);
                    SelectObject(HDC, HBitmap);
                    Graphics = sb._ScratchGraphics = Graphics.FromHdc(HDC);
                } else {
                    HDC = sb._ScratchHDC;
                    HBitmap = sb._ScratchHBitmap;
                    Bitmap = sb._ScratchBuffer;
                    Graphics = sb._ScratchGraphics;
                }

                Graphics.ResetTransform();
                Graphics.TranslateTransform(-region.X, -region.Y, System.Drawing.Drawing2D.MatrixOrder.Prepend);
                Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                DestinationGraphics = graphics;
                DestinationRegion = region;

                Cancelled = false;
            }

            public void Cancel () {
                Cancelled = true;
            }

            public void Dispose () {
                if (Cancelled)
                    return;

                var destDC = DestinationGraphics.GetHdc();

                try {
                    BitBlt(
                        destDC,
                        DestinationRegion.X, DestinationRegion.Y,
                        DestinationRegion.Width, DestinationRegion.Height,
                        HDC,
                        0, 0, SRCCOPY
                    );
                } finally {
                    DestinationGraphics.ReleaseHdc(destDC);
                }

                Cancelled = true;
            }
        }

        protected IntPtr _ScratchHBitmap, _ScratchHDC;
        protected Bitmap _ScratchBuffer = null;
        protected Graphics _ScratchGraphics = null;

        public ScratchBuffer () {
        }

        public Region Get (Graphics graphics, Rectangle region) {
            return new Region(
                this, graphics, region
            );
        }

        public Region Get (Graphics graphics, RectangleF region) {
            return new Region(
                this, graphics, new Rectangle(
                    (int)Math.Floor(region.X), (int)Math.Floor(region.Y),
                    (int)Math.Ceiling(region.Width), (int)Math.Ceiling(region.Height)
                )
            );
        }

        public void Dispose () {
            if (_ScratchGraphics != null) {
                _ScratchGraphics.Dispose();
                _ScratchGraphics = null;
            }

            if (_ScratchBuffer != null) {
                DeleteObject(_ScratchHBitmap);
                DeleteObject(_ScratchHDC);
                _ScratchHDC = _ScratchHBitmap = IntPtr.Zero;
                _ScratchBuffer.Dispose();
                _ScratchBuffer = null;
            }
        }
    }

    public static class RegistryExtensions {
        public static bool SubKeyExists (this RegistryKey key, string name) {
            try {
                var subkey = key.OpenSubKey(name);
                if (subkey != null) {
                    using (subkey)
                        return true;
                } else
                    return false;

            } catch {
                return false;
            }
        }

        public static RegistryKey OpenOrCreateSubKey (this RegistryKey key, string name) {
            var subkey = key.OpenSubKey(name, true);
            if (subkey != null)
                return subkey;

            return key.CreateSubKey(name);
        }
    }

    #region enum HChangeNotifyEventID
    /// <summary>
    /// Describes the event that has occurred.
    /// Typically, only one event is specified at a time.
    /// If more than one event is specified, the values contained
    /// in the <i>dwItem1</i> and <i>dwItem2</i>
    /// parameters must be the same, respectively, for all specified events.
    /// This parameter can be one or more of the following values.
    /// </summary>
    /// <remarks>
    /// <para><b>Windows NT/2000/XP:</b> <i>dwItem2</i> contains the index
    /// in the system image list that has changed.
    /// <i>dwItem1</i> is not used and should be <see langword="null"/>.</para>
    /// <para><b>Windows 95/98:</b> <i>dwItem1</i> contains the index
    /// in the system image list that has changed.
    /// <i>dwItem2</i> is not used and should be <see langword="null"/>.</para>
    /// </remarks>
    [Flags]
    enum HChangeNotifyEventID {
        /// <summary>
        /// All events have occurred.
        /// </summary>
        SHCNE_ALLEVENTS = 0x7FFFFFFF,

        /// <summary>
        /// A file type association has changed. <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/>
        /// must be specified in the <i>uFlags</i> parameter.
        /// <i>dwItem1</i> and <i>dwItem2</i> are not used and must be <see langword="null"/>.
        /// </summary>
        SHCNE_ASSOCCHANGED = 0x08000000,

        /// <summary>
        /// The attributes of an item or folder have changed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the item or folder that has changed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_ATTRIBUTES = 0x00000800,

        /// <summary>
        /// A nonfolder item has been created.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the item that was created.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_CREATE = 0x00000002,

        /// <summary>
        /// A nonfolder item has been deleted.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the item that was deleted.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_DELETE = 0x00000004,

        /// <summary>
        /// A drive has been added.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive that was added.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_DRIVEADD = 0x00000100,

        /// <summary>
        /// A drive has been added and the Shell should create a new window for the drive.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive that was added.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_DRIVEADDGUI = 0x00010000,

        /// <summary>
        /// A drive has been removed. <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive that was removed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_DRIVEREMOVED = 0x00000080,

        /// <summary>
        /// Not currently used.
        /// </summary>
        SHCNE_EXTENDED_EVENT = 0x04000000,

        /// <summary>
        /// The amount of free space on a drive has changed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive on which the free space changed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_FREESPACE = 0x00040000,

        /// <summary>
        /// Storage media has been inserted into a drive.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive that contains the new media.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_MEDIAINSERTED = 0x00000020,

        /// <summary>
        /// Storage media has been removed from a drive.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the root of the drive from which the media was removed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_MEDIAREMOVED = 0x00000040,

        /// <summary>
        /// A folder has been created. <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/>
        /// or <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the folder that was created.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_MKDIR = 0x00000008,

        /// <summary>
        /// A folder on the local computer is being shared via the network.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the folder that is being shared.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_NETSHARE = 0x00000200,

        /// <summary>
        /// A folder on the local computer is no longer being shared via the network.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the folder that is no longer being shared.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_NETUNSHARE = 0x00000400,

        /// <summary>
        /// The name of a folder has changed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the previous pointer to an item identifier list (PIDL) or name of the folder.
        /// <i>dwItem2</i> contains the new PIDL or name of the folder.
        /// </summary>
        SHCNE_RENAMEFOLDER = 0x00020000,

        /// <summary>
        /// The name of a nonfolder item has changed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the previous PIDL or name of the item.
        /// <i>dwItem2</i> contains the new PIDL or name of the item.
        /// </summary>
        SHCNE_RENAMEITEM = 0x00000001,

        /// <summary>
        /// A folder has been removed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the folder that was removed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_RMDIR = 0x00000010,

        /// <summary>
        /// The computer has disconnected from a server.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the server from which the computer was disconnected.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// </summary>
        SHCNE_SERVERDISCONNECT = 0x00004000,

        /// <summary>
        /// The contents of an existing folder have changed,
        /// but the folder still exists and has not been renamed.
        /// <see cref="HChangeNotifyFlags.SHCNF_IDLIST"/> or
        /// <see cref="HChangeNotifyFlags.SHCNF_PATH"/> must be specified in <i>uFlags</i>.
        /// <i>dwItem1</i> contains the folder that has changed.
        /// <i>dwItem2</i> is not used and should be <see langword="null"/>.
        /// If a folder has been created, deleted, or renamed, use SHCNE_MKDIR, SHCNE_RMDIR, or
        /// SHCNE_RENAMEFOLDER, respectively, instead.
        /// </summary>
        SHCNE_UPDATEDIR = 0x00001000,

        /// <summary>
        /// An image in the system image list has changed.
        /// <see cref="HChangeNotifyFlags.SHCNF_DWORD"/> must be specified in <i>uFlags</i>.
        /// </summary>
        SHCNE_UPDATEIMAGE = 0x00008000,

    }
    #endregion // enum HChangeNotifyEventID

    #region enum HChangeNotifyFlags
    /// <summary>
    /// Flags that indicate the meaning of the <i>dwItem1</i> and <i>dwItem2</i> parameters.
    /// The uFlags parameter must be one of the following values.
    /// </summary>
    [Flags]
    enum HChangeNotifyFlags {
        /// <summary>
        /// The <i>dwItem1</i> and <i>dwItem2</i> parameters are DWORD values.
        /// </summary>
        SHCNF_DWORD = 0x0003,
        /// <summary>
        /// <i>dwItem1</i> and <i>dwItem2</i> are the addresses of ITEMIDLIST structures that
        /// represent the item(s) affected by the change.
        /// Each ITEMIDLIST must be relative to the desktop folder.
        /// </summary>
        SHCNF_IDLIST = 0x0000,
        /// <summary>
        /// <i>dwItem1</i> and <i>dwItem2</i> are the addresses of null-terminated strings of
        /// maximum length MAX_PATH that contain the full path names
        /// of the items affected by the change.
        /// </summary>
        SHCNF_PATHA = 0x0001,
        /// <summary>
        /// <i>dwItem1</i> and <i>dwItem2</i> are the addresses of null-terminated strings of
        /// maximum length MAX_PATH that contain the full path names
        /// of the items affected by the change.
        /// </summary>
        SHCNF_PATHW = 0x0005,
        /// <summary>
        /// <i>dwItem1</i> and <i>dwItem2</i> are the addresses of null-terminated strings that
        /// represent the friendly names of the printer(s) affected by the change.
        /// </summary>
        SHCNF_PRINTERA = 0x0002,
        /// <summary>
        /// <i>dwItem1</i> and <i>dwItem2</i> are the addresses of null-terminated strings that
        /// represent the friendly names of the printer(s) affected by the change.
        /// </summary>
        SHCNF_PRINTERW = 0x0006,
        /// <summary>
        /// The function should not return until the notification
        /// has been delivered to all affected components.
        /// As this flag modifies other data-type flags, it cannot by used by itself.
        /// </summary>
        SHCNF_FLUSH = 0x1000,
        /// <summary>
        /// The function should begin delivering notifications to all affected components
        /// but should return as soon as the notification process has begun.
        /// As this flag modifies other data-type flags, it cannot by used by itself.
        /// </summary>
        SHCNF_FLUSHNOWAIT = 0x2000
    }
    #endregion // enum HChangeNotifyFlags

    public class FileAssociation {
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify (HChangeNotifyEventID wEventId,
                                           HChangeNotifyFlags uFlags,
                                           IntPtr dwItem1,
                                           IntPtr dwItem2);

        public readonly string Extension;
        public readonly string Id;
        public readonly string Description;
        public readonly string Icon;
        public readonly string Action;

        public FileAssociation (string extension, string id, string description, string icon, string action) {
            Extension = extension;
            Id = id;
            Description = description;
            Icon = icon;
            Action = action;
        }

        public bool IsAssociated {
            get {
                using (var root = Registry.CurrentUser.OpenSubKey("Software\\Classes")) {
                    bool keysExist = root.SubKeyExists(Extension) &&
                        root.SubKeyExists(Id);

                    if (!keysExist)
                        return false;

                    using (var key = root.OpenSubKey(Extension))
                        if (key.GetValue(null) as string != Id)
                            return false;

                    using (var key = root.OpenSubKey(Id)) {
                        if (key.GetValue(null) as string != Description)
                            return false;

                        using (var subkey = key.OpenSubKey("DefaultIcon"))
                            if (subkey.GetValue(null) as string != Icon)
                                return false;

                        using (var subkey = key.OpenSubKey("shell\\open\\command"))
                            if (subkey.GetValue(null) as string != Action)
                                return false;
                    }
                }

                return true;
            }

            set {
                using (var root = Registry.CurrentUser.OpenSubKey("Software\\Classes", true)) {
                    if (value) {
                        using (var key = root.OpenOrCreateSubKey(Id)) {
                            key.SetValue(null, Description);

                            using (var subkey = key.OpenOrCreateSubKey("DefaultIcon"))
                                subkey.SetValue(null, Icon);

                            using (var subkey = key.OpenOrCreateSubKey("shell\\open\\command"))
                                subkey.SetValue(null, Action);
                        }
                    }

                    using (var key = root.OpenOrCreateSubKey(Extension)) {
                        if (value) {
                            var oldAssociation = key.GetValue(null);
                            if ((oldAssociation as string != Id) && (oldAssociation != null))
                                key.SetValue("OldAssociation", oldAssociation);

                            key.SetValue(null, Id);
                        } else {
                            var oldAssociation = key.GetValue("OldAssociation");

                            if (key.GetValue(null) as string == Id) {
                                if (oldAssociation != null)
                                    key.SetValue(null, oldAssociation);
                                else
                                    root.DeleteSubKey(Extension);
                            }
                        }
                    }
                }

                SHChangeNotify(HChangeNotifyEventID.SHCNE_ASSOCCHANGED, HChangeNotifyFlags.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }

    public static class FileSize {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static unsafe extern char * StrFormatByteSize (
            long fileSize, 
            char * pBuffer, 
            int bufferSize
        );

        public static unsafe string Format (long sizeBytes) {
            char[] buffer = new char[1024];
            fixed (char* pBuffer = buffer) {
                if (StrFormatByteSize(Math.Abs(sizeBytes), pBuffer, 1023) == pBuffer) {
                    var result = new String(pBuffer).Replace("bytes", "B");
                    if (sizeBytes < 0)
                        return "-" + result;
                    else
                        return result;
                } else
                    return null;
            }
        }
    }

    public abstract class KeyedCollection2<TKey, TValue> :
        KeyedCollection<TKey, TValue>
        where TValue : class {

        public KeyedCollection2 ()
            : base() {
        }

        public KeyedCollection2 (IEqualityComparer<TKey> keyComparer)
            : base(keyComparer) {
        }

        public virtual IEnumerable<TKey> Keys {
            get {
                return base.Dictionary.Keys;
            }
        }

        public virtual IList<TValue> Values {
            get {
                return base.Items;
            }
        }

        public bool TryGetValue (TKey key, out TValue value) {
            if (base.Dictionary != null)
                return base.Dictionary.TryGetValue(key, out value);

            if (base.Contains(key)) {
                value = base[key];
                return true;
            }

            value = default(TValue);
            return false;
        }
    }

    public interface IProgressListener {
        string Status {
            set;
        }
        int? Progress {
            set;
        }
        int Maximum {
            set;
        }
    }

    public class CallbackProgressListener : IProgressListener {
        public Action<string> OnSetStatus = null;
        public Action<int?> OnSetProgress = null;
        public Action<int> OnSetMaximum = null;

        public string Status {
            set { 
                if (OnSetStatus != null)
                    OnSetStatus(value);
            }
        }

        public int? Progress {
            set {
                if (OnSetProgress != null)
                    OnSetProgress(value);
            }
        }

        public int Maximum {
            set {
                if (OnSetMaximum != null)
                    OnSetMaximum(value);
            }
        }

        protected void Dispose () {
        }
    }

    public class NameTable : KeyedCollection2<string, string> {
        public NameTable ()
            : base() {
        }

        public NameTable (IEqualityComparer<string> keyComparer)
            : base(keyComparer) {
        }

        protected override string GetKeyForItem (string item) {
            return item;
        }

        new public string Add (string item) {
            return this[item];
        }

        new public string this[string key] {
            get {
                string result;
                if (!TryGetValue(key, out result)) {
                    base.Add(key);
                    result = key;
                }

                return result;
            }
        }
    }

    public static class LineReaderRegexExtensions {
        public static bool IsMatch (this Regex regex, ref LineReader.Line line) {
            var result = regex.IsMatch(line.Buffer, line.Start);
            return result;
        }

        public static bool TryMatch (this Regex regex, ref LineReader.Line line, out Match match) {
            var result = regex.TryMatch(line.Buffer, line.Start, line.Length, out match);
            return result;
        }
    }

    public class LineReader {
        public struct Line {
            public readonly LineReader Reader;
            public readonly int Start, Length;

            internal Line (LineReader reader) {
                Reader = reader;
                Start = Length = 0;
            }

            internal Line (LineReader reader, int start, int length) {
                Reader = reader;
                Start = start;
                Length = length;
            }

            public bool StartsWith (string text) {
                if (Length < text.Length)
                    return false;

                return String.Compare(
                    Reader.Text, Start, 
                    text, 0, 
                    Math.Min(Length, text.Length), false
                ) == 0;
            }

            public bool Contains (string text) {
                if (Length < text.Length)
                    return false;

                return Reader.Text.IndexOf(
                    text, Start, Length
                ) >= 0;
            }

            public string Buffer {
                get {
                    return Reader.Text;
                }
            }

            public override string ToString () {
                if (Length <= 0)
                    return null;

                return Reader.Text.Substring(Start, Length);
            }
        }

        public readonly string Text;

        protected int _LinesRead = 0;
        protected int _Position = 0;

        public LineReader (string text) {
            if (text == null)
                throw new ArgumentNullException();

            Text = text;
        }

        public void Rewind (ref Line line) {
            if ((_Position <= line.Start) || (line.Length <= 0))
                throw new InvalidOperationException();

            _Position = line.Start;
            _LinesRead -= 1;
        }

        public bool ReadLine (out Line line) {
            bool scanningEol = false;
            int start = _Position;
            int end = -1;
            int length = Text.Length;

            while (_Position < length) {
                var current = Text[_Position];

                if ((current == '\r') || (current == '\n')) {
                    if (end < 0)
                        end = _Position;
                    scanningEol = true;
                } else if (scanningEol) {
                    line = new Line(this, start, end - start);
                    _LinesRead += 1;
                    return true;
                }

                _Position += 1;
            }

            if (end > 0) {
                line = new Line(this, start, end - start);
                _LinesRead += 1;
                return true;
            } else if (_Position > start) {
                line = new Line(this, start, length - start);
                _LinesRead += 1;
                return true;
            } else {
                line = new Line(this);
                return false;
            }
        }

        public int Length {
            get {
                return Text.Length;
            }
        }

        public int Position {
            get {
                return _Position;
            }
        }

        public int LinesRead {
            get {
                return _LinesRead;
            }
        }
    }
}
