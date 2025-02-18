using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace RankSSpawnHelper;

internal class Utils
{
    internal static string PluginVersion { get; set; } = null!;

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
            new UIForegroundPayload(0),
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
            new TextPayload("] "),
        };

        payloads.AddRange(newPayloads);
        payloads.Add(new UIForegroundPayload(0));
        DalamudApi.ChatGui.Print(new SeString(payloads));
    }

    public static string FormatLocalPlayerName()
    {
        if (DalamudApi.ClientState.LocalPlayer is { HomeWorld.ValueNullable: { } world } local)
        {
            return $"{local.Name}@{world.Name}";
        }

        return string.Empty;
    }

    public static string EncodeNonAsciiCharacters(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);

        return BitConverter.ToString(bytes)
                           .Replace("-", " ");
    }
}
