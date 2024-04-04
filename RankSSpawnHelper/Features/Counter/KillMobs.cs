using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features;

internal partial class Counter
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(Detour_ActorControl))]
    private Hook<ActorControlDelegate> ActorControl { get; init; } = null!;

    private const int DeathEventId = 6;

    private void Detour_ActorControl(uint entityId, int type, uint buffId, uint direct, uint damage, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
    {
        ActorControl.Original(entityId, type, buffId, direct, damage, sourceId, arg4, arg5, targetId, a10);

        if (!Plugin.Configuration.TrackKillCount)
            return;

        if (type != DeathEventId)
            return;

        var territory = DalamudApi.ClientState.TerritoryType;

        if (!_conditionsMob.ContainsKey(territory))
            return;

        var target       = DalamudApi.ObjectTable.SearchById(entityId);
        var sourceTarget = DalamudApi.ObjectTable.SearchById(direct);
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

    private void Process(GameObject target, GameObject source, ushort territory)
    {
        var targetName = target.Name.TextValue.ToLower();

        if (!_conditionsMob.TryGetValue(territory, out var nameIdMap))
        {
            DalamudApi.PluginLog.Error($"Cannot get condition name with territory id \"{territory}\"");
            return;
        }

        if (!nameIdMap.TryGetValue(targetName, out var npcId))
            return;

        var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

        var sourceOwner = source.OwnerId;
        if (sourceOwner != DalamudApi.ClientState.LocalPlayer.ObjectId &&
            source.ObjectId != DalamudApi.ClientState.LocalPlayer.ObjectId)
            return;

        AddToTracker(currentInstance, Plugin.Managers.Data.GetNpcName(npcId), npcId);
    }

    private delegate void ActorControlDelegate(uint entityId, int id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte  a10);
}