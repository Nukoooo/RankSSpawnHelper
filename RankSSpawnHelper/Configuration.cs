using Dalamud.Configuration;

namespace RankSSpawnHelper;

public enum SpawnNotificationType
{
    Off = 0,
    SpawnableOnly,
    Full,
}

public enum AttemptMessageType
{
    Off,
    Basic,
    Detailed,
}

public enum AttemptMessageFromServerType
{
    Off,
    CurrentDataCenter,
    All,
}

public enum PlayerSearchDisplayType
{
    Off,
    ChatOnly,
    UiOnly,
    Both,
}

internal class Configuration : IPluginConfiguration
{
    public string PluginVersion { get; set; } = string.Empty;

    // 农怪计数
    public bool TrackKillCount { get; set; } = true;

    public bool  TrackerWindowNoTitle      { get; set; } = true;
    public bool  TrackerWindowNoBackground { get; set; } = true;
    public bool  TrackerAutoResize         { get; set; } = true;
    public float TrackerClearThreshold     { get; set; } = 45f;

    // 小异亚计数
    public bool WeeEaCounter { get; set; } = false;

    // 服务器信息显示几线
    public bool ShowInstance { get; set; } = false;

    public SpawnNotificationType SpawnNotificationType     { get; set; } = 0;
    public bool                  CoolDownNotificationSound { get; set; } = true;

    public bool AutoShowHuntMap     { get; set; } = false;
    public bool OnlyFetchInDuration { get; set; } = false;

    public uint FailedMessageColor  { get; set; } = 518;
    public uint SpawnedMessageColor { get; set; } = 59;
    public uint HighlightColor      { get; set; } = 71;

    public AttemptMessageType AttemptMessage { get; set; } = AttemptMessageType.Detailed;

    public AttemptMessageFromServerType AttemptMessageFromServer { get; set; } = AttemptMessageFromServerType.CurrentDataCenter;

    public bool ShowAttemptMessageInDungeons { get; set; } = true;
    public bool PlayerSearch                 { get; set; }

    public bool   UseProxy { get; set; } = false;
    public string ProxyUrl { get; set; } = "http://127.0.0.1:7890";

    public bool AccurateWorldTravelQueue { get; set; } = true;

    public HashSet<uint> ItemsToDiscard { get; set; } = [];

    public int DiscardTimes           { get; set; } = 50;
    public int AmountToDiscardPerLoop { get; set; } = 1;

    public bool AutoDiscardItem  { get; set; } = false;
    public bool AutoSummonMinion { get; set; } = false;
    public bool AutoLeaveDuty    { get; set; } = false;

    public List<uint> BlacklistedTerritories { get; set; } = [];

    int IPluginConfiguration.Version { get; set; }

    public void Save()
        => DalamudApi.Interface.SavePluginConfig(this);
}
