using System;
using Terraria;
using TerrariaApi;
using TerrariaApi.Server;
using TShockAPI;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Timers;

/*
    TODO List:
 * Add permissions for commands
 * Store data in sql instead of hardcoded or in files
 * Fix bug with double reports on boss death
    - Could be caused be incorrect damage calculation deeming boss "dead"
    - Or two NpcStrikes are fired at once
 * Breakdown stats better and allow grouping such as /stat offense
 * Levels & Bonuses
    - Gain exp per kill and level up based on curve
    - Level up bonuse ideas: Choice of +1 def, +1 dmg or +1% dmg, +1 heal on hit, etc...
*/

namespace Statistics
{
    [ApiVersion(1, 15)]
    public class Statistics : TerrariaPlugin
    {
        Timer dayTimer = new Timer(1000);
        byte subCount = 0;
        byte subInterval = 10;

        public int[] bossIDs = new int[] { 4, 5, 13, 14, 15, 35, 36, 50, 113, 114, 115, 116, 117, 118, 119, 125, 126, 127, 128, 129, 130, 131, 134, 135, 136, 139, 222, 245, 246, 247, 248, 249, 262, 263, 264, 265, 266, 267 };
        public int[] bossParents = new int[] { 4, 13, 14, 15, 35, 50, 113, 125, 126, 127, 134, 135, 136, 222, 245, 262, 266 };

        public int[] invasionIDs = new int[] { 
            26, 27, 28, 29, 111, 143, 144, 145, 158, 162, 166, 212, 213, 214, 215, 216,
            251, 252, 253, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 325, 326,
            327, 329, 330, 338,339,340,341, 342, 343, 344, 345, 346, 347, 348, 349, 350, 351, 352
        };
        public int[] pumpkinLevels = new int[] { 25, 40, 50, 80, 100, 160, 180, 200, 250, 300, 375, 450, 525, 675 };
        public int[] frostLevels = new int[] { 25, 40, 50, 80, 100, 160, 180, 200, 250, 300, 375, 450, 525, 675, 850, 1025, 1325, 1550, 2000 };
        public int[] nightBossIDs = new int[] { 4, 35, 125, 126, 127, 128, 134 };

        List<BossInvasion> bosses = new List<BossInvasion>();
        List<BossInvasion> invasions = new List<BossInvasion>();
        List<Player> players = new List<Player>();

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override string Name
        {
            get { return "Statistics"; }
        }

        public override string Author
        {
            get { return "Meth"; }
        }

        public override string Description
        {
            get { return "Gives statitistics on players and DPS meters for boss/invasions."; }
        }

        public Statistics(Main game)
            : base(game)
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dayTimer.Stop();
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
        }

