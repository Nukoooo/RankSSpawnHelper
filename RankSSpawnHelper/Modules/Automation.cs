using System.Numerics;
using Dalamud.Interface.Colors;
using ImGuiNET;
using OtterGui.Widgets;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Modules.Automations;

namespace RankSSpawnHelper.Modules;

// TODO: 功能模块化
internal unsafe class Automation : IUiModule
{
    private readonly Configuration _configuration;

    private readonly ItemAutomation _itemAutomation;
    private readonly AutoLeaveDuty  _autoLeaveDuty;
    private readonly SummonMinion   _summonMinion;

    private readonly ISigScannerModule _sigScanner;

    public Automation(Configuration configuration, ISigScannerModule sigScanner, ICommandHandler commandHandler)
    {
        _configuration = configuration;
        _sigScanner    = sigScanner;

        _itemAutomation = new (sigScanner, commandHandler, configuration);
        _autoLeaveDuty  = new (configuration);
        _summonMinion   = new (configuration);
    }

    public bool Init() =>
        _itemAutomation.Init() && _autoLeaveDuty.Init() && _summonMinion.Init();

    public void Shutdown()
    {
        _itemAutomation.Shutdown();
        _autoLeaveDuty.Shutdown();
        _summonMinion.Shutdown();
    }

    public string UiName => "(半)自动相关";

    public void OnDrawUi()
    {
        Widget.BeginFramedGroup("扔物品", new Vector2(-1, -1));
        _itemAutomation.OnDrawUi();
        Widget.EndFramedGroup();

        var summonMinion = _configuration.AutoSummonMinion;

        if (ImGui.Checkbox("自动召唤宠物", ref summonMinion))
        {
            _configuration.AutoSummonMinion = summonMinion;
            _configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("仅在 延夏/伊尔美格/迷津/天外天垓/湿地 有用");
        }

        ImGui.SameLine();

        var leaveDuty = _configuration.AutoLeaveDuty;

        if (ImGui.Checkbox("自动退本消青魔debuff", ref leaveDuty))
        {
            _configuration.AutoLeaveDuty = leaveDuty;
            _configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("只有 解限 + 单人 + **假火** + 青魔 才有用");
        }
    }
}
