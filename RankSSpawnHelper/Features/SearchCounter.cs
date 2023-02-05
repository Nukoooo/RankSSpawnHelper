using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace RankSSpawnHelper.Features
{
    internal class SearchCounter : IDisposable
    {
        private readonly List<ulong> _playerIds = new();

        public SearchCounter()
        {
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

            if (original != (IntPtr)1)
            {
                Plugin.Print($"按照当前搜索的设置, 和你在同一张地图里大约有 {_playerIds.Count} 人.");
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
            public ulong CommunityID;
            public ushort NextIndex;
            public ushort Index;
            public byte ListType;
            public byte RequestKey;
            public byte RequestParam;
            public byte __padding1;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public SearchPlayerEntry[] entries;
        }

        private delegate IntPtr ProcessSocialListPacketDelegate(IntPtr a1, IntPtr packetData);
    }
}