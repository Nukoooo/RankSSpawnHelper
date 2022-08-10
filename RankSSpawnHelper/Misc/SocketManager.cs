using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Logging;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Features;
using WatsonWebsocket;

namespace RankSSpawnHelper.Misc;

public class SocketManager : IDisposable
{
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

    public SocketManager()
    {
        Task.Factory.StartNew(TryReconnect, TaskCreationOptions.LongRunning);

        Task.Run(async () =>
        {
            await Task.Delay(3000);
            Connect(_url);
        });
    }

    public void Dispose()
    {
        Service.Configuration._trackRangeMode = _oldRangeModeState;

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

        Service.ChatGui.PrintChat(new XivChatEntry
        {
            Message = "成功连接到服务器！目前联网仍处于测试阶段，如果有问题或者意见可以在鸟区/猫区散触群@winter\n或者到Github上开Issue: https://github.com/NukoOoOoOoO/RankSSpawnHelper/issues/new",
            Name = "NukoOoOoOoO",
            Type = XivChatType.TellIncoming
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

                    var chatMessage = (result.failed ? "不好啦！" : "太好啦！") + result.instance + (result.failed ? "寄啦！\n" : "出货啦！\n");
                    chatMessage += (result.failed ? "寄时:" : "出时:") + $" {localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n";
                    chatMessage += $"计数总数: {result.total}\n";
                    chatMessage += "计数详情:\n";
                    foreach (var (k, v) in result.counter)
                        chatMessage += $"  {k}: {v}\n";
                    /*
                    foreach (var (k, v) in userCounter)
                        chatMessage += $"  {k}: {v}\n";
                    */
                    if (result.failed)
                        chatMessage += $"\n喊寄的人: {result.leader}";

                    Service.Counter.SetLastCounterMessage(chatMessage);
                    Service.Counter.ClearKey(result.instance);
                    Service.ChatGui.PrintChat(new XivChatEntry()
                    {
                        Message = chatMessage + "\nPS: 本消息已复制到粘贴板，如果需要再次提示清输入/glcm\nPSS:本区域的计数已清除\nPSSS:以上数据仅供参考",
                        Name = "Joe",
                        Type = result.failed ? XivChatType.Urgent : XivChatType.Shout
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