﻿using System.Collections.Frozen;
using System.Numerics;
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
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Widgets;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Windows;

namespace RankSSpawnHelper.Modules;

internal class Misc : IUiModule
{
    private readonly TrackerApi    _trackerApi;
    private readonly IDataManager  _dataManager;
    private readonly Configuration _configuration;

    private readonly WindowSystem  _windowSystem;

    private HuntMapWindow _huntMapWindow = null!;

    // UI相关
    private readonly string[] _expansions = ["2.0", "3.0", "4.0", "5.0", "6.0", "7.0"];
    private          int      _selectedExpansion;

    private int      _selectedMonster;
    private string[] _monsterNames;

    private int _selectedInstance;

    private readonly string[]        _spawnNotificationType        = ["关闭", "只在可触发时", "一直"];
    private readonly string[]        _attemptMessageFromServerType = ["关闭", "本大区", "本大区+其他大区"];

    private readonly List<ColorInfo> _colorInfos      = [];
    private          ColorPickerType _colorPickerType = ColorPickerType.Failed;
    private          bool            _showColorPicker;

    private readonly HashSet<uint> _shouldShowHuntMapTerritories =
    [
        1192, // 天气预报机器人
        1188, // 湿地
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

    public Misc(TrackerApi    trackerApi,
                IDataManager  dataManager,
                Configuration configuration,
                WindowSystem  windowSystem)
    {
        _trackerApi    = trackerApi;
        _dataManager   = dataManager;
        _configuration = configuration;
        _windowSystem  = windowSystem;

        /*_expansions = Enum.GetNames<GameExpansion>();*/

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

        foreach (var color in DalamudApi.DataManager.Excel.GetSheet<UIColor>())
        {
            var result = _colorInfos.FindIndex(info => info.Color == color.UIForeground);

            if (result == -1)
            {
                _colorInfos.Add(new ()
                {
                    RowId = color.RowId,
                    Color = color.UIForeground,
                });
            }
        }
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

    public string UiName => "其他";

    public void OnDrawUi()
    {
        Widget.BeginFramedGroup("触发概率提醒");

        {
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

        Widget.BeginFramedGroup("触发消息相关");

        {
            ImGui.Text("接收来自服务器的触发消息");
            var attemptMessageFromServer = (int) _configuration.AttemptMessageFromServer;

            for (var i = 0; i < _attemptMessageFromServerType.Length; i++)
            {
                if (ImGui.RadioButton(_attemptMessageFromServerType[i] + "##_attemptMessageFromServerType",
                                      ref attemptMessageFromServer,
                                      i))
                {
                    _configuration.AttemptMessageFromServer = (AttemptMessageFromServerType) attemptMessageFromServer;
                    _configuration.Save();
                }

                if (i == _attemptMessageFromServerType.Length - 1)
                {
                    break;
                }

                ImGui.SameLine();
            }

            if (_configuration.AttemptMessageFromServer != AttemptMessageFromServerType.Off)
            {
                ImGui.SameLine();
                var showMessageInDungenos = _configuration.ShowAttemptMessageInDungeons;

                if (ImGui.Checkbox("在副本里显示消息", ref showMessageInDungenos))
                {
                    _configuration.ShowAttemptMessageInDungeons = showMessageInDungenos;
                    _configuration.Save();
                }
            }

            ImGui.BeginGroup();

            {
                ImGui.Text("失败消息颜色");
                ImGui.SameLine();

                if (ImGui.ColorButton("##触发失败", GetColor(_configuration.FailedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Failed;
                }

                ImGui.SameLine();
                ImGui.TextColored(GetColor(_configuration.FailedMessageColor), "寄啦，救命啊");

                ImGui.Text("成功消息颜色");
                ImGui.SameLine();

                if (ImGui.ColorButton("##触发成功", GetColor(_configuration.SpawnedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Spawned;
                }

                ImGui.SameLine();
                ImGui.TextColored(GetColor(_configuration.SpawnedMessageColor), "出货啦");

                ImGui.Text("    关键词颜色");
                ImGui.SameLine();

                if (ImGui.ColorButton("##高亮", GetColor(_configuration.HighlightColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Highlighted;
                }

                ImGui.SameLine();
                ImGui.TextColored(GetColor(_configuration.HighlightColor), "你为什么偷着乐");

                ImGui.EndGroup();
            }
        }

        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("查询S怪状态");
        DrawQueryHuntStatus();
        Widget.EndFramedGroup();

        DrawColorPicker();
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

        using (ImRaii.PushFont(UiBuilder.IconFont))
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
        if (flag == ConditionFlag.BetweenAreas51 && !value)
        {
            Task.Run(FetchHuntStatus);
        }
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

        var minTime   = huntStatus.ExpectMinTime;
        var maxTIme   = huntStatus.ExpectMaxTime;
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

        DalamudApi.PluginLog.Debug("Fetching spawn points");
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

    private enum ColorPickerType
    {
        Failed,
        Spawned,
        Highlighted,
    }

    private Vector4 GetColor(uint id)
    {
        try
        {
            var bytes = BitConverter.GetBytes(_colorInfos.Find(info => info.RowId == id)
                                                         .Color);

            return new (bytes[3] / 255f, bytes[2] / 255f, bytes[1] / 255f, bytes[0] / 255f);
        }
        catch (Exception)
        {
            return new ();
        }
    }

    private readonly record struct ColorInfo
    {
        internal uint RowId { get; init; }
        internal uint Color { get; init; }
    }

    private void DrawColorPicker()
    {
        if (!_showColorPicker)
        {
            return;
        }

        ImGui.SetNextWindowSize(new (320, 360));

        var type = _colorPickerType switch
        {
            ColorPickerType.Failed      => "给触发失败消息",
            ColorPickerType.Highlighted => "给关键词",
            ColorPickerType.Spawned     => "给触发成功消息",
            _                           => "",
        };

        if (!ImGui.Begin(type + "选个颜色呗",
                         ref _showColorPicker,
                         ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize))
        {
            return;
        }

        ImGui.Columns(10, "##colorcolumns", false);

        foreach (var info in _colorInfos)
        {
            if (ImGui.ColorButton("##" + info.RowId, GetColor(info.RowId)))
            {
                switch (_colorPickerType)
                {
                    case ColorPickerType.Failed:
                        _configuration.FailedMessageColor = info.RowId;

                        break;
                    case ColorPickerType.Highlighted:
                        _configuration.HighlightColor = info.RowId;

                        break;
                    case ColorPickerType.Spawned:
                        _configuration.SpawnedMessageColor = info.RowId;

                        break;
                }

                _configuration.Save();
                _showColorPicker = false;
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.End();
    }
}
