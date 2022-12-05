using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace RankSSpawnHelper.Managers.DataManagers
{
    internal class Player
    {
        private readonly IntPtr _instanceNumberAddress;
        private readonly ExcelSheet<TerritoryType> _terr;
        private Tuple<SeString, string> _lastAttempMessage = new(new SeString(), string.Empty);

        public Player()
        {
            _instanceNumberAddress = DalamudApi.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BD");
            if (_instanceNumberAddress == IntPtr.Zero)
                PluginLog.Warning("InstanceNumberAddress is null");
            _terr                  = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();
        }

        public string GetCurrentTerritory()
        {
            try
            {
                var instanceNumber = Marshal.ReadByte(_instanceNumberAddress, 0x20);

                return DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData?.Name + "@" + _terr.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceName.Value?.Name.ToDalamudString().TextValue +
                       "@" + instanceNumber;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"Exception from Managers::Data::GetCurrentInstance(). {new StackFrame(1).GetMethod()?.Name}");
                return string.Empty;
            }
        }

        /*public int GetCurrentInstance()
        {
            try
            {
                return Marshal.ReadByte(_instanceNumberAddress, 0x20);
            }
            catch
            {
                return -1;
            }
        }*/

        public string GetLocalPlayerName()
        {
            if (DalamudApi.ClientState.LocalPlayer == null)
                return string.Empty;

            return $"{DalamudApi.ClientState.LocalPlayer.Name}@{DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.Name}";
        }

        public Tuple<SeString, string> GetLastAttempMessage()
        {
            return _lastAttempMessage;
        }

        public void SetLastAttempMessage(Tuple<SeString, string> msg)
        {
            _lastAttempMessage = msg;
        }
    }
}