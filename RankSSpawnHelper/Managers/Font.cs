using System;
using System.IO;
using Dalamud.Logging;
using ImGuiNET;

namespace RankSSpawnHelper.Managers;

internal class Font : IDisposable
{
    private bool _fontBuilt;

    public Font()
    {
        DalamudApi.Interface.UiBuilder.BuildFonts += UiBuilder_OnBuildFonts;
        DalamudApi.Interface.UiBuilder.RebuildFonts();
    }

    public ImFontPtr NotoSan24 { get; private set; }
    public ImFontPtr NotoSan18 { get; private set; }

    public void Dispose()
    {
        DalamudApi.Interface.UiBuilder.BuildFonts -= UiBuilder_OnBuildFonts;
    }

    private unsafe void UiBuilder_OnBuildFonts()
    {
        // DalamudApi.Interface.DalamudAssetDirectory;
        // var windowsFolder  = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System));
        var fontName = Plugin.IsChina() ? "NotoSansCJKsc-Medium.otf" : "NotoSansCJKjp-Medium.otf";
        var fontPath = Path.Combine(DalamudApi.Interface.DalamudAssetDirectory.FullName, "UIRes", fontName);

        if (!File.Exists(fontPath))
        {
            PluginLog.Error($"Cannot find font \"{fontName}\". fontPath: {fontPath}");
            return;
        }

        ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig.FontDataOwnedByAtlas = false;
        fontConfig.PixelSnapH           = true;
        NotoSan24                       = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, 24, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
        NotoSan18                       = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, 18, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

        _fontBuilt = true;
        fontConfig.Destroy();
    }

    public bool IsFontBuilt()
    {
        return _fontBuilt;
    }
}