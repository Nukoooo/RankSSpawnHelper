using System;
using System.Collections.Generic;
using Dalamud.Game.Command;

namespace RankSSpawnHelper;

public class Commands : IDisposable
{
    private const string CommandName = "/shelper";
    private const string DebugCommand = "/debug_stuff";
    private readonly List<string> _clearTracker = new() { "/clr", "/cleartracker", "/清除计数" };

    public Commands()
    {
        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开农怪助手设置菜单",
            ShowInHelp = true
        });

        Service.CommandManager.AddHandler(DebugCommand, new CommandInfo(OnCommand)
        {
            ShowInHelp = false
        });

        for (var i = 0; i < _clearTracker.Count; i++)
            Service.CommandManager.AddHandler(_clearTracker[i], new CommandInfo(Command_ClearTracker)
            {
                ShowInHelp = i == 0,
                HelpMessage = "清除农怪计数"
            });
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler(CommandName);
        Service.CommandManager.RemoveHandler(DebugCommand);

        foreach (var cmd in _clearTracker) Service.CommandManager.RemoveHandler(cmd);
    }

    private static void Command_ClearTracker(string cmd, string args)
    {
        Service.Counter.ClearTracker();
        Service.ChatGui.Print("计数已重置");
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
        }
    }
}