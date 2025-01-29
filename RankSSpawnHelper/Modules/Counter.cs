using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Windows;
using IDataManager = RankSSpawnHelper.Managers.IDataManager;

namespace RankSSpawnHelper.Modules;

internal interface ICounter
{
    Dictionary<string, Counter.Tracker> GetLocalTrackers();

    Dictionary<string, Counter.Tracker> GetNetworkedTrackers();

    void RemoveInstance(string instance);

    void UpdateNetworkedTracker(string instance, string condition, int value, long time, uint territoryId);

    (List<string> nameList, int nonWeeEaCount) GetWeeEaCounter();

    void UpdateTrackerData(List<ConnectionManager.TrackerData> data);

    bool CurrentInstanceHasSRank();
}

internal partial class Counter : IUiModule, ICounter
{
    private readonly Dictionary<ushort, Dictionary<string, uint>> _trackerConditions = new ()
    {
        { 1191, new () }, // 遗产之地
        { 1189, new () }, // 树海
        { 961, new () },  // 鸟蛋
        { 959, new () },  // 叹息海
        { 957, new () },  // 萨维奈岛
        { 814, new () },  // 棉花
        { 813, new () },  // Lakeland
        { 817, new () },  // 拉凯提卡大森林
        { 621, new () },  // 湖区
        { 613, new () },  // 红玉海
        { 612, new () },  // 边区
        { 402, new () },  // 魔大陆
        { 400, new () },  // 翻云雾海
        { 147, new () },  // 北萨
    };

    private readonly Configuration      _configuration;
    private readonly IDataManager       _dataManager;
    private readonly IConnectionManager _connectionManager;
    private          Window?            _counterWindow;
    private readonly WindowSystem       _windowSystem;

    private readonly Dictionary<string, Tracker> _localTracker     = [];
    private readonly Dictionary<string, Tracker> _networkedTracker = [];

    private List<ConnectionManager.TrackerData> _trackerData = [];

    private DateTime _lastCleanerRunTime = DateTime.Now;

    private bool _hasSRank;

    public Counter(Configuration      configuration,
                   IDataManager       dataManager,
                   IConnectionManager connectionManager,
                   WindowSystem       windowSystem)
    {
        _configuration     = configuration;
        _dataManager       = dataManager;
        _connectionManager = connectionManager;
        _windowSystem      = windowSystem;
    }

    public bool Init()
    {
        DalamudApi.GameInterop.InitializeFromAttributes(this);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        InitializeData();
        stopwatch.Stop();

        DalamudApi.PluginLog.Info($"{stopwatch.Elapsed}");

        OnReceiveCreateNonPlayerBattleCharaPacket.Enable();
        ActorControl.Enable();
        SystemLogMessage.Enable();
        InventoryTransactionDiscard.Enable();

        DalamudApi.ChatGui.ChatMessage += ChatGui_OnChatMessage;
        DalamudApi.Framework.Update    += Framework_Update;

        DalamudApi.Condition.ConditionChange += Condition_ConditionChange;

        return true;
    }

    public void Shutdown()
    {
        OnReceiveCreateNonPlayerBattleCharaPacket.Dispose();
        ActorControl.Dispose();
        SystemLogMessage.Dispose();
        InventoryTransactionDiscard.Dispose();

        DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
        DalamudApi.Framework.Update          -= Framework_Update;
        DalamudApi.Condition.ConditionChange -= Condition_ConditionChange;
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _counterWindow = _windowSystem.Windows.FirstOrDefault(i => i.WindowName == CounterWindow.Name)
                         ?? throw new InvalidOperationException("Failed to get CounterWindow");
    }

    public string UiName => "计数";

    public void OnDrawUi()
    {
        DrawConfig();
    }

    public Dictionary<string, Tracker> GetLocalTrackers()
        => _localTracker;

    public Dictionary<string, Tracker> GetNetworkedTrackers()
        => _networkedTracker;

    public void RemoveInstance(string instance)
    {
        _localTracker.Remove(instance);
        _networkedTracker.Remove(instance);
    }

    public void UpdateNetworkedTracker(string instance, string condition, int value, long time, uint territoryId)
    {
        if (!_networkedTracker.ContainsKey(instance))
        {
            _networkedTracker.Add(instance,
                                  new ()
                                  {
                                      StartTime      = time,
                                      LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                      Counter = new ()
                                      {
                                          { condition, value },
                                      },
                                      TerritoryId = territoryId,
                                  });

            DalamudApi.PluginLog.Debug($"[SetValue] instance: {instance}, condition: {condition}, value: {value}");
            _counterWindow!.IsOpen = true;

            return;
        }

        if (!_networkedTracker.TryGetValue(instance, out var result))
        {
            return;
        }

        if (result.Counter.TryAdd(condition, value))
        {
            return;
        }

        result.Counter[condition] = value;
        _counterWindow!.IsOpen    = true;

        result.LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        DalamudApi.PluginLog.Debug($"[SetValue] instance: {instance}, key: {condition}, value: {value}");
    }

