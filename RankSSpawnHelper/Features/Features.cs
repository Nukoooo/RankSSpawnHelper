using System;

namespace RankSSpawnHelper.Features
{
    internal class Features : IDisposable
    {
        public Counter Counter;
        public ShowHuntMap ShowHuntMap;
        public ShowInstance ShowInstance;
        public SpawnNotification SpawnNotification;
        public SearchCounter SearchCounter;

        public Features()
        {
            Counter           = new Counter();
            SpawnNotification = new SpawnNotification();
            ShowInstance      = new ShowInstance();
            ShowHuntMap       = new ShowHuntMap();
            SearchCounter     = new SearchCounter();
        }

        public void Dispose()
        {
            Counter.Dispose();
            SpawnNotification.Dispose();
            ShowInstance.Dispose();
            ShowHuntMap.Dispose();
            SearchCounter.Dispose();
        }
    }
}