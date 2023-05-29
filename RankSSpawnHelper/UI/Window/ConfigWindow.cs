using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Managers.DataManagers;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Ui.Window;

public class ConfigWindow : Dalamud.Interface.Windowing.Window
{
    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;
    private readonly string[] _attemptMessageDisplayType = { "不显示", "简单", "详细" };

    private readonly List<ColorInfo> _colorInfos = new();
    private readonly List<string> _expansions = new() { "2.0", "3.0", "4.0", "5.0", "6.0" };

    private readonly string[] _spawnNotificationType = { "关闭", "只在可触发时", "一直" };
    private ColorPickerType _colorPickerType = ColorPickerType.Failed;

    private TextureWrap _image;
    private List<string> _monsterNames;
    private int _selectedExpansion;
    private int _selectedInstance;
    private int _selectedMonster;

    private List<string> _servers;

    private string _serverUrl = string.Empty;
    private bool _showColorPicker;

    public ConfigWindow() : base("菜单设置##ranksspawnhelper1337")
    {
        Initialize();
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _monsterNames ??= Plugin.Managers.Data.Monster.GetMonstersByExpansion((GameExpansion)_selectedExpansion);
    }

    public override void Draw()
    {
        ImGui.BeginTabBar("SpawnHelper主菜单");
        {
            if (ImGui.BeginTabItem("农怪计数"))
            {
                DrawCounterTab();
                ImGui.EndTabItem();
            }

            if (_servers != null)
            {
                if (ImGui.BeginTabItem("S怪状态查询"))
                {
                    DrawQueryTab();
                    ImGui.EndTabItem();
                }
            }

            if (ImGui.BeginTabItem("其他"))
            {
                DrawMiscTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("关于"))
            {
                ImGui.Text("在使用本插件的联网功能时,将会收集以下信息:\n" +
                           "    - 游戏ID以及所在的服务器, 如 Deez Nuts@Siren\n" +
                           "    - 当前所在区域, 如: 海猫茶屋@叹息海@0\n" +
                           "    - 计数的数量&名字, 如: 矮人棉: 50, 彷徨之物: 12\n" +
                           "    - 开始计数的时间戳, 如: 1667166428\n");

                if (_image != null)
                    ImGui.Image(_image.ImGuiHandle, new Vector2(_image.Width, _image.Height) / 1.5f);

                ImGui.EndTabItem();
            }
        }
        ImGui.EndTabBar();
    }

    public override void PostDraw()
    {
        DrawColorPicker();
    }

