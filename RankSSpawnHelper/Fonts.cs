using System;
using Dalamud.Logging;
using ImGuiNET;

namespace RankSSpawnHelper;

internal class Fonts
{
    private static bool _fontBuilt;

    public static ImFontPtr Yahei24 { get; private set; }

    public static bool AreFontsBuilt() => _fontBuilt;

    public static unsafe void OnBuildFonts()
    {
        _fontBuilt = false;
        try
        {
            ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig.FontDataOwnedByAtlas = false;
            fontConfig.PixelSnapH = true;

            // TODO: 用DalamudAsset里的字体
            Yahei24 = ImGui.GetIO().Fonts.AddFontFromFileTTF("C:\\Windows\\Fonts\\msyhbd.ttc", 24, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

            _fontBuilt = true;

            fontConfig.Destroy();
        }
        catch (Exception e)
        {
            PluginLog.Error($"Error when building fonts:{e}");
        }
    }
}