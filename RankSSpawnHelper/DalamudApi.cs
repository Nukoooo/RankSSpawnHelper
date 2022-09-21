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

namespace RankSSpawnHelper
{
    internal class DalamudApi
    {
        [PluginService] internal static DalamudPluginInterface Interface { get; } = null!;

        [PluginService] internal static ChatGui ChatGui { get; } = null!;

        [PluginService] internal static ClientState ClientState { get; } = null!;

        [PluginService] internal static CommandManager CommandManager { get; } = null!;

        [PluginService] internal static SigScanner SigScanner { get; } = null!;

        [PluginService] internal static DataManager DataManager { get; } = null!;

        [PluginService] internal static FateTable FateTable { get; } = null!;

        [PluginService] internal static ObjectTable ObjectTable { get; } = null!;

        [PluginService] internal static Framework Framework { get; } = null!;

        [PluginService] internal static Condition Condition { get; } = null!;

        [PluginService] internal static PartyList PartyList { get; } = null!;

        [PluginService] internal static DtrBar DtrBar { get; } = null!;
    }
}