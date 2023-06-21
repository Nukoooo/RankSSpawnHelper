#if DEBUG || DEBUG_CN

using System;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper;

internal class DebugThingy : IDisposable
{
    /*// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 41 57 48 83 EC ?? 48 8B D9 4C 8B FA", DetourName = nameof(Detour_ReceiveListing))]
    private Hook<ReceiveListingDelegate> _receiveListingHook { get; init; } = null!;


    private delegate void ReceiveListingDelegate(IntPtr managerPtr, IntPtr data);*/

    public DebugThingy()
    {
        SignatureHelper.Initialise(this);
        // DalamudApi.GameNetwork.NetworkMessage += GameNetwork_NetworkMessage;
        // _receiveListingHook.Enable();
        // DalamudApi.PartyFinderGui.ReceiveListing += PartyFinderGui_ReceiveListing;
    }

    /*private unsafe void Detour_ReceiveListing(nint managerPtr, nint data)
    {
        var timestamp = *(uint*)(data + 0x58);
        PluginLog.Debug($"data: 0x{data:X} | {DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLongDateString()}");

        _receiveListingHook.Original(managerPtr, data);
    }*/

    public void Dispose()
    {
        // _receiveListingHook.Dispose();
        // DalamudApi.GameNetwork.NetworkMessage    -= GameNetwork_NetworkMessage;
        // DalamudApi.PartyFinderGui.ReceiveListing -= PartyFinderGui_ReceiveListing;
    }

    private void PartyFinderGui_ReceiveListing(PartyFinderListing listing, PartyFinderListingEventArgs args)
    {
        PluginLog.Debug($"{listing.LastPatchHotfixTimestamp:X} / {listing.LastPatchHotfixTimestamp}");
    }

    private void GameNetwork_NetworkMessage(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
                                            NetworkMessageDirection direction)
    {
        if (direction != NetworkMessageDirection.ZoneDown)
            return;

        PluginLog.Warning($"opcode: {opCode:X}");
    }
}

#endif