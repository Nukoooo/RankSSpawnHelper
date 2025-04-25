using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RankSSpawnHelper.Managers;

internal interface ISigScannerModule
{
    nint Search(string signature);

    bool TryScan(string signature, out nint address);

    bool TryScanText(string signature, out nint address);
}

internal class SigScanner : ISigScannerModule, IModule
{
    private readonly ProcessModule _module;

    private nint _allocatedBytes;

    private nint _textSectionBaseAddress;
    private int  _textSectionSize;

    public SigScanner()
    {
        _module = Process.GetCurrentProcess().MainModule
                  ?? throw new NullReferenceException("ProcessMainModule is null????");

        Setup();
    }

    public bool Init()
        => true;

    public void Shutdown()
    {
        Marshal.FreeHGlobal(_allocatedBytes);
    }

    private void Setup()
    {
        var baseAddress = _module.BaseAddress;

        // We don't want to read all of IMAGE_DOS_HEADER or IMAGE_NT_HEADER stuff so we cheat here.
        var ntNewOffset = Marshal.ReadInt32(baseAddress, 0x3C);
        var ntHeader    = baseAddress + ntNewOffset;

        // IMAGE_NT_HEADER
        var fileHeader  = ntHeader + 4;
        var numSections = Marshal.ReadInt16(ntHeader, 6);

        // IMAGE_OPTIONAL_HEADER
        var optionalHeader = fileHeader + 20;

        var sectionHeader = optionalHeader + 240;

        var sectionCursor = sectionHeader;

        for (var i = 0; i < numSections; i++)
        {
            var sectionName = Marshal.ReadInt64(sectionCursor);

            // .text
            switch (sectionName)
            {
                case 0x747865742E: // .text
                    var textSectionOffset = Marshal.ReadInt32(sectionCursor, 12);
                    var textSectionSize   = Marshal.ReadInt32(sectionCursor, 8);

                    var fileBytes = File.ReadAllBytes(_module.FileName);

                    _allocatedBytes = Marshal.AllocHGlobal(textSectionSize);

                    var pointerToRawData = Marshal.ReadInt32(sectionCursor, 20);

                    Marshal.Copy(fileBytes.AsSpan(pointerToRawData, textSectionSize).ToArray(),
                                 0,
                                 _allocatedBytes,
                                 textSectionSize);

                    _textSectionBaseAddress = baseAddress + textSectionOffset;
                    _textSectionSize        = textSectionSize;

                    return;
            }

            sectionCursor += 40;
        }
    }

    public unsafe nint Search(string signature)
    {
        if (!Avx2.IsSupported)
        {
            return DalamudApi.SigScanner.ScanText(signature);
        }

        var split = signature.Split(' ');

        Span<short> result = stackalloc short[split.Length];

        for (var i = 0; i < split.Length; i++)
        {
            var str = split[i];

            if (str[0] == '?')
            {
                result[i] = -1;

                continue;
            }

            result[i] = Convert.ToByte(str, 16);
        }

        var textPtr  = (byte*) _allocatedBytes;
        var textSpan = new Span<byte>(textPtr, _textSectionSize);

        if (!FindPatternAvx2(result, textPtr, out var position))
        {
            return 0;
        }

        if (position == 0)
        {
            return 0;
        }

        if (textSpan[position] == 0xE8 || textSpan[position] == 0xE9)
        {
            var displacement = *(int*) (textPtr + position + 1);
            position += displacement + sizeof(int) + 1;
        }

        return _textSectionBaseAddress + position;
    }

    public bool TryScan(string signature, out IntPtr address)
    {
        address = Search(signature);

        return address != nint.Zero;
    }

    public bool TryScanText(string signature, out IntPtr address)
    {
        address = Search(signature);

        return address != nint.Zero;
    }

    private unsafe bool FindPatternAvx2(Span<short> signatureSpan,
                                        byte*       data,
                                        out int     position)
    {
        var firstByte = (byte) signatureSpan[0];

        var first = Vector256.Create(firstByte);
        var last  = Vector256.Create((byte) signatureSpan[^1]);

        position = 0;

        var index = 0;

        for (; index < _textSectionSize; index += 32)
        {
            var blockFirst = Avx.LoadVector256(data                                  + index);
            var blockLast  = Avx.LoadVector256((data + index + signatureSpan.Length) - 1);

            var eqFirst = Avx2.CompareEqual(first, blockFirst);
            var eqLast  = Avx2.CompareEqual(last,  blockLast);

            var combined = Avx2.And(eqFirst, eqLast);
            var mask     = Avx2.MoveMask(combined);

            while (mask != 0)
            {
                var bitpos = BitOperations.TrailingZeroCount(mask);

                var bytes = new Span<byte>(data + index + bitpos, signatureSpan.Length);

                var found = true;

                for (var j = 1; j < signatureSpan.Length; j++)
                {
                    if (signatureSpan[j] == -1 || signatureSpan[j] == bytes[j])
                    {
                        continue;
                    }

                    found = false;

                    break;
                }

                if (found)
                {
                    position = index + bitpos;

                    return true;
                }

                mask &= mask - 1;
            }
        }

        return false;
    }
}