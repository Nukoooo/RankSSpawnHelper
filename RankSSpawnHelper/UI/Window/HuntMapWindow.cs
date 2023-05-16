using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.UI.Window
{
    internal class HuntMapWindow : Dalamud.Interface.Windowing.Window
    {
        private string _currentMapInstance = string.Empty;
        private MapTextureInfo _currentMapTexture;
        private string _selectedSpawnPoint = string.Empty;
        private List<SpawnPoints> _spawnPoints = new();

        public HuntMapWindow() : base("狩猎地图##S怪触发小助手")
        {
            Flags |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
        }

        public override void PreOpenCheck()
        {
            if (_currentMapTexture?.texture == null || _currentMapTexture.texture.ImGuiHandle == nint.Zero)
            {
                IsOpen = false;
            }
        }

        public override void Draw()
        {
            ImGui.BeginGroup();
            {
                var cursorScreenPos = ImGui.GetCursorScreenPos();
                ImGui.Image(_currentMapTexture.texture.ImGuiHandle, _currentMapTexture.size * _currentMapTexture.Scale, Vector2.Zero, Vector2.One, Vector4.One);

                foreach (var spawnPoint in _spawnPoints)
                {
                    var textureCoord = _currentMapTexture.GetTexturePosition(new Vector2(spawnPoint.x, spawnPoint.y));
                    var selected     = spawnPoint.key == _selectedSpawnPoint;
                    ImGui.GetWindowDrawList().AddCircleFilled(cursorScreenPos + textureCoord, 5, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));
                    ImGui.GetWindowDrawList().AddCircleFilled(cursorScreenPos + textureCoord, 4, ImGui.GetColorU32(new Vector4(selected ? 1 : 0, selected ? 215.0f / 255.0f : 1, 0, 1)));
                }
            }
            ImGui.EndGroup();
            ImGui.SameLine(0, 15f);
            ImGui.BeginGroup();
            {
                if (_spawnPoints.Count == 0)
                {
                    ImGui.EndGroup();
                    return;
                }

                ImGui.Text($"{_currentMapInstance} 的可触发点位:");
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (var spawnPoint in _spawnPoints)
                {
                    // ReSharper disable once InvertIf
                    if (ImGui.Button($"{spawnPoint.key.Replace("SpawnPoint", "触发点#")} ({spawnPoint.x:0.00}, {spawnPoint.y:0.00})"))
                    {
                        _selectedSpawnPoint = spawnPoint.key;
                        DalamudApi.GameGui.OpenMapWithMapLink(new MapLinkPayload(_currentMapTexture.territory, _currentMapTexture.mapId, spawnPoint.x, spawnPoint.y));
                    }
                }
            }
            ImGui.EndGroup();
        }

        public void SetCurrentMap(MapTextureInfo texture, List<SpawnPoints> spawnPoints, string currentMapInstance)
        {
            _currentMapTexture  = texture;
            _spawnPoints        = spawnPoints;
            _currentMapInstance = currentMapInstance;
            _selectedSpawnPoint = string.Empty;
        }
    }
}