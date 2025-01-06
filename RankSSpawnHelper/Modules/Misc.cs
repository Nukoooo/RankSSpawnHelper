namespace RankSSpawnHelper.Modules;

internal unsafe class Misc : IUiModule
{
    public Misc()
    {
    }

    public bool Init()
    {
        var a = 1;

        /*DalamudApi.PluginLog.Info($"_asmHook.IsEnabled: {_asmHook.IsEnabled}");*/

        return true;
    }

    public void Shutdown()
    {
    }

    public string UiName => "杂项";

    public void OnDrawUi()
    {
    }
}
