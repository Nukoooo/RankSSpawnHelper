using System.Collections.Generic;

namespace RankSSpawnHelper.Models
{
    internal class NetMessage
    {
        // Only used in NewConnection
        public string CurrentInstance;
        public List<Tracker> Trackers;
        public long Time { get; set; }

        public string Type { get; set; }

        // public string Instance { get; set; }
        public Dictionary<string, int> Data { get; set; }
        public bool Failed { get; set; }
        public string Instance { get; set; }
        public uint TerritoryId { get; set; }

        internal class Tracker
        {
            public Dictionary<string, int> Data;
            public string Instance;
            public uint TerritoryId;
            public long Time;
        }
    }
}