using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;

namespace RankSSpawnHelper.Managers
{
    internal class Data
    {
        private readonly List<string> _douDouChaiServers = new()
                                                           {
                                                               "水晶塔",
                                                               "银泪湖",
                                                               "太阳海岸",
                                                               "伊修加德",
                                                               "红茶川"
                                                           };

        private readonly List<string> _luXingNiaoServers = new()
                                                           {
                                                               "红玉海",
                                                               "神意之地",
                                                               "拉诺西亚",
                                                               "幻影群岛",
                                                               "萌芽池",
                                                               "宇宙和音",
                                                               "沃仙曦染",
                                                               "晨曦王座"
                                                           };

        private readonly List<string> _maoXiaoPangServers = new()
                                                            {
                                                                "紫水栈桥",
                                                                "延夏",
                                                                "静语庄园",
                                                                "摩杜纳",
                                                                "海猫茶屋",
                                                                "柔风海湾",
                                                                "琥珀原"
                                                            };

        private readonly List<string> _moGuliServers = new()
                                                       {
                                                           "白银乡",
                                                           "白金幻象",
                                                           "神拳痕",
                                                           "潮风亭",
                                                           "旅人栈桥",
                                                           "拂晓之间",
                                                           "龙巢神殿",
                                                           "梦羽宝境"
                                                       };

        public MapTexture MapTexture;

        public Monster Monster;
        public Player Player;

        public Data()
        {
            Monster    = new Monster();
            Player     = new Player();
            MapTexture = new MapTexture();
        }

        public List<string> GetServers()
        {
            var dcRowId = DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.RowId;
            if (dcRowId == 0)
            {
                var homeWorldName = DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString;
                // very ugly code yes
                if (_luXingNiaoServers.Contains(homeWorldName))
                    return _luXingNiaoServers;

                if (_moGuliServers.Contains(homeWorldName))
                    return _moGuliServers;

                if (_maoXiaoPangServers.Contains(homeWorldName))
                    return _maoXiaoPangServers;

                if (_douDouChaiServers.Contains(homeWorldName))
                    return _douDouChaiServers;

                throw new IndexOutOfRangeException("aaaaaaaaaaaaaaaaaaaaaaa");
            }

            var worlds = DalamudApi.DataManager.GetExcelSheet<World>()?.Where(world => world.DataCenter.Value?.RowId == dcRowId).ToList();

            return worlds?.Select(world => world.Name).Select(dummy => dummy.RawString).ToList();
        }
    }
}