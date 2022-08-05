using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ClickLib.Clicks;

public sealed unsafe class ClickInventoryGrid : ClickBase<ClickInventoryGrid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickInventoryGrid"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickInventoryGrid(IntPtr addon = default)
        : base("InventoryGrid", addon)
    {
    }

    public static implicit operator ClickInventoryGrid(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickInventoryGrid Using(IntPtr addon) => new(addon);

    [ClickName("ClickDragbox")]
    public void Click(int index)
    {
        // this.UnitBase->UldManager.NodeList[index];
    }
}
