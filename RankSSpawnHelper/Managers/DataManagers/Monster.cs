using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Managers.DataManagers
{
    internal enum FetchStatus
    {
        None,
        Fetching,
        Error,
        Success
    }

    internal class Monster
    {
        private const string Url = "https://tracker-api.beartoolkit.com/";
        private readonly HttpClient _httpClient;
        private readonly List<SRankMonster> _sRankMonsters = new();
        private string _errorMessage = string.Empty;
        private FetchStatus _fetchStatus = FetchStatus.None;
        private HuntStatus _lastHuntStatus;

        public Monster()
        {
            _httpClient         = new HttpClient();
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

                         var region = DalamudApi.ClientState.ClientLanguage switch
                                      {
                                          ClientLanguage.Japanese          => "jp",
                                          ClientLanguage.English           => "en",
                                          ClientLanguage.German            => "de",
                                          ClientLanguage.French            => "fr",
                                          ClientLanguage.ChineseSimplified => "cn",
                                          _                                => throw new ArgumentOutOfRangeException()
                                      };

                         try
                         {
                             var result  = _httpClient.GetAsync("https://tracker.beartoolkit.com/resources/hunt.json");
                             var content = await result.Result.Content.ReadAsStringAsync();
                             var json    = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(content);

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
                             PluginLog.Error(e, e.Message);
                         }
                     });
        }

        public List<string> GetMonstersByExpansion(GameExpansion expansion)
        {
            return _sRankMonsters.Where(i => expansion == i.expansion).Select(i => i.localizedName).ToList();
        }

        public string GetMonsterNameById(uint id)
        {
            return _sRankMonsters.Where(i => i.id == id).Select(i => i.localizedName).First();
        }

        public string GetErrorMessage()
        {
            return _errorMessage;
        }

        public FetchStatus GetFetchStatus()
        {
            return _fetchStatus;
        }

        public HuntStatus GetHuntStatus()
        {
            return _lastHuntStatus;
        }

        public void FetchData(string server, string monsterName, int instance)
        {
            if (_fetchStatus == FetchStatus.Fetching)
                return;

            _fetchStatus  = FetchStatus.Fetching;
            _errorMessage = "";

            Task.Run(async () =>
                     {
                         var body = new Dictionary<string, string>
                                    {
                                        { "HuntName", _sRankMonsters.Find(i => i.localizedName == monsterName).keyName + (instance == 0 ? string.Empty : $" {instance}") },
                                        { "WorldName", server }
                                    };

                         var response = await _httpClient.PostAsync(Url + "public/huntStatus", new FormUrlEncodedContent(body));

                         if (response.StatusCode != HttpStatusCode.OK)
                         {
                             _errorMessage = "HttpStatusCode: " + response.StatusCode;
                             _fetchStatus  = FetchStatus.Error;
                             return;
                         }

                         try
                         {
                             var content = await response.Content.ReadAsStringAsync();
                             _lastHuntStatus = JsonConvert.DeserializeObject<HuntStatus>(content);
                             if (_lastHuntStatus != null)
                             {
                                 _lastHuntStatus.localizedName =  monsterName;
                                 _lastHuntStatus.expectMaxTime /= 1000;
                                 _lastHuntStatus.expectMinTime /= 1000;
                                 _lastHuntStatus.instance      =  instance;
                             }

                             _fetchStatus = FetchStatus.Success;
                         }
                         catch (Exception e)
                         {
                             PluginLog.Error(e.Message);
                             _errorMessage = "An error occurred when fetching hunt status.";
                             _fetchStatus  = FetchStatus.Error;
                         }
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

            var response = await _httpClient.PostAsync(Url + "public/huntStatus", new FormUrlEncodedContent(body));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            try
            {
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
                PluginLog.Error(e.Message);

                _fetchStatus = FetchStatus.None;
                return null;
            }
        }
    }
}