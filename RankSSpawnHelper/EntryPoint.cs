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
    private          byte[]       _bytes1 = [];
    private readonly nint       _address1;

    private          byte[] _bytes2 = [];
    private readonly nint   _address2;
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

        if (DalamudApi.SigScanner.TryScanText("81 C2 F5 ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D0 48 8D 8C 24", out _address1) && SafeMemory.ReadBytes(_address1 + 2, 2, out _bytes1))
        {
            SafeMemory.WriteBytes(_address1 + 2, [0xF4, 0x30]);
        }

        if (DalamudApi.SigScanner.TryScanText("83 F8 ?? 73 ?? 44 8B C0 1B D2", out _address2) && SafeMemory.ReadBytes(_address2, 5, out _bytes2))
        {
            SafeMemory.WriteBytes(_address2, [0x90, 0x90, 0x90, 0x90, 0x90]);
        }


#if RELEASE
        if (Plugin.Configuration.PluginVersion == pluginVersion)
            return;
        Plugin.Configuration.PluginVersion = pluginVersion;
#endif

        Plugin.Print(new List<Payload>
        {
            new TextPayload($"版本 {pluginVersion} 的更新日志:\n"),
            new UIForegroundPayload(35),
            new TextPayload("  [-] 增加精准显示跨服等待顺序，无法关闭！无法关闭！无法关闭！无法关闭！无法关闭！无法关闭！无法关闭！\n"),
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

        if (_bytes1.Length > 0 && _address1 != 0)
            SafeMemory.WriteBytes(_address1 + 2, _bytes1);
        if (_bytes2.Length > 0 && _address2 != 0)
            SafeMemory.WriteBytes(_address2, _bytes2);

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