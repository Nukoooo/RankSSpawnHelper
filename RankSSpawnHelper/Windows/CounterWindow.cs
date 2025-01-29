using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Modules;

namespace RankSSpawnHelper.Windows;

internal class CounterWindow : Window
{
    public const     string             Name = "计数窗口";
    private readonly Configuration      _configuration;
    private readonly ICounter           _counter;
    private readonly IConnectionManager _connectionManager;
    private readonly IDataManager       _dataManager;
    private          DateTime           _nextClickTime = DateTime.Now;

    public CounterWindow(ServiceProvider service, Configuration configuration) : base(Name)
    {
        _configuration = configuration;
        _counter       = service.GetService<ICounter>() ?? throw new InvalidOperationException("Failed to get ICounter");

        _connectionManager = service.GetService<IConnectionManager>()
                             ?? throw new InvalidOperationException("Failed to get IConnectionManager");

        _dataManager = service.GetService<IDataManager>()
                       ?? throw new InvalidOperationException("Failed to get IGameDataManager");
    }

    private ImGuiWindowFlags BuildWindowFlags()
    {
        var var           = ImGuiWindowFlags.None;
        var territoryType = DalamudApi.ClientState.TerritoryType;

        var inUltima = territoryType == 960;

        if (inUltima || _configuration.TrackerWindowNoBackground)
        {
            var |= ImGuiWindowFlags.NoBackground;
        }

        if (inUltima || _configuration.TrackerWindowNoTitle)
        {
            var |= ImGuiWindowFlags.NoTitleBar;
        }

        if (inUltima || _configuration.TrackerAutoResize)
        {
            var |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
        }

        return var;
    }

    public override void PreOpenCheck()
    {
        var territoryType = DalamudApi.ClientState.TerritoryType;

        var inUltima = territoryType == 960;

        if (inUltima)
        {
            if (!_configuration.WeeEaCounter)
            {
                IsOpen = false;
            }

            return;
        }

        if (!_configuration.TrackKillCount)
        {
            IsOpen = false;

            return;
        }

        var networkTracker = _counter.GetNetworkedTrackers();
        var localTracker   = _counter.GetLocalTrackers();
        var actualTracker  = _connectionManager.IsConnected() ? networkTracker : localTracker;

        if (actualTracker.Count == 0 || !actualTracker.ContainsKey(_dataManager.FormatCurrentTerritory()))
        {
            IsOpen = false;
        }
    }

    public override void PreDraw()
        => Flags = BuildWindowFlags();

    public override void Draw()
    {
        _dataManager.NotoSan24.Push();

        if (DalamudApi.ClientState.TerritoryType == 960)
        {
            DrawWeeEaCounter();
        }
        else
        {
            DrawTracker();
        }

        _dataManager.NotoSan24.Pop();
    }

    private void DrawTracker()
    {
        var connected      = _connectionManager.IsConnected();
        var networkTracker = _counter.GetNetworkedTrackers();
        var localTracker   = _counter.GetLocalTrackers();
        var actualTracker  = connected ? networkTracker : localTracker;

        var currentInstance = _dataManager.FormatCurrentTerritory();

        if (!actualTracker.TryGetValue(currentInstance, out var value))
        {
            return;
        }

        if (ImGui.Button("[ 寄了点我 ]"))
        {
            if (DateTime.Now <= _nextClickTime)
            {
                Utils.Print(new List<Payload>
                {
                    new UIForegroundPayload(518),
                    new TextPayload($"你还得等 {(_nextClickTime - DateTime.Now).TotalSeconds:F}秒 才能再点这个按钮"),
                    new UIForegroundPayload(0),
                });
            }
            else
            {
                if (_connectionManager.IsConnected())
                {
                    _connectionManager.SendMessage(new ConnectionManager.AttemptMessage
                    {
                        Type        = "ggnore",
                        WorldId     = _dataManager.GetCurrentWorldId(),
                        InstanceId  = _dataManager.GetCurrentInstance(),
                        TerritoryId = DalamudApi.ClientState.TerritoryType,

                        // Instance    = Plugin.Managers.Data.Player.GetCurrentTerritory(),
                        Failed = true,
                    });
                }
                else
                {
                    var startTime = DateTime.UnixEpoch.AddSeconds(value.StartTime).ToLocalTime();

                    var endTime = DateTimeOffset.Now.LocalDateTime;

                    var message = $"{currentInstance}的计数寄了！\n"
                                  + $"开始时间: {startTime.ToShortDateString()}/{startTime.ToShortTimeString()}\n"
                                  + $"结束时间: {endTime.ToShortDateString()}/{endTime.ToShortTimeString()}\n"
                                  + "计数详情: \n";

                    foreach (var (k, v) in value.Counter)
                    {
                        message += $"    {k}: {v}\n";
                    }

                    Utils.Print(new List<Payload>()
                    {
                        new UIForegroundPayload(518),
                        new TextPayload(message),
                        new UIForegroundPayload(0),
                    });

                    _counter.RemoveInstance(currentInstance);
                }

                _nextClickTime = DateTime.Now + TimeSpan.FromSeconds(15);
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(currentInstance);

        var time = DateTime.UnixEpoch.AddSeconds(value.StartTime).ToLocalTime();

        ImGui.Text($"\t开始时间: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

        foreach (var (subKey, subValue) in value.Counter)
        {
            var textToDraw = $"\t{subKey} - {subValue}";

            if (connected
                && localTracker.TryGetValue(currentInstance, out var v)
                && v.Counter.TryGetValue(subKey, out var localValue))
            {
                textToDraw += $" ({localValue})";
            }

            ImGui.Text(textToDraw);
        }
    }

    private void DrawWeeEaCounter()
    {
        if (!_configuration.WeeEaCounter)
        {
            IsOpen = false;

            return;
        }

        var (nameList, nonWeeEaCount) = _counter.GetWeeEaCounter();

        if (ImGui.Button("[ 寄了点我 ]"))
        {
            SendWeeEaAttemptFail(nameList);
        }

        ImGui.SameLine();

        ImGui.Text($"附近的小异亚数量:{nameList.Count}\n非小异亚的数量: {nonWeeEaCount}");
    }

    private void SendWeeEaAttemptFail(List<string> nameList)
    {
        if (nameList.Count < 10)
        {
            Utils.Print(new List<Payload>
            {
                new UIForegroundPayload(518),
                new TextPayload("Error: 附近的小异亚不够10个,寄啥呢"),
                new UIForegroundPayload(0),
            });

            return;
        }

        var time = _nextClickTime;

        if (time > DateTime.Now)
        {
            var delta = time - DateTime.Now;

            Utils.Print(new List<Payload>
            {
                new UIForegroundPayload(518),
                new TextPayload($"Error: 你还得等 {delta:g} 才能再点寄"),
                new UIForegroundPayload(0),
            });

            return;
        }

        _nextClickTime = DateTime.Now + TimeSpan.FromSeconds(15);

        _connectionManager.SendMessage(new ConnectionManager.AttemptMessage
        {
            Type = "WeeEa",

            // Instance    = currentInstance,
            WorldId     = _dataManager.GetCurrentWorldId(),
            InstanceId  = _dataManager.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            Failed      = true,
            Names       = nameList,
        });
    }
}
