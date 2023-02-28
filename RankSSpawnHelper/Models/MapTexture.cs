using System.Numerics;
using ImGuiScene;

namespace RankSSpawnHelper.Models
{
    internal class MapTextureInfo
    {
        public readonly float Scale = 0.25f;
        public uint mapId;
        public Vector2 size;
        public uint territory;
        public TextureWrap texture;

        public Vector2 GetTexturePosition(Vector2 coord)
        {
            return new Vector2(FlagToPixelCoord(Scale, coord.X), FlagToPixelCoord(Scale, coord.Y));
        }

        private static float FlagToPixelCoord(float scale, float flag, int resolution = 2048)
        {
            return (flag - 1f) * 50f * scale * resolution / 2048f;
        }
    }
}