using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RankSSpawnHelper.Misc;

// https://stackoverflow.com/a/64582157 
public static class ZipExtensions
{
    public static string CompressToBase64(this string data) => Convert.ToBase64String(Encoding.UTF8.GetBytes(data).Compress());

    public static string DecompressFromBase64(this string data) => Encoding.UTF8.GetString(Convert.FromBase64String(data).Decompress());

    public static byte[] Compress(this byte[] data)
    {
        using var sourceStream = new MemoryStream(data);
        using var destinationStream = new MemoryStream();

        sourceStream.CompressTo(destinationStream);
        return destinationStream.ToArray();
    }

    public static byte[] Decompress(this byte[] data)
    {
        using var sourceStream = new MemoryStream(data);
        using var destinationStream = new MemoryStream();

        sourceStream.DecompressTo(destinationStream);
        return destinationStream.ToArray();
    }

    public static void CompressTo(this Stream stream, Stream outputStream)
    {
        using var gZipStream = new GZipStream(outputStream, CompressionMode.Compress);

        stream.CopyTo(gZipStream);
        gZipStream.Flush();
    }

    public static void DecompressTo(this Stream stream, Stream outputStream)
    {
        using var gZipStream = new GZipStream(stream, CompressionMode.Decompress);

        gZipStream.CopyTo(outputStream);
    }
}