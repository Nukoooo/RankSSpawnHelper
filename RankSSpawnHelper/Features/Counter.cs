using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;

// ReSharper disable InconsistentNaming

namespace RankSSpawnHelper.Features
{
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

        private readonly CancellationTokenSource _eventLoopTokenSource = new();

        private readonly Dictionary<string, HuntStatus> _huntStatus = new();

        private readonly IntPtr _instanceNumberAddress;

        private readonly Dictionary<string, Tracker> _localTracker = new();

        private readonly Dictionary<ushort, uint> _monsterIdMap = new()
                                                                  {
                                                                      { 959, 10620 },
                                                                      { 957, 10618 },
                                                                      { 814, 8910 },
                                                                      { 817, 8890 },
                                                                      { 621, 5989 },
                                                                      { 613, 5984 },
                                                                      { 612, 5987 },
                                                                      { 402, 4380 },
                                                                      { 400, 4377 },
                                                                      { 147, 2961 }
                                                                  };

        private readonly Dictionary<string, Tracker> _networkedTracker = new();

        private readonly ExcelSheet<TerritoryType> _terr;

        private Tuple<SeString, string> _lastCounterMessage;

        public Counter()
        {
            _terr = Service.DataManager.GetExcelSheet<TerritoryType>();

            _instanceNumberAddress = Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BD");

            _actorControlSelfHook = Hook<ActorControlSelfDelegate>.FromAddress(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), hk_ActorControlSelf);
            _actorControlSelfHook.Enable();
            Task.Factory.StartNew(RemoveDeadTracker, TaskCreationOptions.LongRunning);

