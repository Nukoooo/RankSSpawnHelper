using System;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;

namespace RankSSpawnHelper.Features
{
    internal class ShowInstance : IDisposable
    {
        private readonly DtrBarEntry _dtrBarEntry;

        public ShowInstance()
        {
            try
            {
                _dtrBarEntry = DalamudApi.DtrBar.Get("S怪触发小助手-当前几线");
            }
            catch (Exception)
            {
                return;
            }

            DalamudApi.Framework.Update += Framework_OnUpdate;
        }

        public void Dispose()
        {
            _dtrBarEntry?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Framework_OnUpdate(Framework framework)
        {
            if (_dtrBarEntry == null)
                return;

            try
            {
                if (!Plugin.Configuration.ShowInstance)
                {
                    _dtrBarEntry.Shown = false;
                    return;
                }

                _dtrBarEntry.Shown = true;
                var key   = Plugin.Managers.Data.Player.GetCurrentTerritory();
                var split = key.Split('@');

                _dtrBarEntry.Text = split[2] switch
                                    {
                                        "1" => "\xe0b1线",
                                        "2" => "\xe0b2线",
                                        "3" => "\xe0b3线",
                                        _   => ""
                                    };
            }
            catch (Exception)
            {
                _dtrBarEntry.Shown = false;
            }
        }
    }
}