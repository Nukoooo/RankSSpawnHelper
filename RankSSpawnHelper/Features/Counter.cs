using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Features;

internal partial class Counter : IDisposable
{
    private readonly Dictionary<ushort, Dictionary<string, uint>> _conditionsMob = new()
    {
        { 961, new() },  // 鸟蛋
        { 959, new() },  // 叹息海
        { 957, new() },  // 萨维奈岛
        { 814, new() },  // 棉花
        { 813, new() },  // Lakeland
        { 817, new() },  // 拉凯提卡大森林
        { 621, new() },  // 湖区
        { 613, new() },  // 红玉海
        { 612, new() },  // 边区
        { 402, new() },  // 魔大陆
        { 400, new() },  // 翻云雾海
        { 147, new() },  // 北萨
        { 1191, new() }, // 遗产之地
        { 1189, new() }  // 树海
    };

    private readonly Dictionary<string, Tracker> _localTracker     = new();
    private readonly Dictionary<string, Tracker> _networkedTracker = new();

    private DateTime _lastCleanerRunTime = DateTime.Now;

    public Counter()
    {
        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);

        InitializeData();
        ActorControl.Enable();
        SystemLogMessage.Enable();
        InventoryTransactionDiscard.Enable();
        OnReceiveCreateNonPlayerBattleCharaPacket.Enable();
        ProcessOpenTreasure.Enable();
        ProcessActorControlSelf.Enable();
        /*ProcessInventoryActionAckPacketHook.Enable();*/
        // UseActionHook = Hook<UseActionDelegate>.FromFunctionPointerVariable((IntPtr)ActionManager.Addresses.UseAction.Value, Detour_UseAction);
        /*UseActionHook.Enable();*/

