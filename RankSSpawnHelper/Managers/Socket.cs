using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;
using Websocket.Client;
using Websocket.Client.Models;

namespace RankSSpawnHelper.Managers;

internal class Socket : IDisposable
{
    private const string ServerVersion = "v5";

    private IWebsocketClient _client;

#if DEBUG || DEBUG_CN
    private const string Url = "ws://127.0.0.1:8000";
#else
    private const string Url = "ws://124.220.161.157:8000";
#endif

    private string _userName = string.Empty;

    public Socket()
    {
        DalamudApi.ClientState.Login  += ClientState_OnLogin;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;

        if (DalamudApi.ClientState.LocalPlayer != null && DalamudApi.ClientState.IsLoggedIn)
            Connect(Url);
    }

    private void ClientState_OnLogin(object sender, EventArgs e)
    {
        Task.Run(async () =>
                 {
                     while (DalamudApi.ClientState.LocalPlayer == null)
                     {
                         await Task.Delay(100);
                     }

                     Connect(Url);
                 });
    }

    private void ClientState_OnLogout(object sender, EventArgs e)
    {
        async Task Function()
        {
            if (_client == null) return;

            await _client.Stop(WebSocketCloseStatus.NormalClosure, "Logout");
            await Task.Delay(100);
            _client.Dispose();
        }

        Task.Run(Function);
        _userName = string.Empty;
        PluginLog.Information("ClientState_OnLogout");
    }

    public void Dispose()
    {
        DalamudApi.ClientState.Login  -= ClientState_OnLogin;
        DalamudApi.ClientState.Logout -= ClientState_OnLogout;

        _client?.Dispose();

        GC.SuppressFinalize(this);
    }

#if DEBUG || DEBUG_CN
    public void Connect(string url)
#else
    private void Connect(string url)
#endif
    {
        Task.Run(async () =>
                 {
                     try
                     {
                         if (DalamudApi.ClientState.LocalPlayer == null || !DalamudApi.ClientState.IsLoggedIn)
                         {
                             _client?.Dispose();
                             return;
                         }

                         _client?.Dispose();
                         _userName = Plugin.Managers.Data.Player.GetLocalPlayerName();

                         _client = new WebsocketClient(new Uri(url), () =>
                                                                     {
                                                                         var client = new ClientWebSocket
                                                                         {
                                                                                 Options =
                                                                                 {
                                                                                         KeepAliveInterval = TimeSpan.FromSeconds(40)
                                                                                 }
                                                                         };
                                                                         client.Options.SetRequestHeader("ranks-spawn-helper-user", EncodeNonAsciiCharacters(_userName));
                                                                         client.Options.SetRequestHeader("server-version", ServerVersion);
                                                                         client.Options.SetRequestHeader("user-type", "Dalamud");
                                                                         client.Options.SetRequestHeader("plugin-version", Plugin.PluginVersion);
                                                                         client.Options.SetRequestHeader("iscn", Plugin.IsChina().ToString());
                                                                         return client;
                                                                     })
                         {
                                 ReconnectTimeout      = TimeSpan.FromSeconds(120),
                                 ErrorReconnectTimeout = TimeSpan.FromSeconds(60)
                         };
                         _client.ReconnectionHappened.Subscribe(OnReconntion);
                         _client.MessageReceived.Subscribe(OnMessageReceived);
                         _client.DisconnectionHappened.Subscribe(OnDisconnectionHappened);
                         await _client.Start();
                     }
                     catch (Exception e)
                     {
                         PluginLog.Debug(e, "Exception in Managers::Socket::Connect()");
                     }
                 });
    }

    private void OnDisconnectionHappened(DisconnectionInfo obj)
    {
        PluginLog.Debug($"Disconnection type: {obj.Type}");
        if (!DalamudApi.ClientState.IsLoggedIn)
            _client?.Dispose();
    }

    public bool Connected()
    {
        return _client != null && _client.IsRunning;
    }

#if DEBUG || DEBUG_CN
    public async void Disconnect()
    {
        if (!Connected())
            return;

        await _client.Stop(WebSocketCloseStatus.NormalClosure, "Disconnection");
    }
#else
    public async void Reconnect()
    {
        if (_client == null)
            return;
        await _client.Reconnect();
    }
#endif

    public void SendMessage(BaseMessage message)
    {
        if (!Connected())
            return;

        var str = JsonConvert.SerializeObject(message);
        _client.Send(str);
        PluginLog.Debug($"Managers::Socket::SendMessage: {str}");
    }

