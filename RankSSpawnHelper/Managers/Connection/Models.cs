namespace RankSSpawnHelper.Managers;

internal partial class ConnectionManager
{
    private class UserCounter
    {
        public Dictionary<uint, int>? Counter    { get; set; } = [];
        public int                    TotalCount { get; set; }
        public string                 UserName   { get; set; } = string.Empty;
    }

    private class TrackerData
    {
        public Dictionary<uint, int> CounterData    { get; set; } = [];
        public uint                  WorldId        { get; set; }
        public uint                  TerritoryId    { get; set; }
        public uint                  InstanceId     { get; set; }
        public long                  LastUpdateTime { get; set; }
    }

    private class ReceivedMessage
    {
        public string Type        { get; set; } = string.Empty;
        public uint   WorldId     { get; set; }
        public uint   TerritoryId { get; set; }
        public uint   InstanceId  { get; set; }

        public long Time { get; set; }

        public Dictionary<uint, int> Counter     { get; set; } = [];
        public List<UserCounter>     UserCounter { get; set; } = [];
        public List<TrackerData>     TrackerData { get; set; } = [];

        // Broadcast message
        public string Message { get; set; } = string.Empty;

        // ggnore section
        public int    Total         { get; set; }
        public bool   Failed        { get; set; }
        public string Leader        { get; set; } = string.Empty;
        public bool   HasResult     { get; set; }
        public long   ExpectMinTime { get; set; }
        public long   ExpectMaxTime { get; set; }

        public float? StartPercent { get; set; } = null;
        public float? Percent      { get; set; } = null;
    }

    internal class BaseMessage
    {
        public string Type { get; set; } = string.Empty;

        public uint WorldId     { get; set; } = 0;
        public uint TerritoryId { get; set; } = 0;
        public uint InstanceId  { get; set; } = 0;
    }

    internal class NetTracker
    {
        public Dictionary<uint, int> Data = [];
        public uint                  InstanceId;
        public uint                  TerritoryId;
        public long                  Time;
        public uint                  WorldId;
    }

    internal class NewConnectionMessage : BaseMessage
    {
        public List<NetTracker> Trackers = [];
    }

    internal class AttemptMessage : BaseMessage
    {
        public bool         Failed { get; set; }
        public List<string> Names  { get; set; } = [];
    }

    internal class CounterMessage : BaseMessage
    {
        // Mobs id, count
        public Dictionary<uint, int> Data      { get; set; } = [];
        public long                  StartTime { get; set; } = 0;
        public bool                  IsItem    { get; set; } = false;
    }

    internal class GetTrackerList : BaseMessage
    {
        public List<string> ServerList = [];
    }
}