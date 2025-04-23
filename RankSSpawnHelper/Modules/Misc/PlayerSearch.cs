using Dalamud.Hooking;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using ImGuiNET;
using RankSSpawnHelper.Managers;

namespace RankSSpawnHelper.Modules;

internal class PlayerSearch : IUiModule
{
    private Hook<EventActionReceiveDelegate> EventActionReceiveHook { get; set; } = null!;

    private readonly Configuration _configuration;
    private readonly ICounter      _counterModule;
    private readonly IDataManager  _dataManager;

    public PlayerSearch(Configuration configuration, ICounter counter, IDataManager dataManager)
    {
        _configuration = configuration;
        _counterModule = counter;
        _dataManager   = dataManager;
    }

    public bool Init()
    {
        if (!DalamudApi.SigScanner
                       .TryScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? B8 ?? ?? ?? ?? 49 8B F9",
                                    out var eventActionReceive))
        {
            DalamudApi.PluginLog.Error("Failed to get EventActionReceive address");

            return false;
        }

        unsafe
        {
            EventActionReceiveHook
                = DalamudApi.GameInterop.HookFromAddress<EventActionReceiveDelegate>(eventActionReceive, hk_EventActionReceive);

            EventActionReceiveHook.Enable();
        }

        return true;
    }

    public void Shutdown()
    {
        EventActionReceiveHook?.Dispose();
    }

    public string UiName => string.Empty;

    public void OnDrawUi()
    {
        var playerSearch = _configuration.PlayerSearch;

        if (ImGui.Checkbox("当前分线人数", ref playerSearch))
        {
            _configuration.PlayerSearch = playerSearch;
            _configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("需要右键大水晶，并且当前分线里有S怪才有用");
        }
    }

    private unsafe void hk_EventActionReceive(nint a1, uint type, ushort a3, nint a4, uint* payload, byte payloadCount)
    {
        EventActionReceiveHook.Original(a1, type, a3, a4, payload, payloadCount);
        var id          = (ushort) type;
        var handlerType = (EventHandlerType) (type >> 16);

        if (id != 2 && handlerType != EventHandlerType.Aetheryte)
        {
            return;
        }

        if (!_configuration.PlayerSearch)
        {
            return;
        }

        if (!_counterModule.CurrentInstanceHasSRank())
        {
            return;
        }

        var currentInstance = _dataManager.GetCurrentInstance();

        Utils.Print(currentInstance == 0
                        ? $"当前地图的人数: {payload[currentInstance]}"
                        : $"当前分线（{GetInstanceString()}） 的人数: {payload[currentInstance]}");
    }

    private string GetInstanceString()
    {
        return _dataManager.GetCurrentInstance() switch
        {
            1 => "\xe0b1" + "线",
            2 => "\xe0b2" + "线",
            3 => "\xe0b3" + "线",
            4 => "\xe0b4" + "线",
            5 => "\xe0b5" + "线",
            6 => "\xe0b6" + "线",
            _ => "\xe060" + "线",
        };
    }

    private unsafe delegate void EventActionReceiveDelegate(nint a1, uint type, ushort a3, nint a4, uint* networkData,
                                                            byte count);
}