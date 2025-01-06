using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    private readonly List<string> _weeEaNameList = [];
    private          int          _nonWeeEaCount = 0;

    private void UpdateNameList()
    {
        if (DalamudApi.ClientState.LocalPlayer is not { } local || DalamudApi.ClientState.TerritoryType != 960)
        {
            return;
        }

        lock (_weeEaNameList)
        {
            _weeEaNameList.Clear();
            _nonWeeEaCount = 0;

            var enumerator = DalamudApi.ObjectTable.Where(i => i.Address       != nint.Zero
                                                               && i.ObjectKind == ObjectKind.Companion);

            var localPlayerPos = DalamudApi.ClientState.LocalPlayer.Position;

            foreach (var obj in enumerator)
            {
                var delta = obj.Position - localPlayerPos;

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

                var name = $"{owner.Name.TextValue}@{owner.HomeWorld.GameData!.Name.RawString}";
                _weeEaNameList.Add(name);
            }
        }
    }
}