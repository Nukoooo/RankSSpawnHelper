using System;

namespace RankSSpawnHelper.Managers;

internal class Managers : IDisposable
{
    public Data   Data   = new();
    public Font   Font   = new();
    public Socket Socket = new();

    public void Dispose()
    {
        Socket.Dispose();
        Font.Dispose();
    }
}