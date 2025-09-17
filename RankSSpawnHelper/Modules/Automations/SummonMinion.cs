using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace RankSSpawnHelper.Modules.Automations;

internal class SummonMinion : IModule
{
    private readonly Configuration _configuration;

    private readonly Dictionary<ushort, uint> _minionMap = new ()
    {
        { 1188, 180 },
        { 960, 423 },
        { 816, 303 },
        { 956, 434 },
        { 614, 215 },
        { 397, 148 }
    };

    private readonly List<uint> _unlockedMinion = [];

    private DateTime _lastUpDateTime;

    public SummonMinion(Configuration configuration)
    {
        _configuration = configuration;

        if (DalamudApi.ClientState.IsLoggedIn)
        {
            ClientState_OnLogin();
        }
    }

    public bool Init()
    {
        DalamudApi.ClientState.Login += ClientState_OnLogin;
        DalamudApi.Framework.Update  += Framework_OnUpdate;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.ClientState.Login -= ClientState_OnLogin;
        DalamudApi.Framework.Update  -= Framework_OnUpdate;
    }

    private void Framework_OnUpdate(IFramework framework)
    {
        if (DateTime.Now - _lastUpDateTime <= TimeSpan.FromSeconds(2))
        {
            return;
        }

        if (!_configuration.AutoSummonMinion)
        {
            goto end;
        }

        var territoryType = DalamudApi.ClientState.TerritoryType;

        if (!_minionMap.TryGetValue(territoryType, out var minionId))
        {
            goto end;
        }

        if (!_unlockedMinion.Contains(minionId))
        {
            goto end;
        }

        if (DalamudApi.Condition[ConditionFlag.Mounted]
            || DalamudApi.Condition[ConditionFlag.RidingPillion]
            || DalamudApi.Condition[ConditionFlag.MountOrOrnamentTransition]
            || DalamudApi.Condition[ConditionFlag.Mounting]
            || DalamudApi.Condition[ConditionFlag.Mounting71])
        {
            goto end;
        }

        if (!CanUseAction(minionId))
        {
            goto end;
        }

        if (DalamudApi.ObjectTable[1] == null && CanUseAction(minionId))
        {
            UseAction(minionId);

            goto end;
        }

        var obj = DalamudApi.ObjectTable[1];

        if (obj is not { ObjectKind: Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion })
        {
            UseAction(minionId);

            goto end;
        }

        if (!CanUseAction(minionId))
        {
            goto end;
        }

        if (obj.DataId == minionId)
        {
            goto end;
        }

        UseAction(minionId);

    end:
        _lastUpDateTime = DateTime.Now;

        return;

        static unsafe bool CanUseAction(uint id) =>
            ActionManager.Instance()->GetActionStatus(ActionType.Companion, id) == 0
            && !ActionManager.Instance()->IsRecastTimerActive(ActionType.Action, id);

        static unsafe void UseAction(uint id)
        {
            ActionManager.Instance()->UseAction(ActionType.Companion, id);
        }
    }

    private unsafe void ClientState_OnLogin()
    {
        _unlockedMinion.Clear();

        foreach (var u in _minionMap.Where(u => UIState.Instance()->IsCompanionUnlocked(u.Value)))
        {
            _unlockedMinion.Add(u.Value);
        }
    }
}
