using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Features;

namespace RankSSpawnHelper;

public class Plugin : IDalamudPlugin
{
    private readonly WindowSystem _windowSystem;

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Service.Commands = new Commands();
        Service.ConfigWindow = new ConfigWindow();
        Service.FateRecorder = new FateRecorder();
        Service.Counter = new Counter(pluginInterface);
        Service.WeeEa = new WeeEa();
        Service.ShowInstance = new ShowInstance();

        _windowSystem = new WindowSystem("RankSSpawnHelper");
        _windowSystem.AddWindow(Service.ConfigWindow);
        _windowSystem.AddWindow(Service.Counter.Overlay);
        _windowSystem.AddWindow(Service.WeeEa.overlay);
        _windowSystem.AddWindow(Service.FateRecorder._overlay);

        Service.Interface.UiBuilder.BuildFonts += Fonts.OnBuildFonts;
        Service.Interface.UiBuilder.RebuildFonts();
        Service.Interface.UiBuilder.OpenConfigUi += OpenConfigUi;
        Service.Interface.UiBuilder.Draw += _windowSystem.Draw;

    }

    public string Name => "S怪触发小助手";

    public void Dispose()
    {
        Service.Commands.Dispose();
        GC.SuppressFinalize(this);
        Service.FateRecorder.Dispose();
        Service.Counter.Dispose();
        Service.ShowInstance.Dispose();

        Service.Interface.UiBuilder.BuildFonts -= Fonts.OnBuildFonts;
        Service.Interface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        Service.Interface.UiBuilder.Draw -= _windowSystem.Draw;
    }

    private static void OpenConfigUi()
    {
        Service.ConfigWindow.IsOpen = true;
    }
}