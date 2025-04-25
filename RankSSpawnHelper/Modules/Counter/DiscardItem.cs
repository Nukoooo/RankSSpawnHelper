using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    private Hook<InventoryTransactionDiscardDelegate> InventoryTransactionDiscard { get; set; } = null!;

    private void ChatGui_OnChatMessage(XivChatType  type,
                                       int          timestamp,
                                       ref SeString sender,
                                       ref SeString message,
                                       ref bool     ishandled)
    {
        if (type != XivChatType.SystemMessage || DalamudApi.ClientState.TerritoryType != 621)
        {
            return;
        }

        var reg = DiscardItemReg()
            .Match(message.ToString());

        if (!reg.Success)
        {
            return;
        }

        const string name = "扔垃圾";
        AddToTracker(_dataManager.FormatCurrentTerritory(), name, 0, true);
    }

    private unsafe void Detour_InventoryTransactionDiscard(nint a1, nint a2)
    {
        InventoryTransactionDiscard.Original(a1, a2);

        /*
        var slot      = *(int*)(a2 + 0xC);
        var slotIndex = *(int*)(a2 + 0x10);
        */

        // Use regenny or reclass to find this
        var amount = *(uint*) (a2 + 0x14);
        var itemId = *(uint*) (a2 + 0x18); // will not return HQ item id

        var territoryType = DalamudApi.ClientState.TerritoryType;

        DalamudApi.PluginLog.Debug($"{amount}, {itemId}, {_dataManager.GetItemName(itemId)}");

        if (territoryType != 813 && territoryType != 961 && territoryType != 1189)
        {
            return;
        }

        if (!_trackerConditions.TryGetValue(territoryType, out var value))
        {
            return;
        }

        if (!value.ContainsValue(itemId))
        {
            return;
        }

        switch (territoryType)
        {
            case 961 when amount  < 5:
            case 1189 when amount < 50:
                return;
        }

        var name = _dataManager.GetItemName(itemId);

        AddToTracker(_dataManager.FormatCurrentTerritory(), name, itemId, true);
    }

    /*private unsafe delegate bool UseActionDelegate(nint a1, ActionType actionType, uint actionId, long targetId, uint a4, uint a5, uint a6, void* a7);*/

    private delegate void InventoryTransactionDiscardDelegate(nint a1, nint a2);

    [GeneratedRegex("舍弃了“\ue0bb(.*)”")]
    private static partial Regex DiscardItemReg();

    /*
    private delegate void ProcessInventoryActionAckPacketDelegate(nint a1, uint a2, nint a3);
    */
}
