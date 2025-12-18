using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    private readonly List<string> _weeEaNameList = [];
    private          int          _nonWeeEaCount = 0;

    private void UpdateNameList()
    {
        if (DalamudApi.ClientState.TerritoryType != 960)
        {
            return;
        }

        Vector3 localPosition;

        try
        {
            if (DalamudApi.ObjectTable.LocalPlayer is not { } local)
            {
                return;
            }

            localPosition = local.Position;
        }
        catch (Exception)
        {
            unsafe
            {
                var local = Control.GetLocalPlayer();

                if (local == null)
                {
                    return;
                }

                localPosition = local->Position;
            }
        }

        lock (_weeEaNameList)
        {
            _weeEaNameList.Clear();
            _nonWeeEaCount = 0;

            var enumerator = DalamudApi.ObjectTable.Where(i => i.Address       != nint.Zero
                                                               && i.ObjectKind == ObjectKind.Companion);

            foreach (var obj in enumerator)
            {
                var delta = obj.Position - localPosition;

                var length2D = Math.Sqrt((delta.X * delta.X) + (delta.Z * delta.Z));

                if (length2D > 10)
                {
                    continue;
                }

                if (obj.Name.ToString() != "小异亚")
                {
                    _nonWeeEaCount++;

                    continue;
                }

                var owner = (IPlayerCharacter) DalamudApi.ObjectTable[obj.ObjectIndex - 1]!;

                var name = $"{owner.Name.TextValue}@{owner.HomeWorld.Value.Name.ExtractText()}";
                _weeEaNameList.Add(name);
            }
        }
    }
}