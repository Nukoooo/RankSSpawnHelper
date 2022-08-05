using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace RankSSpawnHelper.Features;

public class FateRecorder : IDisposable
{
    private readonly CancellationTokenSource _eventLoopTokenSource = new();

    private readonly Dictionary<ushort, FateInfo> _fateList = new();

    public Overlay _overlay;

    public FateRecorder()
    {
        _overlay = new Overlay();
        Task.Factory.StartNew(ProcessTask, TaskCreationOptions.LongRunning);
    }

    public void Dispose()
    {
        _eventLoopTokenSource.Cancel();
        _eventLoopTokenSource.Dispose();
    }

    private void ProcessFate()
    {
        if (!Service.Configuration._recordFATEsInSouthThanalan)
            return;

        // 146是南萨的ID
        if (Service.ClientState.TerritoryType != 146)
            return;

        /*foreach (var fate in Service.FateTable)
        {
            if (!_fateList.TryGetValue(fate.FateId, out var fateInfo))
            {
                _fateList.Add(fate.FateId, new FateInfo()
                {
                    name = fate.Name.ToString(),
                    duration =  fate.Duration,
                    startEpoch = fate.StartTimeEpoch,
                    startEpoch2 = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    endEpoch = fate.StartTimeEpoch + fate.Duration,
                    progress = fate.Progress,
                    state = fate.State
                });
                continue;
            }

            fateInfo.name = fate.Name.ToString();
            fateInfo.duration = fate.Duration;
            fateInfo.startEpoch = fate.StartTimeEpoch;
            fateInfo.endEpoch = fate.StartTimeEpoch + fate.Duration;
            fateInfo.progress = fate.Progress;
            if (fateInfo.state != fate.State)
                fateInfo.startEpoch2 = DateTimeOffset.Now.ToUnixTimeSeconds();
            fateInfo.state = fate.State;
            _fateList[fate.FateId] = fateInfo;
        }*/
    }

    private async void ProcessTask()
    {
        var token = _eventLoopTokenSource.Token;

        while (!token.IsCancellationRequested)
            try
            {
                await Task.Delay(200, token);
                ProcessFate();
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
    }

    public class Overlay : Window
    {
        private const ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;

        public Overlay() : base("南萨Fate##RankSSpawnHelper")
        {
            Flags = _windowFlags;
        }

        public override void Draw()
        {
            if (Fonts.AreFontsBuilt())
            {
                ImGui.PushFont(Fonts.Yahei24);
                ImGui.SetWindowFontScale(0.8f);
            }

            var currentTimestamp = DateTimeOffset.Now;

            foreach (var (key, value) in Service.FateRecorder._fateList)
            {
                ImGui.Text($"Fate: {value.name}");
                ImGui.Text($"\t进度: {value.progress} | 状态: {value.state} | 剩余时间: {value.endEpoch - currentTimestamp.ToUnixTimeSeconds()} | 间隔: {value.duration} | 开始时间: {value.startEpoch2}");
            }

            if (!Fonts.AreFontsBuilt()) return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);
        }
    }

    // 只是一个简简单单的struct
    public class FateInfo
    {
        public short duration;
        public long endEpoch;
        public string name;
        public byte progress;
        public long startEpoch;
        public long startEpoch2;
        public FateState state;
    }
}