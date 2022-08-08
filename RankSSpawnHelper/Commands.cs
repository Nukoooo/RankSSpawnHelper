using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using ImGuiNET;

namespace RankSSpawnHelper;

public class Commands : IDisposable
{
    private const string CommandName = "/shelper";
    private const string DebugCommand = "/debug_stuff";
    private const string LastCounterMessage = "/glcm";
    private readonly List<string> _clearTracker = new() { "/clr", "/清除计数" };
    private readonly List<string> _ggnore = new() { "/ggnore", "/寄了" };

    public Commands()
    {
        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开农怪助手设置菜单",
            ShowInHelp = true
        });

#if DEBUG
        Service.CommandManager.AddHandler(DebugCommand, new CommandInfo(OnCommand)
        {
            ShowInHelp = false
        });
#endif

        foreach (var t in _clearTracker)
            Service.CommandManager.AddHandler(t, new CommandInfo(Command_ClearTracker)
            {
                ShowInHelp = true,
                HelpMessage = $"清除本地计数. 清除当前计数: {t} cur/current/当前. 清除所有计数: {t} all/所有/全部"
            });

        foreach (var t in _ggnore)
            Service.CommandManager.AddHandler(t, new CommandInfo(Command_GG)
            {
                ShowInHelp = true,
                HelpMessage = "联网农怪 - 给服务器发送寄了的消息"
            });

        Service.CommandManager.AddHandler(LastCounterMessage, new CommandInfo(OnCommand)
        {
            HelpMessage = "获取上次计数的详情",
            ShowInHelp = true
        });
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler(CommandName);
#if DEBUG
        Service.CommandManager.RemoveHandler(DebugCommand);
#endif
        foreach (var cmd in _clearTracker) Service.CommandManager.RemoveHandler(cmd);
        foreach (var cmd in _ggnore) Service.CommandManager.RemoveHandler(cmd);
    }

    private static void Command_ClearTracker(string cmd, string args)
    {
        switch (args)
        {
            case "全部":
            case "所有":
            case "all":
            {
                Service.Counter.ClearTracker();
                Service.ChatGui.Print("已清除所有计数");
                break;
            }
            case "当前":
            case "cur":
            case "current":
            {
                Service.Counter.ClearKey(Service.Counter.GetCurrentInstance());
                Service.ChatGui.Print("已清除当前区域的计数");
                break;
            }
            default:
            {
                Service.ChatGui.PrintError($"使用方法: {cmd} [cur/all]. 比如清除当前计数: {cmd} cur");
                return;
            }
        }
    }

    private static void Command_GG(string cmd, string args)
    {
        if (!Service.Counter.Socket.Connected())
        {
            Service.ChatGui.PrintError("你没联网你寄啥！");
            return;
        }

        var str = Service.Counter.FormatJsonString("ggnore", Service.Counter.GetCurrentInstance());
        Service.Counter.Socket.SendMessage(str);
    }

    private static void OnCommand(string command, string args)
    {
        switch (command)
        {
            case CommandName:
            {
                Service.ConfigWindow.Toggle();
                break;
            }
            case DebugCommand:
            {
                Service.ChatGui.Print($"territoryId: {Service.ClientState.TerritoryType}, classJob:{Service.ClientState.LocalPlayer.ClassJob.Id}, PartyLength: {Service.PartyList.Length}");

                Service.Counter.Socket.Connect("ws://localhost:8000");
                break;
            }
            case LastCounterMessage:
            {
                var msg = Service.Counter.GetLastCounterMessage();
                if (msg == string.Empty)
                {
                    Service.ChatGui.PrintError("上一次计数的消息是空的");
                    return;
                }

                Service.ChatGui.PrintError(msg + "\nPS: 本消息已复制到粘贴板");
                ImGui.SetClipboardText(msg);

                break;
            }
        }
    }
}