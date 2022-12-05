using System;

namespace RankSSpawnHelper.Features
{
    internal class Features : IDisposable
    {
        public Counter Counter;
        public ShowHuntMap ShowHuntMap;
        public ShowInstance ShowInstance;
        public SpawnNotification SpawnNotification;

        public Features()
        {
            Counter           = new Counter();
            SpawnNotification = new SpawnNotification();
            ShowInstance      = new ShowInstance();
            ShowHuntMap       = new ShowHuntMap();
        }

        public void Dispose()
        {
            Counter.Dispose();
            SpawnNotification.Dispose();
            ShowInstance.Dispose();
            ShowHuntMap.Dispose();
        }
    }
}