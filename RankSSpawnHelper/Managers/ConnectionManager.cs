using System.Net;
using System.Net.WebSockets;
using Dalamud.Interface.Colors;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OtterGui.Widgets;
using RankSSpawnHelper.Modules;
using Websocket.Client;

namespace RankSSpawnHelper.Managers;

internal interface IConnectionManager
{
    bool IsConnected();

    void Reconnect();

    void SendMessage(ConnectionManager.BaseMessage message);
}

internal partial class ConnectionManager : IConnectionManager, IUiModule
{
    private const string Url = "wss://nuko.me/ws";

    /*
    #if DEBUG || DEBUG_CN
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

    private string _proxyUrl;

    public ConnectionManager(IDataManager dataManager, Configuration configuration)
    {
        _dataManager   = dataManager;
        _configuration = configuration;
        _proxyUrl      = configuration.ProxyUrl;
    }

    public bool Init()
    {
        DalamudApi.ClientState.Login  += ClientState_Login;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;

        return true;
    }

    private void ClientState_Login()
    {
        Task.Run(() => Connect(Url));
    }

    private void ClientState_OnLogout(int type, int code)
    {
        _client?.Dispose();
    }

    public void PostInit(ServiceProvider serviceProvider)
    {
        _counter = serviceProvider.GetService<ICounter>() ?? throw new InvalidOperationException("ICounter is null");

        if (DalamudApi.ClientState.LocalPlayer != null)
        {
            Task.Run(() => Connect(Url));
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

    public string UiName => "服务器连接";

    public void OnDrawUi()
    {
        var connected = IsConnected();
        ImGui.Text("连接状态:");
        ImGui.SameLine();

        ImGui.TextColored(connected ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed,
                          connected ? "已连接" : "未连接");

        ImGui.SameLine();

        if (ImGui.Button("重新连接"))
        {
            Reconnect();
        }

        Widget.BeginFramedGroup("代理设置");

        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "连不上的时候再用!!!!");

            ImGui.TextUnformatted("使用方法: {代理类型}://127.0.0.1:{代理端口}, 比如 http://127.0.0.1:7890.");
            ImGui.TextUnformatted("如果不知道怎么填请查看你所使用的代理设置.");
            ImGui.TextUnformatted("Clash(图标是猫的)默认是http,端口7890, Shadowsocks(小飞机)默认是socks5,端口1080");
            ImGui.TextUnformatted("请根据自己的实际情况填写,上述以及默认给出来的链接仅供参考!!!!!");
            ImGui.TextUnformatted("如果是在使用Clash进行代理，建议开启TUN模式");
            ImGui.NewLine();

            var useProxy = _configuration.UseProxy;

            if (ImGui.Checkbox("使用代理连接到服务器", ref useProxy))
            {
                _configuration.UseProxy = useProxy;
                _configuration.Save();
            }

            ImGui.SetNextItemWidth(256);

            if (ImGui.InputTextWithHint("##proxyURL", "代理链接", ref _proxyUrl, 256))
            {
                _configuration.ProxyUrl = _proxyUrl;
            }

            ImGui.SameLine();

            if (ImGui.Button("保存并重新连接"))
            {
                _configuration.Save();
                Reconnect();
            }
        }

        Widget.EndFramedGroup();
    }
}