        public override void Initialize()
        {
            dayTimer.Elapsed += new ElapsedEventHandler(DayTimerTick);
            dayTimer.Start();
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        void DayTimerTick(object source, ElapsedEventArgs e)
        {
            // Continually check for active invasions that should be complete due to day time
            if (Main.invasionType == 0)
            {
                var activeInvasions = invasions.Where(i => i.Active).ToList();
                foreach (var invasion in activeInvasions)
                {
                    // If frost or pumpkin moon, end event recording if it's daytime
                    if (invasion.Type == -4 || invasion.Type == -5)
                    {
                        if (Main.dayTime)
                            invasion.End();
                    }
                    else // All other invasions should end if Main.invasionType is 0
                        invasion.End();
                }
            }
            else
            {
                // If somehow the invasion message was missed, start a new one
                // Likely that someone used /invade instead of item. *Or game naturally spawned?
                var activeInvasion = invasions.Where(i => i.Type == Main.invasionType).FirstOrDefault();
                if (activeInvasion == null || !activeInvasion.Active)
                    invasions.Add(new BossInvasion(-Main.invasionType));

                // Grab the invasion size the first time; OPTIONAL: Calculate invasion size like the game?
                if (activeInvasion.InvasionStartSize == 0)
                    activeInvasion.InvasionStartSize = Main.invasionSize;
            }

            // Boss check
            var activeBosses = bosses.Where(b => b.Active);
            foreach (var boss in activeBosses)
            {
                // The boss has been removed from npc slots. This could be despawn or running too far away?
                if (Main.npc.Where(n => n.active && n.netID == boss.Type).FirstOrDefault() == null)
                {
                    if (boss.NeedsDeactivate)
                        boss.End();
                    else
                        boss.Deactivate();
                }
                // Check nocturnal bosses
                if (nightBossIDs.Contains(boss.Type) && Main.dayTime)
                    boss.End();
            }

            // Send subscription information for events to subscribed players
            if (++subCount >= subInterval)
            {
                subCount = 0;
                var activeInvasion = invasions.Where(i => i.Active).FirstOrDefault();
                var subbedPlayers = players.Where(p => p.EventSubscribed).ToList();
                if (activeInvasion != null && subbedPlayers.Count > 0)
                {
                    string report = ReportProgress(activeInvasion);
                    foreach (var sub in subbedPlayers)
                    {
                        TShock.Players[sub.Index].SendMessage(report, Color.Purple);
                    }
                }
            }
        }

        void OnServerJoin(JoinEventArgs e)
        {
            try
            {
                // Get players by name to keep stats. Different slots will be assigned when rejoining.
                Player player = players.Where(p => p.Name == Main.player[e.Who].name).FirstOrDefault();
                if (player == null)
                {
                    players.Add(new Player(e.Who, Main.player[e.Who].name));
                }
                else
                {
                    player.Index = e.Who;
                }
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.ToString());
            }
        }

