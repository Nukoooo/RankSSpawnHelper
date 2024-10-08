// ReSharper disable InconsistentNaming

namespace RankSSpawnHelper.Models;

public enum GameExpansion
{
    ARealmReborn,
    Heavensward,
    Stormblood,
    Shadowbringers,
    Endwalker,
    Dawntrail,
}

public class SRankMonster
{
    public GameExpansion expansion;
    public uint id;
    public string keyName;
    public string localizedName;
}