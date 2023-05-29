using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;

namespace RankSSpawnHelper.Managers;

internal class Data
{
    public MapTexture MapTexture;

    public Monster Monster;
    public Player Player;
        
    public Data()
    {
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
}