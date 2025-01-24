using System.Numerics;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Widgets;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace RankSSpawnHelper.Modules;

// TODO: 功能模块化
internal unsafe class Automation : IUiModule
{
    private record ItemInfo
    {
        public ItemInfo(uint id, string name)
        {
            Id   = id;
            Name = name;
        }

        public uint   Id   { get; }
        public string Name { get; }
    }

    private readonly Configuration _configuration;

    private readonly List<ItemInfo>                     _items = [];
    private          bool                               _discarded;
    private          Hook<OpenInventoryContextDelegate> _openInventoryContextHook;

    private       string          _searchText = string.Empty;
    private const ImGuiTableFlags TableFlags  = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;

    private readonly Dictionary<ushort, uint> _minionMap = new ()
    {
        { 1178, 180 },
        { 960, 423 },
        { 816, 303 },
        { 956, 434 },
        { 614, 215 },
        { 397, 148 },
    };
    private readonly List<uint> _unlockedMinion = [];
    private          DateTime   _lastUpDateTime;

    public Automation(Configuration configuration)
    {
        _configuration = configuration;

        _items.AddRange(DalamudApi.DataManager.GetExcelSheet<Item>()!
                                  .Where(i => !string.IsNullOrEmpty(i.Name)
                                              && (
                                                  (i.FilterGroup               == 4
                                                   && i.LevelItem.Value?.RowId == 1
                                                   && !i.IsUnique)        // 普通装备且装等为1的物品 比如草布马裤，超级米饭的斗笠
                                                  || (i.FilterGroup == 12 // 材料比如矮人棉，庵摩罗果等 但因为秧鸡胸脯肉，厄尔庇斯鸟蛋和鱼粉是特定地图扔的，所以不会加进列表里
                                                      && i.RowId    != 36256
                                                      && i.RowId    != 27850
                                                      && i.RowId    != 7767)
                                                  || i.FilterGroup == 17 // 鱼饵，比如沙蚕
                                              ))
                                  .Select(i => new ItemInfo(i.RowId, i.Name)));

        if (DalamudApi.ClientState.IsLoggedIn)
        {
            ClientState_OnLogin();
        }
    }

    public bool Init()
    {
        if (!DalamudApi.SigScanner.TryScanText("83 B9 ?? ?? ?? ?? ?? 7E ?? 39 91", out var address))
        {
            return false;
        }

        _openInventoryContextHook
            = DalamudApi.GameInterop.HookFromAddress<OpenInventoryContextDelegate>(address, hk_OpenInventoryContext);

        _openInventoryContextHook.Enable();
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "InputNumeric", AddonInputNumericHandler);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno",  AddonSelectYesnoHandler);

