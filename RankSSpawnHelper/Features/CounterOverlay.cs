using System;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using RankSSpawnHelper.Misc;

namespace RankSSpawnHelper.Features;

public class CounterOverlay : Window
{
    private const ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.None;
    private DateTime _nextClickTime = DateTime.Now;

    public CounterOverlay() : base("农怪计数##RankSSpawnHelper") => Flags = _windowFlags;

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

    public override void PreDraw() => Flags = BuildWindowFlags(_windowFlags);

    public override void Draw()
    {
        var networkTracker = Service.Counter.GetNetworkedTracker();
        var localTracker = Service.Counter.GetLocalTracker();

        // C# is so stupid
        string server;
        string territory;
        string instance;
        string[] split;

        if (!Service.Configuration._trackerShowCurrentInstance)
        {
            if (Fonts.AreFontsBuilt())
            {
                ImGui.PushFont(Fonts.Yahei24);
                ImGui.SetWindowFontScale(0.8f);
            }

            foreach (var (k, v) in localTracker)
            {
                split = k.Split('@');
                server = split[0];
                territory = split[1];
                instance = split[2] == "0" ? string.Empty : $" - {split[2]}线";

                ImGui.Text($"{server} - {territory}{instance}");

                var timeInLoop = DateTimeOffset.FromUnixTimeSeconds(v.startTime).LocalDateTime;
                ImGui.Text($"\t开始时间: {timeInLoop.Month}-{timeInLoop.Day}@{timeInLoop.ToShortTimeString()}");

                foreach (var (subK, subV) in v.counter)
                {
                    var textToDraw = $"\t{subK} - ";
                    if (networkTracker.ContainsKey(k) && networkTracker[k].counter.TryGetValue(subK, out var networkValue))
                        textToDraw += $"{networkValue} ";

                    textToDraw += $"({subV})";

                    ImGui.Text(textToDraw);
                }
            }

            if (!Fonts.AreFontsBuilt()) return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);

            return;
        }

        var currentInstance = Service.Counter.GetCurrentInstance();

        if (!localTracker.TryGetValue(currentInstance, out var value))
        {
            IsOpen = false;
            return;
        }

        split = currentInstance.Split('@');

        if (Fonts.AreFontsBuilt())
        {
            ImGui.PushFont(Fonts.Yahei24);
            ImGui.SetWindowFontScale(0.8f);
        }

        server = split[0];
        territory = split[1];
        instance = split[2] == "0" ? string.Empty : $" - {split[2]}线";

        if (ImGui.Button("[ 寄了点我 ]"))
        {
            if (DateTime.Now > _nextClickTime)
            {
                var str = Service.Counter.FormatJsonString("ggnore", Service.Counter.GetCurrentInstance());
                Service.SocketManager.SendMessage(str);

                _nextClickTime = DateTime.Now.AddMinutes(1);
            }
            else
            {
                Service.ChatGui.PrintError($"你还得等 {(_nextClickTime - DateTime.Now).TotalSeconds:F}秒 才能再点这个按钮");
            }
        }

        ImGui.SameLine();
        ImGui.Text($"{server} - {territory}{instance}");
        
        var time = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
        ImGui.Text($"\t开始时间: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

        foreach (var (subKey, subValue) in value.counter)
        {
            var textToDraw = $"\t{subKey} - ";
            if (networkTracker.ContainsKey(currentInstance) && networkTracker[currentInstance].counter.TryGetValue(subKey, out var networkvalue))
                textToDraw += $"{networkvalue} ";

            textToDraw += $"({subValue})";

            ImGui.Text(textToDraw);
        }

        if (!Fonts.AreFontsBuilt()) return;

        ImGui.PopFont();
        ImGui.SetWindowFontScale(1.0f);
    }
}