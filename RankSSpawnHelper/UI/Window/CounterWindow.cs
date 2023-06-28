using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.UI.Window;

public class CounterWindow : Dalamud.Interface.Windowing.Window
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;
    private readonly string _clickIfFailed;
    private readonly string _startTime;
    private DateTime _nextClickTime = DateTime.Now;

    public CounterWindow() : base("农怪计数##RankSSpawnHelper1337")
    {
        Flags          = WindowFlags;
        _startTime     = Plugin.IsChina() ? "开始时间" : "Start time";
        _clickIfFailed = Plugin.IsChina() ? "寄了点我" : "Click if failed";
    }

    private static ImGuiWindowFlags BuildWindowFlags(ImGuiWindowFlags var)
    {
        if (Plugin.Configuration.TrackerWindowNoBackground)
            var |= ImGuiWindowFlags.NoBackground;
        if (Plugin.Configuration.TrackerWindowNoTitle)
            var |= ImGuiWindowFlags.NoTitleBar;
        if (Plugin.Configuration.TrackerAutoResize)
            var |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
        return var;
    }

    public override void PreOpenCheck()
    {
        if (!Plugin.Configuration.TrackKillCount)
        {
            IsOpen = false;
            return;
        }

        var networkTracker = Plugin.Features.Counter.GetNetworkedTrackers();
        var localTracker   = Plugin.Features.Counter.GetLocalTrackers();
        var actualTracker  = Plugin.Managers.Socket.Main.Connected() ? networkTracker : localTracker;

        if (actualTracker.Count == 0)
        {
            IsOpen = false;
        }
    }

    public override void PreDraw()
    {
        Flags = BuildWindowFlags(WindowFlags);
    }

    public override void Draw()
    {
        var networkTracker = Plugin.Features.Counter.GetNetworkedTrackers();
        var localTracker   = Plugin.Features.Counter.GetLocalTrackers();

        var connected = Plugin.Managers.Socket.Main.Connected();

        var actualTracker = connected ? networkTracker : localTracker;
        if (actualTracker == null)
            return;


        if (!Plugin.Configuration.TrackerShowCurrentInstance)
        {
            if (Plugin.Managers.Font.IsFontBuilt())
            {
                ImGui.PushFont(Plugin.Managers.Font.NotoSan24);
                ImGui.SetWindowFontScale(0.8f);
            }

            foreach (var (k, v) in actualTracker)
            {
                ImGui.Text(k);

                var timeInLoop = DateTimeOffset.FromUnixTimeSeconds(v.startTime).LocalDateTime;
                ImGui.Text($"\t{_startTime}: {timeInLoop.Month}-{timeInLoop.Day}@{timeInLoop.ToShortTimeString()}");

                foreach (var (subK, subV) in v.counter)
                {
                    var textToDraw = $"\t{subK} - {subV}";

                    if (connected && localTracker.TryGetValue(k, out var val) && val.counter.TryGetValue(subK, out var localValue))
                        textToDraw += $" ({localValue})";

                    ImGui.Text(textToDraw);
                }
            }

            if (!Plugin.Managers.Font.IsFontBuilt()) return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);

            return;
        }

        var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

        if (!actualTracker.TryGetValue(currentInstance, out var value))
        {
            IsOpen = false;
            return;
        }

        if (Plugin.Managers.Font.IsFontBuilt())
        {
            ImGui.PushFont(Plugin.Managers.Font.NotoSan24);
            ImGui.SetWindowFontScale(0.8f);
        }

        if (ImGui.Button("[ 寄了点我 ]##only_show_single_instance"))
        {
            if (DateTime.Now > _nextClickTime)
            {
                if (Plugin.Managers.Socket.Main.Connected())
                {
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
                else
                {
                    var startTime = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
                    var endTime   = DateTimeOffset.Now.LocalDateTime;

                    var message = $"{currentInstance}的计数寄了！\n" +
                                  $"开始时间: {startTime.ToShortDateString()}/{startTime.ToShortTimeString()}\n" +
                                  $"结束时间: {endTime.ToShortDateString()}/{endTime.ToShortTimeString()}\n" +
                                  "计数详情: \n";

                    foreach (var (k, v) in value.counter)
                    {
                        message += $"    {k}: {v}\n";
                    }

                    Plugin.Print(new List<Payload>
                                 {
                                     new UIForegroundPayload(518),
                                     new TextPayload(message + "PS:消息已复制到剪贴板"),
                                     new UIForegroundPayload(0)
                                 });

                    ImGui.SetClipboardText(message);
                    Plugin.Features.Counter.RemoveInstance(currentInstance);
                }

                _nextClickTime = DateTime.Now.AddSeconds(15);
            }
            else
            {
                Plugin.Print(new List<Payload>
                             {
                                 new UIForegroundPayload(518),
                                 new TextPayload($"你还得等 {(_nextClickTime - DateTime.Now).TotalSeconds:F}秒 才能再点这个按钮"),
                                 new UIForegroundPayload(0)
                             });
            }
        }

        ImGui.SameLine();
        ImGui.Text($"{currentInstance}");

        var time = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
        ImGui.Text($"\t{_startTime}: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

        foreach (var (subKey, subValue) in value.counter)
        {
            var textToDraw = $"\t{subKey} - {subValue}";

            if (connected && localTracker.TryGetValue(currentInstance, out var v) && v.counter.TryGetValue(subKey, out var localValue))
                textToDraw += $" ({localValue})";

            ImGui.Text(textToDraw);
        }

        if (!Plugin.Managers.Font.IsFontBuilt())
            return;

        ImGui.PopFont();
        ImGui.SetWindowFontScale(1.0f);
    }
}