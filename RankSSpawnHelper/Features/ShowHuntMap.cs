using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Features;

internal class ShowHuntMap : IDisposable
{
    private readonly Dictionary<uint, uint> _monsterTerritory = new()
    {
        { 956, 10617 }, // 布弗鲁
        { 815, 8900 },  // 多智兽
        { 958, 10619 }, // 阿姆斯特朗
        { 816, 8653 },  // 阿格拉俄珀
        { 960, 10622 }, // 狭缝
        { 614, 5985 },  // 伽马
        { 397, 4374 },  // 凯撒贝希摩斯
        { 622, 5986 },  // 兀鲁忽乃朝鲁
        { 139, 2966 },  // 南迪
        { 180, 2967 }   // 牛头黑神
    };

    private readonly Dictionary<string, List<SpawnPoints>> _spawnPoints = new();

    private readonly ExcelSheet<TerritoryType> _territoryType;
    private          bool                      _shouldRequest = true;

    public ShowHuntMap()
    {
        _territoryType = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();

        DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;

        Task.Run(
                 async () =>
                 {
                     await Task.Delay(1000);
                     foreach (var (k, _) in _monsterTerritory)
                     {
                         var territory = _territoryType.GetRow(k);
                         var mapRow    = territory.Map;
                         if (mapRow == null)
                             continue;

                         PluginLog.Debug($"Adding territory {k} | mapId: {territory.Map.Row}");

                         if (mapRow.Value == null)
                         {
                             PluginLog.Debug("mapRow.Value null");
                             continue;
                         }

                         Plugin.Managers.Data.MapTexture.AddMapTexture(k, mapRow.Value);
                     }
                 });
    }

    public void Dispose()
    {
        DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
    }

    public bool CanShowHuntMapWithMonsterName(string name)
    {
        var id = Plugin.Managers.Data.SRank.GetSRankIdByName(name);
        return id != 0 && _monsterTerritory.ContainsValue(id);
    }

    public MapTextureInfo? GeTexture(uint territory)
    {
        var map = _territoryType.GetRow(territory).Map;
        return Plugin.Managers.Data.MapTexture.GetTexture(map.Row);
    }

    public MapTextureInfo? GeTextureWithMonsterName(string name)
    {
        var id        = Plugin.Managers.Data.SRank.GetSRankIdByName(name);
        var territory = _monsterTerritory.Where(i => i.Value == id).Select(i => i.Key).First();
        var map       = _territoryType.GetRow(territory).Map;
        return Plugin.Managers.Data.MapTexture.GetTexture(map.Row);
    }

    public void AddSpawnPoints(string worldName, string huntName, List<SpawnPoints> spawnPoints)
    {
        if (!_spawnPoints.TryAdd($"{worldName}@{huntName}", spawnPoints))
            return;

        // check if we can print / show
        var currentTerritory = DalamudApi.ClientState.TerritoryType;
        if (!_monsterTerritory.TryGetValue(currentTerritory, out var monsterId))
            return;

        var currentWorldName = Plugin.Managers.Data.Player.GetCurrentWorldName();
        if (currentWorldName != worldName)
            return;

        var currentHuntName = Plugin.Managers.Data.SRank.GetSRankKeyNameById(monsterId);
        if (currentHuntName != huntName)
            return;

        PrintOrShowSpawnPoints(spawnPoints);
    }

    public void RemoveSpawnPoint(string worldName, string huntName, string spawnPointKey)
    {
        if (!_spawnPoints.TryGetValue($"{worldName}@{huntName}", out var points))
            return;

        var point = points.Find(i => i.key == spawnPointKey);
        if (point == null)
            return;

        points.Remove(point);
        // check if we can print / show
        var currentTerritory = DalamudApi.ClientState.TerritoryType;
        if (!_monsterTerritory.TryGetValue(currentTerritory, out var monsterId))
            return;

        var currentWorldName = Plugin.Managers.Data.Player.GetCurrentWorldName();
        if (currentWorldName != worldName)
            return;

        var currentHuntName = Plugin.Managers.Data.SRank.GetSRankKeyNameById(monsterId);
        if (currentHuntName != huntName)
            return;

        PrintOrShowSpawnPoints(points);
        Plugin.Windows.HuntMapWindow.UpdateSpawnPoints(points);
    }

    public void DontRequest() => _shouldRequest = false;
    
    public void FetchAndPrint()
    {
        var currentTerritory = DalamudApi.ClientState.TerritoryType;
        if (!_monsterTerritory.TryGetValue(currentTerritory, out var monsterId))
            return;

        var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
        var split           = currentInstance.Split('@');
        var instance        = 0;
        if (split.Length == 3)
            _ = int.TryParse(split[2], out instance);
        
        var huntName = Plugin.Managers.Data.SRank.GetSRankKeyNameById(monsterId);
        if (instance != 0)
            huntName += $" {instance}";

        // send if we dont have it
        if (!_spawnPoints.TryGetValue($"{split[0]}@{huntName}", out var points))
        {
            Plugin.Managers.Socket.TrackerApi.SendHuntmapRequest(split[0], huntName);
            return;
        }

        PrintOrShowSpawnPoints(points);
    }

    private void Condition_OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value)
            return;

        if (!Plugin.Configuration.AutoShowHuntMap ||
            (Plugin.Configuration.AutoShowHuntMap && Plugin.Configuration.OnlyFetchInDuration))
            return;

        if (!_shouldRequest)
        {
            _shouldRequest = true;
            return;
        }
        
        Task.Run(FetchAndPrint);
    }

    private void PrintOrShowSpawnPoints(List<SpawnPoints> points)
    {
        var currentTerritory = DalamudApi.ClientState.TerritoryType;
        var currentInstance  = Plugin.Managers.Data.Player.GetCurrentTerritory();
       
        if (points == null || points.Count == 0)
            return;
        
        Plugin.Windows.HuntMapWindow.SetCurrentMap(GeTexture(currentTerritory), points, currentInstance);

        if (points.Count > 5)
        {
            Plugin.Windows.HuntMapWindow.IsOpen = true;
            return;
        }

        var payloads = new List<Payload>
        {
            new TextPayload($"{currentInstance} 的当前可触发点位:")
        };

        var mapId = _territoryType.GetRow(currentTerritory)!.Map.Row;

        foreach (var spawnPoint in points)
        {
            payloads.Add(new TextPayload("\n"));
            payloads.Add(new MapLinkPayload(currentTerritory, mapId, spawnPoint.x, spawnPoint.y));
            payloads.Add(new TextPayload($"{(char)SeIconChar.LinkMarker}"));
            payloads.Add(
                         new TextPayload(
                                         $"{spawnPoint.key.Replace("SpawnPoint", "")} ({spawnPoint.x:0.00}, {spawnPoint.y:0.00})"));
            payloads.Add(RawPayload.LinkTerminator);
        }

        Plugin.Print(payloads);
    }
}