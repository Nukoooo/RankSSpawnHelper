﻿#nullable enable
using System;
using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Logging;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Models;

namespace RankSSpawnHelper.Managers.DataManagers;

internal class MapTexture : IDisposable
{
    private readonly ConcurrentDictionary<uint, MapTextureInfo> _textures = new();

    public void Dispose()
    {
        foreach (var (_, v) in _textures)
        {
            v.texture?.Dispose();
        }

        _textures.Clear();
    }

    public void AddMapTexture(uint territory, Map map)
    {
        try
        {
            var path    = GetPathFromMap(map);
            var texture = GetTexture(path);
            if (texture != null && texture.ImGuiHandle != nint.Zero)
            {
                PluginLog.Debug($"Added mapid: {map.RowId}");
                _textures.TryAdd(map.RowId, new MapTextureInfo
                {
                    texture   = texture,
                    size      = new Vector2(texture.Width, texture.Height),
                    mapId     = map.RowId,
                    territory = territory,
                });
            }
            else
            {
                texture?.Dispose();
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Exception occurred when loading map texture for id: {map.RowId}。 {ex.Message}");
        }
    }

    public MapTextureInfo? GetTexture(uint map)
    {
        return _textures.TryGetValue(map, out var texture) ? texture : null;
    }

    private static TextureWrap? GetTexture(string path)
    {
        if (path[0] is not ('/' or '\\') && path[1] != ':')
            return DalamudApi.TextureProvider.GetTextureFromGame(path);

        var texFile = DalamudApi.DataManager.GameData.GetFileFromDisk<TexFile>(path);
        return DalamudApi.TextureProvider.GetTexture(texFile);
    }

    private static string GetPathFromMap(Map map)
    {
        var mapKey = map.Id.RawString;
        var rawKey = mapKey.Replace("/", "");
        return $"ui/map/{mapKey}/{rawKey}_m.tex";
    }
}