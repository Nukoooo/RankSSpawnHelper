using Dalamud.Interface.Colors;
using ImGuiNET;
using OtterGui.Widgets;

namespace RankSSpawnHelper.Modules;

internal partial class Counter
{
    private string _proxyUrl = string.Empty;

    private void DrawConfig()
    {
        var trackKillCount = _configuration.TrackKillCount;

        if (ImGui.Checkbox("启用", ref trackKillCount))
        {
            _configuration.TrackKillCount = _counterWindow!.IsOpen = trackKillCount;
            _configuration.Save();
        }

        var connected = _connectionManager.IsConnected();
        ImGui.SameLine();
        ImGui.Text("连接状态:");
        ImGui.SameLine();

        ImGui.TextColored(connected ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed,
                          connected ? "已连接" : "未连接");

        ImGui.SameLine();

        if (ImGui.Button("重新连接"))
        {
            _connectionManager.Reconnect();
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

        Widget.BeginFramedGroup("代理设置");

        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "连不上的时候再用!!!!");

            ImGui.TextUnformatted("使用方法: {代理类型}://127.0.0.1:{代理端口}, 比如 http://127.0.0.1:7890.");
            ImGui.TextUnformatted("如果不知道怎么填请查看你所使用的代理设置.");
            ImGui.TextUnformatted("Clash(图标是猫的)默认是http,端口7890, Shadowsocks(小飞机)默认是socks5,端口1080");
            ImGui.TextUnformatted("请根据自己的实际情况填写,上述以及默认给出来的链接仅供参考!!!!!");
            ImGui.NewLine();

            var useProxy = _configuration.UseProxy;

            if (ImGui.Checkbox("使用代理连接到服务器", ref useProxy))
            {
                _configuration.UseProxy = useProxy;
                _configuration.Save();
            }

            ImGui.SetNextItemWidth(256);

            if (ImGui.InputTextWithHint("##proxyURL", "代理链接", ref _proxyUrl, 256))
            {
                _configuration.ProxyUrl = _proxyUrl;
            }

            ImGui.SameLine();

            if (ImGui.Button("保存并重新连接"))
            {
                _configuration.Save();
                _connectionManager.Reconnect();
            }
        }

        Widget.EndFramedGroup();
    }
}