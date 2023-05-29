using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

internal class BaseMessage
{
    public string Type { get; set; }

    public uint WorldId { get; set; }
    public uint TerritoryId { get; set; }
    public int InstanceId { get; set; }
}

internal class NetTracker
{
    public Dictionary<string, int> Data;
    public string Instance;
    public uint TerritoryId;
    public long Time;
}

internal class NewConnectionMessage : BaseMessage
{
    public string CurrentInstance { get; set; }
    public List<NetTracker> Trackers;
}

internal class AttemptMessage : BaseMessage
{
    public bool Failed { get; set; }
}

internal class CounterMessage : BaseMessage
{
    // Mobs id, count
    public Dictionary<uint, int> Data { get; set; }
    public long StartTime { get; set; }
    public bool IsItem { get; set; } = false;
}

/*
internal class NetMessage
{
    // Only used in NewConnection
    public string CurrentInstance;
    public List<Tracker> Trackers;
    public long Time { get; set; }

    public string Type { get; set; }

    public Dictionary<string, int> Data { get; set; }
    public bool Failed { get; set; }
    public string Instance { get; set; }
    public uint TerritoryId { get; set; }
}*/