            Service.ChatGui.ChatMessage += OnChatMessage;
            Service.Condition.ConditionChange += OnConditionChange;
        }

        public void Dispose()
        {
            _actorControlSelfHook.Dispose();
            _eventLoopTokenSource.Dispose();
            Service.SocketManager.Dispose();
            Service.ChatGui.ChatMessage -= OnChatMessage;
            Service.Condition.ConditionChange -= OnConditionChange;
            GC.SuppressFinalize(this);
        }

        private async void RemoveDeadTracker()
        {
            var token = _eventLoopTokenSource.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_networkedTracker.Count == 0 || _localTracker.Count == 0)
                    {
                        await Task.Delay(5000, token);

                        continue;
                    }

                    await Task.Delay(5000, token);

                    foreach (var (k, v) in _networkedTracker)
                    {
                        var delta = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(v.lastUpdateTime);
                        if (delta.TotalMinutes <= Service.Configuration._trackerClearThreshold)
                        {
                            continue;
                        }

                        _networkedTracker.Remove(k);
                        if (_localTracker.ContainsKey(k))
                        {
                            _localTracker.Remove(k);
                        }
                    }
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
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            // 2115 = 采集的消息类型, SystemMessage = 舍弃物品的消息类型
            if (type != (XivChatType)2115 && type != XivChatType.SystemMessage)
            {
                return;
            }

            var territory = Service.ClientState.TerritoryType;
            if (!_conditionName.TryGetValue(territory, out var targetName))
            {
                return;
            }

            if (message.TextValue == "感觉到了强大的恶名精英的气息……")
            {
                _huntStatus.Remove(GetCurrentInstance());

                // 如果没tracker就不发
                if (!_networkedTracker.ContainsKey(GetCurrentInstance()))
                {
                    return;
                }

                var msg = FormatJsonString("ggnore", GetCurrentInstance());
                Service.SocketManager.SendMessage(msg);
                return;
            }

            var condition = targetName == ".*" ? "舍弃了" : "获得了";

            var reg = Regex.Match(message.ToString(), $"{condition}“({targetName})”");
            if (!reg.Success)
            {
                return;
            }

            targetName = territory switch
                         {
                             // 云海的刚哥要各挖50个, 所以这里分开来
                             400 => reg.Groups[1].ToString(),
                             // 因为正则所以得这样子搞..
                             621 => "扔垃圾",
                             _ => targetName
                         };

            var key = GetCurrentInstance();
            AddToTracker(key, targetName);
        }

        private void OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.BetweenAreas51 || value)
                return;

            Task.Run(async () =>
            {
                var e = Service.ClientState.TerritoryType;

                if (!_monsterIdMap.ContainsKey(e))
                {
                    return;
                }

                var instanceNumber = Marshal.ReadByte(_instanceNumberAddress, 0x20);
                var currentServer = Service.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                var monsterName = Service.MonsterManager.GetMonsterNameById(_monsterIdMap[e]);

                if (!_huntStatus.TryGetValue(GetCurrentInstance(), out var result))
                {
                    result = await Service.MonsterManager.FetchStatus(currentServer, monsterName, instanceNumber);
                }

                if (result == null)
                {
                    return;
                }

                _huntStatus.TryAdd(GetCurrentInstance(), result);

                var payloads = new List<Payload>
                                        {
                                            new UIForegroundPayload(1),
                                            new TextPayload($"{GetCurrentInstance()} - {monsterName}:")
                                        };

                var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.expectMinTime;
                if (isSpawnable)
                {
                    payloads.Add(new TextPayload("\n当前可触发概率: "));
                    payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));
                    payloads.Add(new
                                     TextPayload($"{100 * ((DateTimeOffset.Now.ToUnixTimeSeconds() - result.expectMinTime) / (double)(result.expectMaxTime - result.expectMinTime)):F1}%"));
                    payloads.Add(new UIForegroundPayload(0));
                }
                else
                {
                    payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                    payloads.Add(new UIForegroundPayload((ushort)Service.Configuration._highlightColor));
                    var minTime = DateTimeOffset.FromUnixTimeSeconds(result.expectMinTime);
                    var delta = (minTime - DateTimeOffset.Now).TotalMinutes;

                    payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
                    payloads.Add(new UIForegroundPayload(0));
                }

                payloads.Add(new UIForegroundPayload(0));

                Service.ChatGui.Print(new SeString(payloads));
            });

            if (!Service.Configuration._trackKillCount || !Service.Configuration._trackerShowCurrentInstance)
                return;

            Service.SocketManager.SendMessage(FormatJsonString("ChangeArea"));
            Service.CounterOverlay.IsOpen = _networkedTracker.TryGetValue(GetCurrentInstance(), out _) || _localTracker.TryGetValue(GetCurrentInstance(), out _);
        }

        public Dictionary<string, Tracker> GetNetworkedTracker()
        {
            return _networkedTracker;
        }

        public Dictionary<string, Tracker> GetLocalTracker()
        {
            return _localTracker;
        }

        public void SetValue(string instance, string key, int value, long time, uint territoryId)
        {
            if (!_networkedTracker.ContainsKey(instance))
            {
                _networkedTracker.Add(instance, new Tracker
                                                {
                                                    startTime = time,
                                                    lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                                    counter = new Dictionary<string, int>
                                                              {
                                                                  { key, value }
                                                              },
                                                    territoryId = territoryId
                                                });
                PluginLog.Debug($"[SetValue] instance: {instance}, key: {key}, value: {value}");

                return;
            }

            if (!_networkedTracker.TryGetValue(instance, out var result))
            {
                return;
            }

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
            _networkedTracker.Clear();
            Service.CounterOverlay.IsOpen = false;
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

        public void ClearKey(string key, bool local = false)
        {
            switch (local)
            {
                case false when _networkedTracker.ContainsKey(key):
                    _networkedTracker.Remove(key);
                    break;
                case true when _localTracker.ContainsKey(key):
                    _localTracker.Remove(key);
                    break;
            }
        }

        public string FormatJsonString(string typeStr, string instance = "", string condition = "", int value = 1, bool failed = false)
        {
            var currentInstance = GetCurrentInstance();
            var msg = new NetMessage
                      {
                          Type = typeStr,
                          User = Service.ClientState.LocalPlayer.Name.TextValue + "@" + Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString,
                          Failed = failed,
                          TerritoryId = Service.ClientState.TerritoryType
                      };

            if (typeStr != "ChangeArea")
            {
                msg.Instance = instance;
                msg.Time = !GetNetworkedTracker().TryGetValue(currentInstance, out var currentTracker) ? DateTimeOffset.Now.ToUnixTimeSeconds() : currentTracker.startTime;
                msg.Data = new Dictionary<string, int> { { condition, value } };
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

        private void hk_ActorControlSelf(uint entityId, int type, uint buffID, uint direct, uint damage, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            _actorControlSelfHook.Original(entityId, type, buffID, direct, damage, sourceId, arg4, arg5, targetId, a10);
            // 死亡事件
            if (type != 6)
            {
                return;
            }

            var target = Service.ObjectTable.SearchById(entityId);
            var sourceTarget = Service.ObjectTable.SearchById(direct);
            if (target == null)
            {
                PluginLog.Error($"Cannot found target by id 0x{entityId:X}");
                return;
            }

            if (sourceTarget == null)
            {
                PluginLog.Error($"Cannot found source target by id 0x{direct:X}");
                return;
            }

            PluginLog.Information($"{target.Name} got killed by {sourceTarget.Name}");

            Process(target, sourceTarget, Service.ClientState.TerritoryType);
        }

        private void Process(GameObject target, GameObject source, ushort territory)
        {
            if (!_conditionName.ContainsKey(territory))
            {
                return;
            }

            var targetName = target.Name;

            if (!_conditionName.TryGetValue(territory, out var name))
            {
                PluginLog.Error($"Cannot get condition name with territory id \"{territory}\"");
                return;
            }

            if (!name.Contains(targetName.ToString()))
            {
                return;
            }

            var key = GetCurrentInstance();

            var sourceOwner = source.OwnerId;
            if (!Service.Configuration._trackRangeMode &&
                (Service.Configuration._trackRangeMode || (sourceOwner != Service.ClientState.LocalPlayer.ObjectId && source.ObjectId != Service.ClientState.LocalPlayer.ObjectId)))
            {
                return;
            }

            AddToTracker(key, targetName.ToString());
        }

        private void AddToTracker(string key, string targetName)
        {
            if (!_localTracker.ContainsKey(key))
            {
                var tracker = new Tracker
                              {
                                  counter = new Dictionary<string, int>
                                            {
                                                { targetName, 1 }
                                            },
                                  lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                  startTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                  territoryId = Service.ClientState.TerritoryType
                              };

                _localTracker.Add(key, tracker);
                goto Post;
            }

            if (!_localTracker.TryGetValue(key, out var value))
            {
                PluginLog.Error($"Cannot get value by key {key}");
                return;
            }

            if (!value.counter.ContainsKey(targetName))
                value.counter.Add(targetName, 1);
            else
                value.counter[targetName]++;

            value.lastUpdateTime = value.lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        Post:
            PluginLog.Debug($"+1 to key \"{key}\" [{targetName}]");
            Service.CounterOverlay.IsOpen = Service.Configuration._trackKillCount;

            Service.SocketManager.SendMessage(FormatJsonString("AddData", key, targetName));
        }

        private delegate void ActorControlSelfDelegate(uint entityId, int id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
    }
}