    private void DrawCounterTab()
    {
        var trackKillCount = Plugin.Configuration.TrackKillCount;
        if (ImGui.Checkbox("启用", ref trackKillCount))
        {
            Plugin.Configuration.TrackKillCount = trackKillCount;
            Plugin.Windows.CounterWindow.IsOpen = trackKillCount;
            Plugin.Configuration.Save();
        }

        var trackMode = Plugin.Configuration.TrackRangeMode;
        if (ImGui.Checkbox("范围计数", ref trackMode))
        {
            if (!Plugin.Managers.Socket.Connected())
            {
                Plugin.Configuration.TrackRangeMode = trackMode;
                Plugin.Configuration.Save();
            }
            else
                Plugin.Configuration.TrackRangeMode = false;
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("在联网计数时会暂时关闭\n");

        ImGui.SameLine();
        var showCurrentInstance = Plugin.Configuration.TrackerShowCurrentInstance;
        if (ImGui.Checkbox("只显示当前区域", ref showCurrentInstance))
        {
            Plugin.Configuration.TrackerShowCurrentInstance = showCurrentInstance;
            Plugin.Windows.CounterWindow.IsOpen             = Plugin.Features.Counter.GetLocalTrackers().ContainsKey(Plugin.Managers.Data.Player.GetCurrentTerritory());
            Plugin.Configuration.Save();
        }

        var noTitle = Plugin.Configuration.TrackerWindowNoTitle;
        if (ImGui.Checkbox("窗口无标题", ref noTitle))
        {
            Plugin.Configuration.TrackerWindowNoTitle = noTitle;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var noBackground = Plugin.Configuration.TrackerWindowNoBackground;
        if (ImGui.Checkbox("窗口无背景", ref noBackground))
        {
            Plugin.Configuration.TrackerWindowNoBackground = noBackground;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var autoResize = Plugin.Configuration.TrackerAutoResize;
        if (ImGui.Checkbox("窗口自动调整大小", ref autoResize))
        {
            Plugin.Configuration.TrackerAutoResize = autoResize;
            Plugin.Configuration.Save();
        }

        ImGui.SetNextItemWidth(90);
        var attemptMessageType = (int)Plugin.Configuration.AttemptMessage;
        if (ImGui.Combo("触发消息显示", ref attemptMessageType, _attemptMessageDisplayType, _attemptMessageDisplayType.Length))
        {
            Plugin.Configuration.AttemptMessage = (AttemptMessageType)attemptMessageType;
            Plugin.Configuration.Save();
        }

#if DEBUG
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("服务器链接", ref _serverUrl, 256);

            if (ImGui.Button("连接")) Plugin.Managers.Socket.Connect(_serverUrl);
            ImGui.SameLine();
            if (ImGui.Button("连接到临时服务器")) Plugin.Managers.Socket.Connect("ws://124.220.161.157:8000");
            ImGui.SameLine();
            if (ImGui.Button("断开连接")) Plugin.Managers.Socket.Disconnect();

            ImGui.SameLine();
#endif

        ImGui.Text("连接状态:");
        ImGui.SameLine();
        ImGui.TextColored(Plugin.Managers.Socket.Connected() ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed, Plugin.Managers.Socket.Connected() ? "Connected" : "Disconnected");

#if RELEASE
            ImGui.SameLine();
            if (ImGui.Button("重新连接"))
                Plugin.Managers.Socket.Reconnect();
#endif

        DrawTrackerTable();
    }

    private static void DrawTrackerTable()
    {
        var tracker = Plugin.Features.Counter.GetLocalTrackers();

        if (tracker.Count == 0)
            return;

        // 从FFLogsViewer那边拿过来的, credit goes to Aireil <3333
        if (ImGui.BeginTable("##农怪计数表格", 6, TableFlags, new Vector2(-1, -1)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("所在服务器");
            ImGui.TableSetupColumn("开始时间");
            ImGui.TableSetupColumn("##计数①");
            ImGui.TableSetupColumn("##计数②");
            ImGui.TableSetupColumn("##计数③");
            ImGui.TableSetupColumn("##删除计数清除", ImGuiTableColumnFlags.WidthFixed, 20 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            foreach (var (mainKey, mainValue) in Plugin.Features.Counter.GetLocalTrackers())
            {
                ImGui.PushID($"##农怪表格{mainKey}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                var displayText = mainKey.Replace("@", " ");
                if (!displayText.EndsWith('0'))
                    displayText += "线";
                else
                    displayText = displayText[..^2];

                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText).X);
                ImGui.Text(displayText);

                ImGui.TableNextColumn();
                var time = DateTimeOffset.FromUnixTimeSeconds(mainValue.startTime).LocalDateTime;
                displayText = $"{time.Month}-{time.Day} / {time.ToShortTimeString()}";
                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText).X);
                ImGui.Text(displayText);

                ImGui.TableNextColumn();
                var i = 0;

                foreach (var (subKey, subValue) in mainValue.counter)
                {
                    ImGui.Text($"{subKey}: {subValue}");
                    ImGui.TableNextColumn();
                    i++;
                }

                for (; i < 3; i++)
                {
                    ImGui.Text("-");
                    ImGui.TableNextColumn();
                }

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, ImGui.GetStyle().FramePadding.Y));
                if (ImGui.Button("\xF12D")) Plugin.Features.Counter.RemoveInstance(mainKey);
                ImGui.PopStyleVar();
                ImGui.PopFont();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void DrawQueryTab()
    {
        if (ImGui.Combo("版本", ref _selectedExpansion, _expansions.ToArray(), _expansions.Count))
        {
            _monsterNames    = Plugin.Managers.Data.Monster.GetMonstersByExpansion((GameExpansion)_selectedExpansion);
            _selectedMonster = 0;
        }

        ImGui.Combo("S怪", ref _selectedMonster, _monsterNames.ToArray(), _monsterNames.Count);
        if (ImGui.InputInt("几线", ref _selectedInstance, 1)) _selectedInstance = Math.Clamp(_selectedInstance, 0, 3);

        ImGui.SameLine();

        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Search.ToIconString()))
                Plugin.Managers.Data.Monster.FetchData(_servers, _monsterNames[_selectedMonster], _selectedInstance);
            ImGui.PopFont();
        }

        if (Plugin.Managers.Data.Monster.GetErrorMessage() != string.Empty)
            ImGui.TextColored(ImGuiColors.DPSRed, Plugin.Managers.Data.Monster.GetErrorMessage());
        else
        {
            if (Plugin.Managers.Data.Monster.GetFetchStatus() == FetchStatus.Fetching)
                ImGui.Text("正在获取数据");
            else if (Plugin.Managers.Data.Monster.GetFetchStatus() == FetchStatus.Success)
            {
                var huntStatus = Plugin.Managers.Data.Monster.GetHuntStatus();
                if (huntStatus == null || huntStatus.Count == 0)
                {
                    ImGui.TextColored(ImGuiColors.DPSRed, "成功获取数据但无效??");
                    return;
                }

                var canShowHuntMap = Plugin.Features.ShowHuntMap.CanShowHuntMapWithMonsterName(huntStatus[0].localizedName);

                foreach (var status in huntStatus)
                {
                    var currentInstance = $"{status.worldName}@{status.localizedName}{(status.instance == 0 ? "" : $" {status.instance}线")}";
                    ImGui.Text($"{currentInstance} 的状态:");

                    ImGui.Text("\t是否丢失:");
                    ImGui.SameLine();
                    ImGui.TextColored(status.missing ? ImGuiColors.DPSRed : ImGuiColors.ParsedGreen, status.missing ? "是" : "否");

                    var now     = DateTimeOffset.Now;
                    var minTime = DateTimeOffset.FromUnixTimeSeconds(status.expectMinTime).AddMinutes(-30);
                    var maxTime = DateTimeOffset.FromUnixTimeSeconds(status.expectMaxTime);
                    if (minTime > now)
                    {
                        var delta = minTime - now;
                        ImGui.Text($"\t距离进入可触发时间还有: {delta.Hours + delta.Days * 24:D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");
                    }
                    else
                    {
                        var percentage = 100 * ((now.ToUnixTimeSeconds() - status.expectMinTime) / (double)(status.expectMaxTime - status.expectMinTime));
                        ImGui.Text("\t当前可触发的概率为:");
                        ImGui.SameLine();
                        ImGui.TextColored(percentage > 100.0 ? ImGuiColors.ParsedBlue : ImGuiColors.ParsedGreen, $"{percentage:F2}%%");
                        // ReSharper disable once InvertIf
                        if (now < maxTime)
                        {
                            var delta = maxTime - now;
                            ImGui.Text($"\t距离进入强制期还有: {delta.Hours + delta.Days * 24:D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");
                        }
                    }

                    if (canShowHuntMap)
                    {
                        ImGui.SameLine();

                        if (ImGui.Button($"查看触发点##{status.worldName}"))
                        {
                            Task.Run(async () =>
                                     {
                                         var huntMap = await Plugin.Managers.Data.Monster.FetchHuntMap(status.worldName, status.localizedName, status.instance);
                                         if (huntMap == null || huntMap.spawnPoints.Count == 0)
                                         {
                                             PluginLog.Debug("huntMap == null || huntMap.spawnPoints.Count == 0 from QueryTab");
                                             return;
                                         }

                                         var texture = Plugin.Features.ShowHuntMap.GeTextureWithMonsterName(status.localizedName);
                                         Plugin.Windows.HuntMapWindow.SetCurrentMap(texture, huntMap.spawnPoints, currentInstance);
                                         Plugin.Windows.HuntMapWindow.IsOpen = true;
                                     });
                        }
                    }
                }
            }
        }
    }

