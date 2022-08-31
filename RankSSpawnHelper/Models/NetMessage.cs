using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

// @formatter: off
internal class NetMessage
{
    public long Time { get; set; }
    public string Type { get; set; }
    public string Instance { get; set; }
    public string User { get; set; }
    public Dictionary<string, int> Data { get; set; }
    public bool Failed { get; set; }
    public uint TerritoryId { get; set; }

    // Only used in NewConnection
    public string CurrentInstance;
    public List<Tracker> Trackers;
    internal class Tracker
    {
        public Dictionary<string, int> Data;
        public string Instance;
        public uint TerritoryId;
        public long Time;
    }
}