using System.Collections.Frozen;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Widgets;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Windows;

namespace RankSSpawnHelper.Modules;

internal class MobStatus : IUiModule
{
    private readonly TrackerApi    _trackerApi;
    private readonly IDataManager  _dataManager;
    private readonly Configuration _configuration;

    private readonly WindowSystem  _windowSystem;

    private HuntMapWindow _huntMapWindow = null!;

    // UI相关
    private readonly string[] _expansions;
    private          int      _selectedExpansion;

    private          int      _selectedMonster;
    private          string[] _monsterNames = [];

    private int _selectedInstance;

    private readonly string[] _spawnNotificationType = ["关闭", "只在可触发时", "一直"];

    private readonly HashSet<uint> _shouldShowHuntMapTerritories =
    [
        1192, // 天气预报机器人
        1188, // 天气预报机器人
        956,  // 布弗鲁
        815,  // 多智兽
        958,  // 阿姆斯特朗
        816,  // 阿格拉俄珀
        960,  // 狭缝
        614,  // 伽马
        397,  // 凯撒贝希摩斯
        622,  // 兀鲁忽乃朝鲁
        139,  // 南迪
    ];

    private readonly FrozenDictionary<ushort, uint> _mobIdMap;
    private readonly FrozenDictionary<uint, ushort> _mobMapId;

    public MobStatus(TrackerApi    trackerApi,
                     IDataManager  dataManager,
                     Configuration configuration,
                     WindowSystem  windowSystem)
    {
        _trackerApi    = trackerApi;
        _dataManager   = dataManager;
        _configuration = configuration;
        _windowSystem  = windowSystem;

        _expansions = Enum.GetNames<GameExpansion>();

        Dictionary<ushort, uint> mobIdMap = new ()
        {
            { 1192, 13437 }, // 天气预报机器人
            { 1188, 13444 }, // 伊努索奇
            { 960, 10622 },  // 狭缝
            { 959, 10620 },  // 沉思之物
            { 958, 10619 },  // 阿姆斯特朗
            { 957, 10618 },  // 颇胝迦
            { 956, 10617 },  // 布弗鲁
            { 814, 8910 },   // 得到宽恕的炫学
            { 815, 8900 },   // 多智兽
            { 816, 8653 },   // 阿格拉俄珀
            { 817, 8890 },   // 伊休妲
            { 621, 5989 },   // 盐和光
            { 614, 5985 },   // 伽马
            { 613, 5984 },   // 巨大鳐
            { 612, 5987 },   // 优昙婆罗花
            { 402, 4380 },   // 卢克洛塔
            { 400, 4377 },   // 刚德瑞瓦
            { 397, 4374 },   // 凯撒贝希摩斯
            { 147, 2961 },   // 蚓螈巨虫
        };

        _mobIdMap = mobIdMap.ToFrozenDictionary();
        _mobMapId = mobIdMap.ToFrozenDictionary(x => x.Value, x => x.Key);

        _monsterNames = _dataManager.GetSRanksByExpansion(GameExpansion.ARealmReborn)
                                    .Select(i => i.LocalizedName)
                                    .ToArray();
    }

    public bool Init()
    {
        DalamudApi.Condition.ConditionChange += Condition_ConditionChange;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.Condition.ConditionChange -= Condition_ConditionChange;
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _huntMapWindow = _windowSystem.Windows.FirstOrDefault(i => i.WindowName == HuntMapWindow.Name) as HuntMapWindow
                         ?? throw new InvalidOperationException("Failed to get HuntMapWindow");
    }

    public string UiName => "S怪相关";

