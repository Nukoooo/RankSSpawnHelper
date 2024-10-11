using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace RankSSpawnHelper;

public class EntryPoint : IDalamudPlugin
{
    private readonly Commands     _commands;
    private readonly WindowSystem _windowSystem;

    public EntryPoint(IDalamudPluginInterface pi)
    {
        pi.Create<DalamudApi>();
        pi.Create<Plugin>();

        // Load all of our commands
        _commands = new();

        var assembly = Assembly.GetExecutingAssembly();

        Plugin.Managers      = new();
        Plugin.Configuration = (Configuration)pi.GetPluginConfig() ?? pi.Create<Configuration>();
        Plugin.Features      = new();

        // Initialize the UI
        _windowSystem  = new(typeof(EntryPoint).AssemblyQualifiedName);
        Plugin.Windows = new(ref _windowSystem);

        DalamudApi.Interface.UiBuilder.Draw         += _windowSystem.Draw;
        DalamudApi.Interface.UiBuilder.OpenConfigUi += UiBuilder_OnOpenConfigUi;
        DalamudApi.Interface.UiBuilder.OpenMainUi   += UiBuilder_OnOpenConfigUi;

        var pluginVersion = assembly.GetName().Version.ToString();
        Plugin.PluginVersion = pluginVersion;
        DalamudApi.PluginLog.Info($"Version: {Plugin.PluginVersion}");

#if RELEASE
        if (Plugin.Configuration.PluginVersion == pluginVersion)
            return;
        Plugin.Configuration.PluginVersion = pluginVersion;
#endif

        Plugin.Print(new List<Payload>
        {
            new TextPayload($"版本 {pluginVersion} 的更新日志:\n"),
            new UIForegroundPayload(35),
            new TextPayload("  [-] 修复加载插件后 精准显示跨服等待顺序 不生效的问题\n"),
            new UIForegroundPayload(0),
            new TextPayload("今天人类/畜畜/傻逼死绝了吗?")
        });
    }

    public string Name => "SpawnHelper";

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void UiBuilder_OnOpenConfigUi()
    {
        Plugin.Windows.PluginWindow.IsOpen = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _commands.Dispose();

        Plugin.Managers.Data.MapTexture.Dispose();
        Plugin.Configuration.Save();
        Plugin.Managers.Dispose();
        Plugin.Features.Dispose();

        DalamudApi.Interface.UiBuilder.Draw       -= _windowSystem.Draw;
        DalamudApi.Interface.UiBuilder.OpenMainUi -= UiBuilder_OnOpenConfigUi;
        _windowSystem.RemoveAllWindows();
    }
}