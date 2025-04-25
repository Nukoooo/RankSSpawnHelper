using System.Collections.Frozen;
using System.Numerics;
using System.Text;
using Dalamud.Game;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace RankSSpawnHelper.Managers;

internal enum GameExpansion
{
    ARealmReborn,
    Heavensward,
    Stormblood,
    Shadowbringers,
    Endwalker,
    Dawntrail,
}

internal record HuntData
{
    public uint          Id            { get; init; }
    public string        LocalizedName { get; init; } = string.Empty;
    public string        KeyName       { get; set; }  = string.Empty;
    public GameExpansion Expansion     { get; init; }
}

internal readonly record struct TerritoryInfo
{
    public string Path       { get; init; }
    public float  SizeFactor { get; init; }
    public uint   MapId      { get; init; }

    public Vector2 GetTexturePosition(Vector2 coordinate)
        => new (FlagToPixelCoordinate(SizeFactor, coordinate.X), FlagToPixelCoordinate(SizeFactor, coordinate.Y));

    private static float FlagToPixelCoordinate(float scale, float flag, int resolution = 2048)
        => (((flag - 1f) * scale * 0.01f) / 40.85f) * resolution;
}

internal interface IDataManager
{
    string GetNpcName(uint id);

    uint GetNpcId(string name);

    string GetTerritoryName(uint id);

    uint GetTerritoryId(string name);

    string GetWorldName(uint id);

    uint GetWorldId(string name);

    string GetItemName(uint id);

    uint GetItemId(string name);

    bool IsFromOtherDataCenter(uint worldId);

    string FormatInstance(uint worldId, uint territoryId, uint instance);

    uint GetCurrentWorldId();

    uint GetCurrentTerritoryId();

    uint GetCurrentInstance();

    string FormatCurrentTerritory();

    List<string> GetServerList();

    bool IsSRank(uint id);

    string GetSRankKeyName(uint id);

    string GetSRankName(uint id);

    uint GetSRankIdByLocalizedName(string name);

    HuntData? GetHuntData(uint id);

    HuntData? GetHuntDataByLocalizedName(string name);

    List<HuntData> GetSRanksByExpansion(GameExpansion expansion);

    TerritoryInfo GetTerritoryInfo(uint territoryId);

    bool IsTerritoryItemThingy(uint territoryId);

    IFontHandle NotoSan24 { get; }
    IFontHandle NotoSan18 { get; }
}

internal class DataManger : IDataManager, IModule
{
    private readonly FrozenDictionary<uint, string>  _itemName;
    private readonly FrozenDictionary<uint, string>  _npcName;
    private readonly FrozenDictionary<uint, string>  _territoryName;
    private readonly FrozenDictionary<uint, string>  _worldName;
    private readonly ExcelSheet<World>               _worldSheet;
    private readonly ExcelSheet<TerritoryType>       _territoryTypeSheet;
    private          List<string>                    _serverList    = [];
    private readonly List<HuntData>                  _huntData      = [];
    private readonly Dictionary<uint, TerritoryInfo> _territoryInfo = [];

    private uint _currentDataCenterRowId;

    public DataManger()
    {
        _worldSheet = DalamudApi.DataManager.GetExcelSheet<World>()
                      ?? throw new InvalidOperationException("Failed to get World sheet");

        _npcName
            = DalamudApi.DataManager.GetExcelSheet<BNpcName>()!.ToFrozenDictionary(i => i.RowId, i => i.Singular.ExtractText());

        _territoryTypeSheet = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()!;

        _territoryName = _territoryTypeSheet.ToFrozenDictionary(i => i.RowId,
                                                                i => i.PlaceName.Value!.Name.ExtractText());

        _itemName
            = DalamudApi.DataManager.GetExcelSheet<Item>()!.ToFrozenDictionary(i => i.RowId, i => i.Singular.ExtractText());

        _worldName = _worldSheet.ToFrozenDictionary(i => i.RowId, i => i.Name.ExtractText());

        SetupFont();

        SetupHuntData();

        ClientStateOnLogin();
    }

    public string GetNpcName(uint id)
        => _npcName.GetValueOrDefault(id, string.Empty);