    public void OnDrawUi()
    {
        Widget.BeginFramedGroup("消息提示");

        {
            ImGui.Text("触发概率提醒");
            var spawnNotificationType = (int) _configuration.SpawnNotificationType;

            for (var i = 0; i < _spawnNotificationType.Length; i++)
            {
                if (ImGui.RadioButton(_spawnNotificationType[i] + "##_spawnNotificationType", ref spawnNotificationType, i))
                {
                    _configuration.SpawnNotificationType = (SpawnNotificationType) spawnNotificationType;
                    _configuration.Save();
                }

                if (i == _spawnNotificationType.Length - 1)
                {
                    break;
                }

                ImGui.SameLine();
            }

            if (_configuration.SpawnNotificationType == SpawnNotificationType.Full)
            {
                ImGui.SameLine();
                var coolDownNotificationSound = _configuration.CoolDownNotificationSound;

                if (ImGui.Checkbox("不在触发期时播放提示音", ref coolDownNotificationSound))
                {
                    _configuration.CoolDownNotificationSound = coolDownNotificationSound;
                    _configuration.Save();
                }
            }

            var autoShowHuntMap = _configuration.AutoShowHuntMap;

            if (ImGui.Checkbox("自动获取点位列表", ref autoShowHuntMap))
            {
                _configuration.AutoShowHuntMap = autoShowHuntMap;
                _configuration.Save();
            }

            if (_configuration is { AutoShowHuntMap: true, SpawnNotificationType: > SpawnNotificationType.Off })
            {
                ImGui.SameLine();
                var onlyFetchInDuration = _configuration.OnlyFetchInDuration;

                if (ImGui.Checkbox("只在触发期内获取", ref onlyFetchInDuration))
                {
                    _configuration.OnlyFetchInDuration = onlyFetchInDuration;
                    _configuration.Save();
                }
            }
        }

        Widget.EndFramedGroup();

        ImGui.NewLine();

        Widget.BeginFramedGroup("查询S怪状态");
        DrawQueryHuntStatus();
        Widget.EndFramedGroup();
    }

    private void DrawQueryHuntStatus()
    {
        var serverList = _dataManager.GetServerList();

        if (serverList.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "服务器列表为空");

            return;
        }

        if (ImGui.Combo("版本", ref _selectedExpansion, _expansions, _expansions.Length))
        {
            _monsterNames = _dataManager.GetSRanksByExpansion((GameExpansion) _selectedExpansion)
                                        .Select(i => i.LocalizedName)
                                        .ToArray();

            _selectedMonster = 0;
        }

        ImGui.Combo("S怪", ref _selectedMonster, _monsterNames, _monsterNames.Length);

        if (ImGui.InputInt("几线", ref _selectedInstance, 1))
        {
            _selectedInstance = Math.Clamp(_selectedInstance, 0, 3);
        }

        ImGui.SameLine();

