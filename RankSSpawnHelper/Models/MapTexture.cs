using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiScene;

namespace RankSSpawnHelper.Models
{
    internal class MapTextureInfo
    {
        public TextureWrap texture;
        public Vector2 size;
        public Vector2 center;
        public readonly float Scale = 0.25f;
        public ushort? sizeFactor;
        public short? offsetX;
        public short? offsetY;

        public Vector2 GetTextureOffsetPosition(Vector2 coordinates)
        {
            return coordinates + size / 2.0f;
        }

        public Vector2 GetTexturePosition(Vector2 coordinates)
        {
            return new Vector2(ConvertMapCoordToWorldCoordXZ(coordinates.X, (uint)sizeFactor, (int)offsetX), ConvertMapCoordToWorldCoordXZ(coordinates.Y, (uint)sizeFactor, (int)offsetX)) * Scale;
        }

        internal static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
        {
            float num         = scale / 100f;
            var   rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
            return ConvertRawPositionToMapCoordinate(rawPosition, scale);
        }
        internal static float ConvertRawPositionToMapCoordinate(int pos, float scale)
        {
            float num = scale / 100f;
            return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
        }

        private static float ConvertMapCoordToWorldCoordXZ(float value, uint scale, int offset)
        {
            return (value - 0.02f * offset - 2048f / scale - 1f) / 0.02f;
        }

    }
}
