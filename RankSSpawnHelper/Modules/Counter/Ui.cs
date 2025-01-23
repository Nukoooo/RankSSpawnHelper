using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Widgets;
using RankSSpawnHelper.Managers;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    private          DateTime _lastQueryTrackerDataTime     = DateTime.UnixEpoch;

    private void DrawConfig()
    {
        var trackKillCount = _configuration.TrackKillCount;

        if (ImGui.Checkbox("启用", ref trackKillCount))
        {
            _configuration.TrackKillCount = _counterWindow!.IsOpen = trackKillCount;
            _configuration.Save();
        }

        Widget.BeginFramedGroup("计数窗口");

        {
            var noTitle = _configuration.TrackerWindowNoTitle;

            if (ImGui.Checkbox("无标题", ref noTitle))
            {
                _configuration.TrackerWindowNoTitle = noTitle;
                _configuration.Save();
            }

            ImGui.SameLine();
            var noBackground = _configuration.TrackerWindowNoBackground;

            if (ImGui.Checkbox("无背景", ref noBackground))
            {
                _configuration.TrackerWindowNoBackground = noBackground;
                _configuration.Save();
            }

            ImGui.SameLine();
            var autoResize = _configuration.TrackerAutoResize;

            if (ImGui.Checkbox("自动调整大小", ref autoResize))
            {
                _configuration.TrackerAutoResize = autoResize;
                _configuration.Save();
            }
        }

        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("其他");

        {
            var weeEaCounter = _configuration.WeeEaCounter;

            if (ImGui.Checkbox("小异亚计数", ref weeEaCounter))
            {
                _configuration.WeeEaCounter = weeEaCounter;
                _configuration.Save();
            }

            var clearThreshold = _configuration.TrackerClearThreshold;
            ImGui.Text("x 分钟内没更新自动清除相关计数");
            ImGui.SetNextItemWidth(178);

            if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 30f, 60f, "%.2f分钟"))
            {
                _configuration.TrackerClearThreshold = clearThreshold;
                _configuration.Save();
            }
        }

        Widget.EndFramedGroup();

        ImGui.BeginGroup();
        ImGui.TextUnformatted("本地计数表格");
        DrawTrackerTable();
        ImGui.EndGroup();

        ImGui.NewLine();

        ImGui.BeginGroup();
        ImGui.TextUnformatted("服务器计数表格");
        DrawServerTrackerTable();
        ImGui.EndGroup();
    }

    private void DrawTrackerTable()
    {
        var tracker = GetLocalTrackers();

        if (tracker.Count == 0)
        {
            ImGui.TextUnformatted("暂无数据");

            return;
        }

        using var table = ImRaii.Table("##农怪计数表格",
                                       6,
                                       ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp,
                                       new (-1, -1));

        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("-");
        ImGui.TableSetupColumn("开始时间");
        ImGui.TableSetupColumn("计数①");
        ImGui.TableSetupColumn("计数②");
        ImGui.TableSetupColumn("计数③");
        ImGui.TableSetupColumn("##删除计数清除", ImGuiTableColumnFlags.WidthFixed, 3 * ImGui.GetFrameHeight());
        ImGui.TableHeadersRow();

        foreach (var (mainKey, mainValue) in tracker)
        {
            using (ImRaii.PushId($"##农怪表格{mainKey}"))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                var displayText = mainKey;

                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText)
                                            .X);

                ImGui.Text(displayText);

                ImGui.TableNextColumn();

                var time = DateTimeOffset.FromUnixTimeSeconds(mainValue.StartTime)
                                         .LocalDateTime;

                displayText = $"{time.Month}-{time.Day} / {time.ToShortTimeString()}";

                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText)
                                            .X);

                ImGui.Text(displayText);

                ImGui.TableNextColumn();
                var i = 0;

                foreach (var (subKey, subValue) in mainValue.Counter)
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

                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding,
                                            new Vector2(2,
                                                        ImGui.GetStyle()
                                                             .FramePadding.Y)))
                    {
                        if (ImGui.Button("\xF12D"))
                        {
                            RemoveInstance(mainKey);
                        }
                    }
                }
            }
        }
    }

    private void DrawServerTrackerTable()
    {
        var data = _trackerData;

        if (ImGui.Button("点我获取数据"))
        {
            if (DateTime.Now - _lastQueryTrackerDataTime >= TimeSpan.FromSeconds(30) && _connectionManager.IsConnected())
            {
                _connectionManager.SendMessage(new ConnectionManager.GetTrackerList
                {
                    Type       = "GetTrackerList",
                    ServerList = _dataManager.GetServerList(),
                });

                _lastQueryTrackerDataTime = DateTime.Now;
            }
        }

        if (data.Count == 0)
        {
            ImGui.TextUnformatted("暂无数据");

            return;
        }

        using (ImRaii.Table("##服务器计数表格",
                            5,
                            ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp,
                            new (-1, -1)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("-");
            ImGui.TableSetupColumn("上一次更新时间");
            ImGui.TableSetupColumn("计数①");
            ImGui.TableSetupColumn("计数②");
            ImGui.TableSetupColumn("计数③");
            ImGui.TableHeadersRow();

            foreach (var tracker in data)
            {
                using (ImRaii.PushId($"##服务器计数表格{tracker.WorldId}/{tracker.InstanceId}/{tracker.TerritoryId}"))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    var displayText = _dataManager.FormatInstance(tracker.WorldId, tracker.TerritoryId, tracker.InstanceId);

                    ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText)
                                                .X);

                    ImGui.Text(displayText);

                    ImGui.TableNextColumn();

                    var time = DateTimeOffset.FromUnixTimeSeconds(tracker.LastUpdateTime)
                                             .LocalDateTime;

                    displayText = $"{time.Month}-{time.Day} / {time.ToShortTimeString()}";

                    ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText)
                                                .X);

                    ImGui.Text(displayText);

                    ImGui.TableNextColumn();
                    var i = 0;

                    var isItem = _dataManager.IsTerritoryItemThingy(tracker.TerritoryId);

                    foreach (var (subKey, subValue) in tracker.CounterData)
                    {
                        var key = tracker.TerritoryId == 621 ? "扔垃圾" :
                            isItem ? _dataManager.GetItemName(subKey) : _dataManager.GetNpcName(subKey);

                        ImGui.TextUnformatted($"{key}: {subValue}");
                        ImGui.TableNextColumn();
                        i++;
                    }

                    for (; i < 3; i++)
                    {
                        ImGui.Text("-");
                        ImGui.TableNextColumn();
                    }
                }
            }
        }
    }
}