    private void DrawMiscTab()
    {
        var weeEaCounter = Plugin.Configuration.WeeEaCounter;
        if (ImGui.Checkbox("小异亚计数", ref weeEaCounter))
        {
            Plugin.Configuration.WeeEaCounter = weeEaCounter;
            Plugin.Windows.WeeEaWindow.IsOpen = weeEaCounter && DalamudApi.ClientState.TerritoryType == 960;
            Plugin.Configuration.Save();
        }

        var showInstance = Plugin.Configuration.ShowInstance;
        if (ImGui.Checkbox("在基本情报栏显示几线", ref showInstance))
        {
            Plugin.Configuration.ShowInstance = showInstance;
            Plugin.Configuration.Save();
        }

        var enableAttemptMessagesFromOtherDcs = Plugin.Configuration.EnableAttemptMessagesFromOtherDcs;
        if (ImGui.Checkbox("接受触发成功/失败消息", ref enableAttemptMessagesFromOtherDcs))
        {
            Plugin.Configuration.EnableAttemptMessagesFromOtherDcs = enableAttemptMessagesFromOtherDcs;
            Plugin.Configuration.Save();
        }

        if (enableAttemptMessagesFromOtherDcs)
        {
            ImGui.SameLine();
            var receiveMessageFromOtherDc = Plugin.Configuration.ReceiveAttempMessageFromOtherDc;
            if (ImGui.Checkbox("接收其他大区的触发消息", ref receiveMessageFromOtherDc))
            {
                Plugin.Configuration.ReceiveAttempMessageFromOtherDc = receiveMessageFromOtherDc;
                Plugin.Configuration.Save();
            }
        }

        ImGui.SetNextItemWidth(100);
        // Fuck C#
        var spawnNotificationType = (int)Plugin.Configuration.SpawnNotificationType;
        if (ImGui.Combo("触发概率提示", ref spawnNotificationType, _spawnNotificationType, _spawnNotificationType.Length))
        {
            Plugin.Configuration.SpawnNotificationType = (SpawnNotificationType)spawnNotificationType;
            Plugin.Configuration.Save();
        }

        if (Plugin.Configuration.SpawnNotificationType == SpawnNotificationType.Full)
        {
            ImGui.SameLine();
            var coolDownNotificationSound = Plugin.Configuration.CoolDownNotificationSound;
            if (ImGui.Checkbox("不在触发期时播放提示音", ref coolDownNotificationSound))
            {
                Plugin.Configuration.CoolDownNotificationSound = coolDownNotificationSound;
                Plugin.Configuration.Save();
            }
        }

        var autoShowHuntMap = Plugin.Configuration.AutoShowHuntMap;
        if (ImGui.Checkbox("自动获取点位列表", ref autoShowHuntMap))
        {
            Plugin.Configuration.AutoShowHuntMap = autoShowHuntMap;
            Plugin.Configuration.Save();
        }

        if (Plugin.Configuration.AutoShowHuntMap && Plugin.Configuration.SpawnNotificationType > 0)
        {
            ImGui.SameLine();
            var onlyFetchInDuration = Plugin.Configuration.OnlyFetchInDuration;
            if (ImGui.Checkbox("只在触发期内获取", ref onlyFetchInDuration))
            {
                Plugin.Configuration.OnlyFetchInDuration = onlyFetchInDuration;
                Plugin.Configuration.Save();
            }
        }

        var playerSearchTip = Plugin.Configuration.PlayerSearchTip;
        if (ImGui.Checkbox("显示玩家搜索提示", ref playerSearchTip))
        {
            Plugin.Configuration.PlayerSearchTip = playerSearchTip;
            Plugin.Configuration.Save();
        }

        ImGui.NewLine();

        ImGui.Text("触发失败消息颜色");
        ImGui.SameLine();
        if (ImGui.ColorButton("##触发失败", GetColor(Plugin.Configuration.FailedMessageColor)))
        {
            _showColorPicker = true;
            _colorPickerType = ColorPickerType.Failed;
        }

        ImGui.Text("触发成功消息颜色");
        ImGui.SameLine();
        if (ImGui.ColorButton("##触发成功", GetColor(Plugin.Configuration.SpawnedMessageColor)))
        {
            _showColorPicker = true;
            _colorPickerType = ColorPickerType.Spawned;
        }

        ImGui.Text("关键词颜色");
        ImGui.SameLine();
        if (ImGui.ColorButton("##高亮", GetColor(Plugin.Configuration.HighlightColor)))
        {
            _showColorPicker = true;
            _colorPickerType = ColorPickerType.Highlighted;
        }

        ImGui.SameLine();
        if (ImGui.Button("预览"))
        {
            DalamudApi.ChatGui.PrintChat(new XivChatEntry
                                         {
                                             Message = new SeString(new List<Payload>
                                                                    {
                                                                        new UIForegroundPayload((ushort)Plugin.Configuration.FailedMessageColor),
                                                                        new TextPayload("Something came in the mail today... "),
                                                                        new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                                                                        new TextPayload("deez nuts! "),
                                                                        new UIForegroundPayload(0),
                                                                        new TextPayload("Ha! Got’em.\n"),
                                                                        new UIForegroundPayload((ushort)Plugin.Configuration.SpawnedMessageColor),
                                                                        new TextPayload("Something came in the mail today... "),
                                                                        new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                                                                        new TextPayload("deez nuts! "),
                                                                        new UIForegroundPayload(0),
                                                                        new TextPayload("Ha! Got’em."),
                                                                        new UIForegroundPayload(0)
                                                                    }),
                                             Type = XivChatType.Debug
                                         });
        }


        ImGui.NewLine();
        var clearThreshold = Plugin.Configuration.TrackerClearThreshold;
        ImGui.Text("在多少分钟后没更新就自动清除计数");
#if DEBUG
            if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 1f, 60f, "%.2f分"))
