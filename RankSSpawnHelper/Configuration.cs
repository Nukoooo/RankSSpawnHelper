using Dalamud.Configuration;

namespace RankSSpawnHelper
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } 

        public void Save()
        {
            DalamudApi.Interface.SavePluginConfig(this);
        }
        
        #region Saved configuration values
        // 农怪计数
        public bool TrackKillCount { get; set; } = true;

        // true = 范围计数, false = 单人计数
        public bool TrackRangeMode { get; set; } = false;

        // true = 只显示当前区域, false = 显示所有计数
        public bool TrackerShowCurrentInstance { get; set; } = false;
        public bool TrackerWindowNoTitle { get; set; } = true;
        public bool TrackerWindowNoBackground { get; set; } = true;
        public bool TrackerAutoResize { get; set; } = true;
        public float TrackerClearThreshold { get; set; } = 45f;

        // 小异亚计数
        public bool WeeEaCounter { get; set; } = false;

        // 服务器信息显示几线
        public bool ShowInstance { get; set; } = false;

        public int SpawnNotificationType { get; set; } = 0;
        public bool CoolDownNotificationSound { get; set; } = true;

        public bool AutoShowHuntMap { get; set; } = false;
        public bool OnlyFetchInDuration { get; set; } = false;

        public uint FailedMessageColor { get; set; } = 518;
        public uint SpawnedMessageColor { get; set; } = 59;
        public uint HighlightColor { get; set; } = 71;

        public bool EnableAttemptMessagesFromOtherDcs = false;
        public bool ReceiveAttempMessageFromOtherDc = false;

        public bool PlayerSearchTip = true;
        
        public string PluginVersion = "";
#endregion
    }
}