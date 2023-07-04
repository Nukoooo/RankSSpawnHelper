using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Logging;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;
using SocketIOClient;

namespace RankSSpawnHelper.Managers.Sockets;

internal class TrackerApi : IDisposable
{
    private readonly SocketIO      _huntUpdateclient;
    private readonly SocketIO      _route;
    private readonly Queue<string> _requestQueue = new();

    public TrackerApi()
    {
        _huntUpdateclient = new SocketIO("https://tracker-api.beartoolkit.com/HuntUpdate", new SocketIOOptions
        {
            Path = "/socket"
        });

        _route = new SocketIO("https://tracker-api.beartoolkit.com/PublicRoutes", new SocketIOOptions
        {
            Path = "/socket"
        });

        _huntUpdateclient.OnConnected += Client_OnConnected;
        _route.OnConnected            += Client_OnConnected;

        BindEvent();

        Task.Run(async () =>
                 {
                     if (DalamudApi.ClientState.LocalPlayer == null)
                         return;

                     await _route.ConnectAsync();
                     await _huntUpdateclient.ConnectAsync();
                 });
    }

    public void Dispose()
    {
        _huntUpdateclient.OnConnected -= Client_OnConnected;
        _route.OnConnected            -= Client_OnConnected;
        _huntUpdateclient?.Dispose();
        _route?.Dispose();
    }

    public async void OnLogin()
    {
        // Do nothing if connected, we only need to initialize once
        if (!_huntUpdateclient.Connected)
            await _huntUpdateclient.ConnectAsync();

        if (!_route.Connected)
            await _route.ConnectAsync();
    }

    public async void SendHuntmapRequest(string worldName, string huntName)
    {
        _requestQueue.Enqueue($"{worldName}@{huntName}");
        await _route.EmitAsync("Huntmap", $"{{\"HuntName\": \"{huntName}\", \"WorldName\": \"{worldName}\"}}");
    }

    private async void Client_OnConnected(object sender, EventArgs e)
    {
        while (DalamudApi.ClientState.LocalPlayer == null)
            await Task.Delay(100);

        // tell the server what datacenter we are in
        var dataCenter = DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Value.Name.RawString;
        PluginLog.Debug($"DataCenter: {dataCenter}");

        // HuntMap
        await ((SocketIO)sender).EmitAsync("SetDatacenter", dataCenter);
        // HuntUpdate
        await ((SocketIO)sender).EmitAsync("Change Room Request", dataCenter);

        if (sender == _route)
        {
            SendHuntmapRequest("海猫茶屋", "Tyger");
            SendHuntmapRequest("延夏", "Tyger");
        }

        PluginLog.Debug($"Conncted to tracker api. {((SocketIO)sender).Namespace}");
    }

    private void BindEvent()
    {
        _huntUpdateclient.OnAny((name, response) =>
                                {
                                    // WorldName_HuntName

                                    PluginLog.Debug($"_huntUpdateclient. Name: {name}");

                                    var split = name.Split('_');
                                    if (split[^1] == "SpawnPoint")
                                    {
                                        var point = JsonConvert.DeserializeObject<List<SpawnPoints>>(response.ToString());
                                        Plugin.Features.ShowHuntMap.RemoveSpawnPoint(point[0].worldName, point[0].huntName, point[0].key);
                                        return;
                                    }

                                    // ignore fate
                                    if (split[1].StartsWith("FATE"))
                                        return;
                                });

        _route.OnAny((name, response) =>
                     {
                         PluginLog.Debug($"_route. Name: {name}, response: {response.GetValue().GetString()}");

                         switch (name)
                         {
                             case "SpawnPoint":
                                 // var point = JsonConvert.DeserializeObject<SpawnPoints>(response.GetValue().GetString());
                                 PluginLog.Debug($"{name}");

                                 return;
                             case "Huntmap":
                                 var huntMapName = _requestQueue.Dequeue().Split('@');
                                 var spawnPoints  = JsonConvert.DeserializeObject<List<SpawnPoints>>(response.GetValue().GetString());

                                 Plugin.Features.ShowHuntMap.AddSpawnPoints(huntMapName[0], huntMapName[1],spawnPoints);
                                 break;
                         }
                     });
        PluginLog.Debug("Event binded");
    }
}