        DalamudApi.ClientState.Login += ClientState_OnLogin;
        DalamudApi.Framework.Update  += Framework_OnUpdate;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "InputNumeric", AddonInputNumericHandler);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno",  AddonSelectYesnoHandler);

        DalamudApi.ClientState.Login -= ClientState_OnLogin;
        DalamudApi.Framework.Update  -= Framework_OnUpdate;

        _openInventoryContextHook?.Dispose();
    }

    public string UiName => "(半)自动相关";

    public void OnDrawUi()
    {
        Widget.BeginFramedGroup("扔物品", new Vector2(-1, -1));
        DrawDiscardItemTab();
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
    }

    private void Framework_OnUpdate(IFramework framework)
    {
        if (DateTime.Now - _lastUpDateTime <= TimeSpan.FromSeconds(2))
        {
            return;
        }

        if (!_configuration.AutoSummonMinion)
        {
            goto end;
        }

        var territoryType = DalamudApi.ClientState.TerritoryType;

        if (!_minionMap.TryGetValue(territoryType, out var minionId))
        {
            goto end;
        }

        if (!_unlockedMinion.Contains(minionId))
        {
            goto end;
        }

        if (DalamudApi.Condition[ConditionFlag.Mounted]
            || DalamudApi.Condition[ConditionFlag.Mounted2]
            || DalamudApi.Condition[ConditionFlag.Unknown57]
            || DalamudApi.Condition[ConditionFlag.Mounting]
            || DalamudApi.Condition[ConditionFlag.Mounting71])
        {
            goto end;
        }

        if (!CanUseAction(minionId))
        {
            goto end;
        }

        if (DalamudApi.ObjectTable[1] == null && CanUseAction(minionId))
        {
            UseAction(minionId);

            goto end;
        }

        var obj = DalamudApi.ObjectTable[1];

        if (obj == null)
        {
            UseAction(minionId);

            goto end;
        }

        if (obj.ObjectKind != ObjectKind.Companion)
        {
            UseAction(minionId);

            goto end;
        }

        if (!CanUseAction(minionId))
        {
            goto end;
        }

        if (obj.DataId == minionId)
        {
            goto end;
        }

        UseAction(minionId);

    end:
        _lastUpDateTime = DateTime.Now;

        return;

        static bool CanUseAction(uint id)
            => ActionManager.Instance()->GetActionStatus(ActionType.Companion, id) == 0
               && !ActionManager.Instance()->IsRecastTimerActive(ActionType.Action, id);

        static void UseAction(uint id)
        {
            ActionManager.Instance()->UseAction(ActionType.Companion, id);
        }
    }

    private void ClientState_OnLogin()
    {
        _unlockedMinion.Clear();

        foreach (var u in _minionMap.Where(u => UIState.Instance()->IsCompanionUnlocked(u.Value)))
        {
            _unlockedMinion.Add(u.Value);
        }
    }

    private void DrawDiscardItemTab()
    {
        var enabled = _configuration.AutoDiscardItem;

        if (ImGui.Checkbox("启用##自动扔物品", ref enabled))
        {
            _configuration.AutoDiscardItem = enabled;
            _configuration.Save();
        }

        using (ImRaii.Disabled(!enabled))
        {
            ImGui.InputTextWithHint("##搜索物品名字", "输入你要搜索的物品名", ref _searchText, 256);

            ImGui.Text("物品列表:");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("如果搜索框里没有输入文本或者搜索无结果，表格就会显示已添加的物品\n如果搜索无结果就会显示已添加的物品\n加号是添加，减号是移除，如果这都看不懂那你就是猪猪");
            }

            var result = _items.Where(i => i.Name.Contains(_searchText))
                               .ToArray();

            var isEmpty = _searchText == string.Empty || result.Length == 0;

            using (ImRaii.Table("##可选择的物品列表",
                                2,
                                BuildFlag(TableFlags, isEmpty),
                                new (-ImGui.GetStyle().FramePadding.X * 2, 225)))

            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("物品名字");

                ImGui.TableSetupColumn("##按钮操作",
                                       ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize,
                                       20 * ImGuiHelpers.GlobalScale);

                ImGui.TableHeadersRow();

                if (isEmpty)
                {
                    result = _items.Where(i => _configuration.ItemsToDiscard.Contains(i.Id))
                                   .ToArray();

                    if (result.Length > 0)
                    {
                        foreach (var info in result)
                        {
                            using (ImRaii.PushId($"##物品{info.Id}"))
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();

                                ImGui.Text(info.Name);
                                ImGui.TableNextColumn();

                                using (ImRaii.PushFont(UiBuilder.IconFont))
                                {
                                    using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding,
                                                            new Vector2(2, ImGui.GetStyle().FramePadding.Y)))
                                    {
                                        if (ImGui.Button("\xF068"))
                                        {
                                            _configuration.ItemsToDiscard.Remove(info.Id);
                                            _configuration.Save();
                                        }
                                    }
                                }
                            }
                        }

                        return;
                    }

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("-");
                    ImGui.TableNextColumn();

                    return;
                }

                foreach (var info in result)
                {
                    using (ImRaii.PushId($"##物品{info.Id}"))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.Text(info.Name);
                        ImGui.TableNextColumn();

                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding,
                                                    new Vector2(2, ImGui.GetStyle().FramePadding.Y)))
                            {
                                var isInItemList = _configuration.ItemsToDiscard.Contains(info.Id);

                                if (ImGui.Button(isInItemList ? "\xF068" : "\xF067"))
                                {
                                    if (isInItemList)
                                    {
                                        _configuration.ItemsToDiscard.Remove(info.Id);
                                    }
                                    else
                                    {
                                        _configuration.ItemsToDiscard.Add(info.Id);
                                    }

                                    _configuration.Save();
                                }
                            }
                        }
                    }
                }
            }
        }

        return;

        static ImGuiTableFlags BuildFlag(ImGuiTableFlags flags, bool empty)
        {
            if (!empty)
            {
                flags |= ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable;
            }

            return flags;
        }
    }

    private void* hk_OpenInventoryContext(AgentInventoryContext* agent,
                                          InventoryType          inventoryType,
                                          ushort                 slot,
                                          int                    a4,
                                          ushort                 a5,
                                          byte                   a6)
    {
        var original = _openInventoryContextHook.Original(agent, inventoryType, slot, a4, a5, a6);

        var territoryType = DalamudApi.ClientState.TerritoryType;

        if (DalamudApi.GameGui.GetAddonByName("InventoryBuddy")    != IntPtr.Zero
            || DalamudApi.GameGui.GetAddonByName("InventoryEvent") != IntPtr.Zero
            || DalamudApi.GameGui.GetAddonByName("ArmouryBoard")   != IntPtr.Zero)
        {
            return original;
        }

        if (territoryType != 961 && territoryType != 813 && territoryType != 621 && territoryType != 1189)
        {
            return original;
        }

        var inventory = InventoryManager.Instance()->GetInventoryContainer(inventoryType);

        if (inventory == null)
        {
            return original;
        }

        var itemSlot = inventory->GetInventorySlot(slot);

        if (itemSlot == null)
        {
            return original;
        }

        var itemId   = itemSlot->ItemId;
        var quantity = itemSlot->Quantity;

        if (!IsAllowedToDiscard(itemId))
        {
            return original;
        }

        switch (territoryType)
        {
            case 961 when itemId  != 36256:
            case 813 when itemId  != 27850:
            case 1189 when itemId != 7767:
                return original;
        }

        var addonId = agent->AgentInterface.GetAddonId();

        if (addonId == 0)
        {
            return original;
        }

        var addon = RaptureAtkUnitManager.Instance()->GetAddonById((ushort) addonId);

        if (addon == null)
        {
            return original;
        }

        var atkArray = stackalloc AtkValue[4];
        atkArray[0].SetInt(0);
        atkArray[2].SetInt(0);
        atkArray[3].SetInt(0);

        for (var i = 0; i < agent->ContextItemCount; i++)
        {
            var contextItemParam = agent->EventParams[agent->ContexItemStartIndex + i];

            if (contextItemParam.Type != ValueType.String)
            {
                continue;
            }

            atkArray[1].SetInt(i);

            var name = contextItemParam.GetValueAsString();

            switch (name)
            {
                case "拆分":
                    switch (territoryType)
                    {
                        case 961 when quantity  <= 5:
                        case 813 when quantity  == 1:
                        case 621 when quantity  == 1:
                        case 1189 when quantity <= 50:
                            continue;
                        default:
                            atkArray[1].SetInt(i);
                            addon->FireCallback(4, atkArray);

                            return original;
                    }

                case "舍弃":
                    switch (territoryType)
                    {
                        // 湖区只能扔一件
                        case 961 when quantity  != 5:
                        case 813 when quantity  != 1:
                        case 621 when quantity  != 1:
                        case 1189 when quantity != 50:
                            continue;
                    }

                    addon->FireCallback(4, atkArray);
                    _discarded = true;

                    return original;
            }
        }

        return original;
    }

    private void AddonInputNumericHandler(AddonEvent type, AddonArgs args)
    {
        if (!_configuration.AutoDiscardItem)
        {
            return;
        }

        var territoryType = DalamudApi.ClientState.TerritoryType;

        if (territoryType != 961 && territoryType != 813 && territoryType != 621 && territoryType != 1189)
        {
            return;
        }

        var addon = (AtkUnitBase*) args.Addon;

        if (addon->ContextMenuParentId == 0)
        {
            return;
        }

        // 检查附属的是哪个addon
        var parentAddon = RaptureAtkUnitManager.Instance()->GetAddonById(addon->ContextMenuParentId);

        var parentAddonName = Encoding.UTF8.GetString(parentAddon->Name);

        // 傻逼SE我操你妈
        if (!parentAddonName.StartsWith("Inventory"))
        {
            return;
        }

        var inventoryContext = AgentInventoryContext.Instance();

        var itemId = inventoryContext->TargetDummyItem.ItemId;

        if (!IsAllowedToDiscard(itemId))
        {
            return;
        }

        var isEgg      = itemId == 36256;
        var isFishMeal = itemId == 7767;

        if (isEgg && inventoryContext->TargetDummyItem.Quantity < 5)
        {
            return;
        }

        if (isFishMeal && inventoryContext->TargetDummyItem.Quantity < 50)
        {
            return;
        }

        var numericAddon = (AtkComponentNumericInput*) addon->UldManager.NodeList[4]->GetComponent();

        var numVal = isEgg ? Math.Min(5,  numericAddon->Data.Max) :
            isFishMeal     ? Math.Min(50, numericAddon->Data.Max) :
                             1;

        numericAddon->SetValue(numVal);
        var confirmButton = addon->UldManager.NodeList[3]->GetAsAtkComponentButton();

        ClickAddonButton(addon, confirmButton, 0, AtkEventType.ButtonClick);
    }

    private void AddonSelectYesnoHandler(AddonEvent type, AddonArgs args)
    {
        if (!_discarded)
        {
            return;
        }

        _discarded = false;
        var addon = (AddonSelectYesno*) args.Addon;
        ClickAddonButton(&addon->AtkUnitBase, addon->YesButton, 0, AtkEventType.ButtonClick);
    }

    private bool IsAllowedToDiscard(uint id)
    {
        if (_configuration.ItemsToDiscard.Contains(id))
        {
            return true;
        }

        var territoryType = DalamudApi.ClientState.TerritoryType;

        return (territoryType    == 961  && id == 36256)
               || (territoryType == 813  && id == 27850)
               || (territoryType == 1189 && id == 7767);
    }

    private static void ClickAddonButton(AtkUnitBase* unitBase, AtkComponentButton* target, uint which, AtkEventType type)
    {
        var atkEvent = stackalloc AtkEvent[1];
        atkEvent->Listener = &unitBase->AtkEventListener;
        atkEvent->Target   = (AtkEventTarget*) target->AtkComponentBase.OwnerNode;
        var atkEventData = stackalloc AtkEventData[1];

        unitBase->AtkEventListener.ReceiveEvent(type, (int) which, atkEvent, atkEventData);
    }

    /*private static void ClickAddonComponent(AtkUnitBase* unitBase, AtkComponentNode* target, uint which, AtkEventType type)
    {
        var atkEvent = stackalloc AtkEvent[1];
        atkEvent->Listener = &unitBase->AtkEventListener;
        atkEvent->Target   = (AtkEventTarget*) target;
        var atkEventData = stackalloc AtkEventData[1];

        unitBase->AtkEventListener.ReceiveEvent(type, (int) which, atkEvent, atkEventData);
    }*/

    private delegate void* OpenInventoryContextDelegate(AgentInventoryContext* agent,
                                                        InventoryType          inventory,
                                                        ushort                 slot,
                                                        int                    a4,
                                                        ushort                 a5,
                                                        byte                   a6);
}
