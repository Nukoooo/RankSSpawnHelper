using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature(
                  "48 89 5C 24 ?? 55 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 4C 8B 81 ?? ?? ?? ?? 33 DB 8B 7A ?? 48 8B EA 48 8D 15 ?? ?? ?? ?? 48 8B F1 4D 85 C0 75 ?? 8B C3 EB ?? 0F B7 C3 0F B7 C8 39 3C 8A 74 ?? 66 FF C0 66 83 F8 ?? 72 ?? 48 8B C3 EB ?? 48 8D 04 49 49 39 1C C0 49 8D 04 C0 48 0F 44 C3 80 78 ?? ?? 0F 85 ?? ?? ?? ?? 81 FF ?? ?? ?? ?? 75 ?? 4D 85 C0 0F 84 ?? ?? ?? ?? 0F B7 C3 66 0F 1F 84 00 ?? ?? ?? ?? 0F B7 C8 81 3C 8A ?? ?? ?? ?? 74 ?? 66 FF C0 66 83 F8 ?? 72 ?? E9 ?? ?? ?? ?? 48 8D 04 49 49 83 3C C0 ?? 49 8D 0C C0 0F 84 ?? ?? ?? ?? 48 85 C9 0F 84 ?? ?? ?? ?? 45 33 C9 89 5C 24 ?? 45 33 C0 33 D2 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 8B 96 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 8B 96 ?? ?? ?? ?? 8B CF E8 ?? ?? ?? ?? 8B 8E ?? ?? ?? ?? B8 ?? ?? ?? ?? FF C1 F7 E1 8B C1 2B C2 D1 E8 03 C2 C1 E8 ?? 69 C0 ?? ?? ?? ?? 2B C8 0F BA E9 ?? 89 8E ?? ?? ?? ?? E9 ?? ?? ?? ?? 4D 85 C0 0F 84 ?? ?? ?? ?? 0F B7 C3 0F 1F 80 ?? ?? ?? ?? 0F B7 C8 39 3C 8A 74 ?? 66 FF C0 66 83 F8 ?? 72 ?? E9 ?? ?? ?? ?? 48 8D 04 49 49 83 3C C0 ?? 49 8D 0C C0 0F 84 ?? ?? ?? ?? 48 85 C9 0F 84 ?? ?? ?? ?? 0F BF 55 ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 0F 84 ?? ?? ?? ?? 33 C0 4C 89 B4 24",
                  DetourName = nameof(Detour_InventoryTransactionDiscard))]
    private Hook<InventoryTransactionDiscardDelegate> InventoryTransactionDiscard { get; init; } = null!;

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
