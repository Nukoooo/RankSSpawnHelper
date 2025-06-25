using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    private Hook<ActorControlDelegate> ActorControl { get; set; } = null!;

    private const int DeathEventId = 6;

    private void Detour_ActorControl(uint entityId, int   type, uint buffId, uint direct, uint damage, uint sourceId, uint arg4,
                                     uint arg5,     ulong targetId, byte a10)
    {
        ActorControl.Original(entityId, type, buffId, direct, damage, sourceId, arg4, arg5, targetId, a10);

        if (!_configuration.TrackKillCount)
        {
            return;
        }

        if (type != DeathEventId)
        {
            return;
        }

        var territory = DalamudApi.ClientState.TerritoryType;

        if (!_trackerConditions.ContainsKey(territory))
        {
            return;
        }

        var sourceTarget = DalamudApi.ObjectTable.SearchById(direct);

        var target = DalamudApi.ObjectTable.SearchById(entityId);

        if (target == null)
        {
            DalamudApi.PluginLog.Error($"Cannot found target by id 0x{entityId:X}");

            return;
        }

        if (sourceTarget == null)
        {
            DalamudApi.PluginLog.Error($"Cannot found source target by id 0x{direct:X}");

            return;
        }

        DalamudApi.PluginLog.Information($"{target.Name} got killed by {sourceTarget.Name}");

        Process(target, sourceTarget, territory);
    }

    private void Process(IGameObject target, IGameObject source, ushort territory)
    {
        var targetName = target.Name.TextValue.ToLower();

        if (!_trackerConditions.TryGetValue(territory, out var nameIdMap))
        {
            DalamudApi.PluginLog.Error($"Cannot get condition name with territory id \"{territory}\"");

            return;
        }

        uint localEntityId = 0;

        if (!nameIdMap.TryGetValue(targetName, out var npcId))
        {
            return;
        }

        try
        {
            if (DalamudApi.ClientState.LocalPlayer is not { } local)
            {
                return;
            }

            localEntityId = local.EntityId;
        }
        catch (Exception e)
        {
            unsafe
            {
                var local = Control.GetLocalPlayer();

                if (local == null)
                {
                    return;
                }

                localEntityId = local->EntityId;
            }
        }

        var currentInstance = _dataManager.FormatCurrentTerritory();

        var sourceOwner   = source.OwnerId;
        DalamudApi.PluginLog.Info($"{sourceOwner:X}, Local: {localEntityId:X}, source.EntityId: {source.EntityId:X}");

        if (sourceOwner        != localEntityId
            && source.EntityId != localEntityId)
        {
            return;
        }

        AddToTracker(currentInstance, _dataManager.GetNpcName(npcId), npcId);
    }

    private delegate void ActorControlDelegate(uint  entityId,
                                               int   id,
                                               uint  arg0,
                                               uint  arg1,
                                               uint  arg2,
                                               uint  arg3,
                                               uint  arg4,
                                               uint  arg5,
                                               ulong targetId,
                                               byte  a10);
}
