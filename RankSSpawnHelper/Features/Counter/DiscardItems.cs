using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace RankSSpawnHelper.Features;

internal partial class Counter
{
    private bool _usingItem;
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 55 56 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 4C 8B 89", DetourName = nameof(Detour_InventoryTransactionDiscard))]
    private Hook<InventoryTransactionDiscardDelegate> InventoryTransactionDiscard { get; init; } = null!;

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("E8 ?? ?? ?? ?? EB 64 B1 01", DetourName = nameof(Detour_UseAction))]
    private Hook<UseActionDelegate> UseActionHook { get; init; } = null!;

    private unsafe bool Detour_UseAction(nint a1, ActionType actionType, uint actionId, long targetId, uint a4, uint a5, uint a6, void* a7)
    {
        PluginLog.Debug($"{actionType} / targetId: 0x{targetId:X}, actionId: {actionId}");

        var original = UseActionHook.Original(a1, actionType, actionId, targetId, a4, a5, a6, a7);
        if (actionType != ActionType.Item)
            return original;

        if (targetId != 0xE0000000)
            return original;

        var itemId = actionId;
        if (itemId >= 1000000)
            itemId -= 1000000;

        if (!Plugin.Managers.Data.IsItem(itemId))
            return original;

        PluginLog.Debug($"targetId: 0x{targetId:X}, actionId: {itemId}");

        _usingItem = true;
        
        return original;
    }

    private unsafe void Detour_InventoryTransactionDiscard(nint a1, nint a2)
    {
        /*
        var slot      = *(int*)(a2 + 0xC);
        var slotIndex = *(int*)(a2 + 0x10);
        */

        // Use regenny or reclass to find this
        var amount = *(uint*)(a2 + 0x14);
        var itemId = *(uint*)(a2 + 0x18); // will not return HQ item id

        var territoryType = DalamudApi.ClientState.TerritoryType;

        // filter it out, just in case..
        if (territoryType != 813 && territoryType != 621 && territoryType != 961)
            goto callOrginal;

        if (!_conditionsMob.TryGetValue(territoryType, out var value))
            goto callOrginal;

        // you can discard anything in The Lochs
        if (territoryType != 621 && !value.ContainsValue(itemId))
            goto callOrginal;

        switch (territoryType)
        {
            case 621:
                var action = Plugin.Managers.Data.GetItemAction(itemId);
                if (action.Type == 0)
                    goto final;

                if (_usingItem)
                    goto callOrginal;

                if (!(DalamudApi.Condition[ConditionFlag.Mounted] || DalamudApi.Condition[ConditionFlag.Mounted2]))
                {
                    PluginLog.Debug("Found using an item, skipping");
                    goto callOrginal;
                }

            final:
                itemId = 0;
                break;
            case 961 when amount != 5:
                goto callOrginal;
        }

        var name = territoryType == 621 ? Plugin.IsChina() ? "扔垃圾" : "Item" : Plugin.Managers.Data.GetItemName(itemId);

        AddToTracker(Plugin.Managers.Data.Player.GetCurrentTerritory(), name, itemId, true);

    callOrginal:
        _usingItem = false;
        InventoryTransactionDiscard.Original(a1, a2);
    }

    private unsafe delegate bool UseActionDelegate(nint a1, ActionType actionType, uint actionId, long targetId, uint a4, uint a5, uint a6, void* a7);

    private delegate void InventoryTransactionDiscardDelegate(nint a1, nint a2);
}