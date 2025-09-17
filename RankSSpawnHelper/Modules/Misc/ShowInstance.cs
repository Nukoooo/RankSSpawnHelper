using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using IDataManager = RankSSpawnHelper.Managers.IDataManager;

namespace RankSSpawnHelper.Modules;

internal class ShowInstance : IUiModule
{
    private readonly Configuration _configuration;

    private readonly IDataManager _dataManager;

    private readonly IDtrBarEntry _dtrBar;

    public ShowInstance(Configuration configuration, IDataManager dataManager)
    {
        _configuration = configuration;
        _dataManager   = dataManager;

        _dtrBar = DalamudApi.DtrBar.Get("S怪触发小助手-当前分线");
    }

    public bool Init()
    {
        DalamudApi.Framework.Update += Framework_OnUpdate;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.Framework.Update -= Framework_OnUpdate;
        _dtrBar.Remove();
    }

    public string UiName => string.Empty;

    public void OnDrawUi()
    {
        var showCurrentInstance = _configuration.ShowInstance;

        if (ImGui.Checkbox("显示当前分线", ref showCurrentInstance))
        {
            _configuration.ShowInstance = showCurrentInstance;
            _configuration.Save();
        }

        ImGui.SameLine();
    }

    private void Framework_OnUpdate(IFramework framework)
    {
        try
        {
            if (_configuration.ShowInstance)
            {
                var currentInstance = _dataManager.GetCurrentInstance();

                if (currentInstance == 0)
                {
                    _dtrBar.Shown = false;

                    return;
                }

                _dtrBar.Shown = true;

                _dtrBar.Text = GetInstanceString();
            }
            else
            {
                _dtrBar.Shown = false;
            }
        }
        catch (Exception)
        {
            _dtrBar.Shown = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetInstanceString()
    {
        return _dataManager.GetCurrentInstance() switch
        {
            1 => "\xe0b1" + "线",
            2 => "\xe0b2" + "线",
            3 => "\xe0b3" + "线",
            4 => "\xe0b4" + "线",
            5 => "\xe0b5" + "线",
            6 => "\xe0b6" + "线",
            _ => "\xe060" + "线",
        };
    }
}