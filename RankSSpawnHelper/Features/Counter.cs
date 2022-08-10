using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using RankSSpawnHelper.Misc;

// ReSharper disable InconsistentNaming

namespace RankSSpawnHelper.Features;

internal class Message
{
    public long time { get; set; }
    public string type { get; set; }
    public string instance { get; set; }
    public string user { get; set; }
    public Dictionary<string, int> data { get; set; }
    public bool failed { get; set; }
}

internal class NewConnectionMessage
{
    public string currentInstance;
    public List<Tracker> trackers;
    public string type;
    public string user;

    internal class Tracker
    {
        public Dictionary<string, int> data;
        public string instance;
        public long time;
    }
}

public class Counter : IDisposable
{
    private readonly Hook<ActorControlSelfDelegate> _actorControlSelfHook;

    private readonly Dictionary<ushort, string> _conditionName = new()
    {
        { 959, "思考之物,彷徨之物,叹息之物" }, // 叹息海
        { 957, "毕舍遮,金刚尾,阿输陀花" }, // 萨维奈岛
        { 814, "矮人棉" },
        { 817, "破裂的隆卡器皿,破裂的隆卡石蒺藜,破裂的隆卡人偶" }, // 拉凯提卡大森林
        { 621, ".*" }, // 湖区
        { 613, "无壳观梦螺,观梦螺" }, // 红玉海
        { 612, "狄亚卡,莱西" }, // 边区
        { 402, "美拉西迪亚薇薇尔飞龙,小海德拉,亚拉戈奇美拉" }, // 魔大陆
        { 400, "星极花|皇金矿" }, // 翻云雾海
        { 147, "土元精" } // 北萨
    };

    private readonly IntPtr _instanceNumberAddress;

    private readonly ExcelSheet<TerritoryType> _terr;

    // currentworld+territory+instance, std::pair<time, std::unordered_map<monsterName, count>>
    private readonly Dictionary<string, Tracker> _tracker = new();

    private Tuple<SeString, string> _lastCounterMessage;

    public CounterOverlay Overlay;

    public SocketManager Socket;

    public Counter(DalamudPluginInterface pluginInterface)
    {
        _terr = Service.DataManager.GetExcelSheet<TerritoryType>();
        Overlay = new CounterOverlay();
        Socket = new SocketManager(pluginInterface);

        _instanceNumberAddress =
            Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BD");

        _actorControlSelfHook = Hook<ActorControlSelfDelegate>.FromAddress(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"),
            hk_ActorControlSelf);
        _actorControlSelfHook.Enable();

        Service.ChatGui.ChatMessage += OnChatMessage;
        Service.Condition.ConditionChange += OnConditionChange;
    }

    public void Dispose()
    {
        _actorControlSelfHook.Dispose();
        Socket.Dispose();
        Service.ChatGui.ChatMessage -= OnChatMessage;
        Service.Condition.ConditionChange -= OnConditionChange;
        GC.SuppressFinalize(this);
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // 2115 = 采集的消息类型, SystemMessage = 舍弃物品的消息类型
        if (type != (XivChatType)2115 && type != XivChatType.SystemMessage)
            return;

        var territory = Service.ClientState.TerritoryType;
        if (!_conditionName.TryGetValue(territory, out var targetName))
            return;

        if (message.TextValue == "感觉到了强大的恶名精英的气息……")
        {
            var msg = FormatJsonString("ggnore", GetCurrentInstance());
            Socket.SendMessage(msg);
            return;
        }

        var condition = targetName == ".*" ? "舍弃了" : "获得了";

        var reg = Regex.Match(message.ToString(), $"{condition}“({targetName})”");
        if (!reg.Success)
            return;

        targetName = territory switch
        {
            // 云海的刚哥要各挖50个, 所以这里分开来
            400 => reg.Groups[0].ToString(),
            // 因为正则所以得这样子搞..
            621 => "扔垃圾",
            _ => targetName
        };

        var key = GetCurrentInstance();
        AddToTracker(key, targetName);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value) return;

        if (!Service.Configuration._trackKillCount || !Service.Configuration._trackerShowCurrentInstance) return;

