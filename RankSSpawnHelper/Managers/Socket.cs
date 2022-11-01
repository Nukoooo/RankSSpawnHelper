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
    internal class Socket : IDisposable
    {
        private readonly DalamudLinkPayload _linkPayload;

        private bool _oldRangeModeState;
        private const string ServerVersion = "v3";

        private List<string> _servers;
        private IWebsocketClient _client;

#if DEBUG
        private const string Url = "ws://127.0.0.1:8000";
#else
        private const string Url = "ws://124.220.161.157:8000";
#endif

        private string _userName = string.Empty;

        private const int ChatLinkCommandId = 694201337;

        public Socket()
        {
            _linkPayload = DalamudApi.Interface.AddChatLinkHandler(ChatLinkCommandId,
                                                                   (_, s) =>
                                                                   {
                                                                       var link = s.TextValue.Replace($"{(char)0x00A0}", "").Replace("\n", "").Replace("\r", "");
                                                                       Util.OpenLink(link);
                                                                   });

            Task.Run(async () =>
                     {
                         while (_userName == string.Empty)
                         {
                             _userName = Plugin.Managers.Data.Player.GetLocalPlayerName();

                             await Task.Delay(500);
                         }

                         _client = new WebsocketClient(new Uri(Url), () =>
                                                                     {
                                                                         var client = new ClientWebSocket
                                                                                      {
                                                                                          Options = { KeepAliveInterval = TimeSpan.FromSeconds(8) }
                                                                                      };
                                                                         PluginLog.Debug($"Setting header. {_userName}");
                                                                         client.Options.SetRequestHeader("ranks-spawn-helper-user", EncodeNonAsciiCharacters(_userName));
                                                                         client.Options.SetRequestHeader("server-version", ServerVersion);
                                                                         client.Options.SetRequestHeader("user-type", "Dalamud");
                                                                         return client;
                                                                     });

                         _client.ReconnectTimeout      = TimeSpan.FromSeconds(16);
                         _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(16);
                         _client.ReconnectionHappened.Subscribe(OnReconntion);
                         _client.MessageReceived.Subscribe(OnMessageReceived);

                         _servers = Plugin.Managers.Data.GetServers();

                         await _client.Start();
                     });
        }

        public void Dispose()
        {
            Plugin.Configuration.TrackRangeMode = _oldRangeModeState;
            DalamudApi.Interface.RemoveChatLinkHandler(ChatLinkCommandId);

            _client.Dispose();

            GC.SuppressFinalize(this);
        }

#if DEBUG
        public void Connect(string url)
        {
            if (_client == null || url == string.Empty)
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
                             PluginLog.Debug(e, "Exception in Managers::Socket::Connect()");
                         }
                     });
        }
#endif
        public bool Connected()
        {
            return _client != null && _client.IsRunning;
        }

#if DEBUG
        public async void Disconnect()
        {
            if (!Connected())
                return;

            await _client.Stop(WebSocketCloseStatus.NormalClosure, "Disconnection");
        }
#else
        public async void Reconnect()
        {
            await _client.Reconnect();
        }
