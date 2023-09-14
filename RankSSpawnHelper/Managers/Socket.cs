using System;
using System.Threading.Tasks;
using RankSSpawnHelper.Managers.Sockets;

namespace RankSSpawnHelper.Managers;

internal class Socket : IDisposable
{
    public Main       Main       = new();
    public TrackerApi TrackerApi = new();

    public void Dispose()
    {
        Main.Dispose();
        TrackerApi.Dispose();
    }
    
}