        Socket.SendMessage(FormatJsonString("changeArea"));
        Overlay.IsOpen = _tracker.TryGetValue(GetCurrentInstance(), out _);
    }

    public Dictionary<string, Tracker> GetTracker()
    {
        return _tracker;
    }

    public void SetValue(string instance, string key, int value, long time)
    {
        if (!_tracker.TryGetValue(instance, out var result))
            return;

        if (!result.counter.ContainsKey(key))
        {
            result.counter.Add(key, value);
            return;
        }

        result.startTime = time;
        result.counter[key] = value;
        PluginLog.Debug($"[SetValue] instance: {instance}, key: {key}, value: {value}");
    }

    public void ClearTracker()
    {
        _tracker.Clear();
        Service.Counter.Overlay.IsOpen = false;
    }

    public string GetCurrentInstance()
    {
        try
        {
            var instanceNumber = Marshal.ReadByte(_instanceNumberAddress, 0x20);

            return Service.ClientState.LocalPlayer?.CurrentWorld.GameData?.Name + "@" + _terr.GetRow(Service.ClientState.TerritoryType)?.PlaceName.Value?.Name.ToDalamudString().TextValue +
                   "@" + instanceNumber;
        }
        catch (Exception e)
        {
            return e.ToString();
        }
    }

    public void ClearKey(string key)
    {
        if (_tracker.ContainsKey(key))
            _tracker.Remove(key);
    }

    public string FormatJsonString(string typeStr, string instance = "", string condition = "", int value = 1, bool failed = false)
    {
        var currentInstance = GetCurrentInstance();
        var msg = new Message
        {
            type = typeStr,
            user = Service.ClientState.LocalPlayer.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString,
            failed = failed,
        };

        if (typeStr != "changeArea")
        {
            msg.instance = instance;
            msg.time = !GetTracker().TryGetValue(currentInstance, out var currentTracker) ? DateTimeOffset.Now.ToUnixTimeSeconds() : currentTracker.startTime;
            msg.data = new Dictionary<string, int> { { condition, value } };
        }

        var json = JsonConvert.SerializeObject(msg);
        return json;
    }

    public void SetLastCounterMessage(SeString seString, string msg)
    {
        _lastCounterMessage = new Tuple<SeString, string>(seString, msg);
    }

    public Tuple<SeString, string> GetLastCounterMessage()
    {
        return _lastCounterMessage;
    }

    private void hk_ActorControlSelf(uint entityId, int type, uint buffID, uint direct, uint damage, uint sourceId,
        uint arg4, uint arg5, ulong targetId, byte a10)
    {
        _actorControlSelfHook.Original(entityId, type, buffID, direct, damage, sourceId, arg4, arg5, targetId, a10);
        // 死亡事件
        if (type != 6)
            return;

        // PluginLog.Debug($"{entityId:X}:{type}:{buffID:X}:{direct:X}:{damage}:{sourceId:X}:{arg4}:{arg5}:{targetId:X}:{a10}:{Service.ClientState.LocalPlayer.ObjectId:X}");

        var target = Service.ObjectTable.SearchById(entityId);
        var sourceTarget = Service.ObjectTable.SearchById(direct);
        if (target == null)
        {
            PluginLog.Error($"Cannot found target by id {entityId:X}");
            return;
        }

        if (sourceTarget == null)
        {
            PluginLog.Error($"Cannot found source target by id {direct:X}");
            return;
        }

        PluginLog.Information($"{target.Name} got killed by {sourceTarget.Name}");

        Process(target, sourceTarget, Service.ClientState.TerritoryType);
    }

    private void Process(GameObject target, GameObject source, ushort territory)
    {
        if (!_conditionName.ContainsKey(territory))
            return;

        var targetName = target.Name;

        if (!_conditionName.TryGetValue(territory, out var name))
        {
            PluginLog.Error($"Cannot get condition name with territory id \"{territory}\"");
            return;
        }

        if (!name.Contains(targetName.ToString()))
            return;

        var key = GetCurrentInstance();

        var sourceOwner = source.OwnerId;
        if (!Service.Configuration._trackRangeMode &&
            (Service.Configuration._trackRangeMode || (sourceOwner != Service.ClientState.LocalPlayer.ObjectId && source.ObjectId != Service.ClientState.LocalPlayer.ObjectId))) return;

        AddToTracker(key, targetName.ToString());
    }

    private void AddToTracker(string key, string targetName)
    {
        // TODO: 简洁这部分的代码
        if (!_tracker.ContainsKey(key))
        {
            _tracker.Add(key, new Tracker
                {
                    counter = new Dictionary<string, int>
                    {
                        { targetName, 1 }
                    },
                    lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    startTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                }
            );
            goto Post;
        }

        if (!_tracker.TryGetValue(key, out var value))
        {
            PluginLog.Error($"Cannot get value by key {key}");
            return;
        }

        if (!value.counter.ContainsKey(targetName))
            value.counter.Add(targetName, 1);
        else
            value.counter[targetName]++;

        value.lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        Post:
        PluginLog.Debug($"+1 to key \"{key}\" [{targetName}]");
        Overlay.IsOpen = Service.Configuration._trackKillCount;

        Socket.SendMessage(FormatJsonString("addData", key, targetName));
    }

    public class Tracker
    {
        public Dictionary<string, int> counter;
        public long lastUpdateTime;
        public long startTime;
    }

    public class CounterOverlay : Window
    {
        private const ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;

        public CounterOverlay() : base("农怪计数##RankSSpawnHelper")
        {
            Flags = _windowFlags;
        }

        private static ImGuiWindowFlags BuildWindowFlags(ImGuiWindowFlags var)
        {
            if (Service.Configuration._trackerWindowNoBackground)
                var |= ImGuiWindowFlags.NoBackground;
            if (Service.Configuration._trackerWindowNoTitle)
                var |= ImGuiWindowFlags.NoTitleBar;
            return var;
        }

        public override void PreDraw()
        {
            Flags = BuildWindowFlags(_windowFlags);
        }

        public override void Draw()
        {
            var tracker = Service.Counter.GetTracker();

            if (!Service.Configuration._trackerShowCurrentInstance)
            {
                if (Fonts.AreFontsBuilt())
                {
                    ImGui.PushFont(Fonts.Yahei24);
                    ImGui.SetWindowFontScale(0.8f);
                }

                foreach (var (k, v) in tracker)
                {
                    var splitInLoop = k.Split('@');

                    ImGui.Text($"{splitInLoop[0]} - {splitInLoop[1]}" + (splitInLoop[2] == "0" ? string.Empty : $" - {splitInLoop[2]}线"));
                    var timeInLoop = DateTimeOffset.FromUnixTimeSeconds(v.startTime).LocalDateTime;
                    ImGui.Text($"\t开始时间: {timeInLoop.Month}-{timeInLoop.Day}@{timeInLoop.ToShortTimeString()}");
                    foreach (var (subK, subV) in v.counter) ImGui.Text($"\t{subK} - {subV}");
                }


                if (!Fonts.AreFontsBuilt()) return;

                ImGui.PopFont();
                ImGui.SetWindowFontScale(1.0f);

                return;
            }

            var mainKey = Service.Counter.GetCurrentInstance();

            if (!tracker.TryGetValue(mainKey, out var value))
            {
                IsOpen = false;
                return;
            }

            var split = mainKey.Split('@');

            if (Fonts.AreFontsBuilt())
            {
                ImGui.PushFont(Fonts.Yahei24);
                ImGui.SetWindowFontScale(0.8f);
            }

            ImGui.Text($"{split[0]} - {split[1]}" + (split[2] == "0" ? string.Empty : $" - {split[2]}线"));
            var time = DateTimeOffset.FromUnixTimeSeconds(value.startTime).LocalDateTime;
            ImGui.Text($"\t开始时间: {time.Month}-{time.Day}@{time.ToShortTimeString()}");

            foreach (var (subKey, subValue) in value.counter) ImGui.Text($"\t{subKey} - {subValue}");

            if (!Fonts.AreFontsBuilt()) return;

            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);
        }
    }

    private delegate void ActorControlSelfDelegate(uint entityId, int id, uint arg0, uint arg1, uint arg2,
        uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
}