    public (List<string> nameList, int nonWeeEaCount) GetWeeEaCounter()
        => (_weeEaNameList, _nonWeeEaCount);

    public void UpdateTrackerData(List<ConnectionManager.TrackerData> data)
        => _trackerData = data;

    public bool CurrentInstanceHasSRank()
        => _hasSRank;

    private void InitializeData()
    {
        _trackerConditions[959]
            .Add(_dataManager.GetNpcName(10461), 10461); // xx之物

        _trackerConditions[959]
            .Add(_dataManager.GetNpcName(10462), 10462);

        _trackerConditions[959]
            .Add(_dataManager.GetNpcName(10463), 10463);

        _trackerConditions[957]
            .Add(_dataManager.GetNpcName(10697), 10697); // 毕舍遮

        _trackerConditions[957]
            .Add(_dataManager.GetNpcName(10698), 10698); // 金刚尾

        _trackerConditions[957]
            .Add(_dataManager.GetNpcName(10701), 10701); // 阿输陀花

        _trackerConditions[817]
            .Add(_dataManager.GetNpcName(8789), 8789); // 破裂的隆卡器皿

        _trackerConditions[817]
            .Add(_dataManager.GetNpcName(8598), 8598); // 破裂的隆卡人偶

        _trackerConditions[817]
            .Add(_dataManager.GetNpcName(8599), 8599); // 破裂的隆卡石蒺藜

        _trackerConditions[613]
            .Add(_dataManager.GetNpcName(5750), 5750); // 观梦螺

        _trackerConditions[613]
            .Add(_dataManager.GetNpcName(5751), 5751); // 无壳观梦螺

        _trackerConditions[612]
            .Add(_dataManager.GetNpcName(5685), 5685); // 狄亚卡

        _trackerConditions[612]
            .Add(_dataManager.GetNpcName(5671), 5671); // 莱西

        _trackerConditions[402]
            .Add(_dataManager.GetNpcName(3556), 3556); // 美拉西迪亚薇薇尔飞龙

        _trackerConditions[402]
            .Add(_dataManager.GetNpcName(3580), 3580); // 小海德拉

        _trackerConditions[402]
            .Add(_dataManager.GetNpcName(3540), 3540); // 亚拉戈奇美拉

        _trackerConditions[147]
            .Add(_dataManager.GetNpcName(113), 113); // 土元精

        _trackerConditions[814]
            .Add(_dataManager.GetItemName(27759), 27759); // 矮人棉

        _trackerConditions[400]
            .Add(_dataManager.GetItemName(12634), 12634); // 星极花

        _trackerConditions[400]
            .Add(_dataManager.GetItemName(12536), 12536); // 皇金矿

        // discard
        _trackerConditions[961]
            .Add(_dataManager.GetItemName(36256), 36256);

        _trackerConditions[813]
            .Add(_dataManager.GetItemName(27850), 27850);

        _trackerConditions[1191]
            .Add(_dataManager.GetItemName(44091), 44091);

        _trackerConditions[1189]
            .Add(_dataManager.GetItemName(7767), 7767);
    }

    private unsafe void Detour_OnReceiveCreateNonPlayerBattleCharaPacket(nint a1, nint packetData)
    {
        OnReceiveCreateNonPlayerBattleCharaPacket.Original(a1, packetData);

        if (packetData == nint.Zero)
        {
            return;
        }

        var baseName = *(uint*) (packetData + 0x44);
        DalamudApi.PluginLog.Debug($"baseName: {baseName}");

        if (!_dataManager.IsSRank(baseName))
        {
            return;
        }

#if DEBUG || DEBUG_CN
        Utils.Print("SRank spotted.");
        DalamudApi.PluginLog.Warning("SRank spotted.");
#endif

        _hasSRank = true;

        var territory = DalamudApi.ClientState.TerritoryType;

        if (territory == 960)
        {
            lock (_weeEaNameList)
            {
                _connectionManager.SendMessage(new ConnectionManager.AttemptMessage
                {
                    Type        = "WeeEa",
                    WorldId     = _dataManager.GetCurrentWorldId(),
                    InstanceId  = _dataManager.GetCurrentInstance(),
                    TerritoryId = territory,
                    Failed      = false,
                    Names       = _weeEaNameList,
                });
            }

            return;
        }

        if (!_trackerConditions.ContainsKey(territory))
        {
            return;
        }

        /*2959*/
        var currentInstance = _dataManager.FormatCurrentTerritory();

        if (!_networkedTracker.ContainsKey(currentInstance))
        {
            return;
        }

        _connectionManager.SendMessage(new ConnectionManager.AttemptMessage
        {
            Type        = "ggnore",
            WorldId     = _dataManager.GetCurrentWorldId(),
            InstanceId  = _dataManager.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            Failed      = false,
        });
    }

