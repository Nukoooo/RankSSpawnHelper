using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Misc;
using RankSSpawnHelper.Models;
using WatsonWebsocket;

namespace RankSSpawnHelper.Managers;

public class SocketManager : IDisposable
{
    private readonly DalamudLinkPayload _linkPayload;

    private readonly CancellationTokenSource _eventLoopTokenSource = new();
    private bool _oldRangeModeState;
    private WatsonWsClient _ws;
    private bool _changeHost;

#if DEBUG
    private string _url = "ws://127.0.0.1:8000";
#else
    private string _url = "ws://47.106.224.112:8000";
#endif

    private string _userName = string.Empty;

    private const int ChatLinkCommandId = 694201337;

    public SocketManager()
    {
        Connect(_url);

        _linkPayload = Service.Interface.AddChatLinkHandler(ChatLinkCommandId, LinkHandler);
        Task.Factory.StartNew(TryReconnect, TaskCreationOptions.LongRunning).ContinueWith(task =>
        {
            if (task.Exception == null) return;
            var flattened = task.Exception.Flatten();

            flattened.Handle(ex =>
            {
                PluginLog.Error(ex, "Error in SocketManager");
                return true;
            });
        }, TaskContinuationOptions.LongRunning);
    }

    private static void LinkHandler(uint id, SeString message)
    {
        var link = message.TextValue.Replace($"{(char)0x00A0}", "").Replace("\n", "").Replace("\r", "");
        Util.OpenLink(link);
    }

