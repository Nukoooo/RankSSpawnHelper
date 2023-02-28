#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Logging;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Managers.DataManagers
{
    internal class MapTexture : IDisposable
    {
        private readonly ExcelSheet<Map> _map;
        private readonly Dictionary<uint, MapTextureInfo> _textures = new();

        public MapTexture()
        {
            _map = DalamudApi.DataManager.GetExcelSheet<Map>()!;
        }

        public void Dispose()
        {
            foreach (var (_, v) in _textures)
            {
                v?.texture?.Dispose();
            }

            _textures.Clear();
        }

        public void AddMapTexture(uint territory, Map map)
        {
            if (_textures.ContainsKey(map.RowId))
                return;

            Task.Run(() =>
                     {
                         try
                         {
                             var path    = GetPathFromMap(map);
                             var texture = GetTexture(path);
                             if (texture != null && texture.ImGuiHandle != IntPtr.Zero)
                             {
                                 PluginLog.Debug($"Added mapid: {map.RowId}");
                                 _textures[map.RowId] = new MapTextureInfo
                                                        {
                                                            texture   = texture,
                                                            size      = new Vector2(texture.Width, texture.Height),
                                                            mapId     = map.RowId,
                                                            territory = territory
                                                        };
                             }
                             else
                             {
                                 texture?.Dispose();
                             }
                         }
                         catch (Exception)
                         {
                             PluginLog.Error($"Exception occurred when loading map texture for id: {map.RowId}");
                         }
                     });
        }

        public MapTextureInfo? GetTexture(uint map)
        {
            return _textures.ContainsKey(map) ? _textures[map] : null;
        }

        private static TextureWrap? GetTexture(string path)
        {
            if (path[0] is not ('/' or '\\') && path[1] != ':')
                return DalamudApi.DataManager.GetImGuiTexture(path);
            var texFile = DalamudApi.DataManager.GameData.GetFileFromDisk<TexFile>(path);
            return DalamudApi.DataManager.GetImGuiTexture(texFile);
        }

        private static string GetPathFromMap(Map map)
        {
            var mapKey = map.Id.RawString;
            var rawKey = mapKey.Replace("/", "");
            return $"ui/map/{mapKey}/{rawKey}_m.tex";
        }
    }
}