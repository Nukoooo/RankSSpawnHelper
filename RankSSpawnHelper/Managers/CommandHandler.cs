using Dalamud.Game.Command;

namespace RankSSpawnHelper.Managers;

internal interface ICommandHandler
{
    void AddCommand(string command, CommandInfo @delegate);
}

internal class CommandHandler : ICommandHandler, IModule
{
    private readonly Dictionary<string, CommandInfo> _handlerDelegates = [];

    public void AddCommand(string command, CommandInfo info)
    {
        if (!command.StartsWith('/'))
        {
            command = '/' + command;
        }

        if (_handlerDelegates.TryAdd(command, info))
        {
            DalamudApi.CommandManager.AddHandler(command, info);

            return;
        }

        DalamudApi.PluginLog.Warning($"Command {command} is already added");
    }

    public bool Init()
        => true;

    public void Shutdown()
    {
        foreach (var (cmd, _) in _handlerDelegates)
        {
            var result = DalamudApi.CommandManager.RemoveHandler(cmd);
            DalamudApi.PluginLog.Info($"Result of removing command \"{cmd}\": {result}");
        }
    }
}