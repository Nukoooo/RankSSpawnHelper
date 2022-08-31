using Dalamud.Configuration;

namespace RankSSpawnHelper;

public class Configuration : IPluginConfiguration
{
    // 记录南萨的FATE
    public bool _recordFATEsInSouthThanalan { get; set; } = false;

    // 农怪计数
    public bool _trackKillCount { get; set; } = false;

    // true = 范围计数, false = 单人计数
    public bool _trackRangeMode { get; set; } = false;

    // true = 只显示当前区域, false = 显示所有计数
    public bool _trackerShowCurrentInstance { get; set; } = false;
    public bool _trackerWindowNoTitle { get; set; } = false;
    public bool _trackerWindowNoBackground { get; set; } = false;
    public bool _trackerAutoResize { get; set; } = true;
    public float _trackerClearThreshold { get; set; } = 45f;
    public bool _trackerNoNotification { get; set; } = false;

    // 小异亚计数
    public bool _weeEaCounter { get; set; } = false;

    // 服务器信息显示几线
    public bool _showInstance { get; set; } = false;

    public uint _failedMessageColor { get; set; } = 518;
    public uint _spawnedMessageColor { get; set; } = 59;
    public uint _highlightColor { get; set; } = 71;

    int IPluginConfiguration.Version { get; set; }

    public void Save() => Service.Interface.SavePluginConfig(this);
}