using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace Statistics
{
    public class StatDB
    {
        private static IDbConnection db;
        private static string savepath = Path.Combine(TShock.SavePath, "Statistics.sqlite");

        #region Setup Database
        public static void SetupDB()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        TShock.Config.MySqlDbName,
                        TShock.Config.MySqlUsername,
                        TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string sql = savepath;
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }
            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ?
                (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            sqlcreator.EnsureExists(new SqlTable("StatPlayers",
                new SqlColumn("Name", MySqlDbType.Text),
                new SqlColumn("Healed", MySqlDbType.Int32),
                new SqlColumn("TimesHealed", MySqlDbType.Int32),
                new SqlColumn("ManaRecovered", MySqlDbType.Int32),
                new SqlColumn("TimesManaRecovered", MySqlDbType.Int32),
                new SqlColumn("TimesDealtDamage", MySqlDbType.Int32),
                new SqlColumn("DamageTaken", MySqlDbType.Int32),
                new SqlColumn("TimesDamaged", MySqlDbType.Int32),
                new SqlColumn("DamageGiven", MySqlDbType.Int32),
                new SqlColumn("MaxDamage", MySqlDbType.Int32),
                new SqlColumn("MaxReceived", MySqlDbType.Int32),
                new SqlColumn("CritsTaken", MySqlDbType.Int32),
                new SqlColumn("CritsGiven", MySqlDbType.Int32),
                new SqlColumn("Kills", MySqlDbType.Int32),
				new SqlColumn("Deaths", MySqlDbType.Int32),
				new SqlColumn("PvP_Deaths", MySqlDbType.Int32),
                new SqlColumn("Playtime", MySqlDbType.Int32)));
        }
        #endregion

        #region Add Player
        public static bool AddPlayer(Player Player)
        {
            String query = "INSERT INTO StatPlayers (Name, Healed, TimesHealed, ManaRecovered, TimesManaRecovered, TimesDealtDamage, " +
            "DamageTaken, TimesDamaged, DamageGiven, MaxDamage, MaxReceived, CritsTaken, CritsGiven, Kills, Deaths, PvP_Deaths, Playtime) " +
			"VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16);";

			if (db.Query(query, Player.Name, Player.Healed, Player.TimesHealed, Player.ManaRecovered, Player.TimesManaRecovered,
				Player.TimesDealtDamage, Player.DamageTaken, Player.TimesDamaged, Player.DamageGiven, Player.MaxDamage, Player.MaxReceived,
				Player.CritsTaken, Player.CritsGiven, Player.Kills, Player.Deaths.Mob, Player.Deaths.PVP, Player.Time.Playing) == 1)
				return true;
			else
				return false;
        }
        #endregion

        #region PlayerExists?
        public static bool PlayerExists(string Name)
        {
            String query = "SELECT Name FROM StatPlayers WHERE Name=@0;";
            List<string> usr = new List<string>();
            using (var reader = db.QueryReader(query, Name))
            {
                while (reader.Read())
                {
                    usr.Add(reader.Get<string>("Name"));
					if (usr.Count == 1)
						return true;
					else
						return false;
                }
            }
			return false;
        }
        #endregion

        #region Pull Player
        public static Player PullPlayer(string Name)
        {
            String query = "SELECT Healed, TimesHealed, ManaRecovered, TimesManaRecovered, TimesDealtDamage, DamageTaken, TimesDamaged, " +
                "DamageGiven, MaxDamage, MaxReceived, CritsTaken, CritsGiven, Kills, Deaths, PvP_Deaths, Playtime FROM StatPlayers WHERE Name=@0;";
            Player player;

            try
            {
                using (var reader = db.QueryReader(query, Name))
                {
                    while (reader.Read())
                    {
						player = new Player(TShock.Players.First(p => p.Name == Name).Index, Name)
							{
								Healed = (uint)reader.Get<Int32>("Healed"),
								TimesHealed = (uint)reader.Get<Int32>("TimesHealed"),
								ManaRecovered = (uint)reader.Get<Int32>("ManaRecovered"),
								TimesManaRecovered = (uint)reader.Get<Int32>("TimesManaRecovered"),
								TimesDealtDamage = (uint)reader.Get<Int32>("TimesDealtDamage"),
								DamageTaken = (uint)reader.Get<Int32>("DamageTaken"),
								TimesDamaged = (uint)reader.Get<Int32>("TimesDamaged"),
								DamageGiven = (uint)reader.Get<Int32>("DamageGiven"),
								MaxDamage = (Int16)reader.Get<Int32>("MaxDamage"),
								MaxReceived = (Int16)reader.Get<Int32>("MaxReceived"),
								CritsTaken = (uint)reader.Get<Int32>("CritsTaken"),
								CritsGiven = (uint)reader.Get<Int32>("CritsGiven"),
								Kills = (uint)reader.Get<Int32>("Kills"),
								Deaths = new Deaths() { Mob = reader.Get<Int32>("Deaths"), PVP = reader.Get<Int32>("PvP_Deaths") },
								Time = new Time() { Playing = reader.Get<Int32>("Playtime") }
							};
                        return player;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }
        #endregion

        #region Update | Save Player
        public static void UpdatePlayer(Player Player)
        {
            String query = "UPDATE StatPlayers SET Healed=@1, TimesHealed=@2, ManaRecovered=@3, TimesManaRecovered=@4, TimesDealtDamage=@5, "+
                "DamageTaken=@6, TimesDamaged=@7, DamageGiven=@8, MaxDamage=@9, MaxReceived=@10, CritsTaken=@11, CritsGiven=@12, " +
                "Kills=@13, Deaths=@14, PvP_Deaths=@15, Playtime=@16 WHERE Name=@0;";

            if (db.Query(query, Player.Name, (int)Player.Healed, (int)Player.TimesHealed, (int)Player.ManaRecovered, (int)Player.TimesManaRecovered,
                (int)Player.TimesDealtDamage, (int)Player.DamageTaken, (int)Player.TimesDamaged, (int)Player.DamageGiven, (int)Player.MaxDamage,
                (int)Player.MaxReceived, (int)Player.CritsTaken, (int)Player.CritsGiven, (int)Player.Kills, Player.Deaths.Mob,
				Player.Deaths.PVP, Player.Time.Playing) != 1)
            {
                Log.ConsoleError("[Statistics] Updating a Player's DB Info has failed!");
                return;
            }
            return;
        }
        #endregion
    }
}
