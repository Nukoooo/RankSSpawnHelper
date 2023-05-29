using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using RankSSpawnHelper.Ui;

namespace RankSSpawnHelper;

internal class Plugin
{
    private static bool _isChina;
    private static bool _init;

    internal static Configuration Configuration { get; set; } = null!;
    internal static Features.Features Features { get; set; } = null!;
    internal static Managers.Managers Managers { get; set; } = null!;
    internal static Windows Windows { get; set; } = null!;

    public static bool IsChina()
    {
        if (_init)
            return _isChina;
        _isChina = DalamudApi.SigScanner.TryScanText("48 8D 15 ?? ?? ?? ?? 33 F6 44 89 4C 24", out _);
        _init    = true;

        return _isChina;
    }

    public static void Print(string text)
    {
        var payloads = new List<Payload>
                       {
                           new UIForegroundPayload(1),
                           new TextPayload("["),
                           new UIForegroundPayload(35),
                           new TextPayload("S怪触发"),
                           new UIForegroundPayload(0),
                           new TextPayload("] "),
                           new TextPayload(text),
                           new UIForegroundPayload(0)
                       };
        DalamudApi.ChatGui.Print(new SeString(payloads));
    }

    public static void Print(Payload newPayloads)
    {
        var payloads = new List<Payload>
                       {
                           new UIForegroundPayload(1),
                           new TextPayload("["),
                           new UIForegroundPayload(35),
                           new TextPayload("S怪触发"),
                           new UIForegroundPayload(0),
                           new TextPayload("] "),
                           newPayloads,
                           new UIForegroundPayload(0)
                       };
        DalamudApi.ChatGui.Print(new SeString(payloads));
    }

    public static void Print(IEnumerable<Payload> newPayloads)
    {
        var payloads = new List<Payload>
                       {
                           new UIForegroundPayload(1),
                           new TextPayload("["),
                           new UIForegroundPayload(35),
                           new TextPayload("S怪触发"),
                           new UIForegroundPayload(0),
                           new TextPayload("] ")
                       };
        payloads.AddRange(newPayloads);
        payloads.Add(new UIForegroundPayload(0));
        DalamudApi.ChatGui.Print(new SeString(payloads));
    }
}