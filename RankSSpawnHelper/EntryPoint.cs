using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace RankSSpawnHelper;

public class EntryPoint : IDalamudPlugin
{
    private readonly Commands     _commands;
    private readonly WindowSystem _windowSystem;

    public EntryPoint([RequiredVersion("1.0")] DalamudPluginInterface pi)
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
            new TextPayload("  [-] NET8 blablablablabla\n"),
            new TextPayload("  [-] 修复无法获取点位的问题\n"),
            new TextPayload("  [-] 修复在游戏内注入+加载插件无法获取服务器的bug\n"),
            new TextPayload("  [-] 好想重写啊\n"),
            new UIForegroundPayload(0),
            new TextPayload("今天人类/畜畜/傻逼死绝了吗?"),
        });

        return;
    }

    private void UiBuilder_OnOpenConfigUi()
    {
        Plugin.Windows.PluginWindow.IsOpen = true;
    }

    public string Name => "SpawnHelper";

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}