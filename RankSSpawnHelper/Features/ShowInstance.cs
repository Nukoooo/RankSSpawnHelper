using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;

namespace RankSSpawnHelper.Features
{
    internal class ShowInstance : IDisposable
    {
        private readonly DtrBarEntry _dtrBarEntry;
        public ShowInstance()
        {
            _dtrBarEntry             =  DalamudApi.DtrBar.Get("S怪触发小助手-当前几线");
            DalamudApi.Framework.Update += Framework_OnUpdate;
        }

        private void Framework_OnUpdate(Framework framework)
        {
            if (!Plugin.Configuration.ShowInstance)
            {
                _dtrBarEntry.Shown = false;
                return;
            }

            _dtrBarEntry.Shown = true;
            var key   = Plugin.Managers.Data.Player.GetCurrentInstance();
            var split = key.Split('@');

            _dtrBarEntry.Text = split[2] switch
                                {
                                    "1" => "\xe0b1线",
                                    "2" => "\xe0b2线",
                                    "3" => "\xe0b3线",
                                    _   => "",
                                };
        }

        public void Dispose()
        {
            _dtrBarEntry.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
