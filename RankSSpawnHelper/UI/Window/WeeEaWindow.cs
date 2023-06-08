using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using RankSSpawnHelper.Models;
using static System.TimeSpan;

namespace RankSSpawnHelper.UI.Window;

internal class WeeEaWindow : Dalamud.Interface.Windowing.Window
{
    private readonly Dictionary<string, DateTime> _dateTimes = new();

    public WeeEaWindow() : base("异亚计数##RankSSpawnHelper")
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;
    }

    public override void PreOpenCheck()
    {
        IsOpen = DalamudApi.ClientState.LocalPlayer != null && DalamudApi.ClientState.IsLoggedIn && DalamudApi.ClientState.TerritoryType == 960 &&
                 Plugin.Configuration.WeeEaCounter;
    }

    private void AttemptFail(int count, List<string> nameList)
    {
        try
        {
#if RELEASE || RELEASE_CN
            if (count < 10)
            {
                Plugin.Print(new List<Payload>
                             {
                                 new UIForegroundPayload(518),
                                 new TextPayload("Error: 附近的小异亚不够10个,寄啥呢"),
                                 new UIForegroundPayload(0)
                             });
                return;
            }
#endif
            var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();

            if (!_dateTimes.ContainsKey(currentInstance))
            {
                _dateTimes.Add(currentInstance, DateTime.Now + FromSeconds(15.0));
                Plugin.Managers.Socket.SendMessage(new AttemptMessage
                                                   {
                                                       Type = "WeeEa",
                                                       // Instance    = currentInstance,
                                                       WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
                                                       InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
                                                       TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                       Failed      = true,
                                                       Names       = nameList
                                                   });
                return;
            }

            var time = _dateTimes[currentInstance];
            if (time > DateTime.Now)
            {
                var delta = time - DateTime.Now;
                Plugin.Print(new List<Payload>
                             {
                                 new UIForegroundPayload(518),
                                 new TextPayload($"Error: 你还得等 {delta:g} 才能再点寄"),
                                 new UIForegroundPayload(0)
                             });
                return;
            }

            _dateTimes[currentInstance] = DateTime.Now + FromSeconds(30.0);
            Plugin.Managers.Socket.SendMessage(new AttemptMessage
                                               {
                                                   Type = "WeeEa",
                                                   // Instance    = currentInstance,
                                                   WorldId     = Plugin.Managers.Data.Player.GetCurrentWorldId(),
                                                   InstanceId  = Plugin.Managers.Data.Player.GetCurrentInstance(),
                                                   TerritoryId = DalamudApi.ClientState.TerritoryType,
                                                   Failed      = true,
                                                   Names       = nameList
                                               });
        }
        catch (Exception)
        {
            // do nothing
        }
    }

    public override void Draw()
    {
        var count    = 0;
        var count2   = 0;
        var nameList = new List<string>();

        var enumerator = DalamudApi.ObjectTable.Where(i => i != null && i.Address != nint.Zero
                                                                     && i.ObjectKind == ObjectKind.Companion);

        var localPlayerPos = DalamudApi.ClientState.LocalPlayer.Position;

        foreach (var obj in enumerator)
        {
            var delta = obj.Position - localPlayerPos;

            // xzy 
            var length2D = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);

            if (length2D > 10)
                continue;

            // TODO: Get name from excel sheet
            if (obj.Name.ToString() == "小异亚")
            {
                count++;
                var owner = (PlayerCharacter)DalamudApi.ObjectTable[obj.ObjectIndex - 1];
                if (owner != null)
                {
                    var name = $"{owner.Name.TextValue}@{owner.HomeWorld.GameData.Name.RawString}";
                    nameList.Add(name);
                }

                continue;
            }

            count2++;
        }

        if (Plugin.Managers.Font.IsFontBuilt())
            ImGui.PushFont(Plugin.Managers.Font.NotoSan24);

        if (ImGui.Button("[ 寄了点我 ]"))
        {
            /*var obj = DalamudApi.ObjectTable[1];
            if (obj.IsValid() && obj.ObjectKind == ObjectKind.Companion && obj.Name.TextValue == "小异亚")
                nameList.Add();*/
            AttemptFail(count, nameList);
        }

        ImGui.SameLine();

        ImGui.Text($"附近的小异亚数量:{count}\n非小异亚的数量: {count2}");

        if (Plugin.Managers.Font.IsFontBuilt())
            ImGui.PopFont();
    }
}