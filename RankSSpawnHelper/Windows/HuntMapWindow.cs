using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;
using IPluginDataManager = RankSSpawnHelper.Managers.IDataManager;

namespace RankSSpawnHelper.Windows;

internal class HuntMapWindow : Window
{
    public const  string Name  = "狩猎地图";
    private const float  Scale = 0.25f;

    private readonly IPluginDataManager _dataManager;

    private uint _territoryId;

    private List<SpawnPoints> _spawnPoints = [];
    private int               _selectedSpawnPoint;

    public HuntMapWindow(ServiceProvider provider) : base(Name)
    {
        IsOpen = false;

        Flags |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;

        _dataManager = provider.GetService<IPluginDataManager>()
                       ?? throw new NullReferenceException("Failed to get DataManager");
    }

    public override void OnOpen()
    {
        if (_territoryId == 0)
        {
            IsOpen = false;
        }
    }

    public override void Draw()
    {
        var info = _dataManager.GetTerritoryInfo(_territoryId);

        var sharedTexture = DalamudApi.TextureProvider.GetFromGame(info.Path);
        var texture       = sharedTexture.GetWrapOrEmpty();

        ImGui.BeginGroup();

        {
            var cursorScreenPos = ImGui.GetCursorScreenPos();

            ImGui.Image(texture.Handle, texture.Size * Scale, Vector2.Zero, Vector2.One, Vector4.One);

            for (var i = 0; i < _spawnPoints.Count; i++)
            {
                var spawnPoint   = _spawnPoints[i];

                var textureCoord = info.GetTexturePosition(new (spawnPoint.X, spawnPoint.Y)) * Scale;

                var selected = i == _selectedSpawnPoint;

                ImGui.GetWindowDrawList()
                     .AddCircleFilled(cursorScreenPos + textureCoord, 6, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));

                ImGui.GetWindowDrawList()
                     .AddCircleFilled(cursorScreenPos + textureCoord,
                                      5,
                                      ImGui.GetColorU32(new Vector4(selected ? 1 : 0, selected ? 215.0f / 255.0f : 1, 0, 1)));
            }
        }

        ImGui.EndGroup();
        ImGui.SameLine(0, 15);
        ImGui.BeginGroup();

        {
            if (_spawnPoints.Count == 0)
            {
                ImGui.EndGroup();

                return;
            }

            for (var i = 0; i < _spawnPoints.Count; i++)
            {
                var spawnPoint = _spawnPoints[i];

                if (ImGui.Button($"{spawnPoint.Key.Replace("SpawnPoint", "触发点#")} ({spawnPoint.X:0.00}, {spawnPoint.Y:0.00})"))
                {
                    _selectedSpawnPoint = i;
                    DalamudApi.GameGui.OpenMapWithMapLink(new (_territoryId, info.MapId, spawnPoint.X, spawnPoint.Y));
                }
            }
        }

        ImGui.EndGroup();
    }

    public void SetCurrentMap(List<SpawnPoints> spawnPoints, uint territoryId)
    {
        _selectedSpawnPoint = -1;

        _spawnPoints = spawnPoints;
        _territoryId = territoryId;
        IsOpen       = true;
    }
}