    public void Dispose()
    {
        Service.Configuration._trackRangeMode = _oldRangeModeState;
        Service.Interface.RemoveChatLinkHandler(ChatLinkCommandId);

        _eventLoopTokenSource.Cancel();
        _eventLoopTokenSource.Dispose();

        if (_ws != null)
        {
            _ws.MessageReceived -= Ws_MessageReceived;
            _ws.ServerConnected -= Ws_ServerConnected;
            _ws.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task TryReconnect()
    {
        var token = _eventLoopTokenSource.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_userName == string.Empty)
                {
                    await Task.Delay(20000, token);
                    continue;
                }

                if (_ws == null || _ws.Connected || _changeHost)
                {
                    await Task.Delay(20000, token);
                    continue;
                }

                try
                {
                    _ws.Dispose();
                    _ws = new WatsonWsClient(new Uri(_url)).ConfigureOptions(Options);

                    _ws.ServerConnected += Ws_ServerConnected;
                    _ws.MessageReceived += Ws_MessageReceived;
                    await _ws.StartWithTimeoutAsync(10, token);
                }
                catch (WebSocketException e)
                {
                    PluginLog.Debug(e.Message);
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (AggregateException)
            {
            }
            catch (WebSocketException e)
            {
                PluginLog.Debug(e.Message);
            }
        }
    }

    private void Ws_ServerConnected(object sender, EventArgs e)
    {
        var key = Service.Counter.GetCurrentInstance();
        var tracker = Service.Counter.GetLocalTracker();

        _oldRangeModeState = Service.Configuration._trackRangeMode;
        Service.Configuration._trackRangeMode = false;

        if (!Service.Configuration._trackerNoNotification)
        {
            Service.ChatGui.PrintChat(new XivChatEntry
            {
                Message = new SeString(new List<Payload>
                {
                    new TextPayload("成功连接到服务器！目前联网仍处于测试阶段，如果有问题或者意见可以到Github上开Issue:"),
                    new UIForegroundPayload(527),
                    _linkPayload,
                    new TextPayload("https://github.com/NukoOoOoOoO/RankSSpawnHelper/issues/new"),
                    RawPayload.LinkTerminator,
                    new UIForegroundPayload(0),
                }),
                Type = XivChatType.CustomEmote,
            });
        }

        if (tracker.Count == 0 || !Service.ClientState.LocalPlayer)
            return;

        var list = tracker.Select(t => new NetMessage.Tracker { Data = t.Value.counter, Time = t.Value.startTime, Instance = t.Key, TerritoryId = t.Value.territoryId }).ToList();

        var msg = new NetMessage
        {
            User = Service.ClientState.LocalPlayer?.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString,
            CurrentInstance = key,
            Type = "NewConnection",
            Trackers = list,
            TerritoryId = Service.ClientState.TerritoryType,
        };

        var j = JsonConvert.SerializeObject(msg, Formatting.None);
        SendMessage(j);
    }

    private static void Ws_MessageReceived(object sender, MessageReceivedEventArgs e)
    {
        if (e.MessageType == WebSocketMessageType.Binary)
            Service.ChatGui.Print("IsBinary");
        var msg = e.MessageType switch
        {
            WebSocketMessageType.Binary => Encoding.UTF8.GetString(e.Data.Array.Decompress()),
            WebSocketMessageType.Text => Encoding.UTF8.GetString(e.Data),
            _ => string.Empty,
        };

        if (msg == string.Empty)
            return;

        if (msg.StartsWith("Error:"))
        {
            Service.ChatGui.PrintError(msg);
            return;
        }

        if (msg.StartsWith("广播消息:"))
        {
            Service.ChatGui.PrintChat(new XivChatEntry()
            {
                Message = msg[5..],
                Name = "S怪触发小助手",
                Type = XivChatType.Urgent,
            });
            return;
        }

        try
        {
            var result = JsonConvert.DeserializeObject<ReceivedMessage>(msg);

            switch (result.Type)
            {
                case "counter":
                {
                    foreach (var (key, value) in result.Counter)
                    {
                        Service.Counter.SetValue(result.Instance, key, value, result.Time);
                    }

                    break;
                }
                case "ggnore":
                {
                    var localTime = DateTimeOffset.FromUnixTimeSeconds(result.Time).LocalDateTime;

                    var color = (ushort)(result.Failed ? Service.Configuration._failedMessageColor : Service.Configuration._spawnedMessageColor);
                    var message = (result.Failed ? $"不好啦！ {result.Instance}寄啦！\n寄时: " : $"太好啦！{result.Instance}出货啦！\n出时: ") +
                                  $"{localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n计数总数: {result.Total}\n计数详情:\n";

                    var payloads = new List<Payload>
                    {
                        new UIForegroundPayload(color),
                        new TextPayload(message),
                        new UIForegroundPayload((ushort)Service.Configuration._highlightColor),
                    };

                    foreach (var (k, v) in result.Counter)
                    {
                        payloads.Add(new TextPayload($"    {k}: {v}\n"));
                    }

                    payloads.Add(new TextPayload("人数详情:\n"));

                    foreach (var (k, v) in result.UserCounter)
                    {
                        payloads.Add(new TextPayload($"    {k}: {v}\n"));
                    }

                    payloads.Add(new UIForegroundPayload(0));

                    if (result.Failed)
                    {
                        payloads.Add(new TextPayload("\n喊寄的人: "));
                        payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));
                        payloads.Add(new TextPayload(result.Leader));
                        payloads.Add(new UIForegroundPayload(0));
                    }

                    if (result.HasResult)
                    {
                        var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.ExpectMinTime;
                        if (isSpawnable)
                        {
                            payloads.Add(new TextPayload("\n当前可触发概率: "));
                            payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));
                            payloads.Add(new TextPayload(
                                $"{100 * ((result.Time - result.ExpectMinTime) / (double)(result.ExpectMaxTime - result.ExpectMinTime)):F1}%"));
                            payloads.Add(new UIForegroundPayload(0));
                        }
                        else
                        {
                            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                            payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));
                            var minTime = DateTimeOffset.FromUnixTimeSeconds(result.ExpectMinTime);
                            var delta = (minTime - localTime).TotalMinutes;

                            payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
                            payloads.Add(new UIForegroundPayload(0));
                        }
                    }

                    payloads.Add(new TextPayload("\nPS: 本消息已复制到粘贴板，如果需要再次提示清输入/glcm\nPSS:以上数据仅供参考"));

                    var chatMessage = payloads.Where(payload => payload.Type == PayloadType.RawText)
                        .Aggregate<Payload, string>(null, (current, payload) => current + ((TextPayload)payload).Text);
                    Service.Counter.SetLastCounterMessage(new SeString(payloads), chatMessage);

                    payloads.Add(new TextPayload("\nPSSS:本区域的计数器已清零"));
                    payloads.Add(new UIForegroundPayload(0));

                    Service.Counter.ClearKey(result.Instance);
                    Service.ChatGui.PrintChat(new XivChatEntry
                    {
                        Message = new SeString(payloads),
                    });

                    ImGui.SetClipboardText(chatMessage);
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error(exception, "Exception from Ws_MessageReceived.");
        }
    }

    public void Disconnect()
    {
        if (!Connected()) return;

        Task.Run(async () =>
        {
            await _ws?.StopAsync();
            Service.Configuration._trackRangeMode = _oldRangeModeState;
        });
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

    public void Connect(string url)
    {
        Disconnect();

        Task.Run(async () =>
        {
            _changeHost = true;
            _url = url;
            _ws?.Dispose();

            while (_userName == string.Empty)
            {
                if (Service.ClientState.LocalPlayer == null)
                    await Task.Delay(1000);
                else
                    _userName = Service.ClientState.LocalPlayer?.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString;
            }

            _ws = new WatsonWsClient(new Uri(url)).ConfigureOptions(Options);

            _ws.ServerConnected += Ws_ServerConnected;
            _ws.MessageReceived += Ws_MessageReceived;

            var result = await _ws.StartWithTimeoutAsync(10);
            if (result)
                PluginLog.Debug("Connected to websocket server");
            _changeHost = false;

            return Task.CompletedTask;
        });
    }

    private void Options(ClientWebSocketOptions obj)
    {
        obj.KeepAliveInterval = new TimeSpan(30);
        obj.SetRequestHeader("RankSSpawnHelperUser", EncodeNonAsciiCharacters(_userName));
        obj.SetRequestHeader("ServerVersion", "v2");
    }

    public void SendMessage(string msg)
    {
        if (_ws == null || msg == string.Empty) return;

        var originalBytes = Encoding.UTF8.GetBytes(msg);
        var compressedBytes = originalBytes.Compress();

        if (compressedBytes.Length < originalBytes.Length)
            _ws.SendAsync(compressedBytes);
        else
            _ws.SendAsync(msg);
    }

    public bool Connected() => _ws is { Connected: true };
}