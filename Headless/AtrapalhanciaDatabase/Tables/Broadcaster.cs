using System.Data.SQLite;

namespace AtrapalhanciaDatabase.Tables
{
    public class Broadcaster: ISQLiteTable<Broadcaster, string>
    {
        public static string GetCreateTableString()
        {
            return @"
                CREATE TABLE IF NOT EXISTS Broadcaster (
                    id TEXT PRIMARY KEY,
                    twitch_relation_id INTEGER NOT NULL,
                    email TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    last_access TEXT,
                    FOREIGN KEY (twitch_relation_id) REFERENCES Twitch_Relation(id)
                        ON DELETE CASCADE
                        ON UPDATE CASCADE
                );
            ";
        }

        public static Broadcaster? GetInstance(string id)
        {
            return SQLite.WithConnection((conn) =>
            {
                using var cmd = new SQLiteCommand($"SELECT * FROM Broadcaster WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("id", id);

                using var reader = cmd.ExecuteReader();

                Broadcaster? broadcaster = null;
                int? twitchRelationId = null;

                if (reader.Read())
                {
                    twitchRelationId = reader.GetNullableInt("twitch_relation_id");
                    broadcaster = new Broadcaster();
                    
                    broadcaster.Id = reader.GetString(reader.GetOrdinal("id"));
                    broadcaster.Email = reader.GetString(reader.GetOrdinal("email"));
                    broadcaster.CreatedAt = reader.GetString(reader.GetOrdinal("created_at"));
                    broadcaster.LastAccess = reader.GetNullableString("last_access");
                }

                reader.Close();

                if(twitchRelationId != null && broadcaster != null)
                {
                    broadcaster.twitchRelationId = twitchRelationId;
                }

                return broadcaster;

            });
        }

        public static void Create(string id, int twitch_relation_id, string email)
        {
            SQLite.WithConnection(conn =>
            {
                using var cmd = new SQLiteCommand(@"
                    INSERT OR REPLACE INTO Broadcaster (
                        id,
                        twitch_relation_id,
                        email,
                        created_at,
                        last_access
                    ) VALUES (
                        @id,
                        @twitch_relation_id,
                        @email,
                        @created_at,
                        @last_access
                    );
                ", conn);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@twitch_relation_id", twitch_relation_id);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@created_at", DateTime.Now.ToString());
                cmd.Parameters.AddWithValue("@last_access", DateTime.Now.ToString());

                cmd.ExecuteNonQuery();
            });
        }

        public string Id { get; private set; } = null!;

        public string Email { get; private set; }

        public string CreatedAt { get; private set; }

        public string? LastAccess { get; private set; }


        private int _twitchRelationId;
        private int? twitchRelationId { 
            get
            {
                return _twitchRelationId;
            }
            set
            {
                if(value != null)
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
