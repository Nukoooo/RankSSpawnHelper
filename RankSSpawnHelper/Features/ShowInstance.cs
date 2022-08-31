using System;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;

namespace RankSSpawnHelper.Features;

public class ShowInstance : IDisposable
{
    private readonly DtrBarEntry _dtrBarEntry;

    public ShowInstance()
    {
        Service.Framework.Update += OnFrameworkUpdate;
        _dtrBarEntry = Service.DtrBar.Get("S怪触发小助手-当前几线");
    }

    public void Dispose()
    {
        _dtrBarEntry.Dispose();
        Service.Framework.Update -= OnFrameworkUpdate;
        GC.SuppressFinalize(this);
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        try
        {
            if (!Service.Configuration._showInstance)
            {
                _dtrBarEntry.Shown = false;
                return;
            }

            _dtrBarEntry.Shown = true;

            var key = Service.Counter.GetCurrentInstance();
            var split = key.Split('@');

            _dtrBarEntry.Text = split[2] switch
            {
                "1" => "\xe0b1线",
                "2" => "\xe0b2线",
                "3" => "\xe0b3线",
                _ => "",
            };
        }
        catch (Exception)
        {
            _dtrBarEntry.Shown = false;
        }
    }
}