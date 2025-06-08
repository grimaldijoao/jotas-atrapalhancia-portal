using System.Data.SQLite;

namespace AtrapalhanciaDatabase.Tables
{
    public class TwitchRelation : ISQLiteTable<TwitchRelation, long>
    {
        public static string GetCreateTableString()
        {
            return @"
                CREATE TABLE IF NOT EXISTS Twitch_Relation (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    access_token TEXT,
                    refresh_token TEXT,
                    channel_name TEXT NOT NULL
                );
            ";
        }

        public static TwitchRelation? GetInstance(long id) 
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand($"SELECT * FROM Twitch_Relation WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("id", id);

                using var reader = cmd.ExecuteReader();

                TwitchRelation? twitchRelation = null;

                if (reader.Read())
                {
                    twitchRelation = new TwitchRelation();

                    twitchRelation.Id = reader.GetInt32(reader.GetOrdinal("id"));
                    twitchRelation.ChannelName = reader.GetString(reader.GetOrdinal("channel_name"));
                    twitchRelation.AccessToken = reader.GetNullableString("access_token");
                    twitchRelation.RefreshToken = reader.GetNullableString("refresh_token");

                }

                reader.Close();
                
                return twitchRelation;

            });
        }

        public static TwitchRelation? Create(string channelName, string? accessToken = null, string? refreshToken = null)
        {
            var result = SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    INSERT OR REPLACE INTO Twitch_Relation (channel_name, access_token, refresh_token)
                    VALUES (@channel_name, @access_token, @refresh_token);
                    SELECT last_insert_rowid();
                ", conn);

                cmd.Parameters.AddWithValue("channel_name", channelName);
                cmd.Parameters.AddWithValue("access_token", (object?)accessToken ?? DBNull.Value);
                cmd.Parameters.AddWithValue("refresh_token", (object?)refreshToken ?? DBNull.Value);

                return cmd.ExecuteScalar();
            });


            if (result != null && long.TryParse(result.ToString(), out long id))
            {
                return GetInstance(id);
            }

            return null;
        }

        public int Id { get; set; }

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }

        public string ChannelName { get; set; } = null!;
    }
}
