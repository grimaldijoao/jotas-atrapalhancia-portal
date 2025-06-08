using AtrapalhanciaDatabase.Tables;
using System.Data.SQLite;

namespace AtrapalhanciaDatabase
{
    public static class SQLiteDataReaderExtensions
    {
        public static string? GetNullableString(this SQLiteDataReader reader, string column)
        {
            int index = reader.GetOrdinal(column);
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        public static int? GetNullableInt(this SQLiteDataReader reader, string column)
        {
            int index = reader.GetOrdinal(column);
            return reader.IsDBNull(index) ? null : reader.GetInt32(index);
        }
    }

    public class SQLite
    {
        private static bool initialized = false;

        //? Only one reader at a time and one writer at a time, this is a real file shared by a single machine, dont forget this.
        private static SQLiteConnection connection = new SQLiteConnection("Data Source=atrapalhancia.sqlite; Version=3; Cache=Shared;");

        public static void Initialize()
        {
            if (!File.Exists("atrapalhancia.sqlite"))
            {
                SQLiteConnection.CreateFile("atrapalhancia.sqlite");
            }

            connection.Open();

            using var pragmaCmd = new SQLiteCommand("PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 3000; PRAGMA foreign_keys = ON;", connection);
            pragmaCmd.ExecuteNonQuery();

            Migrate();

            initialized = true;
        }

        private static void Migrate()
        {
            using (var cmd = new SQLiteCommand(TwitchRelation.GetCreateTableString(), connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand(Broadcaster.GetCreateTableString(), connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static void WithConnection(Action<SQLiteConnection> action)
        {
            if (!initialized) throw new Exception("You need to run Initialize() first!");

            const int maxRetries = 10;
            const int delayMs = 100;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    action(connection);
                    return;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("database is locked") && attempt < maxRetries - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }

            throw new Exception("Database is locked after multiple retries.");
        }

        public static T WithConnection<T>(Func<SQLiteConnection, T> func)
        {
            if (!initialized) throw new Exception("You need to run Initialize() first!");

            const int maxRetries = 10;
            const int delayMs = 100;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    T result = func(connection);
                    return result;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("database is locked") && attempt < maxRetries - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }

            throw new Exception("Database is locked after multiple retries.");
        }
    }
}
