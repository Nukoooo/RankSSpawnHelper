using Dalamud.Interface.Windowing;
using RankSSpawnHelper.Ui.Window;
using RankSSpawnHelper.UI.Window;

namespace RankSSpawnHelper.Ui;

internal class Ui
{
    public CounterWindow CounterWindow;
    public HuntMapWindow HuntMapWindow;
    public ConfigWindow PluginWindow;
    public WeeEaWindow WeeEaWindow;

    public Ui(ref WindowSystem windowSystem)
    {
        PluginWindow  = new();
        CounterWindow = new();
        WeeEaWindow   = new();
        HuntMapWindow = new();

        windowSystem.AddWindow(PluginWindow);
        windowSystem.AddWindow(CounterWindow);
        windowSystem.AddWindow(WeeEaWindow);
        windowSystem.AddWindow(HuntMapWindow);
    }
}