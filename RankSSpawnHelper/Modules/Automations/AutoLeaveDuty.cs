using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace RankSSpawnHelper.Modules.Automations;

internal class AutoLeaveDuty : IModule
{
    private readonly Configuration     _configuration;

    public AutoLeaveDuty(Configuration configuration) =>
        _configuration = configuration;

    public bool Init()
    {
        DalamudApi.ClientState.TerritoryChanged += ClientStateOnTerritoryChanged;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.ClientState.TerritoryChanged -= ClientStateOnTerritoryChanged;
    }

    private void ClientStateOnTerritoryChanged(ushort obj)
    {
        if (!_configuration.AutoLeaveDuty)
        {
            return;
        }

        if (DalamudApi.PartyList.Length > 0)
        {
            return;
        }

        if (obj != 1045)
        {
            return;
        }

        DalamudApi.Framework.Update += FrameworkOnUpdate;
    }

    private unsafe void FrameworkOnUpdate(IFramework framework)
    {
        if (!EventFramework.CanLeaveCurrentContent())
        {
            return;
        }

        if (DalamudApi.ClientState.LocalPlayer is { ClassJob.RowId: 36 })
        {
            EventFramework.LeaveCurrentContent(true);
        }

        DalamudApi.Framework.Update -= FrameworkOnUpdate;
    }
}
