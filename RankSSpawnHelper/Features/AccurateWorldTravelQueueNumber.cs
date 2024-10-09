using System;
using Dalamud;

namespace RankSSpawnHelper.Features;

internal class AccurateWorldTravelQueueNumber : IDisposable
{
    private readonly nint   _address1;
    private readonly nint   _address2;
    private readonly byte[] _bytes1 = [];

    private readonly byte[] _bytes2 = [];

    public AccurateWorldTravelQueueNumber()
    {
        if (DalamudApi.SigScanner.TryScanText("81 C2 F5 ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D0 48 8D 8C 24", out _address1))
        {
            if (!SafeMemory.ReadBytes(_address1 + 2, 2, out _bytes1))
            {
                throw new("Failed to read bytes #1");
            }

            if (_bytes1[0] == 0xF4)
                _bytes1[0] = 0xF5;
        }

        if (DalamudApi.SigScanner.TryScanText("83 F8 ?? 73 ?? 44 8B C0 1B D2", out _address2))
        {
            if (!SafeMemory.ReadBytes(_address2, 5, out _bytes2))
            {
                throw new("Failed to read bytes #1");
            }

            if (_bytes2[0] == 0x90)
                _address2 = 0;
        }
    }

    public void Dispose()
    {
        Patch(false);
    }

    public void Patch(bool enabled)
    {
        if (!IsValid())
            return;

        if (enabled)
        {
            SafeMemory.WriteBytes(_address1 + 2, [0xF4, 0x30]);
            SafeMemory.WriteBytes(_address2, [0x90, 0x90, 0x90, 0x90, 0x90]);
        }
        else
        {
            SafeMemory.WriteBytes(_address1 + 2, _bytes1);
            SafeMemory.WriteBytes(_address2, _bytes2);
        }
    }

    public bool IsValid()
    {
        return _address1 != 0 && _address2 != 0;
    }
}