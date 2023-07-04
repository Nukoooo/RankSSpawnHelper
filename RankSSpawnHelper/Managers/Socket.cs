using System;
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
        DalamudApi.ClientState.Login  += ClientState_OnLogin;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;
    }

    public void Dispose()
    {
        Main.Dispose();
        TrackerApi.Dispose();

        DalamudApi.ClientState.Login  -= ClientState_OnLogin;
        DalamudApi.ClientState.Logout -= ClientState_OnLogout;
    }

    private void ClientState_OnLogin(object sender, EventArgs e)
    {
    }

    private void ClientState_OnLogout(object sender, EventArgs e) { }
}