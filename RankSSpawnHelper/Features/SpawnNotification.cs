using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Features
{
    internal class SpawnNotification : IDisposable
    {
        private readonly Dictionary<string, HuntStatus> _huntStatus = new();

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

        private bool _shouldNotNotify;

        public SpawnNotification()
        {
            DalamudApi.ChatGui.ChatMessage       += ChatGui_OnChatMessage;
            DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
        }

        public void Dispose()
        {
            DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
            DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
        }

        private void ChatGui_OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
        {
            if (type != XivChatType.SystemMessage)
                return;

            if (message.TextValue != "感觉到了强大的恶名精英的气息……")
                return;

            _shouldNotNotify = true;

            var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();
            _huntStatus.Remove(currentInstance);
        }

        private void Condition_OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.BetweenAreas51 || value)
                return;

            Task.Run(async () =>
                     {
                         var e = DalamudApi.ClientState.TerritoryType;

                         if (!_monsterIdMap.ContainsKey(e))
                         {
                             return;
                         }

                         var currentInstance = Plugin.Managers.Data.Player.GetCurrentInstance();
                         var split           = currentInstance.Split('@');
                         var monsterName     = Plugin.Managers.Data.Monster.GetMonsterNameById(_monsterIdMap[e]);

                         if (_shouldNotNotify)
                         {
                             _shouldNotNotify = false;
                             return;
                         }

                         if (!_huntStatus.TryGetValue(currentInstance, out var result))
                         {
                             result = await Plugin.Managers.Data.Monster.FetchHuntStatus(split[0], monsterName, int.Parse(split[2]));
                         }

                         result ??= await Plugin.Managers.Data.Monster.FetchHuntStatus(split[0], monsterName, int.Parse(split[2]));

                         _huntStatus.TryAdd(currentInstance, result);

                         var payloads = new List<Payload>
                                        {
                                            new UIForegroundPayload(1),
                                            new TextPayload($"{currentInstance} - {monsterName}:")
                                        };

                         var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.expectMinTime;
                         if (isSpawnable)
                         {
                             payloads.Add(new TextPayload("\n当前可触发概率: "));
                             payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                             payloads.Add(new
                                              TextPayload($"{100 * ((DateTimeOffset.Now.ToUnixTimeSeconds() - result.expectMinTime) / (double)(result.expectMaxTime - result.expectMinTime)):F1}%"));
                             payloads.Add(new UIForegroundPayload(0));
                         }
                         else
                         {
                             payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                             payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                             var minTime = DateTimeOffset.FromUnixTimeSeconds(result.expectMinTime);
                             var delta   = (minTime - DateTimeOffset.Now).TotalMinutes;

                             payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
                             payloads.Add(new UIForegroundPayload(0));
                         }

                         payloads.Add(new UIForegroundPayload(0));

                         DalamudApi.ChatGui.Print(new SeString(payloads));
                     });
        }
    }
}