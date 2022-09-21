using RankSSpawnHelper.Ui;

namespace RankSSpawnHelper
{
    internal class Plugin
    {
        internal static Configuration Configuration { get; set; } = null!;
        internal static Features.Features Features { get; set; } = null!;
        internal static Managers.Managers Managers { get; set; } = null!;
        internal static Windows Windows { get; set; } = null!;
    }
}