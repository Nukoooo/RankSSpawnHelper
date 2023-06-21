using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Network;
using Dalamud.IoC;
using Dalamud.Plugin;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace RankSSpawnHelper;

internal class DalamudApi
{
    [PluginService] internal static DalamudPluginInterface Interface { get; private set; } = null!;

    [PluginService] internal static ChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static ClientState ClientState { get; private set; } = null!;

    [PluginService] internal static CommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static SigScanner SigScanner { get; private set; } = null!;

    [PluginService] internal static DataManager DataManager { get; private set; } = null!;

    [PluginService] internal static GameGui GameGui { get; set; } = null!;

    // [PluginService] internal static FateTable FateTable { get; private set; } = null!;

    [PluginService] internal static ObjectTable ObjectTable { get; private set; } = null!;

    [PluginService] internal static Framework Framework { get; private set; } = null!;

    [PluginService] internal static Condition Condition { get; private set; } = null!;

    // [PluginService] internal static PartyList PartyList { get; private set; } = null!;

    [PluginService] internal static DtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static GameNetwork GameNetwork { get; private set; } = null!;
    [PluginService] internal static PartyFinderGui PartyFinderGui { get; private set; } = null!;
}