using System.Data.SQLite;

namespace AtrapalhanciaDatabase.Tables
{
    public class TwitchReward : ISQLiteTable<TwitchReward, string>
    {
        public static string GetCreateTableString()
        {
            return @"
                CREATE TABLE IF NOT EXISTS Twitch_Reward (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    twitch_relation_id INTEGER NOT NULL,
                    FOREIGN KEY (twitch_relation_id) REFERENCES Twitch_Relation(id)
                        ON DELETE CASCADE
                        ON UPDATE CASCADE
                );
            ";
        }

        public static TwitchReward? GetInstance(string id) 
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand($"SELECT * FROM Twitch_Reward WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("id", id);

                using var reader = cmd.ExecuteReader();

                TwitchReward? twitchReward = null;
                int? twitchRelationId = null;

                if (reader.Read())
                {
                    twitchReward = new TwitchReward();
                    twitchRelationId = reader.GetNullableInt("twitch_relation_id");

                    twitchReward.Id = reader.GetString(reader.GetOrdinal("id"));
                    twitchReward.Name = reader.GetString(reader.GetOrdinal("broadcaster_id"));

                }

                reader.Close();

                if (twitchRelationId != null && twitchReward != null)
                {
                    twitchReward.twitchRelationId = twitchRelationId;
                }

                return twitchReward;

            });
        }

        public static TwitchReward? Create(string reward_id, string name, long twitch_relation_id)
        {
            var result = SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    INSERT OR REPLACE INTO Twitch_Reward (id, name, twitch_relation_id)
                    VALUES (@id, @name, @twitch_relation_id);
                    SELECT last_insert_rowid();
                ", conn);

                cmd.Parameters.AddWithValue("id", reward_id);
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("twitch_relation_id", twitch_relation_id);

                return cmd.ExecuteScalar();
            });


            if (result != null)
            {
                return GetInstance(result.ToString()!);
            }

            return null;
        }

        public static List<TwitchReward> GetRewardsByChannelName(string channelName)
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    SELECT * FROM Twitch_Reward
                    WHERE twitch_relation_id IN (
                        SELECT id FROM Twitch_Relation WHERE channel_name = @channel_name
                    );
                ", conn);

                cmd.Parameters.AddWithValue("channel_name", channelName);

                using var reader = cmd.ExecuteReader();

                List<TwitchReward> rewards = new List<TwitchReward>();

                while (reader.Read())
                {
                    var twitchReward = new TwitchReward();
                    twitchReward.Id = reader.GetString(reader.GetOrdinal("id"));
                    twitchReward.Name = reader.GetString(reader.GetOrdinal("name"));
                    rewards.Add(twitchReward);
                }

                reader.Close();

                return rewards;
            });
        }

        public static void DeleteRewardsByChannelName(string channelName)
        {
            SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    DELETE FROM Twitch_Reward
                    WHERE twitch_relation_id IN (
                        SELECT id FROM Twitch_Relation WHERE channel_name = @channel_name
                    );
                ", conn);

                cmd.Parameters.AddWithValue("channel_name", channelName);
                cmd.ExecuteNonQuery();
            });
        }

        public void Delete()
        {
            SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand(@"
                    DELETE FROM Twitch_Reward
                    WHERE id = @id;
                ", conn);

                cmd.Parameters.AddWithValue("id", Id);
                cmd.ExecuteNonQuery();
            });
        }

        public string Id { get; private set; }

        public string? Name { get; private set; }

        private int _twitchRelationId;
        private int? twitchRelationId
        {
            get
            {
                return _twitchRelationId;
            }
            set
            {
                if (value != null)
                {
                    _twitchRelationId = (int)value;
                    twitchRelation = TwitchRelation.GetInstance((int)value);
                }
            }
        }

        private TwitchRelation? twitchRelation;
        public TwitchRelation? TwitchRelation => twitchRelation;
    }
}
