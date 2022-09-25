using System;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using ImGuiNET;
using RankSSpawnHelper.Attributes;
using RankSSpawnHelper.Models;
using RankSSpawnHelper.Ui;

namespace RankSSpawnHelper
{
    public class EntryPoint : IDalamudPlugin
    {
        private readonly PluginCommandManager<EntryPoint> _commandManager;
        private readonly WindowSystem _windowSystem;

        public EntryPoint([RequiredVersion("1.0")] DalamudPluginInterface pi)
        {
            pi.Create<DalamudApi>();
            pi.Create<Plugin>();

            // Get or create a configuration object
            Plugin.Configuration = (Configuration)pi.GetPluginConfig() ?? pi.Create<Configuration>();

            Plugin.Features      = new Features.Features();
            Plugin.Managers      = new Managers.Managers();

            // Initialize the UI
            _windowSystem  = new WindowSystem(typeof(EntryPoint).AssemblyQualifiedName);
            Plugin.Windows = new Windows(ref _windowSystem);

            DalamudApi.Interface.UiBuilder.Draw         += _windowSystem.Draw;
            DalamudApi.Interface.UiBuilder.OpenConfigUi += OpenConfigUi;

            // Load all of our commands
            _commandManager = new PluginCommandManager<EntryPoint>(this);
        }

        public string Name => "SpawnHelper";

#region Commands initialization
        [Command("/shelper")]
        [HelpMessage("打开或关闭设置菜单")]
        public void ToggleConfigUi(string command, string args)
        {
            Plugin.Windows.PluginWindow.Toggle();
        }

        private void OpenConfigUi()
        {
            Plugin.Windows.PluginWindow.IsOpen = true;
        }

        [Command("/clr")]
        [HelpMessage("清除计数器")]
        public void ClearTracker_1(string cmd, string args)
        {
            ClearTracker_Internal(cmd, args);
        }

        [Command("/清除计数")]
        [HelpMessage("清除计数器")]
        public void ClearTracker_2(string cmd, string args)
        {
            ClearTracker_Internal(cmd, args);
        }

        private static void ClearTracker_Internal(string cmd, string args)
        {
            switch (args)
            {
                case "全部":
                case "所有":
                case "all":
                {
                    Plugin.Features.Counter.RemoveInstance();
                    DalamudApi.ChatGui.Print("已清除所有计数");
                    break;
                }
                case "当前":
                case "cur":
                case "current":
                {
                    var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();
                    Plugin.Features.Counter.RemoveInstance(currentInstance);
                    DalamudApi.ChatGui.Print("已清除当前区域的计数");
                    break;
                }
                default:
                {
                    DalamudApi.ChatGui.PrintError($"使用方法: {cmd} [cur/all]. 比如清除当前计数: {cmd} cur");
                    return;
                }
            }
        }

        [Command("/ggnore")]
        [HelpMessage("触发失败,寄了")]
        public void AttempFailed_1(string cmd, string args)
        {
            AttempFailed_Internal(cmd, args);
        }

        [Command("/寄了")]
        [HelpMessage("触发失败,寄了")]
        public void AttempFailed_2(string cmd, string args)
        {
            AttempFailed_Internal(cmd, args);
        }

        private static void AttempFailed_Internal(string cmd, string args)
        {
            if (!Plugin.Managers.Socket.Connected())
            {
                var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();
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

                DalamudApi.ChatGui.PrintError(message + "PS:消息已复制到剪贴板");
                ImGui.SetClipboardText(message);
                return;
            }

            Plugin.Managers.Socket.SendMessage(new NetMessage
                                               {
                                                   Type        = "ggnore",
                                                   Instance    = Plugin.Managers.Data.Player.GetCurrentInstance(),
                                                   User        = Plugin.Managers.Data.Player.GetLocalPlayerName(),
                                                   TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                   Failed      = true
                                               });
        }
#endregion

#region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            _commandManager.Dispose();

            Plugin.Configuration.Save();
            Plugin.Managers.Dispose();
            Plugin.Features.Dispose();

            DalamudApi.Interface.UiBuilder.Draw -= _windowSystem.Draw;
            _windowSystem.RemoveAllWindows();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }
}