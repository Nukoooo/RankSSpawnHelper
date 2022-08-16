using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace RankSSpawnHelper.Misc;

internal static class Utils
{
    private static readonly List<string> LuXingNiaoServers = new()
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

    private static readonly List<string> MoGuliServers = new()
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

    private static readonly List<string> MaoXiaoPangServers = new()
    {
        "紫水栈桥",
        "延夏",
        "静语庄园",
        "摩杜纳",
        "海猫茶屋",
        "柔风海湾",
        "琥珀原"
    };

    private static readonly List<string> DouDouChaiServers = new()
    {
        "水晶塔",
        "银泪湖",
        "太阳海岸",
        "伊修加德",
        "红茶川"
    };
    
    public static void Initialize()
    {

    }

    public static List<string> GetServers()
    {
        var dcRowId = Service.ClientState.LocalPlayer.HomeWorld.GameData.DataCenter.Value.RowId;
        if (dcRowId == 0)
        {
            var homeWorldName = Service.ClientState.LocalPlayer.HomeWorld.GameData.Name.RawString;
            // very ugly code yes
            if (LuXingNiaoServers.Contains(homeWorldName))
                return LuXingNiaoServers;

            if (MoGuliServers.Contains(homeWorldName))
                return MoGuliServers;

            if (MaoXiaoPangServers.Contains(homeWorldName))
                return MaoXiaoPangServers;

            if (DouDouChaiServers.Contains(homeWorldName))
                return DouDouChaiServers;

            throw new IndexOutOfRangeException("aaaaaaaaaaaaaaaaaaaaaaa");
        }

        var worlds = Service.DataManager.GetExcelSheet<World>()?.Where(world => world.DataCenter.Value?.RowId == dcRowId).ToList();

        return worlds?.Select(world => world.Name).Select(dummy => dummy.RawString).ToList();
    }
}