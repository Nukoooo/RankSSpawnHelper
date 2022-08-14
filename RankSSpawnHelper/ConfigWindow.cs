using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

// ReSharper disable InvertIf
namespace RankSSpawnHelper;

public class ConfigWindow : Window
{
    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;
    private readonly List<ColorInfo> _colorInfos = new();
    private ColorPickerType _colorPickerType = ColorPickerType.Failed;
    private string _serverUrl = string.Empty;
    private bool _showColorPicker;

    public ConfigWindow() : base("S怪触发小助手##RankSSpawnHelper")
    {
        var colorSheet = Service.DataManager.Excel.GetSheet<UIColor>();
        foreach (var color in colorSheet)
        {
            var result = _colorInfos.Find(info => info.Color == color.UIForeground);
            if (result == null)
                _colorInfos.Add(new ColorInfo
                {
                    RowId = color.RowId,
                    Color = color.UIForeground
                });
        }

        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    private static void DrawTrackerTable()
    {
        {
            var tracker = Service.Counter.GetTracker();

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

                foreach (var (mainKey, mainValue) in Service.Counter.GetTracker())
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
                    if (ImGui.Button("\xF12D")) Service.Counter.ClearKey(mainKey);
                    ImGui.PopStyleVar();
                    ImGui.PopFont();

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }
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

    public override void Draw()
    {
        ImGui.BeginTabBar("主菜单aaaaa");

        {
            if (ImGui.BeginTabItem("农怪计数"))
            {
                var trackKillCount = Service.Configuration._trackKillCount;
                if (ImGui.Checkbox("启用", ref trackKillCount))
                {
                    Service.Configuration._trackKillCount = trackKillCount;
                    Service.Counter.Overlay.IsOpen = trackKillCount;
                    Service.Configuration.Save();
                }

                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("开始时间是按照本地时间，如果需要填农怪表格什么的需要自行转换到相对应的时区\n");

                ImGui.SameLine();
                if (ImGui.Button("提交BUG/反馈意见")) Util.OpenLink("https://github.com/NukoOoOoOoO/RankSSpawnHelper/issues/new");

                var trackMode = Service.Configuration._trackRangeMode;
                if (ImGui.Checkbox("范围计数", ref trackMode))
                {
                    if (!Service.Counter.Socket.Connected())
                    {
                        Service.Configuration._trackRangeMode = trackMode;
                        Service.Configuration.Save();
                    }
                    else
                    {
                        Service.Configuration._trackRangeMode = false;
                    }
                }

                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("在联网计数时会暂时关闭\n");


                ImGui.SameLine();
                var showCurrentInstance = Service.Configuration._trackerShowCurrentInstance;
                if (ImGui.Checkbox("只显示当前区域", ref showCurrentInstance))
                {
                    Service.Configuration._trackerShowCurrentInstance = showCurrentInstance;
                    Service.Counter.Overlay.IsOpen =
                        Service.Counter.GetTracker().ContainsKey(Service.Counter.GetCurrentInstance());
                    Service.Configuration.Save();
                }

                var noTitle = Service.Configuration._trackerWindowNoTitle;
                if (ImGui.Checkbox("窗口无标题", ref noTitle))
                {
                    Service.Configuration._trackerWindowNoTitle = noTitle;
                    Service.Configuration.Save();
                }

                ImGui.SameLine();
                var noBackground = Service.Configuration._trackerWindowNoBackground;
                if (ImGui.Checkbox("窗口无背景", ref noBackground))
                {
                    Service.Configuration._trackerWindowNoBackground = noBackground;
                    Service.Configuration.Save();
                }

                var noNotification = Service.Configuration._trackerNoNotification;
                if (ImGui.Checkbox("连接服务器成功时不显示消息", ref noNotification))
                {
                    Service.Configuration._trackerNoNotification = noNotification;
                    Service.Configuration.Save();
                }

                ImGui.SetNextItemWidth(200);
                ImGui.InputTextWithHint("服务器链接", "比如ws://127.0.0.1:8000", ref _serverUrl, 256);

                if (ImGui.Button("连接")) Service.Counter.Socket.Connect(_serverUrl);
                ImGui.SameLine();
                if (ImGui.Button("连接到临时服务器")) Service.Counter.Socket.Connect("ws://47.106.224.112:8000");
                ImGui.SameLine();
                if (ImGui.Button("断开连接")) Service.Counter.Socket.Disconnect();

                ImGui.SameLine();
                ImGui.Text("连接状态:");
                ImGui.SameLine();
                ImGui.TextColored(Service.Counter.Socket.Connected() ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed, Service.Counter.Socket.Connected() ? "Connected" : "Disconnected");

                DrawTrackerTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("其他"))
            {
                var weeEaCounter = Service.Configuration._weeEaCounter;
                if (ImGui.Checkbox("小异亚计数", ref weeEaCounter))
                {
                    Service.Configuration._weeEaCounter = weeEaCounter;
                    Service.WeeEa.overlay.IsOpen = weeEaCounter && Service.ClientState.TerritoryType == 960;
                    Service.Configuration.Save();
                }

                var showInstance = Service.Configuration._showInstance;
                if (ImGui.Checkbox("在基本情报栏显示几线", ref showInstance))
                {
                    Service.Configuration._showInstance = showInstance;
                    Service.Configuration.Save();
                }

                ImGui.NewLine();

                ImGui.Text("触发失败消息颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##触发失败", GetColor(Service.Configuration._failedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Failed;
                }

                ImGui.Text("触发成功消息颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##触发成功", GetColor(Service.Configuration._spawnedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Spawned;
                }

                ImGui.Text("关键词颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##高亮", GetColor(Service.Configuration._highlightColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Highlighted;
                }

                ImGui.SameLine();
                if (ImGui.Button("预览"))
                    Service.ChatGui.PrintChat(new XivChatEntry
                    {
                        Message = new SeString(new List<Payload>
                        {
                            new UIForegroundPayload((ushort)Service.Configuration._failedMessageColor),
                            new TextPayload("Something came in the mail today... "),
                            new UIForegroundPayload((ushort)Service.Configuration._highlightColor),
                            new TextPayload("deez nuts! "),
                            new UIForegroundPayload(0),
                            new TextPayload("Ha! Got’em.\n"),
                            new UIForegroundPayload((ushort)Service.Configuration._spawnedMessageColor),
                            new TextPayload("Something came in the mail today... "),
                            new UIForegroundPayload((ushort)Service.Configuration._highlightColor),
                            new TextPayload("deez nuts! "),
                            new UIForegroundPayload(0),
                            new TextPayload("Ha! Got’em."),
                            new UIForegroundPayload(0)
                        }),
                        Type = XivChatType.Debug
                    });

                ImGui.EndTabItem();
            }
        }

        ImGui.EndTabBar();
    }

    public override void PostDraw()
    {
        if (!_showColorPicker)
            return;

        ImGui.SetNextWindowSize(new Vector2(320, 360));

        var type = _colorPickerType switch
        {
            ColorPickerType.Failed => "给触发失败消息",
            ColorPickerType.Highlighted => "给关键词",
            ColorPickerType.Spawned => "给触发成功消息",
            _ => ""
        };

        if (ImGui.Begin(type + "选个颜色呗", ref _showColorPicker, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize))
        {
            ImGui.Columns(10, "##colorcolumns", false);

            foreach (var info in _colorInfos)
            {
                if (ImGui.ColorButton("##" + info.RowId, GetColor(info.RowId)))
                {
                    switch (_colorPickerType)
                    {
                        case ColorPickerType.Failed:
                            Service.Configuration._failedMessageColor = info.RowId;
                            break;
                        case ColorPickerType.Highlighted:
                            Service.Configuration._highlightColor = info.RowId;
                            break;
                        case ColorPickerType.Spawned:
                            Service.Configuration._spawnedMessageColor = info.RowId;
                            break;
                    }

                    Service.Configuration.Save();
                    _showColorPicker = false;
                }

                ImGui.NextColumn();
            }

            ImGui.Columns(1);
            ImGui.End();
        }
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