    private void OnReconntion(ReconnectionInfo args)
    {
        PluginLog.Debug($"ReconnectionType: {args.Type}");

        var localTracker = Plugin.Features.Counter.GetLocalTrackers();

        if (!DalamudApi.ClientState.LocalPlayer || localTracker == null || localTracker.Count == 0)
            return;

        var name = Plugin.Managers.Data.Player.GetLocalPlayerName();

        List<NetTracker> trackers = new();

        foreach (var tracker in localTracker)
        {
            if (!tracker.Value.trackerOwner.Equals(name))
                continue;

            var split       = tracker.Key.Split('@');
            var worldId     = Plugin.Managers.Data.GetWorldIdByName(split[0]);
            var territoryId = Plugin.Managers.Data.GetTerritoryIdByName(split[1]);
            var instanceId  = 0u;
            if (split.Length == 3)
                _ = uint.TryParse(split[2], out instanceId);

            Dictionary<uint, int> data = new();

            var isItem = tracker.Value.territoryId is 814 or 400 or 961 or 813;

            foreach (var counter in tracker.Value.counter)
            {
                if (isItem)
                    data[Plugin.Managers.Data.GetItemIdByName(counter.Key)] = counter.Value;
                else if (tracker.Value.territoryId == 621)
                    data[0] = counter.Value;
                else
                    data[Plugin.Managers.Data.GetNpcIdByName(counter.Key)] = counter.Value;
            }

            var netTracker = new NetTracker
            {
                    WorldId     = worldId,
                    TerritoryId = territoryId,
                    InstanceId  = instanceId,
                    Data        = data,
                    Time        = tracker.Value.startTime
            };

            trackers.Add(netTracker);
        }

        Plugin.Managers.Socket.SendMessage(new NewConnectionMessage
        {
                Type        = "NewConnection",
                WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
                TerritoryId = DalamudApi.ClientState.TerritoryType,
                InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
                Trackers    = trackers
        });
    }

    private void OnMessageReceived(ResponseMessage args)
    {
        if (args.MessageType != WebSocketMessageType.Binary)
            return;

        // ping pong
        if (args.Binary.Length == 0)
            return;

        var msg = Encoding.UTF8.GetString(args.Binary);

        if (!msg.StartsWith("{"))
        {
            PluginLog.Error($"Managers::Socket::OnMessageReceived. Not a valid json format message. {msg}");
            return;
        }

        PluginLog.Debug($"Managers::Socket::OnMessageReceived. {msg}");

        try
        {
            var result = JsonConvert.DeserializeObject<ReceivedMessage>(msg);

            if (result == null)
                return;

            switch (result.Type)
            {
                case "Error":
                {
                    var message = result.Message;
                    Plugin.Print(new List<Payload>
                    {
                            new UIForegroundPayload(518),
                            new TextPayload($"Error: {message}"),
                            new UIForegroundPayload(0)
                    });

                    break;
                }
                case "Attempt":
                {
                    var instance = Plugin.Managers.Data.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);
                    Plugin.Features.Counter.RemoveInstance(instance);

                    if (Plugin.Configuration.AttemptMessageFromServer == AttemptMessageFromServerType.Off)
                        return;

                    if (DalamudApi.Condition[ConditionFlag.BoundByDuty])
                        return;

                    if (Plugin.Configuration.AttemptMessageFromServer == AttemptMessageFromServerType.CurrentDataCenter && Plugin.Managers.Data.IsFromOtherServer(result.WorldId))
                        return;

                    var message = $"{instance} {(result.Failed ? "寄了" : "出货了")}. 概率: ";
                    if (result.StartPercent != null)
                        message += $"{result.StartPercent:F2}% / ";
                    message += $"{result.Percent:F2}%";

                    Plugin.Print(message);
                    break;
                }
                case "Counter":
                {
                    var instance = Plugin.Managers.Data.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);

                    foreach (var (key, value) in result.Counter)
                    {
                        var isItem = result.TerritoryId is 814 or 400 or 961 or 813;
                        var keyName = isItem                    ? Plugin.Managers.Data.GetItemName(key) :
                                      result.TerritoryId == 621 ? Plugin.IsChina()
                                                                          ? "扔垃圾"
                                                                          : "Item" : Plugin.Managers.Data.GetNpcName(key);
                        Plugin.Features.Counter.UpdateNetworkedTracker(instance, keyName, value, result.Time, result.TerritoryId);
                    }

                    return;
                }
                case "ggnore":
                {
                    if (Plugin.Configuration.AttemptMessage <= AttemptMessageType.Off)
                        return;

                    var localTime = DateTimeOffset.FromUnixTimeSeconds(result.Time).LocalDateTime;

                    var instance = Plugin.Managers.Data.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);

                    var color = (ushort)(result.Failed
                                                 ? Plugin.Configuration.FailedMessageColor
                                                 : Plugin.Configuration.SpawnedMessageColor);
                    var message = (result.Failed
                                           ? $"不好啦！ {instance}寄啦！\n寄时: "
                                           : $"太好啦！{instance}出货啦！\n出时: ") +
                                  $"{localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n计数总数: {result.Total}\n计数详情:\n";

                    var payloads = new List<Payload>
                    {
                            new UIForegroundPayload(color),
                            new TextPayload(message),
                            new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor)
                    };

