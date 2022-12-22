using System.Numerics;
using ImGuiScene;

namespace RankSSpawnHelper.Models
{
    internal class MapTextureInfo
    {
        public uint mapId;
        public uint territory;
        public readonly float Scale = 0.25f;
        public Vector2 size;
        public TextureWrap texture;
        
        public Vector2 GetTexturePosition(Vector2 coord)
        {
            return new Vector2(FlagToPixelCoord(Scale, coord.X), FlagToPixelCoord(Scale, coord.Y));
        }
        
        private static float FlagToPixelCoord(float scale, float flag, int resolution = 2048)
        {
            return (flag - 1f) * 50f * scale * (float)resolution / 2048f;
        }
    }
}