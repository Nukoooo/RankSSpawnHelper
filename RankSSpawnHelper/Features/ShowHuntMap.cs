using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace RankSSpawnHelper.Features
{
    internal class ShowHuntMap : IDisposable
    {
        private readonly Dictionary<uint, uint> _monsterTerritory = new()
                                                                    {
                                                                        { 956, 10617 },
                                                                        { 815, 8900 },
                                                                        { 958, 10619 },
                                                                        { 816, 8653 },
                                                                        { 960, 10622 },
                                                                        { 614, 5985 },
                                                                        { 397, 4373 }
                                                                    };

        private readonly ExcelSheet<TerritoryType> _territoryType;
        private bool _shouldRequest = true;

        public ShowHuntMap()
        {
            _territoryType = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();

            DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
            DalamudApi.ChatGui.ChatMessage       += ChatGui_OnChatMessage;
        }

        public void Dispose()
        {
            DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
            DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
        }

        private void ChatGui_OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
        {
            if (type is not XivChatType.SystemMessage || message.TextValue != "感觉到了强大的恶名精英的气息……")
                return;
            _shouldRequest = false;
        }

        private void Condition_OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.BetweenAreas51 || value)
                return;

            if (!Plugin.Configuration.AutoShowHuntMap)
                return;

            if (!_shouldRequest)
            {
                _shouldRequest = true;
                return;
            }

            var currentTerritory = DalamudApi.ClientState.TerritoryType;
            if (!_monsterTerritory.TryGetValue(currentTerritory, out var monsterId))
                return;

            Task.Run(async () =>
                     {
                         var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
                         var split           = currentInstance.Split('@');
                         if (!int.TryParse(split[2], out var instance))
                             return;

                         var huntMaps = await Plugin.Managers.Data.Monster.FetchHuntMap(split[0], Plugin.Managers.Data.Monster.GetMonsterNameById(monsterId), instance);
                         if (huntMaps.spawnPoints.Count == 0)
                             return;

                         var payloads = new List<Payload>
                                        {
                                            new TextPayload($"{currentInstance} 的当前可触发点位:")
                                        };

                         var mapId = _territoryType.GetRow(currentTerritory)!.Map.Row;

                         foreach (var spawnPoint in huntMaps.spawnPoints)
                         {
                             payloads.Add(new TextPayload("\n"));
                             payloads.Add(new UIForegroundPayload(0x0225));
                             payloads.Add(new UIGlowPayload(0x0226));
                             payloads.Add(new MapLinkPayload(currentTerritory, mapId, spawnPoint.x, spawnPoint.y));
                             payloads.Add(new UIForegroundPayload(500));
                             payloads.Add(new UIGlowPayload(501));
                             payloads.Add(new TextPayload($"{(char)SeIconChar.LinkMarker}"));
                             payloads.Add(new UIForegroundPayload(0));
                             payloads.Add(new UIGlowPayload(0));
                             payloads.Add(new TextPayload($"{spawnPoint.key} ({spawnPoint.x:0.00}, {spawnPoint.y:0.00})"));
                             payloads.Add(RawPayload.LinkTerminator);
                         }

                         Plugin.Print(payloads);
                     });
        }
    }
}