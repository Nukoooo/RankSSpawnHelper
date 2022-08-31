using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

internal class ReceivedMessage
{
    public string Type { get; set; }
    public string Instance { get; set; }
    public long Time { get; set; }
    public Dictionary<string, int> Counter { get; set; }
    public Dictionary<string, int> UserCounter { get; set; }

    // ggnore section
    public int Total { get; set; }
    public bool Failed { get; set; }
    public string Leader { get; set; }
    public bool HasResult { get; set; }
    public long ExpectMinTime { get; set; }
    public long ExpectMaxTime { get; set; }
}