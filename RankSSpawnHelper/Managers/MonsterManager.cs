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

namespace RankSSpawnHelper.Managers;

public class MonsterManager
{
    private const string Url = "https://tracker.ff14hunttool.com/";
    private readonly HttpClient _httpClient;

    private readonly List<SRankMonster> _sRankMonsters = new();
    private HuntStatus _lastHuntStatus;
    public string ErrorMessage = string.Empty;
    public bool IsDataReady;

    public bool IsFetchingData;

    public MonsterManager()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        var bNpcNames = Service.DataManager.GetExcelSheet<BNpcName>();

        {
            for (uint i = 2953; i < 2970; i++)
            {
                var item = new SRankMonster
                {
                    expansion = GameExpansion.ARealmReborn,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
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
                    expansion = GameExpansion.Heavensward,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            for (uint i = 5984; i < 5990; i++)
            {
                var item = new SRankMonster
                {
                    expansion = GameExpansion.Stormblood,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            var aglaope = new SRankMonster
            {
                expansion = GameExpansion.Shadowbringers,
                localizedName = bNpcNames.GetRow(8653).Singular.RawString,
            };
            _sRankMonsters.Add(aglaope); // 阿格拉俄珀
            for (uint i = 8890; i < 8915; i += 5)
            {
                var item = new SRankMonster
                {
                    expansion = GameExpansion.Shadowbringers,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                };
                _sRankMonsters.Add(item);
            }
        }

        {
            for (uint i = 10617; i < 10623; i++)
            {
                var item = new SRankMonster
                {
                    expansion = GameExpansion.Endwalker,
                    localizedName = bNpcNames.GetRow(i).Singular.RawString,
                };

                _sRankMonsters.Add(item);
            }
        }

        Task.Run(async () =>
        {
            while (Service.ClientState.LocalPlayer == null)
            {
                await Task.Delay(500);
            }

            var region = Service.ClientState.ClientLanguage switch
            {
                ClientLanguage.Japanese => "jp",
                ClientLanguage.English => "en",
                ClientLanguage.German => "de",
                ClientLanguage.French => "fr",
                ClientLanguage.ChineseSimplified => "cn",
                _ => throw new ArgumentOutOfRangeException(),
            };

            try
            {
                var result = _httpClient.GetAsync(Url + "resources/hunt.json");
                var content = await result.Result.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(content);

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

    public List<string> GetMonstersNameByExpansion(GameExpansion expansion) => _sRankMonsters.Where(i => expansion == i.expansion).Select(i => i.localizedName).ToList();

    public void FetchData(string server, string monsterName, int instance)
    {
        if (IsFetchingData)
            return;

        IsFetchingData = true;
        IsDataReady = false;
        ErrorMessage = "";

        Task.Run(async () =>
        {
            var body = new Dictionary<string, string>
            {
                { "HuntName", _sRankMonsters.Find(i => i.localizedName == monsterName).keyName + (instance == 0 ? string.Empty : $" {instance}") },
                { "WorldName", server },
            };

            var response = await _httpClient.PostAsync(Url + "api/huntStatus", new FormUrlEncodedContent(body));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                ErrorMessage = "HttpStatusCode:" + response.StatusCode;
                IsFetchingData = false;
                return;
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync();
                _lastHuntStatus = JsonConvert.DeserializeObject<HuntStatus>(content);
                // PluginLog.Debug(content);
                if (_lastHuntStatus != null)
                {
                    _lastHuntStatus.localizedName = monsterName;
                    _lastHuntStatus.expectMaxTime /= 1000;
                    _lastHuntStatus.expectMinTime /= 1000;
                    _lastHuntStatus.instance = instance;
                }

                IsDataReady = true;
            }
            catch (Exception e)
            {
                PluginLog.Error(e.Message);
                ErrorMessage = "An error occurred when fetching hunt status.";
                IsDataReady = false;
            }

            IsFetchingData = false;
        });
    }

    public HuntStatus GetStatus() => _lastHuntStatus;
}