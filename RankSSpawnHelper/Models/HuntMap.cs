using System.Collections.Generic;

namespace RankSSpawnHelper.Models;

public class SpawnPoints
{
    public string? huntName;
    public string? worldName;

    public string key;
    public bool   state;
    public string reporter;
    public bool   verified;
    public float  x;
    public float  y;
}

public class HuntMap
{
    public List<SpawnPoints> spawnPoints;
}