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
                    broadcaster_id TEXT UNIQUE NOT NULL,
                    access_token TEXT,
                    refresh_token TEXT,
                    subscription_id TEXT,
                    channel_name TEXT UNIQUE NOT NULL
                );
            ";
        }

        public static TwitchRelation? GetInstance(string channel_name)
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand($"SELECT * FROM Twitch_Relation WHERE channel_name = @channel_name", conn);
                cmd.Parameters.AddWithValue("channel_name", channel_name);

                using var reader = cmd.ExecuteReader();

                TwitchRelation? twitchRelation = null;

                if (reader.Read())
                {
                    twitchRelation = new TwitchRelation();

                    twitchRelation.Id = reader.GetInt32(reader.GetOrdinal("id"));
                    twitchRelation.BroadcasterId = reader.GetString(reader.GetOrdinal("broadcaster_id"));
                    twitchRelation.ChannelName = reader.GetString(reader.GetOrdinal("channel_name"));
                    twitchRelation.AccessToken = reader.GetNullableString("access_token");
                    twitchRelation.RefreshToken = reader.GetNullableString("refresh_token");

                }

                reader.Close();

                return twitchRelation;

            });
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
                    twitchRelation.BroadcasterId = reader.GetString(reader.GetOrdinal("broadcaster_id"));
                    twitchRelation.ChannelName = reader.GetString(reader.GetOrdinal("channel_name"));
                    twitchRelation.AccessToken = reader.GetNullableString("access_token");
                    twitchRelation.SubscriptionId = reader.GetNullableString("subscription_id");
                    twitchRelation.RefreshToken = reader.GetNullableString("refresh_token");

                }

                reader.Close();
                
                return twitchRelation;

            });
        }

        public static TwitchRelation? Create(string broadcaster_id, string channelName, string? accessToken = null, string? refreshToken = null)
        {
            var result = SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    INSERT OR REPLACE INTO Twitch_Relation (broadcaster_id, channel_name, access_token, refresh_token)
                    VALUES (@broadcaster_id, @channel_name, @access_token, @refresh_token);
                    SELECT last_insert_rowid();
                ", conn);

                cmd.Parameters.AddWithValue("broadcaster_id", broadcaster_id);
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

        public static void UpdateSubscriptionId(string broadcasterId, string subscriptionId)
        {
            SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    UPDATE Twitch_Relation
                    SET subscription_id = @subscription_id
                    WHERE broadcaster_id = @broadcaster_id;
                ", conn);

                cmd.Parameters.AddWithValue("subscription_id", subscriptionId);
                cmd.Parameters.AddWithValue("broadcaster_id", broadcasterId);

                cmd.ExecuteNonQuery();
            });
        }

        public static string? GetSubscriptionId(string broadcasterId)
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    SELECT subscription_id
                    FROM Twitch_Relation
                    WHERE broadcaster_id = @broadcaster_id;
                ", conn);

                cmd.Parameters.AddWithValue("broadcaster_id", broadcasterId);

                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? result?.ToString() : null;
            });
        }

        public int Id { get; private set; }

        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public string? SubscriptionId { get; private set; }

        public string BroadcasterId { get; private set; } = null!;
        public string ChannelName { get; private set; } = null!;
    }
}
