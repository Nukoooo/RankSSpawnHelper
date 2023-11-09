using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using System.Text.RegularExpressions;

namespace RankSSpawnHelper.Features;

internal partial class Counter
{

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 55 56 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 4C 8B 89", DetourName = nameof(Detour_InventoryTransactionDiscard))]
    private Hook<InventoryTransactionDiscardDelegate> InventoryTransactionDiscard { get; init; } = null!;

    /*// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("E8 ?? ?? ?? ?? EB 64 B1 01", DetourName = nameof(Detour_UseAction))]
    private Hook<UseActionDelegate> UseActionHook { get; init; } = null!;*/

    /*// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 41 0F B6 50", DetourName = nameof(Detour_ProcessInventoryActionAckPacket))]
    private Hook<ProcessInventoryActionAckPacketDelegate> ProcessInventoryActionAckPacketHook { get; init; } = null!;

    private unsafe void Detour_ProcessInventoryActionAckPacket(nint a1, uint a2, nint a3)
    {
        Util.DumpMemory(a3, 96);

        ProcessInventoryActionAckPacketHook.Original(a1, a2, a3);
    }*/


    /*private unsafe bool Detour_UseAction(nint a1, ActionType actionType, uint actionId, long targetId, uint a4, uint a5, uint a6, void* a7)
    {
        PluginLog.Debug($"{actionType} / targetId: 0x{targetId:X}, actionId: {actionId}");

        var original = UseActionHook.Original(a1, actionType, actionId, targetId, a4, a5, a6, a7);
        switch (actionType)
        {
            case ActionType.Item when targetId == 0xE0000000:
            {
                var itemId = actionId;
                if (itemId >= 1000000)
                    itemId -= 1000000;

                if (!Plugin.Managers.Data.IsItem(itemId))
                    return original;

                PluginLog.Debug($"targetId: 0x{targetId:X}, actionId: {itemId}");

                _usingItem = true;
                break;
            }
            case ActionType.Spell when targetId == 0xE0000000:
            {
                if (actionId is 300 or 268 or 297)
                    _usingItem = true;
                break;
            }
        }

        return original;
    }*/


    private void ChatGui_OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (type != XivChatType.SystemMessage)
            return;
        var territoryType = DalamudApi.ClientState.TerritoryType;
        if (territoryType != 621)
            return;

        var reg = DiscardItemReg().Match(message.ToString());
        if (!reg.Success)
        {
            return;
        }

        var name =  Plugin.IsChina() ? "扔垃圾" : "Item";
        AddToTracker(Plugin.Managers.Data.Player.GetCurrentTerritory(), name, 0, true);
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
        if (territoryType != 813 && territoryType != 961)
            goto callOrginal;

        if (!_conditionsMob.TryGetValue(territoryType, out var value))
            goto callOrginal;

        // you can discard anything in The Lochs
        if ( !value.ContainsValue(itemId))
            goto callOrginal;

        switch (territoryType)
        {
            case 961 when amount != 5:
                goto callOrginal;
        }

        var name = Plugin.Managers.Data.GetItemName(itemId);

        AddToTracker(Plugin.Managers.Data.Player.GetCurrentTerritory(), name, itemId, true);

    callOrginal:
        InventoryTransactionDiscard.Original(a1, a2);
    }

    /*private unsafe delegate bool UseActionDelegate(nint a1, ActionType actionType, uint actionId, long targetId, uint a4, uint a5, uint a6, void* a7);*/

    private delegate void InventoryTransactionDiscardDelegate(nint a1, nint a2);

    [GeneratedRegex("舍弃了“\ue0bb(.*)”")]
    private static partial Regex DiscardItemReg();

    /*
    private delegate void ProcessInventoryActionAckPacketDelegate(nint a1, uint a2, nint a3);
    */
}