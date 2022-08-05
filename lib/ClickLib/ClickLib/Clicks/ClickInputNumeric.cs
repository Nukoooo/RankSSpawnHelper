using System;
using System.Runtime.InteropServices;
using ClickLib.Attributes;
using ClickLib.Bases;
using ClickLib.Exceptions;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ClickLib.Clicks;
[StructLayout(LayoutKind.Explicit, Size = 233)]
public struct AddonInputNumeric
{
    [FieldOffset(0)]
    public AtkUnitBase AtkUnitBase;
}

public sealed unsafe class ClickInputNumeric : ClickBase<ClickInputNumeric, AddonInputNumeric>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickInputNumeric"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickInputNumeric(IntPtr addon = default)
        : base("InputNumeric", addon)
    {
    }

    public static implicit operator ClickInputNumeric(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickInputNumeric Using(IntPtr addon) => new(addon);

    public void SetValue(int value, bool notLargerThanMax = false)
    {
        var inputAddon = (AtkComponentNumericInput*)this.Addon->AtkUnitBase.UldManager.NodeList[4]->GetComponent();
        if (inputAddon == null)
        {
            throw new InvalidClickException("inputAddon is not available");
        }

        inputAddon->SetValue(notLargerThanMax ? Math.Min(value, inputAddon->Data.Max) : value);
    }

    public void ClickConfirm()
    {
        var confirmButton = (AtkComponentButton*)this.Addon->AtkUnitBase.UldManager.NodeList[3]->GetComponent();
        if (confirmButton == null)
        {
            throw new InvalidClickException("confirmButton is not available");
        }

        this.ClickAddonButton(confirmButton, 0);
    }

    public void ClickCancel()
    {
        var confirmButton = (AtkComponentButton*)this.Addon->AtkUnitBase.UldManager.NodeList[2]->GetComponent();
        if (confirmButton == null)
        {
            throw new InvalidClickException("confirmButton is not available");
        }

        this.ClickAddonButton(confirmButton, 0);
    }
}