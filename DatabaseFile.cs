using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Squared.Task.Data;
using Squared.Task;
using System.Data;
using System.Text.RegularExpressions;
using Squared.Util.RegexExtensions;
using System.Data.SQLite;
using System.IO;

namespace HeapProfiler {
    public class DatabaseSchema {
        public static Regex VersionRegex = new Regex(
            @"pragma\s*(.*?)\.user_version\s*=\s*(?'version'[0-9]*)\s*;",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
        );

        public readonly int Version;
        public readonly string SQL;

        public DatabaseSchema (string sql) {
            SQL = sql;
            Version = 0;

            Match m;
            if (VersionRegex.TryMatch(sql, out m))
                Version = int.Parse(m.Groups["version"].Value);
        }
    }

    public class DatabaseFile : ConnectionWrapper {
        protected string _Filename;

        protected DatabaseFile (
            TaskScheduler scheduler, IDbConnection connection
        ) : base (scheduler, connection) {
        }

        protected static string BuildConnectionString () {
            return String.Format("Data Source=:MEMORY:");
        }

        protected static Future<SQLiteConnection> OpenConnection () {
            var cstring = BuildConnectionString();

            return Future.RunInThread(() => {
                var c = new SQLiteConnection(cstring);
                c.Open();
                return c;
            });
        }

        protected Future<int> GetSchemaVersion () {
            return ExecuteScalar<int>("PRAGMA user_version");
        }

        protected static IEnumerator<object> InitializeConnection (ConnectionWrapper cw) {
            yield return cw.ExecuteSQL("PRAGMA synchronous=0");
            yield return cw.ExecuteSQL("PRAGMA read_uncommitted=1");
            yield return cw.ExecuteSQL("PRAGMA locking_mode=NORMAL");
        }

        public static IEnumerator<object> CreateNew (
            TaskScheduler scheduler, DatabaseSchema schema, string filename
        ) {
            if (File.Exists(filename))
                File.Delete(filename);

            var fConnection = OpenConnection();
            yield return fConnection;

            var result = new DatabaseFile(scheduler, fConnection.Result);

            yield return InitializeConnection(result);

            result._Filename = filename;
            yield return result.Attach(filename, "data");

            yield return result.ExecuteSQL(schema.SQL);

            yield return new Result(result);
        }

        public static IEnumerator<object> OpenExisting (
            TaskScheduler scheduler, DatabaseSchema schema, string filename
        ) {
            if (!File.Exists(filename))
                throw new FileNotFoundException("File does not exist", filename);

            var fConnection = OpenConnection();
            yield return fConnection;

            var result = new DatabaseFile(scheduler, fConnection.Result);

            yield return InitializeConnection(result);

            result._Filename = filename;
            yield return result.Attach(filename, "data");

            var fVersion = result.GetSchemaVersion();
            yield return fVersion;

            if (fVersion.Result < schema.Version)
                throw new NotImplementedException("Schema upgrade required");

            yield return new Result(result);
        }

        protected IEnumerator<object> Attach (string databaseFilename, string databaseName) {
            yield return ExecuteSQL(
                "ATTACH DATABASE ? AS " + databaseName, databaseFilename
            );
        }

        protected IEnumerator<object> Detach (string databaseName) {
            yield return ExecuteSQL(
                "DETACH DATABASE " + databaseName
            );
        }

        public string Filename {
            get {
                return _Filename;
            }
        }

        public IEnumerator<object> Move (string targetFilename) {
            if (targetFilename == _Filename)
                yield break;

            yield return Detach("data");

            File.Move(_Filename, targetFilename);
            _Filename = targetFilename;

            yield return Attach(targetFilename, "data");
        }
    }
}
