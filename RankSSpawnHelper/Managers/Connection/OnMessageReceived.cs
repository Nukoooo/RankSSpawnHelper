using System.Net.WebSockets;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using Newtonsoft.Json;
using Websocket.Client;

namespace RankSSpawnHelper.Managers;

internal partial class ConnectionManager
{
    private void OnMessageReceived(ResponseMessage args)
    {
        if (args.MessageType != WebSocketMessageType.Binary)
        {
            return;
        }

        if (args.Binary is not { } binary || binary.Length == 0)
        {
            return;
        }

        var msg = Encoding.UTF8.GetString(binary);

        if (!msg.StartsWith('{'))
        {
            DalamudApi.PluginLog.Error($"Managers::Socket::OnMessageReceived. Not a valid json format message. {msg}");

            return;
        }

        DalamudApi.PluginLog.Debug($"Managers::Socket::OnMessageReceived. {msg}");

        try
        {
            var result = JsonConvert.DeserializeObject<ReceivedMessage>(msg);

            if (result == null)
            {
                return;
            }

            switch (result.Type)
            {
                case "Error":
                {
                    var message = result.Message;

                    Utils.Print(new List<Payload>
                    {
                        new UIForegroundPayload(518),
                        new TextPayload($"Error: {message}"),
                        new UIForegroundPayload(0),
                    });

                    break;
                }
                case "Attempt":
                {
                    var instance = _dataManager.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);
                    _counter.RemoveInstance(instance);

                    if (_configuration.AttemptMessageFromServer == AttemptMessageFromServerType.Off)
                    {
                        return;
                    }

                    if (DalamudApi.Condition[ConditionFlag.BoundByDuty])
                    {
                        return;
                    }

                    if (_configuration.AttemptMessageFromServer == AttemptMessageFromServerType.CurrentDataCenter
                        && _dataManager.IsFromOtherDataCenter(result.WorldId))
                    {
                        return;
                    }

                    var message = $"{instance} {(result.Failed ? "寄了" : "出货了")}. 概率: ";

                    if (result.StartPercent != null)
                    {
                        message += $"{result.StartPercent:F2}% / ";
                    }

                    message += $"{result.Percent:F2}%";

                    Utils.Print(message);

                    break;
                }
                case "Counter":
                {
                    var instance = _dataManager.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);

                    foreach (var (key, value) in result.Counter)
                    {
                        var isItem = result.TerritoryId is 814 or 400 or 961 or 813;

                        var keyName = isItem
                            ? _dataManager.GetItemName(key)
                            : result.TerritoryId == 621
                                ? "扔垃圾"
                                : _dataManager.GetNpcName(key);

                        _counter.UpdateNetworkedTracker(instance,
                                                        keyName,
                                                        value,
                                                        result.Time,
                                                        result.TerritoryId);
                    }

                    return;
                }
                case "ggnore":
                {
                    if (_configuration.AttemptMessage <= AttemptMessageType.Off)
                    {
                        return;
                    }

                    var localTime = DateTimeOffset.FromUnixTimeSeconds(result.Time)
                                                  .LocalDateTime;

                    var instance = _dataManager.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);

                    var color = (ushort) (result.Failed
                        ? _configuration.FailedMessageColor
                        : _configuration.SpawnedMessageColor);

                    var message = (result.Failed
                                      ? $"不好啦！ {instance}寄啦！\n寄时: "
                                      : $"太好啦！{instance}出货啦！\n出时: ")
                                  + $"{localTime.ToShortDateString()}/{localTime.ToShortTimeString()}\n计数总数: {result.Total}\n计数详情:\n";

                    var payloads = new List<Payload>
                    {
                        new UIForegroundPayload(color),
                        new TextPayload(message),
                        new UIForegroundPayload((ushort) _configuration.HighlightColor),
                    };

                    var isItem = result.TerritoryId is 814 or 400 or 961 or 813;

                    foreach (var (k, v) in result.Counter)
                    {
                        string name;

                        if (isItem)
                        {
                            name = _dataManager.GetItemName(k);
                        }
                        else if (result.TerritoryId == 621)
                        {
                            name = "扔垃圾";
                        }
                        else
                        {
                            name = _dataManager.GetNpcName(k);
                        }

                        payloads.Add(new TextPayload($"    {name}: {v}\n"));
                    }

                    payloads.Add(new UIForegroundPayload(0));

                    if (_configuration.AttemptMessage == AttemptMessageType.Basic)
                    {
                        goto end;
                    }

                    payloads.Add(new TextPayload("人数详情:\n"));
                    payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));

                    foreach (var userCounter in result.UserCounter)
                    {
                        payloads.Add(new TextPayload($"    {userCounter.UserName}: {userCounter.TotalCount}\n"));

                        if (userCounter.Counter == null)
                        {
                            continue;
                        }

                        foreach (var (k, v) in userCounter.Counter)
                        {
                            string name;

                            if (isItem)
                            {
                                name = _dataManager.GetItemName(k);
                            }
                            else if (result.TerritoryId == 621)
                            {
                                name = "扔垃圾";
                            }
                            else
                            {
                                name = _dataManager.GetNpcName(k);
                            }

                            payloads.Add(new TextPayload($"        {name}: {v}\n"));
                        }
                    }

                    payloads.Add(new UIForegroundPayload(0));

                    if (result.Failed && result.Leader != "null")
                    {
                        payloads.Add(new TextPayload("\n喊寄的人: "));
                        payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));
                        payloads.Add(new TextPayload(result.Leader));
                        payloads.Add(new UIForegroundPayload(0));
                    }

                    if (result.HasResult)
                    {
                        var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() >= result.ExpectMinTime;

                        if (isSpawnable)
                        {
                            payloads.Add(new TextPayload("\n当前可触发概率: "));
                            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));

                            payloads.Add(new
                                             TextPayload($"{100 * ((result.Time - result.ExpectMinTime) / (double) (result.ExpectMaxTime - result.ExpectMinTime)):F2}%\n"));
                        }
                        else
                        {
                            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));
                            var minTime = DateTimeOffset.FromUnixTimeSeconds(result.ExpectMinTime);
                            var delta   = (minTime - localTime).TotalMinutes;

                            payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟\n"));
                        }

                        payloads.Add(new UIForegroundPayload(0));
                    }

                end:
                    payloads.Add(new UIForegroundPayload(0));
                    payloads.Add(new TextPayload("\nPS: 本消息已复制到粘贴板\nPSS:以上数据仅供参考"));

                    var chatMessage = payloads.Where(payload => payload.Type == PayloadType.RawText)
                                              .Aggregate<Payload, string>(null!,
                                                                          (current, payload)
                                                                              => current + ((TextPayload) payload).Text);

                    payloads.Add(new UIForegroundPayload(0));

                    _counter.RemoveInstance(instance);
                    Utils.Print(payloads);

                    ImGui.SetClipboardText(chatMessage);

                    return;
                }
                case "Broadcast":
                {
                    Utils.Print($"广播消息: {result.Message}");

                    return;
                }
                case "ChangeArea":
                {
                    var time = DateTimeOffset.FromUnixTimeSeconds(result.Time)
                                             .ToLocalTime();

                    var instance = _dataManager.FormatInstance(result.WorldId, result.TerritoryId, result.InstanceId);

                    Utils.Print(new List<Payload>
                    {
                        new UIForegroundPayload((ushort) _configuration.HighlightColor),
                        new TextPayload($"{instance}"),
                        new UIForegroundPayload(0),
                        new TextPayload("上一次尝试触发的时间: "),
                        new UIForegroundPayload((ushort) _configuration.HighlightColor),
                        new TextPayload($"{time.DateTime.ToShortDateString()} {time.DateTime.ToShortTimeString()}"),
                        new UIForegroundPayload(0),
                    });

                    return;
                }
                case "TrackerList":
                {
                    /*Plugin.Windows.PluginWindow.SetTrackerData(result.TrackerData);*/

                    return;
                }
            }
        }
        catch (Exception exception)
        {
            DalamudApi.PluginLog.Error(exception, "Exception from Ws_MessageReceived.");
        }
    }
}
