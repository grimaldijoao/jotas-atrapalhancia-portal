using AtrapalhanciaDatabase.Tables;
using System.Data.SQLite;
using System.Reflection;

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

            Migrate([typeof(TwitchRelation), typeof(TwitchReward), typeof(Broadcaster)]);

            initialized = true;
        }

        private static void Migrate(Type[] tableTypes)
        {
            foreach (var tableType in tableTypes)
            {
                MethodInfo? method = tableType.GetMethod("GetCreateTableString", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    throw new ArgumentException($"Type {tableType.Name} must implement IMyInterface.");
                }

                using (var cmd = new SQLiteCommand(method.Invoke(null, null) as string, connection))
                {
                    cmd.ExecuteNonQuery();
                }
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
