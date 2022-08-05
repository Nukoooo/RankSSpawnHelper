using System;

using ClickLib.Attributes;
using ClickLib.Bases;

namespace ClickLib.Clicks;

/// <summary>
/// Addon GuildLeve.
/// </summary>
public sealed unsafe class ClickInventoryItemContext : ClickBase<ClickInventoryItemContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickInventoryItemContext"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickInventoryItemContext(IntPtr addon = default)
        : base("wtfisthislol", addon)
    {
    }

    public static implicit operator ClickInventoryItemContext(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickInventoryItemContext Using(IntPtr addon) => new(addon);

    public void Discard() => this.FireCallback(4);

    public void FireCallback(int idx) => this.FireCallback(0, idx, 0U, 0, 0);

}