using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

public class Tracker
{
    public Dictionary<string, int> counter;
    public long lastUpdateTime;
    public long startTime;
}