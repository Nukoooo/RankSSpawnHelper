using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;
using Websocket.Client;
using Websocket.Client.Models;

namespace RankSSpawnHelper.Managers
{
    public class SocketManager : IDisposable
    {
        private readonly DalamudLinkPayload _linkPayload;

        private bool _oldRangeModeState;
        private const string ServerVersion = "v3";

        private IWebsocketClient _client;

#if DEBUG
        private readonly string _url = "ws://127.0.0.1:8000";
#else
    private string _url = "ws://47.106.224.112:8000";
#endif

        private string _userName = string.Empty;

        private const int ChatLinkCommandId = 694201337;

        public SocketManager()
        {
            _linkPayload = Service.Interface.AddChatLinkHandler(ChatLinkCommandId, LinkHandler);

            Task.Run(async () =>
                     {
                         while (_userName == string.Empty)
                         {
                             if (Service.ClientState.LocalPlayer != null)
                             {
                                 _userName = $"{Service.ClientState.LocalPlayer.Name}@{Service.ClientState.LocalPlayer.HomeWorld.GameData.Name}";
                                 break;
                             }

                             await Task.Delay(500);
                         }

                         _client = new WebsocketClient(new Uri(_url), () =>
                                                                      {
                                                                          var client = new ClientWebSocket
                                                                                       {
                                                                                           Options = { KeepAliveInterval = TimeSpan.FromSeconds(5) }
                                                                                       };
                                                                          PluginLog.Warning($"Setting header. {_userName}");
                                                                          client.Options.SetRequestHeader("RankSSpawnHelperUser", EncodeNonAsciiCharacters(_userName));
                                                                          client.Options.SetRequestHeader("ServerVersion", ServerVersion);
                                                                          return client;
                                                                      });

                         _client.ReconnectTimeout = TimeSpan.FromSeconds(10);
                         _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(10);
                         _client.ReconnectionHappened.Subscribe(OnReconntion);
                         _client.MessageReceived.Subscribe(OnMessageReceived);

                         /*
                             _client.DisconnectionHappened.Subscribe(info => { PluginLog.Debug($"Disconnection happended. {info.Type}"); });
                         */

                         await _client.Start();
                     });
        }

        private void OnReconntion(ReconnectionInfo args)
        {
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
                                                                         new UIForegroundPayload(0)
                                                                     }),
                                              Type = XivChatType.CustomEmote
                                          });
            }

            var tracker = Service.Counter.GetLocalTracker();

            if (tracker == null || tracker.Count == 0 || !Service.ClientState.LocalPlayer)
                return;

            var list = tracker.Select(t => new NetMessage.Tracker { Data = t.Value.counter, Time = t.Value.startTime, Instance = t.Key, TerritoryId = t.Value.territoryId }).ToList();
            var key = Service.Counter.GetCurrentInstance();

            var msg = new NetMessage
                      {
                          User = Service.ClientState.LocalPlayer?.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString,
                          CurrentInstance = key,
                          Type = "NewConnection",
                          Trackers = list,
                          TerritoryId = Service.ClientState.TerritoryType
                      };

            SendMessage(JsonConvert.SerializeObject(msg, Formatting.None));
        }

        private static void OnMessageReceived(ResponseMessage args)
        {
            if (args.MessageType != WebSocketMessageType.Binary)
                return;

            // ping pong
            if (args.Binary.Length == 4)
                return;

            var msg = Encoding.UTF8.GetString(args.Binary);

            if (msg.StartsWith("Error:"))
            {
                Service.ChatGui.PrintError(msg);
                return;
            }

            PluginLog.Debug($"Receive message. {msg}");

            try
            {
                var result = JsonConvert.DeserializeObject<ReceivedMessage>(msg);

                switch (result.Type)
                {
                    case "counter":
                    {
                        foreach (var (key, value) in result.Counter)
                        {
                            Service.Counter.SetValue(result.Instance, key, value, result.Time, result.TerritoryId);
                        }

                        return;
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
                                           new UIForegroundPayload((ushort)Service.Configuration._highlightColor)
                                       };

                        foreach (var (k, v) in result.Counter)
                        {
                            payloads.Add(new TextPayload($"    {k}: {v}\n"));
                        }

                        payloads.Add(new UIForegroundPayload(0));

                        payloads.Add(new TextPayload("人数详情:\n"));
                        payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));

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
                                                      Message = new SeString(payloads)
                                                  });

                        ImGui.SetClipboardText(chatMessage);

                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                PluginLog.Error(exception, "Exception from Ws_MessageReceived.");
            }
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

            _client.Dispose();

            GC.SuppressFinalize(this);
        }

        public async void Disconnect()
        {
            if (!Connected()) return;

            await _client.Stop(WebSocketCloseStatus.NormalClosure, "Disconnection");
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
            if (_client == null)
                return;

            Task.Run(async () =>
                     {
                         try
                         {
                             _client.Url = new Uri(url);
                             if (_client.IsStarted)
                                 await _client.Reconnect();
                             else
                                 await _client.Start();
                         }
                         catch (Exception e)
                         {
                             PluginLog.Debug(e, "Exception in SocketManager::Connect");
                         }
                     });
        }

        public void SendMessage(string msg)
        {
            _client.Send(msg);
        }

        public bool Connected()
        {
            return _client != null && _client.IsRunning;
        }
    }
}