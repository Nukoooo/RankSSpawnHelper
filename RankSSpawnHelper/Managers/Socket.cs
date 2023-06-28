using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using ImGuiNET;
using Newtonsoft.Json;
using RankSSpawnHelper.Models;
using Websocket.Client;
using Websocket.Client.Models;

namespace RankSSpawnHelper.Managers;

internal class Socket : IDisposable
{
    public Sockets.Main Main;

    public Socket()
    {
        Main                          =  new Sockets.Main();
        DalamudApi.ClientState.Login  += ClientState_OnLogin;
        DalamudApi.ClientState.Logout += ClientState_OnLogout;
    }

    private void ClientState_OnLogin(object sender, EventArgs e)
    {
    }

    private void ClientState_OnLogout(object sender, EventArgs e)
    {

    }

    public void Dispose()
    {
        Main.Dispose();

        DalamudApi.ClientState.Login  -= ClientState_OnLogin;
        DalamudApi.ClientState.Logout -= ClientState_OnLogout;
    }
}