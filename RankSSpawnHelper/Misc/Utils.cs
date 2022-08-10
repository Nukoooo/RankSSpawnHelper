using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;

namespace RankSSpawnHelper.Misc;

internal static class Utils
{
    private static void PrintAllUIColor()
    {
        foreach (var c in Service.DataManager.Excel.GetSheet<UIColor>())
        {
            var seString = new SeString(new List<Payload>
            {
                new UIForegroundPayload((ushort)c.RowId),
                new UIGlowPayload((ushort)c.RowId),
                new TextPayload($"UIColor {c.RowId}   "),
                new UIGlowPayload(0),
                new TextPayload($"{c.UIForeground:x8}   "),
                new UIForegroundPayload(0),
                new UIGlowPayload((ushort)c.RowId),
                new TextPayload($"{c.UIGlow:x8}"),
                new UIGlowPayload(0)
            });
            Service.ChatGui.PrintChat(new XivChatEntry
            {
                Message = seString
            });
        }
    }
}