using Websocket.Client;

namespace RankSSpawnHelper.Managers;

internal partial class ConnectionManager
{
    private void OnReconnection(ReconnectionInfo args)
    {
        DalamudApi.Framework.Run(() => { });
        DalamudApi.PluginLog.Debug($"ReconnectionType: {args.Type}");

        if (_counter == null)
        {
            return;
        }

        var localTracker = _counter.GetLocalTrackers();

        if (!DalamudApi.ClientState.IsLoggedIn || localTracker == null || localTracker.Count == 0)
        {
            return;
        }

        var name = Utils.FormatLocalPlayerName();

        List<NetTracker> trackers = [];

        foreach (var tracker in localTracker)
        {
            if (!tracker.Value.TrackerOwner.Equals(name))
            {
                continue;
            }

            var split       = tracker.Key.Split('@');
            var worldId     = _dataManager.GetWorldId(split[0]);
            var territoryId = _dataManager.GetTerritoryId(split[1]);
            var instanceId  = 0u;

            if (split.Length == 3)
            {
                _ = uint.TryParse((string?) split[2], out instanceId);
            }

            Dictionary<uint, int> data = new ();

            var isItem = _dataManager.IsTerritoryItemThingy(tracker.Value.TerritoryId);

            foreach (var counter in tracker.Value.Counter)
            {
                if (tracker.Value.TerritoryId == 621)
                {
                    data[0] = counter.Value;
                }
                else if (isItem)
                {
                    data[_dataManager.GetItemId(counter.Key)] = counter.Value;
                }

                else
                {
                    data[_dataManager.GetNpcId(counter.Key)] = counter.Value;
                }
            }

            var netTracker = new NetTracker
            {
                WorldId     = worldId,
                TerritoryId = territoryId,
                InstanceId  = instanceId,
                Data        = data,
                Time        = tracker.Value.StartTime,
            };

            trackers.Add(netTracker);
        }

        SendMessage(new NewConnectionMessage
        {
            Type        = "NewConnection",
            WorldId     = _dataManager.GetCurrentWorldId(),
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            InstanceId  = _dataManager.GetCurrentInstance(),
            Trackers    = trackers,
        });
    }
}