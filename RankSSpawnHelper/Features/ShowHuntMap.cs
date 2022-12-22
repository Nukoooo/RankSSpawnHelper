using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Features
{
    internal class ShowHuntMap : IDisposable
    {
        private readonly Dictionary<uint, uint> _monsterTerritory = new()
                                                                    {
                                                                        { 956, 10617 }, // 布弗鲁
                                                                        { 815, 8900 }, // 多智兽
                                                                        { 958, 10619 }, // 阿姆斯特朗
                                                                        { 816, 8653 }, // 阿格拉俄珀
                                                                        { 960, 10622 }, // 狭缝
                                                                        { 614, 5985 }, // 伽马
                                                                        { 397, 4374 }, // 凯撒贝希摩斯
                                                                        { 622, 5986 }, // 兀鲁忽乃朝鲁
                                                                        { 139, 2966 }, // 南迪
                                                                        { 180, 2967 } // 牛头黑神
                                                                    };

        private readonly ExcelSheet<TerritoryType> _territoryType;
        private bool _shouldRequest = true;

        public ShowHuntMap()
        {
            _territoryType = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();

            DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
            DalamudApi.ChatGui.ChatMessage       += ChatGui_OnChatMessage;

            /*Task.Run(async () =>
                     {
                         await Task.Delay(1000);
                         foreach (var (k, _) in _monsterTerritory)
                         {
                             var territory = _territoryType.GetRow(k);
                             var mapRow    = territory.Map;
                             if (mapRow == null) 
                                 continue;
                             PluginLog.Debug($"Adding territory {k} | mapId: {territory.Map.Row}");

                             if (mapRow.Value == null)
                             {
                                 PluginLog.Debug($"mapRow.Value null");
                                 continue;
                             }

                             Plugin.Managers.Data.MapTexture.AddMapTexture(mapRow.Value);
                         }
                     });*/

        }

        public void Dispose()
        {
            DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
            DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
        }

        public bool CanShowHuntMap(uint territroy)
        {
            return _monsterTerritory.ContainsKey(territroy);
        }

        public MapTextureInfo? GeTexture(uint territory)
        {
            var map = _territoryType.GetRow(territory).Map;
            return Plugin.Managers.Data.MapTexture.GetTexture(map.Row);
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
                         if (huntMaps == null || huntMaps.spawnPoints.Count == 0)
                         {
                             return;
                         }

                         var payloads = new List<Payload>
                                        {
                                            new TextPayload($"{currentInstance} 的当前可触发点位:")
                                        };

                         if (huntMaps.spawnPoints.Count > 7)
                         {
                             payloads.Add(new TextPayload($"\n因为点位超过7个所以不显示. 当前数量: {huntMaps.spawnPoints.Count}."));
                             Plugin.Print(payloads);
                             return;
                         }

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
                             payloads.Add(new TextPayload($"{spawnPoint.key.Replace("SpawnPoint", "")} ({spawnPoint.x:0.00}, {spawnPoint.y:0.00})"));
                             payloads.Add(RawPayload.LinkTerminator);
                         }

                         Plugin.Print(payloads);
                     });
        }
    }
}