                    var isItem = result.TerritoryId is 814 or 400 or 961 or 813;

                    foreach (var (k, v) in result.Counter)
                    {
                        string name;
                        if (isItem)
                            name = Plugin.Managers.Data.GetItemName(k);
                        else if (result.TerritoryId == 621)
                            name = Plugin.IsChina()
                                           ? "扔垃圾"
                                           : "Item";
                        else
                            name = Plugin.Managers.Data.GetNpcName(k);

                        payloads.Add(new TextPayload($"    {name}: {v}\n"));
                    }

                    payloads.Add(new UIForegroundPayload(0));

                    if (Plugin.Configuration.AttemptMessage == AttemptMessageType.Basic)
                        goto end;

                    payloads.Add(new TextPayload("人数详情:\n"));
                    payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));

                    foreach (var userCounter in result.UserCounter)
                    {
                        payloads.Add(new TextPayload($"    {userCounter.UserName}: {userCounter.TotalCount}\n"));
                        if (userCounter.Counter == null)
                            continue;

                        foreach (var (k, v) in userCounter.Counter)
                        {
                            string name;
                            if (isItem)
                                name = Plugin.Managers.Data.GetItemName(k);
                            else if (result.TerritoryId == 621)
                                name = Plugin.IsChina()
                                               ? "扔垃圾"
                                               : "Item";
                            else
                                name = Plugin.Managers.Data.GetNpcName(k);

                            payloads.Add(new TextPayload($"        {name}: {v}\n"));
                        }
                    }


                    payloads.Add(new UIForegroundPayload(0));

                    if (result.Failed && result.Leader != "null")
                    {
                        payloads.Add(new TextPayload("\n喊寄的人: "));
                        payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                        payloads.Add(new TextPayload(result.Leader));
                        payloads.Add(new UIForegroundPayload(0));
                    }

                    if (result.HasResult)
                    {
                        var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() >= result.ExpectMinTime;
                        if (isSpawnable)
                        {
                            payloads.Add(new TextPayload("\n当前可触发概率: "));
                            payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                            payloads.Add(new TextPayload(
                                                         $"{100 * ((result.Time - result.ExpectMinTime) / (double)(result.ExpectMaxTime - result.ExpectMinTime)):F2}%\n"));
                            payloads.Add(new UIForegroundPayload(0));
                        }
                        else
                        {
                            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                            payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                            var minTime = DateTimeOffset.FromUnixTimeSeconds(result.ExpectMinTime);
                            var delta   = (minTime - localTime).TotalMinutes;

                            payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟\n"));
                            payloads.Add(new UIForegroundPayload(0));
                        }
                    }

                end:
                    payloads.Add(new UIForegroundPayload(0));
                    payloads.Add(new TextPayload("\nPS: 本消息已复制到粘贴板\nPSS:以上数据仅供参考"));

                    var chatMessage = payloads.Where(payload => payload.Type == PayloadType.RawText)
                                              .Aggregate<Payload, string>(null, (current, payload) => current + ((TextPayload)payload).Text);

                    payloads.Add(new UIForegroundPayload(0));

                    Plugin.Features.Counter.RemoveInstance(instance);
                    Plugin.Print(payloads);

                    ImGui.SetClipboardText(chatMessage);

                    return;
                }
                case "Broadcast":
                {
                    Plugin.Print($"广播消息: {result.Message}");
                    return;
                }
                case "ChangeArea":
                {
                    var time     = DateTimeOffset.FromUnixTimeSeconds(result.Time).ToLocalTime();
                    var instance = Plugin.Managers.Data.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);
                    Plugin.Print(new List<Payload>
                    {
                            new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                            new TextPayload($"{instance}"),
                            new UIForegroundPayload(0),
                            new TextPayload("上一次尝试触发的时间: "),
                            new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                            new TextPayload($"{time.DateTime.ToShortDateString()} {time.DateTime.ToShortTimeString()}"),
                            new UIForegroundPayload(0)
                    });
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error(exception, "Exception from Ws_MessageReceived.");
        }
    }

    private static string EncodeNonAsciiCharacters(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);

        var sb = new StringBuilder();
        foreach (var c in bytes)
        {
            var encodedValue = ((int)c).ToString("x2").ToUpper() + " ";
            sb.Append(encodedValue);
        }

        return sb.ToString();
    }
}