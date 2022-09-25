// ReSharper disable InconsistentNaming

namespace RankSSpawnHelper.Models
{
    public class HuntStatus
    {
        public int attemptCount;
        public long expectMaxTime;
        public long expectMinTime;
        public int instance;
        public string lastAttempt;
        public long lastDeathTime;
        public string localizedName;
        public bool missing;
        public string worldName;
    }
}