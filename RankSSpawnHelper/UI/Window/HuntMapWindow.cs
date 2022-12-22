using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.UI.Window
{
    internal class HuntMapWindow : Dalamud.Interface.Windowing.Window
    {
        private MapTextureInfo _currentMapTexture;

        public HuntMapWindow() : base("狩猎地图##S怪触发小助手")
        {
            Flags |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
        }

        public override void PreOpenCheck()
        {
            if (_currentMapTexture?.texture == null || _currentMapTexture.texture.ImGuiHandle == IntPtr.Zero)
            {
                IsOpen = false;
            }
        }

        public override void Draw()
        {
            ImGui.Image(_currentMapTexture.texture.ImGuiHandle, _currentMapTexture.size * _currentMapTexture.Scale, Vector2.Zero, Vector2.One, Vector4.One);

            ImGui.BeginGroup();
            ImGui.GetWindowDrawList().AddCircleFilled(_currentMapTexture.GetTexturePosition(new Vector2(19.1f, 16.2f)), 4, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)));
            PluginLog.Debug($"{_currentMapTexture.GetTexturePosition(new Vector2(19.1f, 16.2f))}");
            ImGui.EndGroup();
            {
                ImGui.SameLine();
                ImGui.Columns(1);
                ImGui.Text("Stuff");
            }
        }

        public void SetCurrentMapTexture(MapTextureInfo texture)
        {
            _currentMapTexture = texture;
        }
        
    }
}
