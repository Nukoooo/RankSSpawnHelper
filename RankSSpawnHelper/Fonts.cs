using System;
using Dalamud.Logging;
using ImGuiNET;

namespace RankSSpawnHelper;

internal class Fonts
{
    private static bool _fontBuilt;

    public static ImFontPtr Yahei24 { get; private set; }

    public static bool AreFontsBuilt()
    {
        return _fontBuilt;
    }

    public static unsafe void OnBuildFonts()
    {
        _fontBuilt = false;
        try
        {
            ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig.FontDataOwnedByAtlas = false;
            fontConfig.PixelSnapH = true;

            Yahei24 = ImGui.GetIO().Fonts.AddFontFromFileTTF("C:\\Windows\\Fonts\\msyhbd.ttc", 24, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
            // 如果我再创建一个的话就会crash????不知道为什么
            // Yahei16 = ImGui.GetIO().Fonts.AddFontFromFileTTF("C:\\Windows\\Fonts\\msyhbd.ttc", 16, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

            _fontBuilt = true;

            fontConfig.Destroy();
        }
        catch (Exception e)
        {
            PluginLog.Error($"Error when building fonts:{e}");
        }
    }
}