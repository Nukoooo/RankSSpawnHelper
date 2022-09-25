using System;
using System.IO;
using Dalamud.Logging;
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

            var fontName = strFontsFolder + "\\msyhbd.ttc";

            if (!File.Exists(fontName))
            {
                PluginLog.Error($"找不到微软雅黑字体. 尝试搜寻的路径: {fontName}");
                return;
            }

            ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig.FontDataOwnedByAtlas = false;
            fontConfig.PixelSnapH           = true;
            Yahei24                         = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontName, 24, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

            _fontBuilt = true;
            fontConfig.Destroy();
        }

        public bool IsFontBuilt()
        {
            return _fontBuilt;
        }
    }
}