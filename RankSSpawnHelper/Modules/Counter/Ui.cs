using ImGuiNET;
using OtterGui.Widgets;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
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
            var clearThreshold = _configuration.TrackerClearThreshold;
            ImGui.Text("x 分钟内没更新自动清除相关计数");
            ImGui.SetNextItemWidth(178);

            if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 30f, 60f, "%.2f分钟"))
            {
                _configuration.TrackerClearThreshold = clearThreshold;
                _configuration.Save();
            }

            var weeEaCounter = _configuration.WeeEaCounter;

            if (ImGui.Checkbox("小异亚计数", ref weeEaCounter))
            {
                _configuration.WeeEaCounter = weeEaCounter;
                _configuration.Save();
            }
        }

        Widget.EndFramedGroup();
    }
}