        using (var unused = ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(FontAwesomeIcon.Search.ToIconString()))
            {
                _trackerApi.FetchHuntStatuses(serverList,
                                              _dataManager.GetHuntDataByLocalizedName(_monsterNames[_selectedMonster]),
                                              (uint) _selectedInstance);

                return;
            }
        }

        if (_trackerApi.IsFetchingHuntStatus())
        {
            ImGui.TextUnformatted("正在获取中");

            return;
        }

        var (huntData, statusList) = _trackerApi.GetHuntStatus();

        if (statusList.Count == 0)
        {
            return;
        }

        var canShowHuntMap = _mobMapId.TryGetValue((ushort) _dataManager.GetSRankIdByLocalizedName(huntData.LocalizedName),
                                                   out var territoryId);

        foreach (var status in statusList)
        {
            ImGui.TextUnformatted($"{status.WorldName}:");

            ImGui.TextUnformatted("\t丢失:");
            ImGui.SameLine();
            ImGui.TextColored(status.Missing ? ImGuiColors.DPSRed : ImGuiColors.ParsedGreen, status.Missing ? "是" : "否");

            var now = DateTimeOffset.Now;

            var minTime = DateTimeOffset.FromUnixTimeMilliseconds((long) status.ExpectMinTime);
            var maxTime = DateTimeOffset.FromUnixTimeMilliseconds((long) status.ExpectMaxTime);

            if (minTime > now)
            {
                var delta = minTime - now;
                ImGui.TextUnformatted($"\t距离进入可触发时间还有: {delta.Hours + (delta.Days * 24):D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");

                continue;
            }

            var percentage = 100
                             * ((now       - minTime)
                                / (maxTime - minTime));

            ImGui.TextUnformatted("\t当前可触发的概率为:");
            ImGui.SameLine();

            ImGui.TextColored(percentage > 100.0 ? ImGuiColors.ParsedBlue : ImGuiColors.ParsedGreen,
                              $"{percentage:F2}%%");

            // ReSharper disable once InvertIf
            if (now < maxTime)
            {
                var delta = maxTime - now;
                ImGui.TextUnformatted($"\t距离进入强制期还有: {delta.Hours + (delta.Days * 24):D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");
            }

            if (!canShowHuntMap)
            {
                continue;
            }

            ImGui.SameLine();

            if (ImGui.Button($"查看触发点位##{status.WorldName}"))
            {
                Task.Run(async () =>
                {
                    if (await _trackerApi.FetchSpawnPoints(status.WorldName,
                                                           huntData.KeyName,
                                                           _selectedInstance) is not { } huntMap)
                    {
                        return;
                    }

                    await DalamudApi.Framework.RunOnFrameworkThread(() =>
                    {
                        _huntMapWindow.SetCurrentMap(huntMap.SpawnPoints,
                                                     territoryId);
                    });
                });
            }
        }
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value)
        {
            return;
        }

        Task.Run(FetchHuntStatus);
    }

    private async Task FetchHuntStatus()
    {
        if (_configuration.SpawnNotificationType == SpawnNotificationType.Off)
        {
            return;
        }

        var territory = DalamudApi.ClientState.TerritoryType;

        if (!_mobIdMap.TryGetValue(territory, out var id))
        {
            return;
        }

        if (_dataManager.GetHuntData(id) is not { } huntData)
        {
            return;
        }

        var worldName = _dataManager.GetWorldName(_dataManager.GetCurrentWorldId());

        var huntStatus = await _trackerApi.FetchHuntStatus(huntData.KeyName, worldName, _dataManager.GetCurrentInstance());

        if (huntStatus is null)
        {
            return;
        }

        var time = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var payloads = new List<Payload>
        {
            new UIForegroundPayload(1),
            new TextPayload($"{_dataManager.FormatCurrentTerritory()} - {huntData.LocalizedName}:"),
        };

        var minTime   = huntStatus.Value.ExpectMinTime;
        var maxTIme   = huntStatus.Value.ExpectMaxTime;
        var spawnable = time > minTime;

        if (spawnable)
        {
            payloads.Add(new TextPayload("\n当前可触发概率: "));
            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));
            payloads.Add(new TextPayload($"{100 * ((time - minTime) / (maxTIme - minTime)):F1}%"));
            payloads.Add(new UIForegroundPayload(0));
        }
        else
        {
            if (_configuration.SpawnNotificationType == SpawnNotificationType.SpawnableOnly)
            {
                if (!_configuration.AutoShowHuntMap
                    || !_shouldShowHuntMapTerritories.Contains(territory)
                    || _configuration.OnlyFetchInDuration)
                {
                    return;
                }

                await FetchHuntSpawnPoints(huntData, territory);

                return;
            }

            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));
            var delta = (DateTimeOffset.FromUnixTimeMilliseconds((long) minTime) - DateTimeOffset.Now).TotalMinutes;
            payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
            payloads.Add(new UIForegroundPayload(0));

            if (_configuration.CoolDownNotificationSound)
            {
                UIGlobals.PlayChatSoundEffect(6);
                UIGlobals.PlayChatSoundEffect(6);
                UIGlobals.PlayChatSoundEffect(6);
            }
        }

        payloads.Add(new UIForegroundPayload(0));
        Utils.Print(payloads);

        if (!_configuration.AutoShowHuntMap || !_shouldShowHuntMapTerritories.Contains(territory))
        {
            return;
        }

        DalamudApi.PluginLog.Info("Fetching spawn points");
        await FetchHuntSpawnPoints(huntData, territory);
    }

    private async Task FetchHuntSpawnPoints(HuntData status, ushort territory)
    {
        var currentWorld = _dataManager.GetCurrentWorldId();
        var worldName    = _dataManager.GetWorldName(currentWorld);

        if (await _trackerApi.FetchSpawnPoints(worldName,
                                               status.KeyName,
                                               _selectedInstance) is not { } huntSpawnPoints)
        {
            return;
        }

        var spawnPoints = huntSpawnPoints.SpawnPoints;

        if (spawnPoints.Count > 5)
        {
            await DalamudApi.Framework.RunOnFrameworkThread(() =>
            {
                _huntMapWindow.SetCurrentMap(spawnPoints,
                                             territory);
            });

            return;
        }

        var info = _dataManager.GetTerritoryInfo(territory);

        var payloads = new List<Payload>
        {
            new
                TextPayload($"{_dataManager.FormatInstance(currentWorld, territory, _dataManager.GetCurrentInstance())} 的当前可触发点位:"),
        };

        foreach (var spawnPoint in spawnPoints)
        {
            payloads.Add(new TextPayload("\n"));
            payloads.Add(new MapLinkPayload(territory, info.MapId, spawnPoint.X, spawnPoint.Y));
            payloads.Add(new TextPayload($"{(char) SeIconChar.LinkMarker}"));

            payloads.Add(new
                             TextPayload($"{spawnPoint.Key.Replace("SpawnPoint", "")} ({spawnPoint.X:0.00}, {spawnPoint.Y:0.00})"));

            payloads.Add(RawPayload.LinkTerminator);
        }

        Utils.Print(payloads);
    }
}
