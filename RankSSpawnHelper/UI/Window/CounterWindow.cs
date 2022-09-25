using System;
using ImGuiNET;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.UI.Window
{
    public class CounterWindow : Dalamud.Interface.Windowing.Window
    {
        private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;
        private DateTime _nextClickTime = DateTime.Now;

        public CounterWindow() : base("农怪计数##RankSSpawnHelper1337")
        {
            Flags = WindowFlags;
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
            var localTracker = Plugin.Features.Counter.GetLocalTrackers();

            if (localTracker.Count != 0)
                return;

            IsOpen = false;
        }

        public override void PreDraw()
        {
            Flags = BuildWindowFlags(WindowFlags);
        }

        public override void Draw()
        {
            var networkTracker = Plugin.Features.Counter.GetNetworkedTrackers();
            var localTracker   = Plugin.Features.Counter.GetLocalTrackers();

            var connected = Plugin.Managers.Socket.Connected();

            var actualTracker = connected ? networkTracker : localTracker;

            // C# is so stupid
            string   server;
            string   territory;
            string   instance;
            string[] split;

            if (!Plugin.Configuration.TrackerShowCurrentInstance)
            {
                if (Plugin.Managers.Font.IsFontBuilt())
                {
                    ImGui.PushFont(Plugin.Managers.Font.Yahei24);
                    ImGui.SetWindowFontScale(0.8f);
                }

                foreach (var (k, v) in actualTracker)
                {
                    split     = k.Split('@');
                    server    = split[0];
                    territory = split[1];
                    instance  = split[2] == "0" ? string.Empty : $" - {split[2]}线";

                    ImGui.Text($"{server} - {territory}{instance}");

                    var timeInLoop = DateTimeOffset.FromUnixTimeSeconds(v.startTime).LocalDateTime;
                    ImGui.Text($"\t开始时间: {timeInLoop.Month}-{timeInLoop.Day}@{timeInLoop.ToShortTimeString()}");

                    foreach (var (subK, subV) in v.counter)
                    {
                        var textToDraw = $"\t{subK} - {subV}";

                        if (connected && localTracker.ContainsKey(k) && localTracker[k].counter.TryGetValue(subK, out var localValue))
                            textToDraw += $" ({localValue})";
                        
                        ImGui.Text(textToDraw);
                    }
                }

                if (!Plugin.Managers.Font.IsFontBuilt()) return;

                ImGui.PopFont();
                ImGui.SetWindowFontScale(1.0f);

                return;
            }

            var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();

            if (!actualTracker.TryGetValue(currentInstance, out var value))
            {
                IsOpen = false;
                return;
            }

            split = currentInstance.Split('@');

            if (Plugin.Managers.Font.IsFontBuilt())
            {
                ImGui.PushFont(Plugin.Managers.Font.Yahei24);
                ImGui.SetWindowFontScale(0.8f);
            }

            server    = split[0];
            territory = split[1];
            instance  = split[2] == "0" ? string.Empty : $" - {split[2]}线";

            if (ImGui.Button("[ 寄了点我 ]"))
            {
                if (DateTime.Now > _nextClickTime)
                {
                    Plugin.Managers.Socket.SendMessage(new NetMessage
                                                       {
                                                           Type        = "ggnore",
                                                           Instance    = Plugin.Managers.Data.Player.GetCurrentInstance(),
                                                           User        = Plugin.Managers.Data.Player.GetLocalPlayerName(),
                                                           TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                           Failed      = true
                                                       });

                    _nextClickTime = DateTime.Now.AddMinutes(1);
                }
                else
                {
                    DalamudApi.ChatGui.PrintError($"你还得等 {(_nextClickTime - DateTime.Now).TotalSeconds:F}秒 才能再点这个按钮");
                }
            }

            ImGui.SameLine();
            ImGui.Text($"{server} - {territory}{instance}");

            var time = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
            ImGui.Text($"\t开始时间: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

            foreach (var (subKey, subValue) in value.counter)
            {
                var textToDraw = $"\t{subKey} - {subValue}";

                if (connected && localTracker.ContainsKey(currentInstance) && localTracker[currentInstance].counter.TryGetValue(subKey, out var localValue))
                    textToDraw += $" ({localValue})";

                ImGui.Text(textToDraw);
            }

            if (!Plugin.Managers.Font.IsFontBuilt())
                return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);
        }
    }
}