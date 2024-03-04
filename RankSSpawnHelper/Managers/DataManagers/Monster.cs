using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Managers.DataManagers;

internal enum FetchStatus
{
    None,
    Fetching,
    Error,
    Success
}

internal class SRank
{
    private const    string             Url             = "https://tracker-api.beartoolkit.com/";
    private readonly HttpClient         _httpClient     = new();
    private readonly List<HuntStatus>   _lastHuntStatus = new();
    private readonly List<SRankMonster> _sRankMonsters  = new();
    private          string             _errorMessage   = string.Empty;
    private          FetchStatus        _fetchStatus    = FetchStatus.None;

    public SRank()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        InitSRankMonsterData();
    }

    private void InitSRankMonsterData()
    {
        var bNpcNames = DalamudApi.DataManager.GetExcelSheet<BNpcName>();

        {
            for (uint i = 2953; i < 2970; i++)
            {
                var item = new SRankMonster
                {
                    expansion     = GameExpansion.ARealmReborn,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                    id            = i
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            for (uint i = 4374; i < 4381; i++)
            {
                if (i == 4379) continue;

                var item = new SRankMonster
                {
                    expansion     = GameExpansion.Heavensward,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                    id            = i
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            for (uint i = 5984; i < 5990; i++)
            {
                var item = new SRankMonster
                {
                    expansion     = GameExpansion.Stormblood,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                    id            = i
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            var aglaope = new SRankMonster
            {
                expansion     = GameExpansion.Shadowbringers,
                localizedName = bNpcNames.GetRow(8653).Singular.RawString,
                id            = 8653
            };
            _sRankMonsters.Add(aglaope); // 阿格拉俄珀
            for (uint i = 8890; i < 8915; i += 5)
            {
                var item = new SRankMonster
                {
                    expansion     = GameExpansion.Shadowbringers,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                    id            = i
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            for (uint i = 10617; i < 10623; i++)
            {
                var item = new SRankMonster
                {
                    expansion     = GameExpansion.Endwalker,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                    id            = i
                };

                _sRankMonsters.Add(item);
            }
        }

        Task.Run(async () =>
                 {
                     while (DalamudApi.ClientState.LocalPlayer == null)
                     {
                         await Task.Delay(500);
                     }

                     string region;

                     if (Plugin.IsChina())
                     {
                         region = "cn";
                     }
                     else
                     {
                         region = DalamudApi.ClientState.ClientLanguage switch
                                  {
                                      ClientLanguage.Japanese => "jp",
                                      ClientLanguage.English  => "en",
                                      ClientLanguage.German   => "de",
                                      ClientLanguage.French   => "fr",
                                      _                       => throw new ArgumentOutOfRangeException()
                                  };
                     }

                     try
                     {
                         var content = Encoding.UTF8.GetString(Resource.hunt);
                         var json    = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(content);

                         { }

                         foreach (var (key, value) in json)
                         {
                             if (!value.TryGetValue(region, out var name))
                                 continue;

                             foreach (var m in _sRankMonsters.Where(monster => monster.localizedName == name))
                             {
                                 m.keyName = key;
                             }
                         }
                     }
                     catch (Exception e)
                     {
                         DalamudApi.PluginLog.Error(e, e.Message);
                     }
                 });
    }

    public List<string> GetSRanksByExpansion(GameExpansion expansion)
    {
        return _sRankMonsters.Where(i => expansion == i.expansion).Select(i => i.localizedName).ToList();
    }

    public uint GetSRankIdByName(string name)
    {
        return _sRankMonsters.Where(i => i.localizedName == name).Select(i => i.id).FirstOrDefault();
    }

    public string GetSRankNameById(uint id)
    {
        return _sRankMonsters.Where(i => i.id == id).Select(i => i.localizedName).FirstOrDefault();
    }

    public string GetSRankKeyNameById(uint id)
    {
        return _sRankMonsters.Where(i => i.id == id).Select(i => i.keyName).FirstOrDefault();
    }

    public bool IsSRank(uint id)
    {
        return _sRankMonsters.Any(i => i.id == id);
    }

    public string GetErrorMessage()
    {
        return _errorMessage;
    }

    public FetchStatus GetFetchStatus()
    {
        return _fetchStatus;
    }

    public List<HuntStatus> GetHuntStatus()
    {
        return _lastHuntStatus;
    }

    public void FetchData(List<string> servers, string monsterName, int instance)
    {
        if (_fetchStatus == FetchStatus.Fetching)
            return;

        _fetchStatus  = FetchStatus.Fetching;
        _errorMessage = "";

        Task.Run(async () =>
                 {
                     if (servers.Count == 0)
                     {
                         _fetchStatus  = FetchStatus.Error;
                         _errorMessage = "服务器为空??";
                         return;
                     }

                     _lastHuntStatus.Clear();

                     foreach (var body in servers.Select(server => new Dictionary<string, string>
                              {
                                  {
                                      "HuntName",
                                      _sRankMonsters.Find(i => i.localizedName == monsterName).keyName +
                                      (instance == 0 ? string.Empty : $" {instance}")
                                  },
                                  { "WorldName", server }
                              }))
                     {
                         var response = await _httpClient.PostAsync(Url + "public/huntStatus", new FormUrlEncodedContent(body));

                         if (response.StatusCode != HttpStatusCode.OK)
                         {
                             _errorMessage = "HttpStatusCode: " + response.StatusCode;
                             _fetchStatus  = FetchStatus.Error;
                             return;
                         }

                         try
                         {
                             var content    = await response.Content.ReadAsStringAsync();
                             var huntStatus = JsonConvert.DeserializeObject<HuntStatus>(content);
                             if (huntStatus != null)
                             {
                                 huntStatus.localizedName =  monsterName;
                                 huntStatus.expectMaxTime /= 1000;
                                 huntStatus.expectMinTime /= 1000;
                                 huntStatus.instance      =  instance;

                                 _lastHuntStatus.Add(huntStatus);
                             }
                         }
                         catch (Exception e)
                         {
                             DalamudApi.PluginLog.Error(e.Message);
                             _errorMessage = "An error occurred when fetching hunt status.";
                             _fetchStatus  = FetchStatus.Error;
                             return;
                         }
                     }

                     _fetchStatus = FetchStatus.Success;
                 });
    }

    public async Task<HuntStatus> FetchHuntStatus(string server, string monsterName, int instance)
    {
        if (_fetchStatus == FetchStatus.Fetching)
            return null;

        var body = new Dictionary<string, string>
        {
            { "HuntName", _sRankMonsters.Find(i => i.localizedName == monsterName).keyName + (instance == 0 ? string.Empty : $" {instance}") },
            { "WorldName", server }
        };

        try
        {
            var response = await _httpClient.PostAsync(Url + "public/huntStatus", new FormUrlEncodedContent(body));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                DalamudApi.PluginLog.Error($"获取S怪状态失败. StatusCode: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var status  = JsonConvert.DeserializeObject<HuntStatus>(content);
            if (status != null)
            {
                status.localizedName =  monsterName;
                status.expectMaxTime /= 1000;
                status.expectMinTime /= 1000;
                status.instance      =  instance;
            }

            _fetchStatus = FetchStatus.Success;
            return status;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e.Message);

            _fetchStatus = FetchStatus.None;
            return null;
        }
    }

    public async Task<HuntMap> FetchHuntMap(string server, string monster, int instance)
    {
        try
        {
            if (_fetchStatus == FetchStatus.Fetching)
                return null;

            var body = new Dictionary<string, string>
            {
                { "HuntName", _sRankMonsters.Find(i => i.localizedName == monster).keyName + (instance == 0 ? string.Empty : $" {instance}") },
                { "WorldName", server }
            };

            var response = await _httpClient.PostAsync(Url + "public/huntmap", new FormUrlEncodedContent(body));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                DalamudApi.PluginLog.Error($"获取狩猎地图失败. StatusCode: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var huntMap = JsonConvert.DeserializeObject<HuntMap>(content);

            _fetchStatus = FetchStatus.Success;
            return huntMap;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e.Message);

            _fetchStatus = FetchStatus.None;
            return null;
        }
    }
}