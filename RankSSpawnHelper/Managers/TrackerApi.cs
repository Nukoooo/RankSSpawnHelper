using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RankSSpawnHelper.Managers;

internal struct HuntStatus
{
    [JsonPropertyName("expectMaxTime")]
    public double ExpectMaxTime { get; init; }

    [JsonPropertyName("expectMinTime")]
    public double ExpectMinTime { get; init; }

    [JsonPropertyName("missing")]
    public int _missing { get; init; }

    public bool Missing => _missing != 0;

    [JsonIgnore]
    public uint Instance { get; set; }

    [JsonPropertyName("worldName")]
    public string WorldName { get; init; }
}

internal readonly record struct SpawnPoints
{
    [JsonPropertyName("key")]
    public string Key { get; init; }

    [JsonPropertyName("x")]
    public float X { get; init; }

    [JsonPropertyName("y")]
    public float Y { get; init; }
}

internal readonly record struct HuntSpawnPoints()
{
    [JsonPropertyName("spawnPoints")]
    public List<SpawnPoints> SpawnPoints { get; init; } = [];
}

internal class TrackerApi
{
    private readonly HttpClient _httpClient = new ();

    private bool _isFetchingHuntStatus;
    private bool _isFetchingSpawnPoint;

    private (HuntData data, List<HuntStatus> statusList) _statuses = new (new (), []);

    public TrackerApi()
    {
        _httpClient.BaseAddress = new ("https://tracker-api.beartoolkit.com/");
        _httpClient.Timeout     = TimeSpan.FromSeconds(5);
    }

    public (HuntData, List<HuntStatus>) GetHuntStatus()
        => _statuses;

    public bool IsFetchingHuntStatus()
        => _isFetchingHuntStatus;

    public void FetchHuntStatuses(List<string> serverList, HuntData? data, uint instance = 0)
        => Task.Run(async () =>
        {
            if (_isFetchingHuntStatus || data == null)
            {
                return;
            }

            _statuses.data = data;
            _statuses.statusList.Clear();

            _isFetchingHuntStatus = true;

            var huntName = data.KeyName;

            if (instance > 0)
            {
                huntName += $" {instance}";
            }

            foreach (var server in serverList)
            {
                var dict = new Dictionary<string, string>
                {
                    { "WorldName", server },
                    { "HuntName", huntName },
                };

                var response = await _httpClient.PostAsync("public/huntStatus", new FormUrlEncodedContent(dict));
                response.EnsureSuccessStatusCode();

                if (!response.IsSuccessStatusCode)
                {
                    DalamudApi.PluginLog
                              .Error($"Failed to request hunt status. {server}@{huntName}, status: {response.StatusCode}");

                    continue;
                }

                var content    = await response.Content.ReadAsStringAsync();
                var huntStatus = JsonSerializer.Deserialize<HuntStatus>(content);
                huntStatus.Instance = instance;
                _statuses.statusList.Add(huntStatus);
            }

            _isFetchingHuntStatus = false;
        });

    public async Task<HuntStatus?> FetchHuntStatus(string huntName, string server, uint instance = 0)
    {
        if (instance > 0)
        {
            huntName += $" {instance}";
        }

        var dict = new Dictionary<string, string>
        {
            { "WorldName", server },
            { "HuntName", huntName },
        };

        try
        {
            var response = await _httpClient.PostAsync("public/huntStatus", new FormUrlEncodedContent(dict));
            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
            {
                DalamudApi.PluginLog
                          .Error($"Failed to request hunt status. {server}@{huntName}, status: {response.StatusCode}");

                Utils.Print($"获取S怪状态失败. Code: {response.StatusCode}");

                return null;
            }

            var huntStatus = JsonSerializer.Deserialize<HuntStatus>(await response.Content.ReadAsStringAsync());

            return huntStatus;
        }
        catch (Exception e)
        {
            Utils.Print($"获取S怪状态失败. {e.Message}");

            return null;
        }
    }

    public async Task<HuntSpawnPoints?> FetchSpawnPoints(string server, string keyName, int instance)
    {
        try
        {
            if (_isFetchingSpawnPoint)
            {
                return null;
            }

            var body = new Dictionary<string, string>
            {
                { "HuntName", keyName + (instance == 0 ? string.Empty : $" {instance}") },
                { "WorldName", server },
            };

            _isFetchingSpawnPoint = true;

            var response = await _httpClient.PostAsync("public/huntmap", new FormUrlEncodedContent(body));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                DalamudApi.PluginLog.Error($"获取狩猎点位失败. StatusCode: {response.StatusCode}");
                Utils.Print($"获取点位失败. StatusCode: {response.StatusCode}");

                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var huntMap = JsonSerializer.Deserialize<HuntSpawnPoints>(content);

            _isFetchingSpawnPoint = false;

            return huntMap;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e.Message);
            Utils.Print($"获取S怪点位失败. {e.Message}");

            _isFetchingHuntStatus = false;

            return null;
        }
    }
}
