/*
Pumpkin Moon
------------------------
Scarecrow = 1pt
Splinterling = 2pt
Hellhound = 4pt
Poltergeist = 8pt
Headless Horseman = 25pt
Mourning Wood = 75pt
Pumpking = 150pt

Wave 1 = 25pt
Wave 2 = 40pt
Wave 3 = 50pt
Wave 4 = 80pt
Wave 5 = 100pt
Wave 6 = 160pt
Wave 7 = 180pt
Wave 8 = 200pt
Wave 9 = 250pt
Wave10 = 300pt
Wave11 = 375pt
Wave12 = 450pt
Wave13 = 525pt
Wave14 = 675pt
Wave15

Remember:
Main.npcName[int]

Frost Moon
Zombie Elf = 1pt
Gingerbread Man = 1pt
Present Mimic = 10pt
Elf Archer = 2pt
Nutcracker = 4pt
Nutcracker = 4pt
Elf Copter = 6pt
Krampus = 8pt
Flocko = 4pt
Yeti = 16pt
Everscream = 40pt
Santa-NK1 = 80pt
Ice Queen = 120pt

Wave 1 = 25p
Wave 2 = 40p
Wave 3 = 50p
Wave 4 = 80p
Wave 5 = 100p
Wave 6 = 160p
Wave 7 = 180p
Wave 8 = 200p
Wave 9 = 250p
Wave10 = 300p
Wave11 = 375p
Wave12 = 450p
Wave13 = 525p
Wave14 = 675p
Wave15 = 850p
Wave16 = 1025p
Wave17 = 1325p
Wave18 = 1550p
Wave19 = 2000p
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;

namespace Statistics
{
    public enum BossType
    {
        GoblinInvasion = -1,
        TheSnowLegion = -2,
        ThePirates = -3,
        PumpkinMoon = -4,
        FrostMoon = -5,
        EyeOfCthulu = 4,
        EaterOfWorlds = 13,
        Skeletron = 35,
        KingSlime = 50,
        WallOfFlesh = 113,
        Retinazer = 125,
        Spazmatism = 126,
        SkeletronPrime = 127,
        PrimeCannon = 128,
        TheDestroyer = 134,
        QueenBee = 222,
        Golem = 245,
        Plantera = 262,
        BrainOfCthulu = 266
    }

    public class BossInvasion
    {
        public bool Active { get; set; }
        public bool NeedsDeactivate { get; set; }
        public bool Invasion { get; set; }
        public int Type { get; set; }
        public string Name { get; set; }
        public DateTime EventStart { get; set; }
        public DateTime EventEnd { get; set; }
        public List<Player> Players { get; set; }
        public int InvasionStartSize { get; set; }
        public int LastPlayerHit { get; set; }
        public int LastHit { get; set; }

        public BossInvasion()
        {
            Active = true;
            Players = new List<Player>();
            EventStart = DateTime.Now;
        }

        public BossInvasion(int type) : this()
        {
            Type = type;
            Name = Main.npcName[type];// Enum.GetName(typeof(BossType), type);
            if (Type < 0)
                Invasion = true;

            if (Type == -1)
                Type = 1;
            if (Type == -2)
                Type = 2;
            if (Type == -3)
                Type = 3;
        }

        public BossInvasion(int type, string name) : this()
        {
            Type = type;
            Name = name;
            if (Type < 0)
                Invasion = true;

            if (Type == -1)
                Type = 1;
            if (Type == -2)
                Type = 2;
            if (Type == -3)
                Type = 3;
        }

        public void Deactivate()
        {
            NeedsDeactivate = true;
        }

        public void End()
        {
            EventEnd = DateTime.Now;
            Active = false;
            foreach (Player player in Players)
            {
                TShock.Players[player.Index].SendMessage(string.Format("{0} recording available. Type /{1} to view stats.", Invasion ? "Event" : "Battle", Invasion ? "battle" : "boss"), Color.LightCyan);
            }
            //TSPlayer.All.SendMessage(string.Format("{0} recording available. Type /{1} to view stats.", Invasion ? "Event" : "Battle", Invasion ? "battle" : "boss"), Color.LightCyan);
        }

        public void AddDamage(Player player, int damage)
        {
            Player plr = Players.Where(p => p.Index == player.Index).FirstOrDefault();
            if (plr == null)
            {
                plr = new Player(player.Index, player.Name);
                plr.DamageGiven += (uint)damage;
                Players.Add(plr);
                return;
            }
            
            plr.DamageGiven += (uint)damage;
            LastHit = damage;
            LastPlayerHit = player.Index;
        }

        public string ReportBattle()
        {
            if (Active)
                return "Encounter is still active.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("Encounter: {0}", Name));
            long total = Players.Sum(p => p.DamageGiven);
            double seconds = (EventEnd - EventStart).TotalSeconds;
            if (seconds <= 0)
                seconds = 1;
            foreach (Player player in Players.OrderByDescending(p => p.DamageGiven))
            {
                sb.AppendLine(string.Format("{0}: {1:n0} ({2:n2}%) - {3:n2}dps", player.Name, player.DamageGiven, player.DamageGiven * 100.0 / total, player.DamageGiven / seconds));
            }

            return sb.ToString();
        }

        public void ReportBattle(TSPlayer player)
        {
            if (Active)
                EventEnd = DateTime.Now;

            player.SendMessage(string.Format("Encounter: {0} - {1}", Name, Utils.FormatTime(EventEnd - EventStart)), Color.LightGreen);
            long total = Players.Sum(p => p.DamageGiven);
            double seconds = (EventEnd - EventStart).TotalSeconds;
            if (seconds <= 0)
                seconds = 1;
            foreach (Player plr in Players.OrderByDescending(p => p.DamageGiven))
            {
                player.SendMessage(string.Format("{0}: {1:n0} ({2:n2}%) - {3:n2}dps", plr.Name, plr.DamageGiven, plr.DamageGiven * 100.0 / total, plr.DamageGiven / seconds), Color.LightGreen);
            }
            if(LastHit > 0)
                player.SendMessage(string.Format("Last hit: {0} for {1:n0} damage.", Main.player[LastPlayerHit].name, LastHit), Color.Green);
        }
    }
}
