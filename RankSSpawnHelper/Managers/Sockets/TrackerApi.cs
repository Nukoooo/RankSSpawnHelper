using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocketIOClient;

namespace RankSSpawnHelper.Managers.Sockets;
internal class TrackerApi : IDisposable
{
    private readonly SocketIO _client;

    public TrackerApi()
    {
        _client             =  new SocketIO("https://tracker-api.beartoolkit.com/socket/");
        _client.OnConnected += Client_OnConnected;
    }

    public void Dispose()
    {
        _client.OnConnected -= Client_OnConnected;
        _client?.Dispose();
    }

    public async void OntLogin()
    {
        // Do nothing if connected, we only need to initialize once
        if (_client.Connected)
            return;

        await _client.ConnectAsync();
    }

    private async void Client_OnConnected(object sender, EventArgs e)
    {
        while (DalamudApi.ClientState.LocalPlayer == null)
            await Task.Delay(100);

        // tell the server what datacenter we are in
        // HuntMap
        await _client.EmitAsync("SetDatacenter", DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Value.Name.RawString);
        // HuntUpdate
        await _client.EmitAsync("Change Room Request", DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Value.Name.RawString);
    }


}