        void OnChat(ServerChatEventArgs e)
        {
            string text = e.Text;
            if (e.Text.StartsWith("/"))
            {
                var sender = TShock.Players[e.Who];
                //var sender = players.Where(p => p.Index == e.Who).FirstOrDefault();
                string[] arr = e.Text.Split(' ');
                switch (arr[0])
                {
                    case "/stat":
                        Player player;
                        if (arr.Length > 1)
                        {
                            player = players.Where(p => p.Name.ToLower().Contains(arr[1].ToLower())).FirstOrDefault();
                            if (player == null)
                            {
                                sender.SendWarningMessage("No player found with name like: " + arr[1]);
                                e.Handled = true;
                                break;
                            }
                        }
                        else
                            player = players.Where(p => p.Index == sender.Index).FirstOrDefault();

                        if (arr.Length > 2)
                        {
                            switch (arr[2].ToLower())
                            {
                                case "crit":
                                case "crits":
                                case "critical":
                                    sender.SendMessage(string.Format("{0}'s Crit Chance: Magic: {1}% Melee: {2}% Ranged: {3}%", player.Name, Main.player[player.Index].magicCrit, Main.player[player.Index].meleeCrit, Main.player[player.Index].rangedCrit), Color.Green);
                                    break;
                            }
                        }
                        else
                        {
                            sender.SendMessage(string.Format("{0}'s stats - Kills: {1:n0} Damage: {2:n0}(Max {3:n0}) Crits: {4:n0}({5:n2}%)",
                                player.Name, player.Kills, player.DamageGiven, player.MaxDamage, player.CritsGiven, player.CritPercent), Color.Green);
                            sender.SendMessage(string.Format("Hurt: {0:n0}(Max {1:n0}) Crits: {2:n0} Healed: {3:n0}({4:n0}) Mana: {5:n0}({6:n0})",
                                player.DamageTaken, player.MaxReceived, player.CritsTaken, player.Healed, player.TimesHealed,
                                player.ManaRecovered, player.TimesManaRecovered), Color.Green);
                        }
                        e.Handled = true;
                        break;
                    case "/battle":
                    case "/boss":
                        int index = 0;
                        if (arr.Length > 1)
                        {
                            if (!Int32.TryParse(arr[1], out index))
                            {
                                sender.SendErrorMessage("Pass in a numeric value for previous battles.");
                                e.Handled = true;
                                break;
                            }
                        }

                        if (arr[0] == "/boss")
                        {
                            if (index > bosses.Count - 1)
                            {
                                sender.SendErrorMessage(string.Format("Out of boss records index. Number recorded is {0}.", bosses.Count));
                                e.Handled = true;
                                break;
                            }
                            bosses[bosses.Count - 1 - index].ReportBattle(sender);
                        }
                        else
                        {
                            if (index > invasions.Count - 1)
                            {
                                sender.SendErrorMessage(string.Format("Out of event records index. Number recorded is {0}.", invasions.Count));
                                e.Handled = true;
                                break;
                            }
                            invasions[invasions.Count - 1 - index].ReportBattle(sender);
                        }
                        //sender.SendMessage(bosses[index].ReportBattle(), Color.LightBlue);
                        e.Handled = true;
                        break;
                    case "/bosses":
                        if (arr.Length > 1)
                        {
                            if (arr[1] == "clear")
                            {
                                bosses = new List<BossInvasion>();
                                break;
                            }
                        }
                        else
                        {
                            sender.SendInfoMessage("Boss encounters on record: {0:n0}", bosses.Count);
                        }
                        e.Handled = true;
                        break;
                    case "/invasion":
                    case "/event":
                        if (arr.Length > 1)
                            sender.SendMessage(string.Format("Invasion: {0} Size: {1} Wave: {2} Points: {3}", Main.invasionType, Main.invasionSize, NPC.waveCount, NPC.waveKills), Color.LightGreen);
                        else
                        {
                            var invasion = invasions.Where(i => i.Type == Main.invasionType).FirstOrDefault();
                            if (Main.pumpkinMoon)
                                invasion = invasions.Where(i => i.Type == -4).FirstOrDefault();
                            if (Main.snowMoon)
                                invasion = invasions.Where(i => i.Type == -5).FirstOrDefault();

                            if (invasion != null)
                                sender.SendMessage(ReportProgress(invasion), Color.LightGreen);
                        }
                        e.Handled = true;
                        break;
                    case "/events":
                        if (arr.Length > 1)
                        {
                            if (arr[1] == "clear")
                            {
                                invasions = new List<BossInvasion>();
                                break;
                            }
                        }
                        else
                        {
                            sender.SendInfoMessage("Event encounters on record: {0:n0}", invasions.Count);
                        }
                        e.Handled = true;
                        break;
                    case "/wave":
                        if (arr.Length > 1)
                        {
                            Int32 w = Int32.Parse(arr[1]);
                            NPC.waveCount = w;
                            sender.SendInfoMessage("Wave set to " + w);
                        }
                        else
                            sender.SendMessage(string.Format("Wave {0}: {1}", NPC.waveCount, NPC.waveKills), Color.LightGreen);
                        e.Handled = true;
                        break;
                    case "/unsub":
                    case "/unsubscribe":
                        player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                        if (arr.Length > 1)
                        {
                            switch (arr[1])
                            {
                                case "battle":
                                case "event":
                                case "invasion":
                                    player.EventSubscribed = false;
                                    break;
                            }
                        }
                        else
                            player.EventSubscribed = false;
                        sender.SendMessage("You are no longer subscribed.", Color.Lavender);
                        e.Handled = true;
                        break;
                    case "/sub":
                    case "/subscribe":
                        player = players.Where(p => p.Index == sender.Index).FirstOrDefault();
                        if (arr.Length > 1)
                        {
                            Player plr = players.Where(p => p.Name.ToLower() == arr[1].ToLower()).FirstOrDefault();
                            if (plr != null)
                            {
                                sender.SendMessage(string.Format("Player is {0}.", plr.EventSubscribed ? "subscribed" : "not subscribed"), Color.White);
                            }
                            switch (arr[1])
                            {
                                case "battle":
                                case "event":
                                case "invasion":
                                    player.EventSubscribed = true;
                                    break;
                            }
                        }
                        else
                            player.EventSubscribed = true;
                        sender.SendMessage("You are now subscribed.", Color.Lavender);
                        e.Handled = true;
                        break;
                    case "/tliteral":
                        sender.SendInfoMessage(Main.time.ToString());
                        e.Handled = true;
                        break;
                    case "/ttm":
                        sender.SendInfoMessage("TimeUntilMorning: " + TimeTilMorning());
                        e.Handled = true;
                        break;
                    case "/ttn":
                        sender.SendInfoMessage("TimeUntilNight: " + TimeTilNight());
                        e.Handled = true;
                        break;
                }
            }
        }

