using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

public class SpawnPoints
{
    public string key;
    public string reporter;
    public bool verified;
    public float x;
    public float y;
}

public class HuntMap
{
    public List<SpawnPoints> spawnPoints;
}