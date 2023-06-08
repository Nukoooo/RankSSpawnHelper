using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

public class UserCounter
{
    public Dictionary<uint, int> Counter = null;
    public int TotalCount;
    public string UserName;
}

internal class ReceivedMessage
{
    public string Type { get; set; }
    public uint WorldId { get; set; }
    public uint TerritoryId { get; set; }
    public uint InstanceId { get; set; }

    public long Time { get; set; }

    public Dictionary<uint, int> Counter { get; set; }
    public List<UserCounter> UserCounter { get; set; }

    // Broadcast message
    public string Message { get; set; }

    // ggnore section
    public int Total { get; set; }
    public bool Failed { get; set; }
    public string Leader { get; set; }
    public bool HasResult { get; set; }
    public long ExpectMinTime { get; set; }
    public long ExpectMaxTime { get; set; }

    public float? StartPercent { get; set; } = null;
    public float? Percent { get; set; } = null;
}