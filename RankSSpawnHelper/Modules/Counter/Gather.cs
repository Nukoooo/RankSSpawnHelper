using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 55 56 41 54 41 55 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 49 8B F1",
               DetourName = nameof(Detour_ProcessSystemLogMessage))]
    private Hook<SystemLogMessageDelegate> SystemLogMessage { get; init; } = null!;

    private unsafe void Detour_ProcessSystemLogMessage(nint a1, uint eventId, uint logId, uint* data, byte length)
    {
        SystemLogMessage.Original(a1, eventId, logId, data, length);

#if DEBUG || DEBUG_CN
        for (var i = 0; i < length; i++)
        {
            DalamudApi.PluginLog.Warning($"a4[#{i}]: {data[i]}");
        }

        DalamudApi.PluginLog.Warning($"eventID: 0x{eventId:X}, logId: {logId}");
#endif

        // logId = 9332 => 特殊恶名精英的手下开始了侦察活动……
        // logId = 1159 => 物品x数量 制作成功!

        var isGatherMessage = logId is 1049 or 1050;
        var isCraftMessage  = logId == 1157;

        if (!isGatherMessage && !isCraftMessage)
        {
            return;
        }

        var itemId       = data[0];
        var isHq         = itemId > 1000000;
        var normalizedId = itemId > 1000000 ? itemId - 1000000 : itemId;

        // 27759 -- 矮人棉
        // 12634 -- 星极花, 12536 -- 皇金矿

        var territoryType = DalamudApi.ClientState.TerritoryType;

        if (!_trackerConditions.TryGetValue(territoryType, out var value))
        {
            return;
        }

        if (territoryType == 1191 /*遗产之地*/ && !isHq)
        {
            return;
        }

        // if the item id isnt in the list
        if (!value.ContainsValue(normalizedId))
        {
            return;
        }

        var name = _dataManager.GetItemName(normalizedId);

        AddToTracker(_dataManager.FormatCurrentTerritory(), name, normalizedId, true);
    }

    private unsafe delegate void SystemLogMessageDelegate(nint a1, uint a2, uint a3, uint* a4, byte a5);
}
