using System;
using System.Runtime.InteropServices;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ClickLib.Clicks;

[StructLayout(LayoutKind.Explicit, Size = 233)]
public struct AddonGuildLeveDifficulty
{
    [FieldOffset(0)]
    public AtkUnitBase AtkUnitBase;
}

public sealed class ClickGuildLeveDifficulty : ClickBase<ClickGuildLeveDifficulty, AddonGuildLeveDifficulty>
{
    public ClickGuildLeveDifficulty(IntPtr addon = default)
        : base("GuildLeveDifficulty", addon)
    {
    }

    public static implicit operator ClickGuildLeveDifficulty(IntPtr addon) => new(addon);

    /// <summary>
    ///     Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickGuildLeveDifficulty Using(IntPtr addon) => new(addon);

    public void Confirm() => this.ClickAddonButtonIndex(10, 0);
}