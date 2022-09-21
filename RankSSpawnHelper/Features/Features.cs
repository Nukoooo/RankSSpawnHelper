using System;

namespace RankSSpawnHelper.Features
{
    internal class Features : IDisposable
    {
        public Counter Counter;
        public SpawnNotification SpawnNotification;
        public ShowInstance ShowInstance;

        public Features()
        {
            Counter           = new Counter();
            SpawnNotification = new SpawnNotification();
            ShowInstance      = new ShowInstance();
        }

        public void Dispose()
        {
            Counter.Dispose();
            SpawnNotification.Dispose();
            ShowInstance.Dispose();
        }
    }
}