using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;

// ReSharper disable InvertIf
namespace RankSSpawnHelper;

public class ConfigWindow : Window
{
    // private const double EORZEA_TIME_CONSTANT = 3600D / 175D;
    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;

    // private const string TimeRegexPattern = @"([0-5]?\d):([0-5]?\d)";
    private string _serverUrl = string.Empty;
    // private string _timeInput = string.Empty;
    // private string _trackerInputText = string.Empty;

    public ConfigWindow() : base("S怪触发小助手##RankSSpawnHelper")
    {
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

            /*if (ImGui.BeginTabItem("定ET+喊话"))
            {
                unsafe
                {
                    var eorzeaTime = DateTimeOffset.FromUnixTimeSeconds(Framework.Instance()->EorzeaTime).DateTime;
                    var localTime = Utils.LocalTimeToEorzeaTime();
                    
                    ImGui.Text($"FrameWork->eorzeaTime: {eorzeaTime.Hour}:{eorzeaTime.Minute}:{eorzeaTime.Second}");
                    ImGui.Text($"LocalTimeToEorzeaTime: {localTime.Hour}:{localTime.Minute}:{localTime.Second}");
                    
                    ImGui.Text("在多久后定时(本地时间):");
                    ImGui.InputTextWithHint("##timeInputYes", "格式: 分钟:秒 如00:24, 00:01", ref _timeInput, 32);

                }
                ImGui.EndTabItem();
            }*/

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

                ImGui.EndTabItem();
            }
        }

        ImGui.EndTabBar();
    }
}