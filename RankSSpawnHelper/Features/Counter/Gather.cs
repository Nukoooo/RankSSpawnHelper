using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features;

internal partial class Counter
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 55 56 57 41 54 41 55 41 57 48 8D 6C 24", DetourName = nameof(Detour_ProcessSystemLogMessage))]
    private Hook<SystemLogMessageDelegate> SystemLogMessage { get; init; } = null!;

    private unsafe void Detour_ProcessSystemLogMessage(nint a1, uint eventId, uint logId, nint a4, byte a5)
    {
        SystemLogMessage.Original(a1, eventId, logId, a4, a5);
#if DEBUG || DEBUG_CN
        DalamudApi.PluginLog.Warning($"eventID: 0x{eventId:X}, logId: {logId}");
#endif
        // logId = 9332 => 特殊恶名精英的手下开始了侦察活动……

        var isGatherMessage = logId is 1049 or 1050;

        if (!isGatherMessage)
            return;

        var itemId = *(uint*)a4;

        // 27759 -- 矮人棉
        // 12634 -- 星极花, 12536 -- 皇金矿

        var territoryType = DalamudApi.ClientState.TerritoryType;
        if (!_conditionsMob.TryGetValue(territoryType, out var value))
            return;

        // if the item id isnt in the list
        if (!value.ContainsValue(itemId))
            return;

        var name = Plugin.Managers.Data.GetItemName(itemId);

        AddToTracker(Plugin.Managers.Data.Player.GetCurrentTerritory(), name, itemId, true);
    }

    private delegate void SystemLogMessageDelegate(nint a1, uint a2, uint a3, nint a4, byte a5);
}