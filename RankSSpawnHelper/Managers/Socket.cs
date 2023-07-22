using System;
using System.Threading.Tasks;
using RankSSpawnHelper.Managers.Sockets;

namespace RankSSpawnHelper.Managers;

internal class Socket : IDisposable
{
    public Main       Main;
    public TrackerApi TrackerApi;

    public Socket()
    {
        Main                          =  new Main();
        TrackerApi                    =  new TrackerApi();
    }

    public void Dispose()
    {
        Main.Dispose();
        TrackerApi.Dispose();
    }
    
}