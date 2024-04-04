using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RankSSpawnHelper.Models;
using SocketIOClient;

namespace RankSSpawnHelper.Managers.Sockets;

internal class TrackerApi : IDisposable
{
    private readonly Queue<string>           _requestQueue = new();
    private          SocketIOClient.SocketIO _huntUpdateclient;
    private          SocketIOClient.SocketIO _route;

    public TrackerApi()
    {
        DalamudApi.ClientState.Login  += ClientState_OnLogin;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;

        if (!DalamudApi.ClientState.IsLoggedIn)
            return;

        ConnectHuntUpdate();
        ConnectRoute();
    }

    public void Dispose()
    {
        if (_huntUpdateclient != null)
            _huntUpdateclient.OnConnected -= Client_OnConnected;

        if (_route != null)
            _route.OnConnected -= Client_OnConnected;

        DalamudApi.ClientState.Login  -= ClientState_OnLogin;
        DalamudApi.ClientState.Logout -= ClientState_OnLogout;

        _huntUpdateclient?.Dispose();
        _route?.Dispose();
    }

    private void ClientState_OnLogin()
    {
        ConnectHuntUpdate();
        ConnectRoute();
    }

    private void ClientState_OnLogout()
    {
        Dispose();
    }

    private async void ConnectHuntUpdate()
    {
        if (_huntUpdateclient != null)
            return;

        _huntUpdateclient = new("https://tracker-api.beartoolkit.com/HuntUpdate", new()
        {
            Path                 = "/socket",
            Reconnection         = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay    = 2.5,
            ReconnectionDelayMax = 5,
        });

        _huntUpdateclient.OnAny((name, response) =>
                                {
                                    // WorldName_HuntName
                                    try
                                    {
                                        DalamudApi.PluginLog.Debug($"_huntUpdateclient. Name: {name}, {response}");

                                        var split = name.Split('_');
                                        if (split.Last() == "SpawnPoint")
                                        {
                                            var point = response.GetValue<SpawnPoints>();
                                            DalamudApi.Framework.Run(() => Plugin.Features.ShowHuntMap.RemoveSpawnPoint(point.worldName, point.huntName, point.key));
                                            return;
                                        }

                                        // ignore fate
                                        if (split[1].StartsWith("FATE"))
                                            return;

                                        var huntStatus = response.GetValue<HuntStatus>();
                                        DalamudApi.PluginLog.Debug($"huntStatus: {huntStatus.worldName}");

                                        // TODO: maybe update huntstatus here
                                    }
                                    catch (Exception e)
                                    {
                                        DalamudApi.PluginLog.Error(e, "Error in /HuntUpdate");
                                    }
                                });

        _huntUpdateclient.OnConnected    += Client_OnConnected;
        _huntUpdateclient.OnDisconnected += (sender, s) => { DalamudApi.PluginLog.Debug($"_huntUpdateclient.OnDisconnect {s}"); };

        try
        {
            await _huntUpdateclient.ConnectAsync();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, "Error when connecting to /HuntUpdate");
        }
    }

    private async void ConnectRoute()
    {
        if (_route != null)
            return;

        _route = new("https://tracker-api.beartoolkit.com/PublicRoutes", new()
        {
            Path                 = "/socket",
            Reconnection         = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay    = 5,
            ReconnectionDelayMax = 10,
        });
        
        _route.OnAny((name, response) =>
                     {
                         DalamudApi.PluginLog.Debug($"_route. Name: {name}, response: {response}");

                         try
                         {
                             switch (name)
                             {
                                 case "SpawnPoint":
                                     // var point = JsonConvert.DeserializeObject<SpawnPoints>(response.GetValue().GetString());
                                     DalamudApi.PluginLog.Debug($"{name}");

                                     return;
                                 case "Huntmap":
                                     var huntMapName = _requestQueue.Dequeue().Split('@');
                                     // TODO: find a better way for this crap
                                     /*var spawnPoints = response.GetValue<List<SpawnPoints>>();*/
                                     var spawnPoints = JsonConvert.DeserializeObject<List<SpawnPoints>>(response.GetValue().GetString());
                                     DalamudApi.PluginLog.Debug($"{spawnPoints.Count} / {spawnPoints[0].x} - {spawnPoints[0].y}");
                                     Plugin.Features.ShowHuntMap.AddSpawnPoints(huntMapName[0], huntMapName[1], spawnPoints);
                                     break;
                             }
                         }
                         catch (Exception e)
                         {
                             DalamudApi.PluginLog.Error(e, "Error in /PublicRoutes");
                         }
                     });

        _route.OnConnected    += Client_OnConnected;
        _route.OnDisconnected += (sender, s) => { DalamudApi.PluginLog.Debug($"_router.OnDisconnect {s}"); };
        _route.OnError        += (sender, s) => { DalamudApi.PluginLog.Debug($"_router.OnError {s}"); };
        try
        {
            await _route.ConnectAsync();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, "Error when connecting to /PublicRoutes");
        }
    }

    public async void SendHuntmapRequest(string worldName, string huntName)
    {
        switch (_route)
        {
            case null:
                DalamudApi.PluginLog.Debug("_route null");
                return;
            case { Connected: false }:
                DalamudApi.PluginLog.Debug("Not connected");
                return;
            default:
                _requestQueue.Enqueue($"{worldName}@{huntName}");
                await _route.EmitAsync("Huntmap", $"{{\"HuntName\": \"{huntName}\", \"WorldName\": \"{worldName}\"}}");
                break;
        }
    }

    private void Client_OnConnected(object sender, EventArgs e)
    {
        DalamudApi.Framework.Run(async () =>
                                 {
                                     while (!DalamudApi.ClientState.IsLoggedIn)
                                         await Task.Delay(100);

                                     try
                                     {
                                         // tell the server what datacenter we are in
                                         var dataCenter = DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Value.Name.RawString;
                                         DalamudApi.PluginLog.Debug($"DataCenter: {dataCenter}");

                                         // HuntMap
                                         await ((SocketIO)sender).EmitAsync("SetDatacenter", dataCenter);
                                         // HuntUpdate
                                         await ((SocketIO)sender).EmitAsync("Change Room Request", dataCenter);

                                         DalamudApi.PluginLog.Debug($"Conncted to tracker api. {((SocketIO)sender).Namespace}");
                                     }
                                     catch (Exception ex)
                                     {
                                         DalamudApi.PluginLog.Error(ex, "Error when conneting to tracker api.");
                                     }
                                 });
    }
}