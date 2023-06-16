using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace RankSSpawnHelper.Models;

internal class Tracker
{
    public Dictionary<string, int> counter;
    public long lastUpdateTime;
    public long startTime;
    public uint territoryId;
    public string trackerOwner;
}