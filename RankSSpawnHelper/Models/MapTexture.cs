using System;
using System.Numerics;
using ImGuiScene;

namespace RankSSpawnHelper.Models;

internal class MapTextureInfo
{
    public readonly float       Scale = 0.25f;
    public          uint        mapId;
    public          Vector2     size;
    public          uint        territory;
    public          TextureWrap texture;
    public          float       SizeFactor;

    public Vector2 GetTexturePosition(Vector2 coord)
    {
        return new Vector2(FlagToPixelCoord(SizeFactor, coord.X), FlagToPixelCoord(SizeFactor, coord.Y));
    }

    private static float FlagToPixelCoord(float scale, float flag, int resolution = 2048)
    {
        return (flag - 1f) * scale * 0.01f / 40.85f * resolution;
    }
}