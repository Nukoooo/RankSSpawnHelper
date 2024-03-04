using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;
using SocketIOClient;

namespace RankSSpawnHelper.Managers.Sockets;

internal class TrackerApi : IDisposable
{
    private readonly Queue<string> _requestQueue = new();
    private          SocketIO      _huntUpdateclient;
    private          SocketIO      _route;

    public TrackerApi()
    {
        DalamudApi.ClientState.Login += Client_OnLogin;

        if (DalamudApi.ClientState.LocalPlayer == null || !DalamudApi.ClientState.IsLoggedIn)
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
        DalamudApi.ClientState.Login -= Client_OnLogin;

        _huntUpdateclient?.Dispose();
        _route?.Dispose();
    }

    private void Client_OnLogin()
    {
        Task.Run(async () =>
                 {
                     if (DalamudApi.ClientState.LocalPlayer == null)
                         await Task.Delay(100);

                     OnLogin();
                 });
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
                                             var point = JsonConvert.DeserializeObject<List<SpawnPoints>>(response.ToString());
                                             Plugin.Features.ShowHuntMap.RemoveSpawnPoint(point[0].worldName, point[0].huntName, point[0].key);
                                             return;
                                         }

                                         // ignore fate
                                         if (split[1].StartsWith("FATE"))
                                             return;

                                         // TODO: maybe update huntstatus here
                                     }
                                     catch (Exception e)
                                     {
                                         DalamudApi.PluginLog.Error(e, "Erro when getting /HuntUpdate");
                                     }
                                 });

        _huntUpdateclient.OnConnected += Client_OnConnected;
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
            ReconnectionDelay    = 2.5,
            ReconnectionDelayMax = 5
        });

        _route.OnAny((name, response) =>
                      {
                          DalamudApi.PluginLog.Debug($"_route. Name: {name}, response: {response.GetValue().GetString()}");

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
                                      var spawnPoints = JsonConvert.DeserializeObject<List<SpawnPoints>>(response.GetValue().GetString());

                                      Plugin.Features.ShowHuntMap.AddSpawnPoints(huntMapName[0], huntMapName[1], spawnPoints);
                                      break;
                              }
                          }
                          catch (Exception e)
                          {
                              DalamudApi.PluginLog.Error(e, "Erro when getting /PublicRoutes");
                          }
                      });

        _route.OnConnected += Client_OnConnected;
        try
        {
            await _route.ConnectAsync();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, "Error when connecting to /PublicRoutes");
        }
    }

    private void OnLogin()
    {
        ConnectHuntUpdate();
        ConnectRoute();
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

    private async void Client_OnConnected(object sender, EventArgs e)
    {
        while (DalamudApi.ClientState.LocalPlayer == null || DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData == null)
            await Task.Delay(100);

        // tell the server what datacenter we are in
        var dataCenter = DalamudApi.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Value.Name.RawString;
        DalamudApi.PluginLog.Debug($"DataCenter: {dataCenter}");

        try
        {
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
    }
    
}