    [Signature("48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B DA 8B F9 E8 ?? ?? ?? ?? 3C ?? 75 ?? E8 ?? ?? ?? ?? 3C ?? 75 ?? 80 BB ?? ?? ?? ?? ?? 75 ?? 8B 05 ?? ?? ?? ?? 39 43 ?? 0F 85 ?? ?? ?? ?? 0F B6 53 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 53 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 54 24 ?? C7 44 24 ?? ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 90 48 8D 92 ?? ?? ?? ?? 0F 10 03 0F 10 4B ?? 48 8D 9B ?? ?? ?? ?? 0F 11 42 ?? 0F 10 43 ?? 0F 11 4A ?? 0F 10 4B ?? 0F 11 42 ?? 0F 10 43 ?? 0F 11 4A ?? 0F 10 4B ?? 0F 11 42 ?? 0F 10 43 ?? 0F 11 4A ?? 0F 10 4B ?? 0F 11 42 ?? 0F 11 4A ?? 48 83 E8 ?? 75 ?? 48 8B 03",
               DetourName = nameof(Detour_OnReceiveCreateNonPlayerBattleCharaPacket))]
    private Hook<OnReceiveCreateNonPlayerBattleCharaPacketDelegate> OnReceiveCreateNonPlayerBattleCharaPacket { get; init; }
        = null!;

    private delegate void OnReceiveCreateNonPlayerBattleCharaPacketDelegate(nint a1, nint packetData);

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51)
        {
            return;
        }

        if (value)
        {
            _hasSRank = false;
        }
        else if (DalamudApi.ClientState.TerritoryType == 960)
        {
            _counterWindow!.IsOpen = true;
        }
    }

    private void AddToTracker(string key, string targetName, uint targetId, bool isItem = false)
    {
        if (!_localTracker.ContainsKey(key))
        {
            var tracker = new Tracker
            {
                Counter = new ()
                {
                    { targetName, 1 },
                },
                LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                StartTime      = DateTimeOffset.Now.ToUnixTimeSeconds(),
                TerritoryId    = DalamudApi.ClientState.TerritoryType,
                TrackerOwner   = Utils.FormatLocalPlayerName(),
            };

            _localTracker.Add(key, tracker);

            goto Post;
        }

        if (!_localTracker.TryGetValue(key, out var value))
        {
            DalamudApi.PluginLog.Error($"Cannot get value by key {key}");

            return;
        }

        if (!value.Counter.TryAdd(targetName, 1))
        {
            value.Counter[targetName]++;
        }

        value.LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
    Post:
        DalamudApi.PluginLog.Debug($"+1 to key \"{key}\" [{targetName}]");
        _counterWindow!.IsOpen = true;

        _connectionManager.SendMessage(new ConnectionManager.CounterMessage
        {
            Type        = "AddData",
            WorldId     = _dataManager.GetCurrentWorldId(),
            InstanceId  = _dataManager.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            StartTime = !GetLocalTrackers()
                .TryGetValue(key, out var currentTracker)
                ? DateTimeOffset.Now.ToUnixTimeSeconds()
                : currentTracker.StartTime,
            Data = new ()
            {
                { targetId, 1 },
            },
            IsItem = isItem,
        });
    }

    private void Framework_Update(IFramework framework)
    {
        UpdateNameList();

        // check every 5 seconds
        if (DateTime.Now - _lastCleanerRunTime <= TimeSpan.FromSeconds(5))
        {
            return;
        }

        _lastCleanerRunTime = DateTime.Now;

        if (_localTracker.Count == 0)
        {
            return;
        }

        foreach (var (k, v) in _localTracker)
        {
            var delta = DateTime.Now - DateTime.UnixEpoch.AddSeconds(v.LastUpdateTime).ToLocalTime();

            if (delta.TotalMinutes <= _configuration.TrackerClearThreshold)
            {
                continue;
            }

            _networkedTracker.Remove(k);
            _localTracker.Remove(k);
        }
    }
}
