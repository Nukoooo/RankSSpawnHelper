using System;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace RankSSpawnHelper.Features;

public class CounterOverlay : Window
{
    private const ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.None;

    public CounterOverlay() : base("农怪计数##RankSSpawnHelper")
    {
        Flags = _windowFlags;
    }

    private static ImGuiWindowFlags BuildWindowFlags(ImGuiWindowFlags var)
    {
        if (Service.Configuration._trackerWindowNoBackground)
            var |= ImGuiWindowFlags.NoBackground;
        if (Service.Configuration._trackerWindowNoTitle)
            var |= ImGuiWindowFlags.NoTitleBar;
        if (Service.Configuration._trackerAutoResize)
            var |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
        return var;
    }

    public override void PreDraw()
    {
        Flags = BuildWindowFlags(_windowFlags);
    }

    public override void Draw()
    {
        var tracker = Service.Counter.GetTracker();
        var localTracker = Service.Counter.GetLocalTracker();

        if (!Service.Configuration._trackerShowCurrentInstance)
        {
            if (Fonts.AreFontsBuilt())
            {
                ImGui.PushFont(Fonts.Yahei24);
                ImGui.SetWindowFontScale(0.8f);
            }

            foreach (var (k, v) in tracker)
            {
                var splitInLoop = k.Split('@');

                ImGui.Text($"{splitInLoop[0]} - {splitInLoop[1]}" + (splitInLoop[2] == "0" ? string.Empty : $" - {splitInLoop[2]}线"));
                var timeInLoop = DateTimeOffset.FromUnixTimeSeconds(v.startTime).LocalDateTime;
                ImGui.Text($"\t开始时间: {timeInLoop.Month}-{timeInLoop.Day}@{timeInLoop.ToShortTimeString()}");
                foreach (var (subK, subV) in v.counter)
                {
                    var textToDraw = $"\t{subK} - {subV}";
                    if (localTracker.ContainsKey(k) && localTracker[k].counter.TryGetValue(subK, out var localValue))
                        textToDraw += $" ({localValue})";
                    
                    ImGui.Text(textToDraw);
                }
            }


            if (!Fonts.AreFontsBuilt()) return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);

            return;
        }

        var mainKey = Service.Counter.GetCurrentInstance();

        if (!tracker.TryGetValue(mainKey, out var value))
        {
            IsOpen = false;
            return;
        }

        var split = mainKey.Split('@');

        if (Fonts.AreFontsBuilt())
        {
            ImGui.PushFont(Fonts.Yahei24);
            ImGui.SetWindowFontScale(0.8f);
        }

        ImGui.Text($"{split[0]} - {split[1]}" + (split[2] == "0" ? string.Empty : $" - {split[2]}线"));
        var time = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
        ImGui.Text($"\t开始时间: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

        foreach (var (subKey, subValue) in value.counter)
        {
            var textToDraw = $"\t{subKey} - {subValue}";
            if (localTracker.ContainsKey(mainKey) && localTracker[mainKey].counter.TryGetValue(subKey, out var localSubValue))
                textToDraw += $" ({localSubValue})";
            
            ImGui.Text(textToDraw);
        }

        if (!Fonts.AreFontsBuilt()) return;

        ImGui.PopFont();
        ImGui.SetWindowFontScale(1.0f);
    }
}