        DalamudApi.Framework.Update          += Framework_Update;
        DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
        DalamudApi.ChatGui.ChatMessage       += ChatGui_OnChatMessage;
    }

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 8B F2 49 8B D8 41 0F B6 50 ?? 48 8B F9 E8 ?? ?? ?? ?? 4C 8D 44 24 ?? C7 44 24 ?? ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 66 0F 1F 84 00 ?? ?? ?? ?? 4D 8D 80 ?? ?? ?? ?? 0F 10 03 0F 10 4B ?? 48 8D 9B ?? ?? ?? ?? 41 0F 11 40 ?? 0F 10 43 ?? 41 0F 11 48 ?? 0F 10 4B ?? 41 0F 11 40 ?? 0F 10 43 ?? 41 0F 11 48 ?? 0F 10 4B ?? 41 0F 11 40 ?? 0F 10 43 ?? 41 0F 11 48 ?? 0F 10 4B ?? 41 0F 11 40 ?? 41 0F 11 48 ?? 48 83 E8 ?? 75 ?? 48 8B 03",
               DetourName = nameof(Detour_OnReceiveCreateNonPlayerBattleCharaPacket))]
    private Hook<ProcessSpawnNpcDelegate> OnReceiveCreateNonPlayerBattleCharaPacket { get; init; } = null!;

    public void Dispose()
    {
        OnReceiveCreateNonPlayerBattleCharaPacket.Dispose();
        ActorControl.Dispose();
        SystemLogMessage.Dispose();
        InventoryTransactionDiscard.Dispose();
        ProcessOpenTreasure.Dispose();
        ProcessActorControlSelf.Dispose();
        /*UseActionHook.Dispose();*/
        /*ProcessInventoryActionAckPacketHook.Dispose();*/

        DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
        DalamudApi.Framework.Update          -= Framework_Update;
        DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
        GC.SuppressFinalize(this);
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
            return;

        foreach (var (k, v) in _localTracker)
        {
            var delta = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(v.lastUpdateTime);
            if (delta.TotalMinutes <= Plugin.Configuration.TrackerClearThreshold)
                continue;

            _networkedTracker.Remove(k);
            _localTracker.Remove(k);
        }
    }

    public Dictionary<string, Tracker> GetLocalTrackers()
    {
        return _localTracker;
    }

    public Dictionary<string, Tracker> GetNetworkedTrackers()
    {
        return _networkedTracker;
    }

    /*
     * Remove an instance local and networked tracker
     * if instance is an empty string then it will clear the trackers
     */
    public void RemoveInstance(string instance = "")
    {
        if (instance == string.Empty)
        {
            _localTracker.Clear();
            _networkedTracker.Clear();
            return;
        }

        _localTracker.Remove(instance);
        _networkedTracker.Remove(instance);
    }

    public void UpdateNetworkedTracker(string instance, string condition, int value, long time, uint territoryId)
    {
        if (!_networkedTracker.ContainsKey(instance))
        {
            _networkedTracker.Add(instance, new()
            {
                startTime      = time,
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                counter = new()
                {
                    { condition, value }
                },
                territoryId = territoryId
            });

            DalamudApi.PluginLog.Debug($"[SetValue] instance: {instance}, condition: {condition}, value: {value}");
            Plugin.Windows.CounterWindow.IsOpen = true;
            return;
        }

        if (!_networkedTracker.TryGetValue(instance, out var result))
            return;

        if (!result.counter.ContainsKey(condition))
        {
            result.counter.Add(condition, value);
            return;
        }

        result.counter[condition]           = value;
        Plugin.Windows.CounterWindow.IsOpen = true;
        result.lastUpdateTime               = DateTimeOffset.Now.ToUnixTimeSeconds();
        DalamudApi.PluginLog.Debug($"[SetValue] instance: {instance}, key: {condition}, value: {value}");
    }

    private void Condition_OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value)
            return;

        if (!Plugin.Configuration.TrackKillCount)
            return;

        var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

        Plugin.Managers.Socket.Main.SendMessage(new AttemptMessage
        {
            Type        = "ChangeArea",
            WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
            InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType
            // Instance    = currentInstance,
        });

        if (!Plugin.Configuration.TrackerShowCurrentInstance || _localTracker.ContainsKey(currentInstance))
            return;

        Plugin.Windows.CounterWindow.IsOpen = false;
    }

    private unsafe void Detour_OnReceiveCreateNonPlayerBattleCharaPacket(nint a1, uint a2, nint packetData)
    {
        OnReceiveCreateNonPlayerBattleCharaPacket.Original(a1, a2, packetData);
        if (packetData == nint.Zero)
            return;

        var baseName = *(uint*)(packetData + 0x44);
        DalamudApi.PluginLog.Debug($"baseName: {baseName}");

        if (!Plugin.Managers.Data.SRank.IsSRank(baseName))
            return;

        Plugin.Features.ShowHuntMap.DontRequest();

#if DEBUG || DEBUG_CN
        Plugin.Print("SRank spotted.");
        DalamudApi.PluginLog.Warning("SRank spotted.");
#endif

        var territory = DalamudApi.ClientState.TerritoryType;

        if (territory == 960)
        {
            lock (_weeEaNameList)
            {
                Plugin.Managers.Socket.Main.SendMessage(new AttemptMessage
                {
                    Type        = "WeeEa",
                    WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
                    InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
                    TerritoryId = territory,
                    Failed      = false,
                    Names       = _weeEaNameList
                });
            }

            return;
        }

        if (!_conditionsMob.ContainsKey(territory))
            return;

        /*2959*/
        var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
        if (!_networkedTracker.ContainsKey(currentInstance))
            return;

        Plugin.Managers.Socket.Main.SendMessage(new AttemptMessage
        {
            Type        = "ggnore",
            WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
            InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            Failed      = false
        });
    }

    private void AddToTracker(string key, string targetName, uint targetId, bool isItem = false)
    {
        if (!_localTracker.ContainsKey(key))
        {
            var tracker = new Tracker
            {
                counter = new()
                {
                    { targetName, 1 }
                },
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                startTime      = DateTimeOffset.Now.ToUnixTimeSeconds(),
                territoryId    = DalamudApi.ClientState.TerritoryType,
                trackerOwner   = Player.GetLocalPlayerName()
            };

            _localTracker.Add(key, tracker);
            goto Post;
        }

        if (!_localTracker.TryGetValue(key, out var value))
        {
            DalamudApi.PluginLog.Error($"Cannot get value by key {key}");
            return;
        }

        if (!value.counter.ContainsKey(targetName))
        {
            value.counter.Add(targetName, 1);
        }
        else
        {
            value.counter[targetName]++;
        }

        value.lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
    Post:
        DalamudApi.PluginLog.Debug($"+1 to key \"{key}\" [{targetName}]");

        Plugin.Windows.CounterWindow.IsOpen = true;

        Plugin.Managers.Socket.Main.SendMessage(new CounterMessage
        {
            Type        = "AddData",
            WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
            InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            StartTime = !GetLocalTrackers().TryGetValue(key, out var currentTracker)
                            ? DateTimeOffset.Now.ToUnixTimeSeconds()
                            : currentTracker.startTime,
            Data = new()
            {
                { targetId, 1 }
            },
            IsItem = isItem
        });
    }

    private void InitializeData()
    {
        var npcNames = DalamudApi.DataManager.GetExcelSheet<BNpcName>();
        var items    = DalamudApi.DataManager.GetExcelSheet<Item>();

        string GetNpcName(uint row)
        {
            var name = npcNames.GetRow(row).Singular.RawString.ToLower();
            return name;
        }

        string GetItemName(uint row)
        {
            var name = items.GetRow(row).Singular.RawString.ToLower();
            return name;
        }

        _conditionsMob[959].Add(GetNpcName(10461), 10461); // xx之物
        _conditionsMob[959].Add(GetNpcName(10462), 10462);
        _conditionsMob[959].Add(GetNpcName(10463), 10463);

        _conditionsMob[957].Add(GetNpcName(10697), 10697); // 毕舍遮
        _conditionsMob[957].Add(GetNpcName(10698), 10698); // 金刚尾
        _conditionsMob[957].Add(GetNpcName(10701), 10701); // 阿输陀花

        _conditionsMob[817].Add(GetNpcName(8789), 8789); // 破裂的隆卡器皿
        _conditionsMob[817].Add(GetNpcName(8598), 8598); // 破裂的隆卡人偶
        _conditionsMob[817].Add(GetNpcName(8599), 8599); // 破裂的隆卡石蒺藜

        _conditionsMob[613].Add(GetNpcName(5750), 5750); // 观梦螺
        _conditionsMob[613].Add(GetNpcName(5751), 5751); // 无壳观梦螺

        _conditionsMob[612].Add(GetNpcName(5685), 5685); // 狄亚卡
        _conditionsMob[612].Add(GetNpcName(5671), 5671); // 莱西

        _conditionsMob[402].Add(GetNpcName(3556), 3556); // 美拉西迪亚薇薇尔飞龙
        _conditionsMob[402].Add(GetNpcName(3580), 3580); // 小海德拉
        _conditionsMob[402].Add(GetNpcName(3540), 3540); // 亚拉戈奇美拉

        _conditionsMob[147].Add(GetNpcName(113), 113); // 土元精

        // gather
        _conditionsMob[814].Add(GetItemName(27759), 27759); // 矮人棉
        _conditionsMob[400].Add(GetItemName(12634), 12634); // 星极花
        _conditionsMob[400].Add(GetItemName(12536), 12536); // 皇金矿

        // discard
        _conditionsMob[961].Add(GetItemName(36256), 36256);
        _conditionsMob[813].Add(GetItemName(27850), 27850);

        _conditionsMob[1191].Add(GetItemName(44091), 44091);
        _conditionsMob[1189].Add(GetItemName(7767), 7767);
    }

    private delegate void ProcessSpawnNpcDelegate(nint a1, uint a2, nint packetData);
}