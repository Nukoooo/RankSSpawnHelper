using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RankSSpawnHelper.Modules;
using Websocket.Client;

namespace RankSSpawnHelper.Managers;

internal interface IConnectionManager
{
    bool IsConnected();

    void Reconnect();

    void SendMessage(ConnectionManager.BaseMessage message);
}

internal partial class ConnectionManager : IConnectionManager, IModule
{
    private const string Url = "wss://nuko.me/ws";
    /*#if DEBUG || DEBUG_CN
        private const string Url = "ws://127.0.0.1:8000/ws";
    #else
        private const string Url = "wss://nuko.me/ws";
    #endif*/

    private const    string           ServerVersion = "v5";
    private          WebsocketClient? _client;
    private          string           _userName = string.Empty;
    private readonly IDataManager     _dataManager;
    private readonly Configuration    _configuration;
    private          ICounter         _counter = null!;

    public ConnectionManager(IDataManager dataManager, Configuration configuration)
    {
        _dataManager   = dataManager;
        _configuration = configuration;
    }

    public bool Init()
    {
        DalamudApi.ClientState.Login  += ClientState_Login;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;

        return true;
    }

    private void ClientState_Login()
    {
        _ = Connect(Url);
    }

    private void ClientState_OnLogout()
    {
        _client?.Dispose();
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _counter = serviceProvider.GetService<ICounter>() ?? throw new InvalidOperationException("ICounter is null");

        if (DalamudApi.ClientState.LocalPlayer != null)
        {
            _ = Connect(Url);
        }
    }

    public void Shutdown()
    {
        DalamudApi.ClientState.Login  -= ClientState_Login;
        DalamudApi.ClientState.Logout -= ClientState_OnLogout;
        _client?.Dispose();
    }

    private async Task Connect(string url)
    {
        try
        {
            if (!DalamudApi.ClientState.IsLoggedIn)
            {
                _client?.Dispose();

                return;
            }

            _client?.Dispose();
            _userName = Utils.FormatLocalPlayerName();

            ClientWebSocket ClientFactory()
            {
                var client = new ClientWebSocket
                {
                    Options =
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(40),
                    },
                };

                client.Options.SetRequestHeader("ranks-spawn-helper-user",
                                                Utils.EncodeNonAsciiCharacters(_userName));

                client.Options.SetRequestHeader("server-version", ServerVersion);
                client.Options.SetRequestHeader("user-type",      "Dalamud");
                client.Options.SetRequestHeader("plugin-version", _configuration.PluginVersion);

                // LMAO
                client.Options.SetRequestHeader("iscn", "true");

                if (_configuration.UseProxy)
                {
                    client.Options.Proxy = new WebProxy(_configuration.ProxyUrl);
                }

                return client;
            }

            _client = new (new (url), ClientFactory)
            {
                ReconnectTimeout      = TimeSpan.FromSeconds(120),
                ErrorReconnectTimeout = TimeSpan.FromSeconds(60),
            };

            _client.ReconnectionHappened.Subscribe(info => { DalamudApi.Framework.Run(() => OnReconnection(info)); });
            _client.MessageReceived.Subscribe(args => { DalamudApi.Framework.Run(() => OnMessageReceived(args)); });
            _client.DisconnectionHappened.Subscribe(args => { DalamudApi.Framework.Run(() => OnDisconnectionHappened(args)); });

            await _client.Start();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Debug(e, "Exception in Managers::Socket::Connect()");
        }
    }

    private void OnDisconnectionHappened(DisconnectionInfo obj)
    {
        DalamudApi.PluginLog.Debug($"Disconnection type: {obj.Type}. {obj.CloseStatusDescription}");

        if (!DalamudApi.ClientState.IsLoggedIn)
        {
            _client?.Dispose();
        }
    }

    public bool IsConnected()
        => _client is { IsStarted: true, IsRunning: true };

    public void Reconnect()
        => Task.Run(async () => await Connect(Url));

    public void SendMessage(BaseMessage message)
    {
        if (!IsConnected())
        {
            return;
        }

        var str = JsonConvert.SerializeObject(message);
        _client!.Send(str);
        DalamudApi.PluginLog.Debug($"Managers::Socket::SendMessage: {str}");
    }
}