#endif

        public void SendMessage(NetMessage message)
        {
            if (!Connected())
                return;

            var str = JsonConvert.SerializeObject(message);
            _client.Send(str);
            PluginLog.Debug($"Managers::Socket::SendMessage: {str}");
        }

        private void OnReconntion(ReconnectionInfo args)
        {
            _oldRangeModeState                  = Plugin.Configuration.TrackRangeMode;
            Plugin.Configuration.TrackRangeMode = false;

            if (args.Type == ReconnectionType.Initial && !Plugin.Configuration.TrackerNoNotification)
            {
                DalamudApi.ChatGui.PrintChat(new XivChatEntry
                                             {
                                                 Message = new SeString(new List<Payload>
                                                                        {
                                                                            new TextPayload("成功连接到服务器！如果有问题或者意见可以到Github上开Issue:"),
                                                                            new UIForegroundPayload(527),
                                                                            _linkPayload,
                                                                            new TextPayload("https://github.com/NukoOoOoOoO/DalamudPlugins/issues/new"),
                                                                            RawPayload.LinkTerminator,
                                                                            new UIForegroundPayload(0)
                                                                        }),
                                                 Type = XivChatType.CustomEmote
                                             });
            }

            var localTracker = Plugin.Features.Counter.GetLocalTrackers();

            if (localTracker == null || localTracker.Count == 0 || !DalamudApi.ClientState.LocalPlayer)
                return;

            var list = localTracker.Select(t => new NetMessage.Tracker { Data = t.Value.counter, Time = t.Value.startTime, Instance = t.Key, TerritoryId = t.Value.territoryId }).ToList();
            var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();

            SendMessage(new NetMessage
                        {
                            Type            = "NewConnection",
                            User            = Plugin.Managers.Data.Player.GetLocalPlayerName(),
                            CurrentInstance = currentInstance,
                            Trackers        = list,
                            TerritoryId     = DalamudApi.ClientState.TerritoryType
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

            PluginLog.Debug($"Managers::Socket::OnMessageReceived. {msg}");

            if (msg.StartsWith("Error:"))
            {
                DalamudApi.ChatGui.PrintError($"[S怪触发] {msg}");
                return;
            }

            if (!msg.StartsWith("{"))
                return;

            try
            {
                var result = JsonConvert.DeserializeObject<ReceivedMessage>(msg);

                switch (result.Type)
                {
                    case "counter":
                    {
                        foreach (var (key, value) in result.Counter)
                        {
                            Plugin.Features.Counter.UpdateNetworkedTracker(result.Instance, key, value, result.Time, result.TerritoryId);
                        }

                        return;
                    }
                    case "ggnore":
                    {
                        var localTime = DateTimeOffset.FromUnixTimeSeconds(result.Time).LocalDateTime;

                        var color = (ushort)(result.Failed ? Plugin.Configuration.FailedMessageColor : Plugin.Configuration.SpawnedMessageColor);
                        var message = (result.Failed ? $"不好啦！ {result.Instance}寄啦！\n寄时: " : $"太好啦！{result.Instance}出货啦！\n出时: ") +
                                      $"{localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n计数总数: {result.Total}\n计数详情:\n";

                        var payloads = new List<Payload>
                                       {
                                           new UIForegroundPayload(color),
                                           new TextPayload(message),
                                           new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor)
                                       };

                        foreach (var (k, v) in result.Counter)
                        {
                            payloads.Add(new TextPayload($"    {k}: {v}\n"));
                        }

                        payloads.Add(new UIForegroundPayload(0));

                        payloads.Add(new TextPayload("人数详情:\n"));
                        payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));

                        foreach (var (k, v) in result.UserCounter)
                        {
                            payloads.Add(new TextPayload($"    {k}: {v}\n"));
                        }

                        payloads.Add(new UIForegroundPayload(0));

                        if (result.Failed)
                        {
                            payloads.Add(new TextPayload("\n喊寄的人: "));
                            payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                            payloads.Add(new TextPayload(result.Leader));
                            payloads.Add(new UIForegroundPayload(0));
                        }

                        if (result.HasResult)
                        {
                            var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.ExpectMinTime;
                            if (isSpawnable)
                            {
                                payloads.Add(new TextPayload("\n当前可触发概率: "));
                                payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                                payloads.Add(new TextPayload(
                                                             $"{100 * ((result.Time - result.ExpectMinTime) / (double)(result.ExpectMaxTime - result.ExpectMinTime)):F1}%"));
                                payloads.Add(new UIForegroundPayload(0));
                            }
                            else
                            {
                                payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                                payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                                var minTime = DateTimeOffset.FromUnixTimeSeconds(result.ExpectMinTime);
                                var delta   = (minTime - localTime).TotalMinutes;

                                payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
                                payloads.Add(new UIForegroundPayload(0));
                            }
                        }

                        payloads.Add(new TextPayload("\nPS: 本消息已复制到粘贴板\nPSS:以上数据仅供参考"));

                        var chatMessage = payloads.Where(payload => payload.Type == PayloadType.RawText)
                                                  .Aggregate<Payload, string>(null, (current, payload) => current + ((TextPayload)payload).Text);
                        Plugin.Managers.Data.Player.SetLastAttempMessage(new Tuple<SeString, string>(new SeString(payloads), chatMessage));

                        payloads.Add(new TextPayload("\nPSSS:本区域的计数器已清零"));
                        payloads.Add(new UIForegroundPayload(0));

                        Plugin.Features.Counter.RemoveInstance(result.Instance);
                        DalamudApi.ChatGui.PrintChat(new XivChatEntry
                                                     {
                                                         Message = new SeString(payloads)
                                                     });

                        ImGui.SetClipboardText(chatMessage);

                        return;
                    }
                    case "Broadcast":
                    {
                        var message         = result.Message;
                        var isAttempMessage = message.Where(i => i == '@').ToList().Count == 2 && (message.EndsWith("出货了") || message.EndsWith("寄了"));
                        var serverName      = message[..message.IndexOf('@')];
                        var shouldPrint = Plugin.Configuration.EnableAttemptMessagesFromOtherDcs &&
                                          ((_servers.Contains(serverName) && !Plugin.Configuration.ReceiveAttempMessageFromOtherDc) || Plugin.Configuration.ReceiveAttempMessageFromOtherDc);
                        if (isAttempMessage && !shouldPrint)
                            return;

                        DalamudApi.ChatGui.Print(new SeString(new List<Payload>
                                                              {
                                                                  new UIForegroundPayload(1),
                                                                  new TextPayload("[S怪触发]"),
                                                                  new UIForegroundPayload(35),
                                                                  new TextPayload($"广播消息: {result.Message}"),
                                                                  new UIForegroundPayload(0),
                                                                  new UIForegroundPayload(0)
                                                              }));


                        return;
                    }
                    case "ChangeArea":
                    {
                        var time = DateTimeOffset.FromUnixTimeSeconds(result.Time);
                        DalamudApi.ChatGui.Print(new SeString(new List<Payload>
                                                              {
                                                                  new UIForegroundPayload(1),
                                                                  new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                                                                  new TextPayload($"{result.Instance}"),
                                                                  new UIForegroundPayload(0),
                                                                  new TextPayload("上一次尝试触发的时间: "),
                                                                  new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                                                                  new TextPayload($"{time.DateTime.ToShortDateString()} {time.DateTime.ToShortTimeString()}"),
                                                                  new UIForegroundPayload(0),
                                                                  new UIForegroundPayload(0)
                                                              }
                                                             ));
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
}