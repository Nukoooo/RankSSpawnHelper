using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin;
using RankSSpawnHelper.Features;
using RankSSpawnHelper.Managers;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
namespace RankSSpawnHelper;

internal class Service
{
    internal static Commands Commands { get; set; } = null!;
    internal static Configuration Configuration { get; set; } = null!;
    internal static ConfigWindow ConfigWindow { get; set; } = null!;
    internal static FateRecorder FateRecorder { get; set; } = null!;
    internal static Counter Counter { get; set; } = null!;
    internal static CounterOverlay CounterOverlay { get; set; } = null!;
    internal static WeeEa WeeEa { get; set; } = null!;
    internal static ShowInstance ShowInstance { get; set; } = null!;
    internal static MonsterManager MonsterManager { get; set; } = null!;
    internal static SocketManager SocketManager { get; set; } = null!;

    [PluginService] internal static DalamudPluginInterface Interface { get; private set; } = null!;

    [PluginService] internal static ChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static ClientState ClientState { get; private set; } = null!;

    [PluginService] internal static CommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static SigScanner SigScanner { get; private set; } = null!;

    [PluginService] internal static DataManager DataManager { get; private set; } = null!;

    [PluginService] internal static FateTable FateTable { get; private set; } = null!;

    [PluginService] internal static ObjectTable ObjectTable { get; private set; } = null!;

    [PluginService] internal static Framework Framework { get; set; } = null!;

    [PluginService] internal static Condition Condition { get; set; } = null!;

    [PluginService] internal static PartyList PartyList { get; set; } = null!;

    [PluginService] internal static DtrBar DtrBar { get; set; } = null!;
}