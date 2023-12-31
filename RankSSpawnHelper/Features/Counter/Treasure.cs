using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features;

internal partial class Counter
{
    private const byte TreasureMapsCode = 0x54;

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 48 83 EC ?? 0F B6 81 ?? ?? ?? ?? 48 8B D9 F3 0F 11 89", DetourName = nameof(Detour_ProcessOpenTreasurePacket))]
    private Hook<ProcessOpenTreasurePacketDeleagate> ProcessOpenTreasure { get; init; } = null!;

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B D9 49 8B F8 41 0F B7 08", DetourName = nameof(Detour_ProcessActorControlSelfPacket))]
    private Hook<ProcessActorControlSelfPacketDelegate> ProcessActorControlSelf { get; init; } = null!;

    private void Detour_ProcessOpenTreasurePacket(nint a1, float a2, float a3, float a4)
    {
        PluginLog.Warning($"[OpenTreasurePacket] a1: 0x{a1:X}, a2: {a2} a3: {a3} a4: {a4}");

        ProcessOpenTreasure.Original(a1, a2, a3, a4);
    }

    private char Detour_ProcessActorControlSelfPacket(long a1, long a2, nint data)
    {
        ProcessTreasureMap(data);

        return ProcessActorControlSelf.Original(a1, a2, data);
    }

    // https://git.anna.lgbt/anna/Globetrotter/src/branch/main/Globetrotter/TreasureMaps.cs
    private unsafe void ProcessTreasureMap(nint data)
    {
        var category = *(byte*)data;
        if (category != TreasureMapsCode)
        {
            return;
        }

        var eventItemId = *(uint*)(data + 4);
        var subRowId = *(uint*)(data + 8);
        var justOpened = *(uint*)(data + 12) == 1;
        PluginLog.Debug($"{eventItemId}, {subRowId}, {justOpened}");

        switch (eventItemId)
        {
            case 2001763: // 飞龙革制的宝物地图
                break;
            // 阿格
            case 2001351: // 神秘地图
            case 2001352: // 鞣革制的秘宝地图
            case 2001089: // 巨蟾蜍革制的宝物地图
            case 2001090: // 野猪革制的宝物地图
            case 2001091: // 毒蜥蜴革制的宝物地图
                break;

        } 
    }

    private delegate char ProcessActorControlSelfPacketDelegate(long a1, long a2, nint dataPtr);
    private delegate void ProcessOpenTreasurePacketDeleagate(nint a1, float a2, float a3, float a4);
}