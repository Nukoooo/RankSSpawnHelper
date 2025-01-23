namespace RankSSpawnHelper.Managers;

internal partial class ConnectionManager
{
    private record UserCounter
    {
        public Dictionary<uint, int>? Counter    { get; set; } = [];
        public int                    TotalCount { get; set; }
        public string                 UserName   { get; set; } = string.Empty;
    }

    internal record TrackerData
    {
        public Dictionary<uint, int> CounterData    { get; init; } = [];
        public uint                  WorldId        { get; init; }
        public uint                  TerritoryId    { get; init; }
        public uint                  InstanceId     { get; init; }
        public long                  LastUpdateTime { get; init; }
    }

    private record ReceivedMessage
    {
        public string Type        { get; init; } = string.Empty;
        public uint   WorldId     { get; init; }
        public uint   TerritoryId { get; init; }
        public uint   InstanceId  { get; init; }

        public long Time { get; init; }

        public Dictionary<uint, int> Counter     { get; init; } = [];
        public List<UserCounter>     UserCounter { get; init; } = [];
        public List<TrackerData>     TrackerData { get; init; } = [];

        // Broadcast message
        public string Message { get; init; } = string.Empty;

        // ggnore section
        public int    Total         { get; init; }
        public bool   Failed        { get; init; }
        public string Leader        { get; init; } = string.Empty;
        public bool   HasResult     { get; init; }
        public long   ExpectMinTime { get; init; }
        public long   ExpectMaxTime { get; init; }

        public float? StartPercent { get; init; } = null;
        public float? Percent      { get; init; } = null;
    }

    internal record BaseMessage
    {
        public string Type { get; set; } = string.Empty;

        public uint WorldId     { get; set; } = 0;
        public uint TerritoryId { get; set; } = 0;
        public uint InstanceId  { get; set; } = 0;
    }

    internal record NetTracker
    {
        public Dictionary<uint, int> Data        { get; init; }
        public uint                  InstanceId  { get; init; }
        public uint                  TerritoryId { get; init; }
        public long                  Time        { get; init; }
        public uint                  WorldId     { get; init; }
    }

    internal record NewConnectionMessage : BaseMessage
    {
        public List<NetTracker> Trackers = [];
    }

    internal record AttemptMessage : BaseMessage
    {
        public bool         Failed { get; set; }
        public List<string> Names  { get; set; } = [];
    }

    internal record CounterMessage : BaseMessage
    {
        // Mobs id, count
        public Dictionary<uint, int> Data      { get; init; }
        public long                  StartTime { get; init; }
        public bool                  IsItem    { get; init; }
    }

    internal record GetTrackerList : BaseMessage
    {
        public List<string> ServerList = [];
    }
}
