// ReSharper disable once CheckNamespace

namespace RankSSpawnHelper.Modules;

// ReSharper disable once UnusedType.Global
internal partial class Counter
{
    internal class Tracker
    {
        public Dictionary<string, int> Counter        { get; init; } = [];
        public long                    LastUpdateTime { get; set; }
        public long                    StartTime      { get; init; }
        public uint                    TerritoryId    { get; set; }
        public string                  TrackerOwner   { get; set; } = string.Empty;
    }
}