    public uint GetNpcId(string name)
        => _npcName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase))
                   .Select(key => key.Key)
                   .FirstOrDefault();

    public string GetTerritoryName(uint id)
        => _territoryName.GetValueOrDefault(id, string.Empty);

    public uint GetTerritoryId(string name)
        => _territoryName.Where(key => string.Equals(key.Value, name, StringComparison.OrdinalIgnoreCase))
                         .Select(key => key.Key)
                         .FirstOrDefault();

    public uint GetWorldId(string name)
        => _worldName.Where(key => string.Equals(key.Value, name, StringComparison.OrdinalIgnoreCase))
                     .Select(key => key.Key)
                     .FirstOrDefault();

    public string GetItemName(uint id)
        => _itemName.GetValueOrDefault(id, string.Empty);

    public uint GetItemId(string name)
        => _itemName.Where(key => string.Equals(key.Value, name, StringComparison.OrdinalIgnoreCase))
                    .Select(key => key.Key)
                    .FirstOrDefault();

    public bool IsFromOtherDataCenter(uint worldId)
        => _currentDataCenterRowId
           != _worldSheet.GetRow(worldId)!
                         .DataCenter.Value!.RowId;

    public string GetWorldName(uint id)
        => _worldName.GetValueOrDefault(id, string.Empty);

    public string FormatInstance(uint worldId, uint territoryId, uint instance)
        => GetWorldName(worldId) + '@' + GetTerritoryName(territoryId) + (instance == 0 ? string.Empty : $"@{instance}");

    public uint GetCurrentWorldId()
        => DalamudApi.ClientState.LocalPlayer is { } local ? local.CurrentWorld.RowId : 0;

    public uint GetCurrentTerritoryId()
        => DalamudApi.ClientState.TerritoryType;

    public unsafe uint GetCurrentInstance()
        => UIState.Instance()->PublicInstance.InstanceId;

    public string FormatCurrentTerritory()
    {
        var worldId = GetCurrentWorldId();

        return worldId == 0 ? string.Empty : FormatInstance(worldId, GetCurrentTerritoryId(), GetCurrentInstance());
    }

    public List<string> GetServerList()
        => _serverList;

    public bool IsSRank(uint id)
        => _huntData.Any(i => i.Id == id);

    public string GetSRankKeyName(uint id)
        => _huntData.First(i => i.Id == id)
                    .KeyName;

    public string GetSRankName(uint id)
        => _huntData.First(i => i.Id == id)
                    .LocalizedName;

    public uint GetSRankIdByLocalizedName(string name)
        => _huntData.First(i => i.LocalizedName == name)
                    .Id;

    public HuntData? GetHuntData(uint id)
        => _huntData.Find(i => i.Id == id);

    public HuntData? GetHuntDataByLocalizedName(string name)
        => GetHuntData(GetSRankIdByLocalizedName(name));

    public List<HuntData> GetSRanksByExpansion(GameExpansion expansion)
        => _huntData.Where(i => i.Expansion == expansion)
                    .ToList();

    public TerritoryInfo GetTerritoryInfo(uint territoryId)
    {
        if (_territoryInfo.TryGetValue(territoryId, out var value))
        {
            return value;
        }

        value = new ()
        {
            Path       = GetTerritoryMapPath(territoryId),
            SizeFactor = GetTerritorySizeFactor(territoryId),
            MapId      = GetTerritoryMapRowId(territoryId),
        };

        _territoryInfo.Add(territoryId, value);

        return value;
    }

    public bool IsTerritoryItemThingy(uint territoryId)
        => territoryId is 814 or 400 or 961 or 813 or 1189 or 1191;

    public IFontHandle NotoSan24 { get; private set; } = null!;
    public IFontHandle NotoSan18 { get; private set; } = null!;

    public bool Init()
    {
        DalamudApi.ClientState.Login += ClientStateOnLogin;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.ClientState.Login -= ClientStateOnLogin;
        NotoSan18.Dispose();
        NotoSan24.Dispose();
    }

    private void ClientStateOnLogin()
    {
        if (DalamudApi.ClientState.LocalPlayer is not { } local
            || local.HomeWorld.ValueNullable is not { } homeWorld
            || homeWorld.DataCenter.ValueNullable is not { } dc)
        {
            return;
        }

        _currentDataCenterRowId = dc.RowId;

        _serverList = _worldSheet.Where(i => i.DataCenter.Value.RowId == dc.RowId)
                                 .Select(i => i.Name.ExtractText())
                                 .ToList();
    }

    private void SetupFont()
    {
        const string fontName = "NotoSansCJKsc-Medium.otf";

        var fontPath = Path.Combine(DalamudApi.Interface.DalamudAssetDirectory.FullName, "UIRes", fontName);

        using (ImGuiHelpers.NewFontGlyphRangeBuilderPtrScoped(out var builder))
        {
            builder.AddRanges(ImGui.GetIO()
                                   .Fonts.GetGlyphRangesChineseFull());

            var range = builder.BuildRangesToArray();

            NotoSan24
                = DalamudApi.Interface.UiBuilder.FontAtlas
                            .NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontFromFile(fontPath,
                                                                              new ()
                                                                              {
                                                                                  SizePx      = 24,
                                                                                  GlyphRanges = range,
                                                                              })));

            NotoSan18
                = DalamudApi.Interface.UiBuilder.FontAtlas
                            .NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontFromFile(fontPath,
                                                                              new ()
                                                                              {
                                                                                  SizePx      = 18,
                                                                                  GlyphRanges = range,
                                                                              })));
        }
    }

    private void SetupHuntData()
    {
        var bNpcNames = DalamudApi.DataManager.GetExcelSheet<BNpcName>()
                        ?? throw new InvalidOperationException("Failed to get BNpcName sheet");

        for (uint i = 2953; i < 2970; i++)
        {
            var item = new HuntData
            {
                Expansion = GameExpansion.ARealmReborn,
                LocalizedName = bNpcNames.GetRow(i)!
                                         .Singular.ExtractText(),
                Id = i,
            };

            _huntData.Add(item);
        }

        for (uint i = 4374; i < 4381; i++)
        {
            if (i == 4379)
            {
                continue;
            }

            var item = new HuntData
            {
                Expansion = GameExpansion.Heavensward,
                LocalizedName = bNpcNames.GetRow(i)!
                                         .Singular.ExtractText(),
                Id = i,
            };

            _huntData.Add(item);
        }

        for (uint i = 5984; i < 5990; i++)
        {
            var item = new HuntData
            {
                Expansion = GameExpansion.Stormblood,
                LocalizedName = bNpcNames.GetRow(i)!
                                         .Singular.ExtractText(),
                Id = i,
            };

            _huntData.Add(item);
        }

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Shadowbringers,
            LocalizedName = bNpcNames.GetRow(8653)!
                                     .Singular.ExtractText(),
            Id = 8653,
        }); // 阿格拉俄珀

        for (uint i = 8890; i < 8915; i += 5)
        {
            var item = new HuntData
            {
                Expansion = GameExpansion.Shadowbringers,
                LocalizedName = bNpcNames.GetRow(i)!
                                         .Singular.ExtractText(),
                Id = i,
            };

            _huntData.Add(item);
        }

        for (uint i = 10617; i < 10623; i++)
        {
            var item = new HuntData
            {
                Expansion = GameExpansion.Endwalker,
                LocalizedName = bNpcNames.GetRow(i)!
                                         .Singular.ExtractText(),
                Id = i,
            };

            _huntData.Add(item);
        }

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Dawntrail,
            Id        = 13156,
            LocalizedName = bNpcNames.GetRow(13156)!
                                     .Singular.ExtractText(),
        });

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Dawntrail,
            Id        = 13437,
            LocalizedName = bNpcNames.GetRow(13437)!
                                     .Singular.ExtractText(),
        });

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Dawntrail,
            Id        = 12754,
            LocalizedName = bNpcNames.GetRow(12754)!
                                     .Singular.ExtractText(),
        });

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Dawntrail,
            Id        = 13399,
            LocalizedName = bNpcNames.GetRow(13399)!
                                     .Singular.ExtractText(),
        });

        _huntData.Add(new ()
        {
            Expansion = GameExpansion.Dawntrail,
            Id        = 13444,
            LocalizedName = bNpcNames.GetRow(13444)!
                                     .Singular.ExtractText(),
        });

        var region = DalamudApi.ClientState.ClientLanguage switch
        {
            ClientLanguage.Japanese => "jp",
            ClientLanguage.English  => "en",
            ClientLanguage.German   => "de",
            ClientLanguage.French   => "fr",
            (ClientLanguage) 4      => "cn",
            _                       => throw new ArgumentOutOfRangeException(),
        };

        var content = Encoding.UTF8.GetString(Resource1.hunt);

        var json = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(content)
                   ?? throw new InvalidOperationException("Failed to decode hunt.json");

        foreach (var (key, value) in json)
        {
            if (!value.TryGetValue(region, out var name))
            {
                continue;
            }

            foreach (var huntData in _huntData.Where(huntData => huntData.LocalizedName == name))
            {
                huntData.KeyName = key;

                break;
            }
        }
    }

    private string GetTerritoryMapPath(uint id)
    {
        var territoryType = _territoryTypeSheet.GetRow(id);

        if (territoryType.Map.ValueNullable is not { } map)
        {
            return string.Empty;
        }

        var mapKey = map.Id.ExtractText();
        var rawKey = mapKey.Replace("/", "");

        return $"ui/map/{mapKey}/{rawKey}_m.tex";
    }

    private float GetTerritorySizeFactor(uint id)
    {
        var territoryType = _territoryTypeSheet.GetRow(id);

        return territoryType.Map.ValueNullable is not { } map ? 1 : map.SizeFactor;
    }

    private uint GetTerritoryMapRowId(uint id)
    {
        var territoryType = _territoryTypeSheet.GetRow(id);

        return territoryType.Map.ValueNullable is not { } map ? 0 : map.RowId;
    }
}
