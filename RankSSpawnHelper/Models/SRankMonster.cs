namespace RankSSpawnHelper.Models;

public enum GameExpansion
{
    ARealmReborn,
    Heavensward,
    Stormblood,
    Shadowbringers,
    Endwalker
}

public class SRankMonster
{
    public GameExpansion expansion;
    public string keyName;
    public string localizedName;
}