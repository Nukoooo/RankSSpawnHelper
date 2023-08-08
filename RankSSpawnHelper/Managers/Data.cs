using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Gui.PartyFinder.Types;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;
#if DEBUG || DEBUG_CN
using Dalamud.Logging;
#endif

namespace RankSSpawnHelper.Managers;

internal class Data : IDisposable
{
    private readonly Dictionary<uint, (string, ItemAction?)> _itemName;

    private readonly Dictionary<uint, string> _npcName;
    private readonly Dictionary<uint, string> _territoryName;
    private readonly TextInfo                 _textInfo;
    private readonly Dictionary<uint, string> _worldName;
    private readonly ExcelSheet<World>        _worldSheet;

    private long       _serverRestartTime;
    public  MapTexture MapTexture;
    public  Player     Player;

    public SRank SRank;

    public Data()
    {
        _worldSheet    = DalamudApi.DataManager.GetExcelSheet<World>();
        _npcName       = DalamudApi.DataManager.GetExcelSheet<BNpcName>()!.ToDictionary(i => i.RowId, i => i.Singular.RawString);
        _itemName      = DalamudApi.DataManager.GetExcelSheet<Item>()!.ToDictionary(i => i.RowId, i => (i.Singular.RawString, i.ItemAction.Value));
        _worldName     = _worldSheet.ToDictionary(i => i.RowId, i => i.Name.RawString);
        _territoryName = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()!.ToDictionary(i => i.RowId, i => i.PlaceName.Value.Name.RawString);


        _textInfo = new CultureInfo("en-US", false).TextInfo;

        SRank      = new SRank();
        Player     = new Player();
        MapTexture = new MapTexture();

        DalamudApi.PartyFinderGui.ReceiveListing += PartyFinderGui_ReceiveListing;
    }

    public void Dispose()
    {
        DalamudApi.PartyFinderGui.ReceiveListing -= PartyFinderGui_ReceiveListing;
    }

    private void PartyFinderGui_ReceiveListing(PartyFinderListing listing, PartyFinderListingEventArgs args)
    {
        _serverRestartTime = listing.LastPatchHotfixTimestamp;
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
#if DEBUG || DEBUG_CN
        PluginLog.Debug($"Local: {DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.Name}, {_worldSheet.GetRow(worldId).DataCenter.Value.Name}, IsFromOtherDC: {dcRowId != _worldSheet.GetRow(worldId).DataCenter.Value.RowId}");
#endif
        return dcRowId != _worldSheet.GetRow(worldId).DataCenter.Value.RowId;
    }

    public string GetNpcName(uint id)
    {
        return _npcName.TryGetValue(id, out var name) ? _textInfo.ToTitleCase(name) : "";
    }

    public string GetWorldName(uint id)
    {
        return _worldName.TryGetValue(id, out var name) ? _textInfo.ToTitleCase(name) : "";
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
        return _itemName.Where(key => string.Equals(key.Value.Item1, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public uint GetNpcIdByName(string name)
    {
        return _npcName.Where(key => string.Equals(key.Value, name, StringComparison.CurrentCultureIgnoreCase)).Select(key => key.Key).FirstOrDefault();
    }

    public string GetItemName(uint id)
    {
        return _textInfo.ToTitleCase(_itemName[id].Item1);
    }

    public ItemAction GetItemAction(uint id)
    {
        return _itemName[id].Item2;
    }

    public long GetServerRestartTimeRaw()
    {
        return _serverRestartTime;
    }
}