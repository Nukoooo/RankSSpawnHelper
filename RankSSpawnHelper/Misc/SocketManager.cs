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
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Features;
using WatsonWebsocket;

namespace RankSSpawnHelper.Misc;

public class SocketManager : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly DalamudLinkPayload _linkPayload;

    private class Message
    {
        public string type { get; set; }
        public string instance { get; set; }
        public long time { get; set; }
        public Dictionary<string, int> counter { get; set; }

        // ggnore section
        public int total { get; set; }
        public bool failed { get; set; }
        public string leader { get; set; }
        public bool hasResult { get; set; }
        public long expectMinTime { get; set; }
        public ulong expectMaxTime { get; set; }
    }

    private readonly CancellationTokenSource _eventLoopTokenSource = new();
    private bool _oldRangeModeState;
    private WatsonWsClient ws;
    private bool _changeHost;
#if DEBUG
    private string _url = "ws://127.0.0.1:8000";
#else
    private string _url = "ws://47.106.224.112:8000";
#endif

    public SocketManager(DalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _linkPayload = _pluginInterface.AddChatLinkHandler(69420, LinkHandler);
        Task.Factory.StartNew(TryReconnect, TaskCreationOptions.LongRunning);

        Task.Run(async () =>
        {
            await Task.Delay(3000);
            Connect(_url);
        });
    }

    private static void LinkHandler(uint id, SeString message)
    {
        var link = message.TextValue.Replace($"{(char)0x00A0}", "").Replace("\n", "").Replace("\r", "");
        Util.OpenLink(link);
    }

    public void Dispose()
    {
        Service.Configuration._trackRangeMode = _oldRangeModeState;
        _pluginInterface.RemoveChatLinkHandler(69420);

        _eventLoopTokenSource.Cancel();
        _eventLoopTokenSource.Dispose();

        if (ws != null)
        {
            ws.MessageReceived -= Ws_MessageReceived;
            ws.ServerConnected -= Ws_ServerConnected;
            ws.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task TryReconnect()
    {
        var token = _eventLoopTokenSource.Token;

        while (!token.IsCancellationRequested)
            try
            {
                if (ws == null || ws.Connected || _changeHost)
                {
                    await Task.Delay(2000, token);
                    continue;
                }

                ws.Dispose();
                ws = new WatsonWsClient(new Uri(_url));
                ws.ServerConnected += Ws_ServerConnected;
                ws.MessageReceived += Ws_MessageReceived;
                await ws.StartAsync();
                await Task.Delay(5000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // ignored
            }
    }

    private void Ws_ServerConnected(object sender, EventArgs e)
    {
        var key = Service.Counter.GetCurrentInstance();
        var tracker = Service.Counter.GetTracker();

        _oldRangeModeState = Service.Configuration._trackRangeMode;
        Service.Configuration._trackRangeMode = false;

        if (!Service.Configuration._trackerNoNotification)
            Service.ChatGui.PrintChat(new XivChatEntry
            {
                Message = new SeString(new List<Payload>
                {
                    new TextPayload("成功连接到服务器！目前联网仍处于测试阶段，如果有问题或者意见可以到Github上开Issue:"),
                    new UIForegroundPayload(527),
                    _linkPayload,
                    new TextPayload("https://github.com/NukoOoOoOoO/RankSSpawnHelper/issues/new"),
                    RawPayload.LinkTerminator,
                    new UIForegroundPayload(0)
                }),
                Type = XivChatType.CustomEmote
            });

        // 如果计数器是空的，或者localplayer无效那就没有发送的必要
        if (tracker.Count == 0 || !Service.ClientState.LocalPlayer)
            return;

        var list = tracker.Select(t => new NewConnectionMessage.Tracker { data = t.Value.counter, time = t.Value.startTime, instance = t.Key }).ToList();

        var msg = new NewConnectionMessage
        {
            user = Service.ClientState.LocalPlayer.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString,
            currentInstance = key,
            type = "newConnection",
            trackers = list
        };

        var j = JsonConvert.SerializeObject(msg, Formatting.None);
        SendMessage(j);
    }

    private void Ws_MessageReceived(object sender, MessageReceivedEventArgs e)
    {
        var msg = Encoding.UTF8.GetString(e.Data);
        PluginLog.Debug($"Receive message: {msg}");

        if (msg.StartsWith("Error:"))
        {
            Service.ChatGui.PrintError(msg);
            return;
        }

        try
        {
            var result = JsonConvert.DeserializeObject<Message>(msg);

            switch (result.type)
            {
                case "counter":
                {
                    foreach (var (key, value) in result.counter) Service.Counter.SetValue(result.instance, key, value, result.time);
                    break;
                }
                case "ggnore":
                {
                    // var userCounter = json["userCounter"].ToObject<Dictionary<string, int>>();

                    var localTime = DateTimeOffset.FromUnixTimeSeconds(result.time).LocalDateTime;

                    var payloads = new List<Payload>
                    {
                        new UIForegroundPayload((ushort)(result.failed ? 518 : 59)),
                        new TextPayload((result.failed ? "不好啦！" : "太好啦！") + result.instance + (result.failed ? "寄啦！\n" : "出货啦！\n")),
                        new TextPayload((result.failed ? "寄时:" : "出时:") + $" {localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n"),
                        new TextPayload($"计数总数: {result.total}\n计数详情:\n"),
                        new UIForegroundPayload(71)
                    };
                    foreach (var (k, v) in result.counter)
                    {
                        payloads.Add(new TextPayload($"    {k}: {v}\n"));
                    }
                    payloads.Add(new UIForegroundPayload(0));
                    /*
                        foreach (var (k, v) in userCounter)
                            chatMessage += $"  {k}: {v}\n";
                    */

                    if (result.failed)
                    {
                        payloads.Add(new TextPayload("\n喊寄的人: "));
                        payloads.Add(new UIForegroundPayload(71));
                        payloads.Add(new TextPayload(result.leader));
                        payloads.Add(new UIForegroundPayload(0));
                    }

                    if (result.hasResult)
                    {
                        var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.expectMinTime;
                        if (isSpawnable)
                        {
                            payloads.Add(new TextPayload("\n当前可触发概率: "));
                            payloads.Add(new UIForegroundPayload(71));
                            payloads.Add(new TextPayload(
                                $"{100 * ((DateTimeOffset.Now.ToUnixTimeSeconds() - result.expectMinTime) / (long)(result.expectMaxTime - (ulong)result.expectMinTime)):F1}%"));
                            payloads.Add(new UIForegroundPayload(0));
                        }
                        else
                        {
                            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                            payloads.Add(new UIForegroundPayload(71));
                            var minTime = DateTimeOffset.FromUnixTimeSeconds(result.expectMinTime);
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

                    Service.Counter.ClearKey(result.instance);
                    Service.ChatGui.PrintChat(new XivChatEntry
                    {
                        Message = new SeString(payloads),
                        Type = XivChatType.Debug
                    });

                    ImGui.SetClipboardText(chatMessage);
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error("Exception from Ws_MessageReceived: " + exception);
        }
    }

    public void Disconnect()
    {
        if (!Connected()) return;

        Task.Run(async () =>
        {
            await ws?.StopAsync(WebSocketCloseStatus.NormalClosure, "Connect to other host");
            Service.Configuration._trackRangeMode = _oldRangeModeState;
        });
    }

    public void Connect(string url)
    {
        Disconnect();

        Task.Run(async () =>
        {
            _changeHost = true;
            _url = url;
            ws?.Dispose();

            ws = new WatsonWsClient(new Uri(url));
            ws.ServerConnected += Ws_ServerConnected;
            ws.MessageReceived += Ws_MessageReceived;

            await ws.StartAsync();
            await Task.Delay(500);
            if (Connected())
                PluginLog.Debug("Connected to websocket server");
            _changeHost = false;

            return Task.CompletedTask;
        });
    }

    public void SendMessage(string msg)
    {
        if (ws == null) return;

        if (ws.SendAsync(msg).Result) PluginLog.Debug("Successfully sent the message to server. msg: " + msg);
    }

    public bool Connected()
    {
        return ws is { Connected: true };
    }
}