        void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                int plr = e.Msg.whoAmI;
                using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    Player player = players.Where(p => p.Index == e.Msg.whoAmI).FirstOrDefault();
                    switch (e.MsgID)
                    {
                        case PacketTypes.PlayerDamage:
                            DoPlayerDamage(reader, player);
                            break;
                        case PacketTypes.EffectHeal:
                            DoPlayerHeal(reader, player);
                            break;
                        case PacketTypes.EffectMana:
                            DoPlayerMana(reader, player);
                            break;
                        case PacketTypes.NpcStrike:
                            DoNpcStrike(reader, player);
                            break;
                        case PacketTypes.SpawnBossorInvasion:
                            DoSpawnBoss(reader);
                            break;
                    }
                }
            }
        }

        private void DoSpawnBoss(BinaryReader reader)
        {
            int playerInt = reader.ReadInt32();
            int type = reader.ReadInt32();

            int t = type;
            if (type < 0)
            {
                if (type == -1)
                    t = 1;
                if (type == -2)
                    t = 2;
                if (type == -3)
                    t = 3;
                BossInvasion _event = invasions.Where(b => b.Type == t).FirstOrDefault();
                if (_event == null || !_event.Active)
                    _event = new BossInvasion(type);

                invasions.Add(_event);
            }
            else
            {
                BossInvasion _boss = bosses.Where(b => b.Type == type).FirstOrDefault();
                if (_boss == null || !_boss.Active)
                    _boss = new BossInvasion(type);

                bosses.Add(_boss);
            }
        }

        private void DoNpcStrike(BinaryReader reader, Player player)
        {
            Int16 npcID = reader.ReadInt16();
            Int16 dmg = reader.ReadInt16();
            float knockback = reader.ReadSingle();
            byte direction = reader.ReadByte();
            bool critical = reader.ReadBoolean();
            NPC npc = Main.npc[npcID];
            double calcDmg = dmg;

            if (player != null)
            {
                player.TimesDealtDamage++;

                if (critical)
                    player.CritsGiven++;

                calcDmg = Main.CalculateDamage(dmg, npc.ichor ? npc.defense - 20 : npc.defense);
                player.DamageGiven += (uint)calcDmg;
                if (calcDmg > player.MaxDamage)
                    player.MaxDamage = (short)calcDmg;

                /* // Old method, use Main.CalculateDamage
                if (dmg > -1)
                {
                    idmg = (short)((dmg - npc.defense / 2) * (critical ? 2 : 1));
                    if (idmg <= 0)
                        idmg = 1;
                    player.DamageGiven += (uint)idmg;
                    if (idmg > player.MaxDamage)
                        player.MaxDamage = idmg;
                }
                */

                // Does this ever happen?
                //if (dmg == -1)
                //TSPlayer.All.SendInfoMessage("Kill!");

                if (npc.life - calcDmg <= 0)
                    player.Kills++;
            }

            if (npc.boss || bossIDs.Contains(npc.netID))
            {
                // Make sure to count children of boss for damage
                // Especially since some bosses like Golem and Destroyer are multiple parts
                int type = npc.netID;
                if (type == 5) // Eye of Cthulu
                    type = 4;
                if (type == 14 || type == 15) // Eater of Worlds
                    type = 13;
                if (type == 36) // Skeletron
                    type = 35;
                if (type >= 114 && type <= 119) // Wall of Flesh
                    type = 113;
                //if (type == 126) // The Twins // Separate entities, do not group together
                //    type = 125;
                if (type >= 128 && type <= 131) // Skeletron Prime
                    type = 127;
                if (type == 135 || type == 136 || type == 139) // The Destroyer
                    type = 134;
                if (type >= 246 && type <= 249) // Golem
                    type = 245;
                if (type >= 263 && type <= 265) // Plantera
                    type = 262;
                if (type == 267) // Brain of Cthulu
                    type = 266;

                BossInvasion boss = bosses.Where(b => b.Type == type && b.Active).FirstOrDefault();
                if (boss == null)
                {
                    // Disclude Probes and leeches as they can be left over after a boss fight
                    if ((new int[] { 139, 117, 118, 119 }).Contains(npc.netID))
                        return;
                    boss = new BossInvasion(type);
                    bosses.Add(boss);
                }

                // Update start time to first hit of encounter
                if (boss.Players.Count == 0)
                    boss.EventStart = DateTime.Now;

                boss.AddDamage(player, (int)calcDmg);
                if (boss.Active && npc.life - calcDmg <= 0 && bossParents.Contains(npc.netID))
                {
                    // Since Eater of Worlds is actually 50 entities, must check on each kill that no other pieces are alive
                    // Also, any piece of the Eater of Worlds must have a tail, therefore the minimum number of pieces are 2.
                    if (npc.netID >= 13 && npc.netID <= 15 && Main.npc.Where(n => n.active && (new int[] { 13, 14, 15 }).Contains(n.netID)).Count() > 2)
                        return;

                    boss.KilledByPlayer = player.Name;
                    boss.LastHit = (int)calcDmg;
                    boss.End();
                    //TSPlayer.All.SendMessage(string.Format("{0} killed {1} with {2} damage.", player.Name, boss.Name, idmg), Color.Purple);
                }
            }

            if (invasionIDs.Contains(npc.netID))
            {
                BossInvasion _event;
                if (Main.pumpkinMoon)
                {
                    _event = invasions.Where(b => b.Type == -4 && b.Active).FirstOrDefault();
                }
                else if (Main.snowMoon)
                {
                    _event = invasions.Where(b => b.Type == -5 && b.Active).FirstOrDefault();
                }
                else
                {
                    _event = invasions.Where(b => b.Type == Main.invasionType && b.Active).FirstOrDefault();
                }

                // It is possible to hit this if lingering event mobs are being attacked
                // after event has finished. Do not record.
                if (_event != null && _event.Active)
                {
                    // Update actual start with first hit
                    if (_event.Players.Count == 0)
                    {
                        _event.EventStart = DateTime.Now;
                    }

                    _event.AddDamage(player, (int)calcDmg);
                }
            }
        }

        private static void DoPlayerHeal(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            Int16 healAmount = reader.ReadInt16();

            if (player != null)
            {
                player.TimesHealed++;
                player.Healed += (uint)healAmount;
            }
        }

        private static void DoPlayerMana(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            Int16 manaAmount = reader.ReadInt16();

            if (player != null)
            {
                player.TimesManaRecovered++;
                player.ManaRecovered += (uint)manaAmount;
            }
        }

        private static void DoPlayerDamage(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            byte hitDirection = reader.ReadByte();
            Int16 damage = reader.ReadInt16();
            bool pvp = reader.ReadBoolean();
            bool crit = reader.ReadBoolean();
            //if(damage < 0)
            //string text = reader.ReadString();

            if (player != null)
            {
                player.TimesDamaged++;
                if (crit)
                    player.CritsTaken++;
                Int16 ddmg = (short)((damage - Main.player[playerID].statDefense / 2) * (crit ? 2 : 1));
                if (ddmg <= 0)
                    ddmg = 1;
                player.DamageTaken += (uint)ddmg;
                if (ddmg > player.MaxReceived)
                    player.MaxReceived = ddmg;
            }
        }

        public string ReportProgress(BossInvasion e)
        {
            if (e.Type > 0 && e.Type < 4) // Typical Invasions
            {
                if (e.Players.Count == 0)
                {
                    float num = (float)Main.dayRate;
                    double distance = 0;
                    if (Main.invasionX > (double)Main.spawnTileX)
                    {
                        distance = Main.invasionX - Main.spawnTileX;
                    }
                    else if (Main.invasionX < (double)Main.spawnTileX)
                    {
                        distance = Main.spawnTileX - Main.invasionX;
                    }
                    return string.Format("{0} moving into position: {1} ({2} ft)", e.Name, Utils.FormatTime(distance / Main.dayRate / 60), distance / 2);
                }
                int kills = e.InvasionStartSize - Main.invasionSize;
                return string.Format("{0} progress: {1}/{2} ({3:n0}%) Remaining: {4}", e.Name, kills, e.InvasionStartSize, kills * 100.0 / e.InvasionStartSize, Main.invasionSize);
            }
            else if (e.Type == -4) // Pumpkin Moon
            {
                if (NPC.waveCount <= pumpkinLevels.Length)
                {
                    try
                    {
                        return string.Format("{0} Wave {1}/{2} - Points: {3}/{4} - Overall: {5:n0}% Remaining: {6}", e.Name, NPC.waveCount, pumpkinLevels.Length, NPC.waveKills, pumpkinLevels[NPC.waveCount - 1],
                            ((NPC.waveCount - 1) * 100 / pumpkinLevels.Length) + (NPC.waveKills / pumpkinLevels[NPC.waveCount - 1] * 100 / pumpkinLevels.Length), TimeTilMorning());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                        return ex.Message;
                    }
                }
                else
                {
                    return string.Format("{0} Final Wave - Points: {1} Remaining: {2}", e.Name, NPC.waveKills, TimeTilMorning());
                }
            }
            else if (e.Type == -5) // Frost Moon
            {
                if (NPC.waveCount <= frostLevels.Length)
                {
                    return string.Format("{0} Wave {1}/{2} - Points: {3}/{4} - Overall: {5:n0}% Remaining: {6}", e.Name, NPC.waveCount, frostLevels.Length, NPC.waveKills, frostLevels[NPC.waveCount - 1],
                        ((NPC.waveCount - 1) * 100 / frostLevels.Length) + (NPC.waveKills / frostLevels[NPC.waveCount - 1] * 100 / frostLevels.Length), TimeTilMorning());
                }
                else
                {
                    return string.Format("{0} Wave {1} - Points: {2} Remaining: {3}", e.Name, NPC.waveCount, NPC.waveKills, TimeTilMorning());
                }
            }
            else
                return string.Format("How the fuck did this get executed?! Type: {0}", e.Type);

            //return string.Empty;
        }

        public string TimeTilMorning()
        {
            double seconds;
            if (Main.dayTime)
            {
                seconds = (32400 + (54000 - Main.time)) / 60;
                return string.Format("{0} ({1:n0}%)", Utils.FormatTime(seconds), Main.time * 100 / 54000);
            }

            seconds = (32400 - Main.time) / 60;
            return string.Format("{0} ({1:n0}%)", Utils.FormatTime(seconds), Main.time * 100 / 32400);
        }

        public string TimeTilNight()
        {
            double seconds;
            if (Main.dayTime)
            {
                seconds = (54000 - Main.time) / 60;
                return string.Format("{0} ({1:n0}%)", Utils.FormatTime(seconds), Main.time * 100 / 54000);
            }

            seconds = (54000 + (32400 - Main.time)) / 60;
            return string.Format("{0} ({1:n0}%)", Utils.FormatTime(seconds), Main.time * 100 / 54000);
        }
    }
}
