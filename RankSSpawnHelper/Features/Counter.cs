using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Features
{
    internal class Counter : IDisposable
    {
        private readonly Dictionary<ushort, string> _conditionName = new()
                                                                     {
                                                                         { 961, "厄尔庇斯之鸟蛋" },
                                                                         { 959, "思考之物,彷徨之物,叹息之物" }, // 叹息海
                                                                         { 957, "毕舍遮,金刚尾,阿输陀花" }, // 萨维奈岛
                                                                         { 814, "矮人棉" },
                                                                         { 813, "秧鸡胸脯肉" },
                                                                         { 817, "破裂的隆卡器皿,破裂的隆卡石蒺藜,破裂的隆卡人偶" }, // 拉凯提卡大森林
                                                                         { 621, ".*" }, // 湖区
                                                                         { 613, "无壳观梦螺,观梦螺" }, // 红玉海
                                                                         { 612, "狄亚卡,莱西" }, // 边区
                                                                         { 402, "美拉西迪亚薇薇尔飞龙,小海德拉,亚拉戈奇美拉" }, // 魔大陆
                                                                         { 400, "星极花|皇金矿" }, // 翻云雾海
                                                                         { 147, "土元精" } // 北萨
                                                                     };

        private readonly CancellationTokenSource _eventLoopTokenSource = new();

        private readonly Dictionary<string, Tracker> _localTracker = new();
        private readonly Dictionary<string, Tracker> _networkedTracker = new();
        private readonly List<string> _ssList = new();

        public Counter()
        {
            SignatureHelper.Initialise(this);

            var bNpcName = DalamudApi.DataManager.GetExcelSheet<BNpcName>();

            _ssList.Add(bNpcName.GetRow(8915).Singular.RawString);
            _ssList.Add(bNpcName.GetRow(10615).Singular.RawString);

            ActorControlSelf.Enable();
            Task.Factory.StartNew(RemoveInactiveTracker, TaskCreationOptions.LongRunning);

            DalamudApi.ChatGui.ChatMessage       += ChatGui_ChatMessage;
            DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
        }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(Detour_ActorControlSelf))]
        private Hook<ActorControlSelfDelegate> ActorControlSelf { get; init; } = null!;

        public void Dispose()
        {
            ActorControlSelf.Dispose();
            _eventLoopTokenSource.Dispose();
            DalamudApi.ChatGui.ChatMessage       -= ChatGui_ChatMessage;
            DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
            GC.SuppressFinalize(this);
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
                _networkedTracker.Add(instance, new Tracker
                                                {
                                                    startTime      = time,
                                                    lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                                    counter = new Dictionary<string, int>
                                                              {
                                                                  { condition, value }
                                                              },
                                                    territoryId = territoryId
                                                });
                PluginLog.Debug($"[SetValue] instance: {instance}, condition: {condition}, value: {value}");
                Plugin.Windows.CounterWindow.IsOpen = true;
                return;
            }

            if (!_networkedTracker.TryGetValue(instance, out var result))
            {
                return;
            }

            if (!result.counter.ContainsKey(condition))
            {
                result.counter.Add(condition, value);
                return;
            }

            result.counter[condition]           = value;
            Plugin.Windows.CounterWindow.IsOpen = true;
            result.lastUpdateTime               = DateTimeOffset.Now.ToUnixTimeSeconds();
            PluginLog.Debug($"[SetValue] instance: {instance}, key: {condition}, value: {value}");
        }

        private void Condition_OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.BetweenAreas51 || value)
            {
                return;
            }

            if (!Plugin.Configuration.TrackKillCount)
            {
                return;
            }

            var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

            Plugin.Managers.Socket.SendMessage(new NetMessage
                                               {
                                                   Type        = "ChangeArea",
                                                   Instance    = currentInstance,
                                                   TerritoryId = DalamudApi.ClientState.TerritoryType
                                               });

            if (Plugin.Configuration.TrackerShowCurrentInstance && !_localTracker.ContainsKey(currentInstance))
            {
                Plugin.Windows.CounterWindow.IsOpen = false;
            }
        }

        private void ChatGui_ChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            // 2115 = 采集的消息类型, SystemMessage = 舍弃物品的消息类型
            if (type != (XivChatType)2115 && type != XivChatType.SystemMessage)
            {
                return;
            }

            var territory = DalamudApi.ClientState.TerritoryType;
            if (!_conditionName.TryGetValue(territory, out var targetName) && territory != 960)
            {
                return;
            }

            string currentInstance;
            if (message.TextValue == "感觉到了强大的恶名精英的气息……")
            {
                // _huntStatus.Remove(currentInstance);

                // Find Rank SS
                if (DalamudApi.ObjectTable.Any(i => i.IsValid() && _ssList.Contains(i.Name.TextValue)))
                    return;

                if ((from obj in DalamudApi.ObjectTable
                     where obj.IsValid()
                     where !obj.IsDead
                     where obj.ObjectKind == ObjectKind.BattleNpc
                     select obj as BattleNpc
                     into npc
                     where npc.BattleNpcKind == BattleNpcSubKind.Enemy
                     select npc).Any(npc => _ssList.Contains(npc.Name.TextValue)))
                {
                    return;
                }

                currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

                if (territory == 960)
                {
                    Plugin.Managers.Socket.SendMessage(new NetMessage
                                                       {
                                                           Type        = "WeeEa",
                                                           Instance    = currentInstance,
                                                           TerritoryId = territory,
                                                           Failed      = false
                                                       });
                    return;
                }

                // 如果没tracker就不发
                if (!_networkedTracker.ContainsKey(currentInstance) && territory != 961 && territory != 813)
                {
                    return;
                }

                Plugin.Managers.Socket.SendMessage(new NetMessage
                                                   {
                                                       Type        = "ggnore",
                                                       Instance    = currentInstance,
                                                       TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                       Time = !GetLocalTrackers().TryGetValue(currentInstance, out var currentTracker)
                                                                  ? DateTimeOffset.Now.ToUnixTimeSeconds()
                                                                  : currentTracker.startTime
                                                   });

                return;
            }


            var msg       = message.TextValue;
            var condition = targetName is ".*" or "秧鸡胸脯肉" or "厄尔庇斯之鸟蛋" ? "舍弃了" : "获得了";

            var reg = Regex.Match(msg, $"{condition}“({targetName})”");
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
                             _   => targetName
                         };

            if (territory == 961)
            {
                // check if the amount is 5 or not
                if (!msg[..^1].EndsWith("5"))
                    return;
            }

            currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
            AddToTracker(currentInstance, targetName);
        }

        private void Detour_ActorControlSelf(uint entityId, int type, uint buffId, uint direct, uint damage, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            ActorControlSelf.Original(entityId, type, buffId, direct, damage, sourceId, arg4, arg5, targetId, a10);
            if (!Plugin.Configuration.TrackKillCount)
                return;

            // 死亡事件
            if (type != 6)
            {
                return;
            }

            var target       = DalamudApi.ObjectTable.SearchById(entityId);
            var sourceTarget = DalamudApi.ObjectTable.SearchById(direct);
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

            Process(target, sourceTarget, DalamudApi.ClientState.TerritoryType);
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

            var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

            var sourceOwner = source.OwnerId;
            if (!Plugin.Configuration.TrackRangeMode &&
                (Plugin.Configuration.TrackRangeMode || (sourceOwner != DalamudApi.ClientState.LocalPlayer.ObjectId && source.ObjectId != DalamudApi.ClientState.LocalPlayer.ObjectId)))
                return;

            AddToTracker(currentInstance, targetName.ToString());
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
                                  startTime      = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                  territoryId    = DalamudApi.ClientState.TerritoryType
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
            {
                value.counter.Add(targetName, 1);
            }
            else
            {
                value.counter[targetName]++;
            }

            value.lastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        Post:
            PluginLog.Debug($"+1 to key \"{key}\" [{targetName}]");

            Plugin.Windows.CounterWindow.IsOpen = true;

            Plugin.Managers.Socket.SendMessage(new NetMessage
                                               {
                                                   Type = "AddData",
                                                   Instance = key,
                                                   TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                   Time = !GetLocalTrackers().TryGetValue(key, out var currentTracker) ? DateTimeOffset.Now.ToUnixTimeSeconds() : currentTracker.startTime,
                                                   Data = new Dictionary<string, int>
                                                          { { targetName, 1 } }
                                               });
        }

        private async void RemoveInactiveTracker()
        {
            var token = _eventLoopTokenSource.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_localTracker.Count == 0)
                    {
                        await Task.Delay(5000, token);

                        continue;
                    }

                    await Task.Delay(5000, token);

                    foreach (var (k, v) in _localTracker)
                    {
                        var delta = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(v.lastUpdateTime);
                        if (delta.TotalMinutes <= Plugin.Configuration.TrackerClearThreshold)
                        {
                            continue;
                        }

                        PluginLog.Debug($"Removing track {v}. delta: {delta}");

                        _networkedTracker.Remove(k);
                        _localTracker.Remove(k);
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

        private delegate void ActorControlSelfDelegate(uint entityId, int id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
    }
}