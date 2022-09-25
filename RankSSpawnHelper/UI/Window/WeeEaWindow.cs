using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ImGuiNET;

namespace RankSSpawnHelper.UI.Window
{
    internal class WeeEaWindow : global::Dalamud.Interface.Windowing.Window
    {
        public WeeEaWindow() : base("异亚计数##RankSSpawnHelper")
        {
            Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;
        }

        public override void PreOpenCheck()
        {
            IsOpen = DalamudApi.ClientState.LocalPlayer != null && DalamudApi.ClientState.IsLoggedIn && DalamudApi.ClientState.TerritoryType == 960 &&
                     Plugin.Configuration.WeeEaCounter;
        }

        public override void Draw()
        {
            var count  = 0;
            var count2 = 0;

            var enumerator = DalamudApi.ObjectTable.Where(i =>
                                                            i != null && i.Address != IntPtr.Zero &&
                                                            i is Npc &&
                                                            i.ObjectKind == ObjectKind.Companion);

            foreach (var obj in enumerator)
            {
                var delta = obj.Position - DalamudApi.ClientState.LocalPlayer.Position;

                // xzy 
                var length2D = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);

                if (length2D > 17.0)
                    continue;

                if (obj.Name.ToString() == "小异亚")
                    count++;
                else
                    count2++;
            }

            if (Plugin.Managers.Font.IsFontBuilt())
                ImGui.PushFont(Plugin.Managers.Font.Yahei24);

            ImGui.Text($"附近的小异亚数量:{count}\n非小异亚的数量: {count2}");

            if (Plugin.Managers.Font.IsFontBuilt())
                ImGui.PopFont();
        }
    }
}