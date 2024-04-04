using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper;

internal class Commands : IDisposable
{
    private readonly List<string> _clearTracker = new()
    {
        "/清除计数",
        "/clear_tracker",
    };

    private readonly List<string> _ggnore = new()
    {
        "/寄了",
        "/ggnore",
    };

    private readonly List<string> _fetchHuntMap = new()
    {
        "/fetch_huntmap",
        "/获取点位",
    };

    private readonly List<string> _clearPlayerSearch = new()
    {
        "/clear_player_search",
        "/清除玩家搜索",
    };

    public Commands()
    {
        foreach (var cmd in _clearTracker)
        {
            DalamudApi.CommandManager.AddHandler(cmd, new(ClearTrackers)
            {
                ShowInHelp  = true,
                HelpMessage = "清除计数器",
            });
        }

        foreach (var cmd in _ggnore)
        {
            DalamudApi.CommandManager.AddHandler(cmd, new(SendFailedAttempt)
            {
                ShowInHelp  = true,
                HelpMessage = "寄了",
            });
        }

        foreach (var cmd in _fetchHuntMap)
        {
            DalamudApi.CommandManager.AddHandler(cmd, new((_, _) => Plugin.Features.ShowHuntMap.FetchAndPrint())
            {
                ShowInHelp  = true,
                HelpMessage = "获取当前地图的点位",
            });
        }

        foreach (var cmd in _clearPlayerSearch)
        {
            DalamudApi.CommandManager.AddHandler(cmd, new((_, _) =>
                                                          {
                                                              if (!Plugin.Features.SearchCounter.ClearPlayerList())
                                                                  Plugin.Print("无法清除玩家搜索计数,因为当前正在搜索");
                                                          })
            {
                ShowInHelp  = true,
                HelpMessage = "清除玩家搜索计数",
            });
        }

        DalamudApi.CommandManager.AddHandler("/shelper", new((_, _) => Plugin.Windows.PluginWindow.IsOpen = true)
        {
            ShowInHelp  = true,
            HelpMessage = "打开设置菜单",
        });

    }

    public void Dispose()
    {
        foreach (var cmd in _clearTracker)
            DalamudApi.CommandManager.RemoveHandler(cmd);

        foreach (var cmd in _ggnore)
            DalamudApi.CommandManager.RemoveHandler(cmd);

        foreach (var cmd in _fetchHuntMap)
            DalamudApi.CommandManager.RemoveHandler(cmd);

        foreach (var cmd in _clearPlayerSearch)
            DalamudApi.CommandManager.RemoveHandler(cmd);

        DalamudApi.CommandManager.RemoveHandler("/shelper");
    }

    private static void ClearTrackers(string cmd, string args)
    {
        switch (args)
        {
            case "全部":
            case "所有":
            case "all":
            {
                Plugin.Features.Counter.RemoveInstance();
                Plugin.Print("已清除所有计数");
                break;
            }
            case "当前":
            case "cur":
            case "current":
            {
                var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
                Plugin.Features.Counter.RemoveInstance(currentInstance);
                Plugin.Print("已清除当前区域的计数");
                break;
            }
            default:
            {
                Plugin.Print(new List<Payload>
                {
                    new UIForegroundPayload(518),
                    new TextPayload($"使用方法: {cmd} [cur/all]. 比如清除当前计数: {cmd} cur"),
                    new UIForegroundPayload(0)
                });
                return;
            }
        }
    }

    private static void SendFailedAttempt(string cmd, string args)
    {
        if (!Plugin.Managers.Socket.Main.Connected())
        {
            var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
            if (!Plugin.Features.Counter.GetLocalTrackers().TryGetValue(currentInstance, out var tracker))
                return;

            var startTime = DateTimeOffset.FromUnixTimeSeconds(tracker.startTime).LocalDateTime;
            var endTime   = DateTimeOffset.Now.LocalDateTime;

            var message = $"{currentInstance}的计数寄了！\n" +
                          $"开始时间: {startTime.ToShortDateString()}/{startTime.ToShortTimeString()}\n" +
                          $"结束时间: {endTime.ToShortDateString()}/{endTime.ToShortTimeString()}\n" +
                          "计数详情: ";

            foreach (var (k, v) in tracker.counter)
            {
                message += $"    {k}: {v}\n";
            }

            Plugin.Print(new List<Payload>
            {
                new UIForegroundPayload(518),
                new TextPayload(message + "PS:消息已复制到剪贴板"),
                new UIForegroundPayload(0),
            });

            ImGui.SetClipboardText(message);
            return;
        }

        Plugin.Managers.Socket.Main.SendMessage(new AttemptMessage
        {
            Type        = "ggnore",
            WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
            InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            // Instance    = Plugin.Managers.Data.Player.GetCurrentTerritory(),
            Failed = true
        });
    }
}