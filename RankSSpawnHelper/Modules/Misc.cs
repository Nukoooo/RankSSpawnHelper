using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Raii;
using OtterGui.Widgets;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Windows;
using IDataManager = RankSSpawnHelper.Managers.IDataManager;
using UIColor = Lumina.Excel.Sheets.UIColor;

namespace RankSSpawnHelper.Modules;

// TODO: 选项用模块,不要都放一起
internal class Misc : IUiModule
{
    private readonly TrackerApi      _trackerApi;
    private readonly IDataManager    _dataManager;
    private readonly Configuration   _configuration;
    private readonly ICommandHandler _commandHandler;
    private readonly WindowSystem    _windowSystem;

    private HuntMapWindow _huntMapWindow = null!;

    // UI相关
    private readonly string[] _expansions = ["2.0", "3.0", "4.0", "5.0", "6.0", "7.0"];
    private          int      _selectedExpansion;

    private int      _selectedInstance;
    private int      _selectedMonster;
    private string[] _monsterNames;

    private readonly string[] _attemptMessageFromServerType = ["关闭", "本大区", "本大区+其他大区"];

    private readonly List<ColorInfo> _colorInfos      = [];
    private          ColorPickerType _colorPickerType = ColorPickerType.Failed;
    private          bool            _showColorPicker;

    private readonly List<IUiModule>   _miscModules = [];
    private readonly SpawnNotification _spawnNotificationModule;

    private readonly HashSet<ushort> _shouldShowHuntMapTerritories =
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

    public Misc(TrackerApi        trackerApi,
                IDataManager      dataManager,
                Configuration     configuration,
                WindowSystem      windowSystem,
                ICommandHandler   commandHandler,
                ISigScannerModule sigScanner,
                ICounter          counter)
    {
        _trackerApi     = trackerApi;
        _dataManager    = dataManager;
        _configuration  = configuration;
        _windowSystem   = windowSystem;
        _commandHandler = commandHandler;

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
            { 139, 2966 },   // 南迪
        };

        _mobIdMap = mobIdMap.ToFrozenDictionary();
        _mobMapId = mobIdMap.ToFrozenDictionary(x => x.Value, x => x.Key);

        /*_expansions = Enum.GetNames<GameExpansion>();*/

        _monsterNames = _dataManager.GetSRanksByExpansion(GameExpansion.ARealmReborn)
                                    .Select(i => i.LocalizedName)
                                    .ToArray();

        _miscModules.Add(new WorldTravel(configuration, sigScanner));
        _miscModules.Add(new ShowInstance(configuration, dataManager));
        _miscModules.Add(new PlayerSearch(configuration, counter, sigScanner, dataManager));

        _spawnNotificationModule = new (configuration,
                                        dataManager,
                                        trackerApi,
                                        commandHandler,
                                        windowSystem,
                                        _mobIdMap,
                                        _mobMapId,
                                        _shouldShowHuntMapTerritories);

        foreach (var color in DalamudApi.DataManager.Excel.GetSheet<UIColor>()!)
        {
            var result = _colorInfos.FindIndex(info => info.Color == color.Dark);

            if (result == -1)
            {
                _colorInfos.Add(new ()
                {
                    RowId = color.RowId,
                    Color = color.Dark
                });
            }
        }
    }

    public bool Init()
    {
        //DalamudApi.Condition.ConditionChange += Condition_ConditionChange;

        foreach (var module in _miscModules.Where(module => !module.Init()))
        {
            DalamudApi.PluginLog.Error($"Failed to init misc module {module.GetType().FullName}");

            return false;
        }

        if (!_spawnNotificationModule.Init())
        {
            return false;
        }

        return true;
    }

    public void Shutdown()
    {
        foreach (var module in _miscModules)
        {
            module.Shutdown();
        }

        _spawnNotificationModule.Shutdown();

        //DalamudApi.Condition.ConditionChange -= Condition_ConditionChange;
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _spawnNotificationModule.PostInit(serviceProvider);

        _huntMapWindow = _windowSystem.Windows.FirstOrDefault(i => i.WindowName == HuntMapWindow.Name) as HuntMapWindow
                         ?? throw new InvalidOperationException("Failed to get HuntMapWindow");
    }

    public string UiName => "其他";

    public void OnDrawUi()
    {
        Widget.BeginFramedGroup("触发概率提醒", new Vector2(-1, -1));

        {
            _spawnNotificationModule.OnDrawUi();
        }

        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("触发消息相关", new Vector2(-1, -1));

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

        Widget.BeginFramedGroup("杂项", new Vector2(-1, -1));

        foreach (var miscModule in _miscModules)
        {
            miscModule.OnDrawUi();
        }

        var datetime = _dataManager.GetLastPatchHotFixTimestamp();

        if (datetime != DateTime.MinValue)
        {
            ImGui.Text("上一次服务器重启时间: ");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, datetime.ToLocalTime().ToString(CultureInfo.InvariantCulture));
        }

        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("查询S怪状态", new Vector2(-1, -1));
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
                                                   out var territoryId)
                             && _shouldShowHuntMapTerritories.Contains(territoryId);

        foreach (var status in statusList)
        {
            ImGui.TextUnformatted($"{status.WorldName}:");

            ImGui.TextUnformatted("\t丢失:");
            ImGui.SameLine();
            ImGui.TextColored(status.Missing ? ImGuiColors.DPSRed : ImGuiColors.ParsedGreen, status.Missing ? "是" : "否");

            var now = DateTime.Now.ToLocalTime();

            var minTime = DateTime.UnixEpoch.AddMilliseconds(status.ExpectMinTime).ToLocalTime();
            var maxTime = DateTime.UnixEpoch.AddMilliseconds(status.ExpectMaxTime).ToLocalTime();

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
