using Dalamud;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace RankSSpawnHelper.Modules;

internal class WorldTravel : IUiModule
{
    private nint   _address1;
    private byte[] _bytes1 = null!;

    private nint   _address2;
    private byte[] _bytes2 = null!;

    private readonly Configuration _configuration;

    public WorldTravel(Configuration configuration)
        => _configuration = configuration;

    public bool Init()
    {
        if (!DalamudApi.SigScanner.TryScanText("81 C2 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D0 48 8D 8C 24", out _address1))
        {
            DalamudApi.PluginLog.Error("[WorldTravel] Failed to get address #1");

            return false;
        }

        if (!SafeMemory.ReadBytes(_address1 + 2, 2, out _bytes1))
        {
            DalamudApi.PluginLog.Error("[WorldTravel] Failed to read bytes #1");

            return false;
        }

        if (_bytes1[0] == 0xF4)
        {
            _bytes1[0] = 0xF5;
        }

        if (!DalamudApi.SigScanner.TryScanText("83 F8 ?? 73 ?? 44 8B C0 1B D2", out _address2))
        {
            DalamudApi.PluginLog.Error("[WorldTravel] Failed to get address #2");

            return false;
        }

        if (!SafeMemory.ReadBytes(_address2, 5, out _bytes2))
        {
            DalamudApi.PluginLog.Error("[WorldTravel] Failed to read bytes #2");

            return false;
        }

        if (_bytes2[0] == 0x90)
        {
            _address2 = 0;
        }

        PatchWorldTravelQueue(_configuration.AccurateWorldTravelQueue);

        return true;
    }

    public void Shutdown()
    {
        PatchWorldTravelQueue(false);
    }

    private void PatchWorldTravelQueue(bool enabled)
    {
        if (_address1 == nint.Zero || _address2 == nint.Zero)
        {
            return;
        }

        if (enabled)
        {
            SafeMemory.WriteBytes(_address1 + 2, [0xF4, 0x30]);
            SafeMemory.WriteBytes(_address2,     [0x90, 0x90, 0x90, 0x90, 0x90]);
        }
        else
        {
            SafeMemory.WriteBytes(_address1 + 2, _bytes1);
            SafeMemory.WriteBytes(_address2,     _bytes2);
        }
    }

    public string UiName => string.Empty;

    public void OnDrawUi()
    {
        var isValid = _address1 != nint.Zero && _address2 != nint.Zero;

        {
            using var disable          = ImRaii.Disabled(!isValid);
            var       worldTravelQueue = _configuration.AccurateWorldTravelQueue;

            if (ImGui.Checkbox("显示实际跨服人数", ref worldTravelQueue))
            {
                _configuration.AccurateWorldTravelQueue = worldTravelQueue;
                _configuration.Save();
            }
        }

        if (!isValid)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("无法使用，可能因为和ACT的插件有冲突");
            }
        }

        ImGui.SameLine();
    }
}