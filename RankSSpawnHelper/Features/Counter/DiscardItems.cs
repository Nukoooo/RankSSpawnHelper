using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace RankSSpawnHelper.Features;

internal partial class Counter : IDisposable
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 55 56 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 4C 8B 89", DetourName = nameof(Detour_InventoryTransactionDiscard))]
    private Hook<InventoryTransactionDiscardDelegate> InventoryTransactionDiscard { get; init; } = null!;

    private unsafe void Detour_InventoryTransactionDiscard(nint a1, nint a2)
    {
        /*
        var slot      = *(uint*)(a2 + 0xC);
        var slotIndex = *(uint*)(a2 + 0x10);
        */

        // Use regenny or reclass to find this
        var amount = *(uint*)(a2 + 0x14);
        var itemId = *(uint*)(a2 + 0x18);

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
                if (!(DalamudApi.Condition[ConditionFlag.Mounted] || DalamudApi.Condition[ConditionFlag.Mounted2]) &&
                    ActionManager.Instance()->GetActionStatus(ActionType.Item, itemId) != 0)
                {
                    PluginLog.Debug("Found using an item, skipping");
                    goto callOrginal;
                }

                itemId = 0;
                break;
            case 961 when amount != 5:
                goto callOrginal;
        }

        var name = territoryType == 621 ? Plugin.IsChina() ? "扔垃圾" : "Item" : Plugin.Managers.Data.GetItemName(itemId);

        AddToTracker(Plugin.Managers.Data.Player.GetCurrentTerritory(), name, itemId, true);

    callOrginal:
        InventoryTransactionDiscard.Original(a1, a2);
    }

    private delegate void InventoryTransactionDiscardDelegate(nint a1, nint a2);
}