using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;

namespace RankSSpawnHelper.Managers;

internal class Data
{
    private readonly Dictionary<uint, string> _itemName;

    private readonly Dictionary<uint, string> _npcName;
    private readonly Dictionary<uint, string> _territoryName;
    private readonly TextInfo _textInfo;
    private readonly Dictionary<uint, string> _worldName;
    private readonly ExcelSheet<World> _worldSheet;
    public MapTexture MapTexture;

    public Monster Monster;
    public Player Player;

    public Data()
    {
        _worldSheet    = DalamudApi.DataManager.GetExcelSheet<World>();
        _npcName       = DalamudApi.DataManager.GetExcelSheet<BNpcName>()!.ToDictionary(i => i.RowId, i => i.Singular.RawString);
        _itemName      = DalamudApi.DataManager.GetExcelSheet<Item>()!.ToDictionary(i => i.RowId, i => i.Singular.RawString);
        _worldName     = _worldSheet.ToDictionary(i => i.RowId, i => i.Name.RawString);
        _territoryName = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()!.ToDictionary(i => i.RowId, i => i.PlaceName.Value.Name.RawString);

        _textInfo = new CultureInfo("en-US", false).TextInfo;

        Monster    = new Monster();
        Player     = new Player();
        MapTexture = new MapTexture();
    }

    public List<string> GetServers()
    {
        if (DalamudApi.ClientState.LocalPlayer == null)
            return new List<string>();
        var dcRowId = DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.RowId;
        if (dcRowId == 0)
        {
            throw new IndexOutOfRangeException("aaaaaaaaaaaaaaaaaaaaaaa");
        }

        var worlds = _worldSheet.Where(world => world.DataCenter.Value?.RowId == dcRowId).ToList();

        return worlds?.Select(world => world.Name).Select(dummy => dummy.RawString).ToList();
    }

    public bool IsFromOtherServer(uint worldId)
    {
        var dcRowId = DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.RowId;
        return dcRowId != _worldSheet.GetRow(worldId).DataCenter.Value.RowId;
    }

    public string GetNpcName(uint id)
    {
        return _textInfo.ToTitleCase(_npcName[id]);
    }

    public string GetWorldName(uint id)
    {
        return _textInfo.ToTitleCase(_worldName[id]);
    }

    public string FormatInstance(uint world, uint territory, uint instance)
    {
        return instance == 0 ? $"{GetWorldName(world)}@{GetTerritoryName(territory)}" : $"{GetWorldName(world)}@{GetTerritoryName(territory)}@{instance}";
    }

    public string GetTerritoryName(uint id)
    {
        return _textInfo.ToTitleCase(_territoryName[id]);
    }

    public uint GetTerritoryIdByName(string name)
    {
        return _territoryName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public uint GetWorldIdByName(string name)
    {
        return _worldName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public uint GetItemIdByName(string name)
    {
        return _itemName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public uint GetNpcIdByName(string name)
    {
        return _npcName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public string GetItemName(uint id)
    {
        return _textInfo.ToTitleCase(_itemName[id]);
    }
}