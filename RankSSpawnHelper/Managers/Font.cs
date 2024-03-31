using System;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace RankSSpawnHelper.Managers;

internal class Font : IDisposable
{
    public Font()
    {
        const string fontName = "NotoSansCJKsc-Medium.otf";

        var fontPath = Path.Combine(DalamudApi.Interface.DalamudAssetDirectory.FullName, "UIRes", fontName);

        using (ImGuiHelpers.NewFontGlyphRangeBuilderPtrScoped(out var builder))
        {
            builder.AddRanges(ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
            var range = builder.BuildRangesToArray();

            NotoSan24 = DalamudApi.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontFromFile(fontPath, new() { SizePx = 24, GlyphRanges = range })));
            NotoSan18 = DalamudApi.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontFromFile(fontPath, new() { SizePx = 18, GlyphRanges = range })));
        }
    }

    public IFontHandle NotoSan24 { get; private set; }
    public IFontHandle NotoSan18 { get; private set; }

    public void Dispose()
    {
        NotoSan18.Dispose();
        NotoSan24.Dispose();
    }
}