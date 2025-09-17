using System.Collections.Frozen;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Windows;

namespace RankSSpawnHelper.Modules;

internal class SpawnNotification : IUiModule
{
    private readonly Configuration   _configuration;
    private readonly IDataManager    _dataManager;
    private readonly TrackerApi      _trackerApi;
    private readonly ICommandHandler _commandHandler;
    private readonly WindowSystem    _windowSystem;

    private HuntMapWindow _huntMapWindow = null!;

    private readonly HashSet<ushort> _shouldShowHuntMapTerritories;

    private readonly string[] _spawnNotificationType = ["关闭", "只在可触发时", "一直"];

    private readonly FrozenDictionary<ushort, uint> _mobIdMap;
    private readonly FrozenDictionary<uint, ushort> _mobMapId;

    public SpawnNotification(Configuration                  configuration,
                             IDataManager                   dataManager,
                             TrackerApi                     trackerApi,
                             ICommandHandler                commandHandler,
                             WindowSystem                   windowSystem,
                             FrozenDictionary<ushort, uint> modIdMap,
                             FrozenDictionary<uint, ushort> mobMapId,
                             HashSet<ushort>                shouldShowHuntMapSet)
    {
        _configuration  = configuration;
        _dataManager    = dataManager;
        _trackerApi     = trackerApi;
        _commandHandler = commandHandler;
        _windowSystem   = windowSystem;

        _mobIdMap                     = modIdMap;
        _mobMapId                     = mobMapId;
        _shouldShowHuntMapTerritories = shouldShowHuntMapSet;
    }

    public bool Init()
    {
        DalamudApi.Condition.ConditionChange += Condition_ConditionChange;

        _commandHandler.AddCommand("/获取点位",
                                   new ((_, _) =>
                                   {
                                       var territory = DalamudApi.ClientState.TerritoryType;

                                       if (territory == 0)
                                       {
                                           return;
                                       }

                                       if (!_shouldShowHuntMapTerritories.Contains(territory))
                                       {
                                           Utils.Print("当前地图没有获取点位的必要");

                                           return;
                                       }

                                       if (!_mobIdMap.TryGetValue(territory, out var id)
                                           || _dataManager.GetHuntData(id) is not { } huntData)
                                       {
                                           return;
                                       }

                                       Task.Run(() => FetchHuntSpawnPoints(huntData,
                                                                           territory,
                                                                           (int) _dataManager.GetCurrentInstance()));
                                   })
                                   {
                                       HelpMessage = "获取当前地图的点位",
                                       ShowInHelp  = true,
                                   });

        return true;
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _huntMapWindow = _windowSystem.Windows.FirstOrDefault(i => i.WindowName == HuntMapWindow.Name) as HuntMapWindow
                         ?? throw new InvalidOperationException("Failed to get HuntMapWindow");
    }

    public void Shutdown()
    {
        DalamudApi.Condition.ConditionChange -= Condition_ConditionChange;
    }

    public string UiName => string.Empty;

    public void OnDrawUi()
    {
        var spawnNotificationType = (int) _configuration.SpawnNotificationType;

        for (var i = 0; i < _spawnNotificationType.Length; i++)
        {
            if (ImGui.RadioButton(_spawnNotificationType[i] + "##_spawnNotificationType", ref spawnNotificationType, i))
            {
                _configuration.SpawnNotificationType = (SpawnNotificationType) spawnNotificationType;
                _configuration.Save();
            }

            if (i == _spawnNotificationType.Length - 1)
            {
                break;
            }

            ImGui.SameLine();
        }

        if (_configuration.SpawnNotificationType == SpawnNotificationType.Full)
        {
            ImGui.SameLine();
            var coolDownNotificationSound = _configuration.CoolDownNotificationSound;

            if (ImGui.Checkbox("不在触发期时播放提示音", ref coolDownNotificationSound))
            {
                _configuration.CoolDownNotificationSound = coolDownNotificationSound;
                _configuration.Save();
            }
        }

        var autoShowHuntMap = _configuration.AutoShowHuntMap;

        if (ImGui.Checkbox("自动获取点位列表", ref autoShowHuntMap))
        {
            _configuration.AutoShowHuntMap = autoShowHuntMap;
            _configuration.Save();
        }

        if (_configuration is { AutoShowHuntMap: true, SpawnNotificationType: > SpawnNotificationType.Off })
        {
            ImGui.SameLine();
            var onlyFetchInDuration = _configuration.OnlyFetchInDuration;

            if (ImGui.Checkbox("只在触发期内获取", ref onlyFetchInDuration))
            {
                _configuration.OnlyFetchInDuration = onlyFetchInDuration;
                _configuration.Save();
            }

            if (ImGui.BeginPopupContextItem("blacklisted_territories"))
            {
                foreach (var territory in _shouldShowHuntMapTerritories)
                {
                    if (!_mobIdMap.TryGetValue(territory, out var id))
                    {
                        continue;
                    }

                    var territoryName = _dataManager.GetTerritoryName(territory);
                    var srankName     = _dataManager.GetSRankName(id);

                    var contains = _configuration.BlacklistedTerritories.Contains(territory);

                    if (ImGui.Checkbox($"{territoryName} - {srankName}##popup", ref contains))
                    {
                        if (contains)
                        {
                            _configuration.BlacklistedTerritories.Add(territory);
                        }
                        else
                        {
                            _configuration.BlacklistedTerritories.Remove(territory);
                        }

                        _configuration.Save();
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.Button("黑名单"))
            {
                ImGui.OpenPopup("blacklisted_territories");
            }

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("选中的地图将不会自动获取点位");
            }
        }
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.BetweenAreas51 && !value)
        {
            Task.Run(FetchHuntStatus);
        }
    }

