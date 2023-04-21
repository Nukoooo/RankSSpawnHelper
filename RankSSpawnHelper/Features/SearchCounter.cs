using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features
{
    internal class SearchCounter : IDisposable
    {
        private readonly List<ulong> _playerIds = new();
        private int _searchCount;
        private readonly IntPtr rdataBegin = IntPtr.Zero;
        private readonly IntPtr rdataEnd = IntPtr.Zero;

        public SearchCounter()
        {
            rdataBegin = DalamudApi.SigScanner.RDataSectionBase;
            rdataEnd   = DalamudApi.SigScanner.RDataSectionBase + DalamudApi.SigScanner.RDataSectionSize;
            PluginLog.Debug($".data begin: {DalamudApi.SigScanner.DataSectionBase:X}, end: {DalamudApi.SigScanner.DataSectionBase + DalamudApi.SigScanner.DataSectionSize:X}, offset: {DalamudApi.SigScanner.DataSectionOffset}");
            PluginLog.Debug($".rdata begin: {DalamudApi.SigScanner.RDataSectionBase:X}, end: {DalamudApi.SigScanner.RDataSectionBase + DalamudApi.SigScanner.RDataSectionSize:X}, offset: {DalamudApi.SigScanner.RDataSectionOffset}");
            PluginLog.Debug($".text begin: {DalamudApi.SigScanner.TextSectionBase:X}, end: {DalamudApi.SigScanner.TextSectionBase + DalamudApi.SigScanner.TextSectionSize:X}, offset: {DalamudApi.SigScanner.TextSectionOffset}");
            PluginLog.Debug($"{DalamudApi.SigScanner.SearchBase:X}");

            SignatureHelper.Initialise(this);
            ProcessSocailListPacket.Enable();
            DalamudApi.Condition.ConditionChange += Condition_ConditionChange;
        }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        [Signature("48 89 5C 24 ?? 56 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 48 8B F2", DetourName = nameof(Detour_ProcessSocialListPacket))]
        private Hook<ProcessSocialListPacketDelegate> ProcessSocailListPacket { get; init; } = null!;

        public void Dispose()
        {
            ProcessSocailListPacket.Dispose();
        }

        private void Condition_ConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.BetweenAreas51 || value)
            {
                return;
            }

            _playerIds.Clear();
            _searchCount = 0;
        }

        private IntPtr Detour_ProcessSocialListPacket(IntPtr a1, IntPtr packetData)
        {
            var original         = ProcessSocailListPacket.Original(a1, packetData);
            var socialListStruct = Marshal.PtrToStructure<SocialList>(packetData);
            if (socialListStruct.ListType != 4) // we only need results from player search
                return original;

            var currentTerritoryId = DalamudApi.ClientState.TerritoryType;

            foreach (var playerEntry in socialListStruct.entries)
            {
                if (playerEntry.territoryType != currentTerritoryId)
                    continue;
                if (_playerIds.Contains(playerEntry.Id))
                {
                    PluginLog.Debug($"Found ID {playerEntry.Id:X} in player list, ignoring");
                    continue;
                }

                _playerIds.Add(playerEntry.Id);
                PluginLog.Debug($"TerritoryId: {playerEntry.territoryType} | PlayerUniqueId: {playerEntry.Id:X}");
            }

            PluginLog.Debug($"{original:X} | {packetData:X}");

            // sometimes original would be at .rdata section
            // so we are gonna skip that
            if (original == (IntPtr)1 || (original >= (nint?)rdataBegin && original <= (nint?)rdataEnd))
                return original;

            _searchCount++;
            Plugin.Print(new List<Payload>
                         {
                             new TextPayload("在经过 "),
                             new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                             new TextPayload($"{_searchCount} "),
                             new UIForegroundPayload(0),
                             new TextPayload("次搜索后, 和你在同一张图里大约有 "),
                             new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                             new TextPayload($"{_playerIds.Count} "),
                             new UIForegroundPayload(0),
                             new TextPayload("人.")
                         });

            if (Plugin.Configuration.PlayerSearchTip)
            {
                Plugin.Print(new List<Payload>
                             {
                                 new TextPayload("对计数有疑问?或者不知道怎么用? 可以试试下面的方法: " +
                                                 "\n1. 先搜当前地图人数(不选择任何军队以及其他选项,只选地图)" +
                                                 "\n2. 如果得到的人数超过200,那就只选一个军队进行搜索" +
                                                 "\n      比如: 先搜双蛇,再搜恒辉,最后搜黑涡,以此反复循环" +
                                                 "\n3. 得到的人数将会是这几次搜索的总人数(已经排除重复的人)"),
                                 new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                                 new TextPayload("\n本消息可以在 设置 -> 其他 里关掉")
                             });
            }

            return original;
        }

        [StructLayout(LayoutKind.Explicit, Size = 88, Pack = 1)]
        public struct SearchPlayerEntry
        {
            [FieldOffset(0x0)]
            public ulong Id;

            [FieldOffset(0x14)]
            public uint territoryType;
        }

        [StructLayout(LayoutKind.Sequential, Size = 896)]
        public struct SocialList
        {
            public ulong CommunityID; // 0
            public ushort NextIndex; // 8
            public ushort Index; // 10
            public byte ListType; // 12
            public byte RequestKey; // 13
            public byte RequestParam; // 14
            public byte __padding1; // 15

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public SearchPlayerEntry[] entries;
        }

        private delegate IntPtr ProcessSocialListPacketDelegate(IntPtr a1, IntPtr packetData);
    }
}