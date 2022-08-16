using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

internal class NetMessage
{
    public long time { get; set; }
    public string type { get; set; }
    public string instance { get; set; }
    public string user { get; set; }
    public Dictionary<string, int> data { get; set; }
    public bool failed { get; set; }
}


internal class NetMessage_NewConnection
{
    public string currentInstance;
    public List<Tracker> trackers;
    public string type;
    public string user;

    internal class Tracker
    {
        public Dictionary<string, int> data;
        public string instance;
        public long time;
    }
}