    private async Task FetchHuntStatus()
    {
        if (_configuration.SpawnNotificationType == SpawnNotificationType.Off)
        {
            return;
        }

        var territory = DalamudApi.ClientState.TerritoryType;

        if (!_mobIdMap.TryGetValue(territory, out var id))
        {
            return;
        }

        if (_dataManager.GetHuntData(id) is not { } huntData)
        {
            return;
        }

        var worldName = _dataManager.GetWorldName(_dataManager.GetCurrentWorldId());

        var huntStatus = await _trackerApi.FetchHuntStatus(huntData.KeyName, worldName, _dataManager.GetCurrentInstance());

        if (huntStatus is null)
        {
            return;
        }

        var time = DateTime.Now.ToLocalTime();

        var payloads = new List<Payload>
        {
            new UIForegroundPayload(1),
            new TextPayload($"{_dataManager.FormatCurrentTerritory()} - {huntData.LocalizedName}:"),
        };

        var minTime   = DateTime.UnixEpoch.AddMilliseconds(huntStatus.ExpectMinTime).ToLocalTime();
        var maxTIme   = DateTime.UnixEpoch.AddMilliseconds(huntStatus.ExpectMaxTime).ToLocalTime();
        var spawnable = time > minTime;

        if (spawnable)
        {
            payloads.Add(new TextPayload("\n当前可触发概率: "));
            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));
            payloads.Add(new TextPayload($"{100 * ((time - minTime) / (maxTIme - minTime)):F1}%"));
            payloads.Add(new UIForegroundPayload(0));
        }
        else
        {
            if (_configuration.SpawnNotificationType == SpawnNotificationType.SpawnableOnly)
            {
                if (!_configuration.AutoShowHuntMap
                    || !_shouldShowHuntMapTerritories.Contains(territory)
                    || _configuration.OnlyFetchInDuration)
                {
                    return;
                }

                if (_configuration.BlacklistedTerritories.Contains(territory))
                {
                    return;
                }

                await FetchHuntSpawnPoints(huntData, territory, (int) _dataManager.GetCurrentInstance());

                return;
            }

            payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
            payloads.Add(new UIForegroundPayload((ushort) _configuration.HighlightColor));

            var delta = (minTime - time).TotalMinutes;

            payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分"));
            payloads.Add(new UIForegroundPayload(0));

            if (_configuration.CoolDownNotificationSound)
            {
                UIGlobals.PlayChatSoundEffect(6);
                UIGlobals.PlayChatSoundEffect(6);
                UIGlobals.PlayChatSoundEffect(6);
            }
        }

        payloads.Add(new UIForegroundPayload(0));
        Utils.Print(payloads);

        if (!_configuration.AutoShowHuntMap || !_shouldShowHuntMapTerritories.Contains(territory))
        {
            return;
        }

        if (_configuration.BlacklistedTerritories.Contains(territory))
        {
            return;
        }

        DalamudApi.PluginLog.Debug("Fetching spawn points");
        await FetchHuntSpawnPoints(huntData, territory, (int) _dataManager.GetCurrentInstance());
    }

    private async Task FetchHuntSpawnPoints(HuntData status, ushort territory, int instance)
    {
        var currentWorld = _dataManager.GetCurrentWorldId();
        var worldName    = _dataManager.GetWorldName(currentWorld);

        if (await _trackerApi.FetchSpawnPoints(worldName,
                                               status.KeyName,
                                               instance) is not { } huntSpawnPoints)
        {
            return;
        }

        var spawnPoints = huntSpawnPoints.SpawnPoints;

        if (spawnPoints.Count > 5)
        {
            await DalamudApi.Framework.RunOnFrameworkThread(() =>
            {
                _huntMapWindow.SetCurrentMap(spawnPoints,
                                             territory);
            });

            return;
        }

        var info = _dataManager.GetTerritoryInfo(territory);

        var payloads = new List<Payload>
        {
            new
                TextPayload($"{_dataManager.FormatInstance(currentWorld, territory, _dataManager.GetCurrentInstance())} 的当前可触发点位:"),
        };

        foreach (var spawnPoint in spawnPoints)
        {
            payloads.Add(new TextPayload("\n"));
            payloads.Add(new MapLinkPayload(territory, info.MapId, spawnPoint.X, spawnPoint.Y));
            payloads.Add(new TextPayload($"{(char) SeIconChar.LinkMarker}"));

            payloads.Add(new
                             TextPayload($"{spawnPoint.Key.Replace("SpawnPoint", "")} ({spawnPoint.X:0.00}, {spawnPoint.Y:0.00})"));

            payloads.Add(RawPayload.LinkTerminator);
        }

        Utils.Print(payloads);
    }
}