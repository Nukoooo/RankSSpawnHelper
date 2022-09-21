using System;
using System.IO;
using ImGuiNET;

namespace RankSSpawnHelper.Managers
{
    internal class Font : IDisposable
    {
        private bool _fontBuilt;

        public Font()
        {
            DalamudApi.Interface.UiBuilder.BuildFonts += UiBuilder_OnBuildFonts;
            DalamudApi.Interface.UiBuilder.RebuildFonts();
        }

        public ImFontPtr Yahei24 { get; private set; }

        public void Dispose()
        {
            DalamudApi.Interface.UiBuilder.BuildFonts -= UiBuilder_OnBuildFonts;
        }

        private unsafe void UiBuilder_OnBuildFonts()
        {
            var windowsFolder  = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System));
            var strFontsFolder = Path.Combine(windowsFolder.FullName, "Fonts");

            ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig.FontDataOwnedByAtlas = false;
            fontConfig.PixelSnapH           = true;
            Yahei24                         = ImGui.GetIO().Fonts.AddFontFromFileTTF(strFontsFolder + "\\msyhbd.ttc", 24, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

            _fontBuilt = true;
            fontConfig.Destroy();
        }

        public bool IsFontBuilt()
        {
            return _fontBuilt;
        }
    }
}