#else
        if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 30f, 60f, "%.2f分"))
#endif
        {
            Plugin.Configuration.TrackerClearThreshold = clearThreshold;
            Plugin.Configuration.Save();
        }
    }

    private void DrawColorPicker()
    {
        if (!_showColorPicker)
            return;

        ImGui.SetNextWindowSize(new Vector2(320, 360));

        var type = _colorPickerType switch
                   {
                       ColorPickerType.Failed      => "给触发失败消息",
                       ColorPickerType.Highlighted => "给关键词",
                       ColorPickerType.Spawned     => "给触发成功消息",
                       _                           => ""
                   };

        if (!ImGui.Begin(type + "选个颜色呗", ref _showColorPicker, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize)) return;
        ImGui.Columns(10, "##colorcolumns", false);

        foreach (var info in _colorInfos)
        {
            if (ImGui.ColorButton("##" + info.RowId, GetColor(info.RowId)))
            {
                switch (_colorPickerType)
                {
                    case ColorPickerType.Failed:
                        Plugin.Configuration.FailedMessageColor = info.RowId;
                        break;
                    case ColorPickerType.Highlighted:
                        Plugin.Configuration.HighlightColor = info.RowId;
                        break;
                    case ColorPickerType.Spawned:
                        Plugin.Configuration.SpawnedMessageColor = info.RowId;
                        break;
                }

                Plugin.Configuration.Save();
                _showColorPicker = false;
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.End();
    }

    private Vector4 GetColor(uint id)
    {
        try
        {
            var bytes = BitConverter.GetBytes(_colorInfos.Find(info => info.RowId == id).Color);
            return new Vector4((float)bytes[3] / 255, (float)bytes[2] / 255, (float)bytes[1] / 255, (float)bytes[0] / 255);
        }
        catch (Exception)
        {
            return new Vector4(0f, 0f, 0f, 0f);
        }
    }

    private void Initialize()
    {
        var colorSheet = DalamudApi.DataManager.Excel.GetSheet<UIColor>();
        foreach (var color in colorSheet)
        {
            var result = _colorInfos.Find(info => info.Color == color.UIForeground);
            if (result == null)
            {
                _colorInfos.Add(new ColorInfo
                                {
                                    RowId = color.RowId,
                                    Color = color.UIForeground
                                });
            }
        }

        Task.Run(async () => { _image = await DalamudApi.Interface.UiBuilder.LoadImageAsync(Resource.a); });

        Task.Run(async () =>
                 {
                     while (DalamudApi.ClientState.LocalPlayer == null)
                     {
                         await Task.Delay(100);
                     }

                     _servers = Data.GetServers();
                 });
    }

    private enum ColorPickerType
    {
        Failed,
        Spawned,
        Highlighted
    }

    internal class ColorInfo
    {
        internal uint RowId { get; set; }
        internal uint Color { get; set; }
    }
}