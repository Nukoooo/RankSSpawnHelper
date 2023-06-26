using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features;

internal partial class Counter : IDisposable
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 48 83 EC ?? 0F B6 81 ?? ?? ?? ?? 48 8B D9 F3 0F 11 89", DetourName = nameof(Detour_ProcessOpenTreasurePacket))]
    private Hook<ProcessOpenTreasurePacketDeleagate> ProcessOpenTreasure { get; init; } = null!;

    private void Detour_ProcessOpenTreasurePacket(nint a1, float a2, float a3, float a4)
    {
        PluginLog.Warning($"[OpenTreasurePacket] a1: 0x{a1:X}, a2: {a2} a3: {a3} a4: {a4}");

        ProcessOpenTreasure.Original(a1, a2, a3, a4);
    }

    private delegate void ProcessOpenTreasurePacketDeleagate(nint a1, float a2, float a3, float a4);
}