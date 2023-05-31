using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;

namespace RankSSpawnHelper.Managers;

internal class Data
{
    public MapTexture MapTexture;

    public Monster Monster;
    public Player Player;

    private readonly Dictionary<uint, string> _npcName;
    private readonly Dictionary<uint, string> _itemName;
    private readonly TextInfo _textInfo;

        
    public Data()
    {
        _npcName  = DalamudApi.DataManager.GetExcelSheet<BNpcName>().ToDictionary(i => i.RowId, i=> i.Singular.RawString);
        _itemName = DalamudApi.DataManager.GetExcelSheet<Item>().ToDictionary(i => i.RowId, i => i.Singular.RawString);
        _textInfo  = new CultureInfo("en-US", false).TextInfo;

        Monster    = new Monster();
        Player     = new Player();
        MapTexture = new MapTexture();
    }

    public static List<string> GetServers()
    {
        var dcRowId = DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.RowId;
        if (dcRowId == 0)
        {
            throw new IndexOutOfRangeException("aaaaaaaaaaaaaaaaaaaaaaa");
        }

        var worlds = DalamudApi.DataManager.GetExcelSheet<World>()?.Where(world => world.DataCenter.Value?.RowId == dcRowId).ToList();

        return worlds?.Select(world => world.Name).Select(dummy => dummy.RawString).ToList();
    }

    public string GetNpcName(uint id)
    {
        return _textInfo.ToTitleCase(_npcName[id]);
    }

    public string GetItemName(uint id)
    {
        return _textInfo.ToTitleCase(_itemName[id]);
    }
}