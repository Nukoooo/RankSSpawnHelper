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
    public Dictionary<uint, int> Data;
    public uint InstanceId;
    public uint TerritoryId;
    public long Time;
    public uint WorldId;
}

internal class NewConnectionMessage : BaseMessage
{
    public List<NetTracker> Trackers;
}

internal class AttemptMessage : BaseMessage
{
    public bool Failed { get; set; }
    public List<string> Names { get; set; } = null;
}

internal class CounterMessage : BaseMessage
{
    // Mobs id, count
    public Dictionary<uint, int> Data { get; set; }
    public long StartTime { get; set; }
    public bool IsItem { get; set; } = false;
}

internal class GetTrackerList : BaseMessage
{
    public       List<string> ServerList = new();
}