using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Statistics
{
    public class Player
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public UInt32 Healed { get; set; }
        public UInt32 TimesHealed { get; set; }
        public UInt32 ManaRecovered { get; set; }
        public UInt32 TimesManaRecovered { get; set; }
        public UInt32 TimesDealtDamage { get; set; }
        public UInt32 DamageTaken { get; set; }
        public UInt32 TimesDamaged { get; set; }
        public UInt32 DamageGiven { get; set; }
        public Int16 MaxDamage { get; set; }
        public Int16 MaxReceived { get; set; }
        public UInt32 CritsTaken { get; set; }
        public UInt32 CritsGiven { get; set; }
        public UInt32 Kills { get; set; }
        public bool BossSubscribed { get; set; }
        public bool EventSubscribed { get; set; }

        public double CritPercent
        {
            get
            {
                return CritsGiven * 100.00 / TimesDealtDamage;
            }
        }

        public Player()
        {
            BossSubscribed = true;
            EventSubscribed = true;
        }

        public Player(int index) : this()
        {
            Index = index;
        }

        public Player(int index, string name) : this()
        {
            Index = index;
            Name = name;
        }
    }
}
