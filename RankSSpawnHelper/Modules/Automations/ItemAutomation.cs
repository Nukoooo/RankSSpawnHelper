using System.Collections.Frozen;
using System.Numerics;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.Sheets;
using OtterGui.Raii;
using RankSSpawnHelper.Managers;
using ZLinq;

namespace RankSSpawnHelper.Modules.Automations;

internal unsafe class ItemAutomation : IUiModule
{
    private readonly ISigScannerModule _scannerModule;
    private readonly ICommandHandler   _commandHandler;
    private readonly Configuration     _configuration;

    private readonly FrozenSet<ItemInfo> _items;

    private static readonly Dictionary<uint, (uint ItemId, int Amount)> SplittableItemsByTerritory = new ()
    {
        { 621, (0u, 1) },     // 天营门
        { 813, (27850u, 1) }, // 雷克兰德
        { 961, (36256u, 5) }, // 厄尔庇斯
        { 1189, (7767u, 50) } // 树海
    };

    private string _searchText           = string.Empty;

    private static delegate* unmanaged<InventoryManager*, InventoryType, short, int, int> InventoryManager_SplitItem   = null!;
    private static delegate* unmanaged<InventoryManager*, InventoryType, short, int>      InventoryManager_DiscardItem = null!;

    private bool          _isLoopRunning;
    private int           _loopTargetCount;
    private int           _loopProcessedCount;
    private int           _amountToProcessPerLoop;

    private InventoryType _targetInventoryType = InventoryType.Invalid;

    private (uint ItemId, int Amount) _loopTargetItemInfo;

    public ItemAutomation(ISigScannerModule scannerModule, ICommandHandler commandHandler, Configuration configuration)
    {
        _scannerModule  = scannerModule;
        _commandHandler = commandHandler;
        _configuration  = configuration;

        HashSet<ItemInfo> items = [];

        foreach (var item in DalamudApi.DataManager.GetExcelSheet<Item>()!
                                       .AsValueEnumerable().Where(i => !string.IsNullOrEmpty(i.Name.ExtractText())
                                                                       && (
                                                                           i is
                                                                           {
                                                                               FilterGroup: 4,
                                                                               LevelItem.Value.RowId: 1,
                                                                               IsUnique: false
                                                                           } // 普通装备且装等为1的物品 比如草布马裤
                                                                           || (i.FilterGroup
                                                                               == 12 // 材料比如矮人棉，庵摩罗果等 但因为秧鸡胸脯肉，厄尔庇斯鸟蛋和鱼粉是特定地图扔的，所以不会加进列表里
                                                                               && i.RowId != 36256
                                                                               && i.RowId != 27850
                                                                               && i.RowId != 7767)
                                                                           || i.FilterGroup == 17 // 鱼饵，比如沙蚕
                                                                       )))
        {
            items.Add(new (item.RowId, item.Name.ExtractText()));
        }

        _items = items.ToFrozenSet();
    }

    public bool Init()
    {
        if (!_scannerModule
                .TryScanText("40 55 53 56 57 41 55 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 8D B2",
                             out var splitItemAddress))
        {
            throw new InvalidOperationException("无法找到 InventoryManager::SplitItem 的地址");
        }

        if (!_scannerModule.TryScanText("40 56 57 41 57 48 83 EC ?? 45 0F BF F8", out var discardItemAddress))
        {
            throw new InvalidOperationException("无法找到 InventoryManager::DiscardItem 的地址");
        }

        InventoryManager_SplitItem = (delegate* unmanaged<InventoryManager*, InventoryType, short, int, int>) splitItemAddress;
        InventoryManager_DiscardItem = (delegate* unmanaged<InventoryManager*, InventoryType, short, int>) discardItemAddress;

        _commandHandler.AddCommand("/自动拆分", new (OnCommandAutoSplit));
        DalamudApi.ContextMenu.OnMenuOpened += OnMenuOpened;

        return true;
    }

    public void Shutdown()
    {
        DalamudApi.ContextMenu.OnMenuOpened -= OnMenuOpened;
    }

    public string UiName => "";

    public void OnDrawUi()
    {
        var enabled = _configuration.AutoDiscardItem;

        if (ImGui.Checkbox("启用##自动扔物品", ref enabled))
        {
            _configuration.AutoDiscardItem = enabled;
            _configuration.Save();
        }

        using (ImRaii.Disabled(!enabled))
        {
            ImGui.SameLine();

            if (ImGui.Button("点我自动拆分+舍弃"))
            {
                DalamudApi.Framework.RunOnFrameworkThread(() =>
                {
                    StartSplitAndDiscard(_configuration.DiscardTimes,
                                         _configuration.AmountToDiscardPerLoop);
                });
            }

            var discardTimes = _configuration.DiscardTimes;
            ImGui.SetNextItemWidth(100);

            if (ImGui.InputInt("重复次数", ref discardTimes, 1))
            {
                _configuration.DiscardTimes = Math.Clamp(discardTimes, 1, 50);
                _configuration.Save();
            }

            ImGui.SameLine();

            var amountToDiscard = _configuration.AmountToDiscardPerLoop;
            ImGui.SetNextItemWidth(100);

            if (ImGui.InputInt("单次数量", ref amountToDiscard, 1))
            {
                _configuration.AmountToDiscardPerLoop = Math.Clamp(amountToDiscard, 1, 50);
                _configuration.Save();
            }

            ImGui.SameLine();

            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("总计丢弃 = 重复次数 × 单次数量\n"
                                 + "例如：设置重复5次，每次丢弃10个物品，"
                                 + "总计将丢弃 5 × 10 = 50个物品。\n"
                                 + "【S怪触发区域特殊规则】\n"
                                 + "为了适配S怪的触发条件，在以下区域，“单次数量”设置将被忽略，\n"
                                 + "并强制使用固定的丢弃数量：\n\n"
                                 + "  • 雷克兰德: 每次丢弃 1 个\n"
                                 + "  • 厄尔庇斯: 每次丢弃 5 个\n"
                                 + "  • 树海: 每次丢弃 50 个\n\n"
                                 + "例如：在厄尔庇斯将“重复次数”设为3，实际会丢弃 3 × 5 = 15 个物品。");
            }

            ImGui.InputTextWithHint("##搜索物品名字", "输入你要搜索的物品名", ref _searchText, 256);

            ImGui.Text("物品列表:");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("如果搜索框里没有输入文本或者搜索无结果，表格就会显示已添加的物品\n"
                                 + "如果搜索无结果就会显示已添加的物品\n"
                                 + "加号是添加，减号是移除，如果这都看不懂那你就是猪猪\n"
                                 + "树海，厄尔庇斯以及雷克兰德对应的物品已自动处理，无需自行添加");
            }

            var result = _items.AsValueEnumerable()
                               .Where(i => i.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                               .ToArray();

            var isEmpty = string.IsNullOrWhiteSpace(_searchText) || result.Length == 0;

            using (ImRaii.Table("##可选择的物品列表",
                                2,
                                BuildFlag(ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp, isEmpty),
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
                    result = _items.AsValueEnumerable()
                                   .Where(i => _configuration.ItemsToDiscard.Contains(i.Id))
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

    private void OnCommandAutoSplit(string command, string arguments)
    {
        DalamudApi.Framework.RunOnFrameworkThread(() => StartSplitAndDiscard(_configuration.DiscardTimes,
                                                                             _configuration.AmountToDiscardPerLoop));
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Inventory)
        {
            return;
        }

        if (args.Target is not MenuTargetInventory { TargetItem: { } targetItem })
        {
            return;
        }

        if (targetItem.ContainerType is <= GameInventoryType.Inventory4
             or GameInventoryType.SaddleBag1 or GameInventoryType.SaddleBag2
            && IsAllowedToDiscard(targetItem.ItemId))
        {
            args.AddMenuItem(new ()
            {
                Name        = "自动拆分舍弃",
                Prefix      = SeIconChar.BoxedLetterS,
                PrefixColor = 70,
                OnClicked = _ =>
                {
                    StartSplitAndDiscard(_configuration.DiscardTimes,
                                         _configuration.AmountToDiscardPerLoop,
                                         targetItem.ItemId,
                                         (InventoryType) targetItem.ContainerType);
                }
            });
        }
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

    private void StartSplitAndDiscard(int           totalCount,
                                      int           amountToProcessPerLoop = 1,
                                      uint          itemIdToOverride       = 0,
                                      InventoryType targetInventoryType    = InventoryType.Invalid)
    {
        if (_isLoopRunning)
        {
            Utils.Print("已经在运行了！！！");

            return;
        }

        if (!_configuration.AutoDiscardItem)
        {
            Utils.Print("没有启用 自动拆分/舍弃物品");

            return;
        }

        if (totalCount <= 0)
        {
            Utils.Print("次数不能为0");

            return;
        }

        if (!SplittableItemsByTerritory.TryGetValue(DalamudApi.ClientState.TerritoryType, out var targetItemInfo))
        {
            Utils.Print("当前所在的地图不支持该操作。");

            return;
        }

        _isLoopRunning          = true;
        _loopTargetCount        = totalCount * (targetItemInfo.ItemId != 0 ? 1 : amountToProcessPerLoop);
        _loopProcessedCount     = 0;
        _loopTargetItemInfo     = targetItemInfo;
        _amountToProcessPerLoop = amountToProcessPerLoop;
        _targetInventoryType    = targetInventoryType;

        if (_loopTargetItemInfo.ItemId == 0)
        {
            _loopTargetItemInfo.ItemId = itemIdToOverride;
        }

        ProcessLoop();
    }

    private void ProcessLoop()
    {
        if (!_isLoopRunning)
        {
            return;
        }

        var manager = InventoryManager.Instance();

        var remainingNeeded = _loopTargetCount - _loopProcessedCount;

        var amount = Math.Min(remainingNeeded, _amountToProcessPerLoop);

        var info = BuildInventoryItemInfo(manager, _loopTargetItemInfo.ItemId);

        if (info.Count == 0)
        {
            StopLoop("操作中止: 背包里没有对应的物品");

            return;
        }

        var preDiscards = PerformDiscards(manager, info, _loopTargetItemInfo, amount);

        if (preDiscards > 0)
        {
            _loopProcessedCount += preDiscards;

            if (_loopProcessedCount >= _loopTargetCount)
            {
                StopLoop("操作完成。");

                return;
            }

            DalamudApi.Framework.RunOnTick(ProcessLoop, TimeSpan.FromMilliseconds(175));

            return;
        }

        var splitsDoneThisCycle = PerformSplits(manager, _loopTargetItemInfo, remainingNeeded);

        if (splitsDoneThisCycle == 0)
        {
            StopLoop("操作中止。无法拆分更多物品");

            return;
        }

        Utils.Print($"本轮成功拆分 {splitsDoneThisCycle} 次");

        DalamudApi.Framework.RunOnTick(ProcessLoop, TimeSpan.FromMilliseconds(200));
    }

    private void StopLoop(string finalMessage)
    {
        if (!_isLoopRunning)
        {
            return;
        }

        Utils.Print($"\n{finalMessage} 共成功处理 {_loopProcessedCount} / {_loopTargetCount} 次。");

        _isLoopRunning = false;
    }

    private int PerformSplits(InventoryManager* manager, (uint Id, int Quantity) targetItemInfo, int maxSplits)
    {
        var info = BuildInventoryItemInfo(manager, targetItemInfo.Id);

        var splitsPerformed = 0;

        foreach (var item in info)
        {
            if (item.Quantity <= 1)
            {
                continue;
            }

            var maxSplit = (item.Quantity - 1) / targetItemInfo.Quantity;

            for (var i = 0; i < maxSplit; i++)
            {
                if (splitsPerformed >= maxSplits)
                {
                    return splitsPerformed;
                }

                var errorCode = InventoryManager_SplitItem(manager, item.Type, item.Slot, targetItemInfo.Quantity);

                if (errorCode != 0)
                {
                    Utils.Print("背包已满，拆分中止。");

                    return splitsPerformed;
                }

                splitsPerformed++;
            }
        }

        return splitsPerformed;
    }

    private static int PerformDiscards(InventoryManager*       manager,
                                       List<InventoryItemInfo> itemInfos,
                                       (uint Id, int Quantity) targetItemInfo,
                                       int                     countToDiscard)
    {
        var matchedList = itemInfos
                          .Where(item => item.Quantity == targetItemInfo.Quantity)
                          .ToList();

        if (matchedList.Count == 0)
        {
            return 0;
        }

        var discardsPerformed = 0;

        foreach (var item in matchedList)
        {
            if (discardsPerformed >= countToDiscard)
            {
                break;
            }

            var errorCode = InventoryManager_DiscardItem(manager, item.Type, item.Slot);

            if (errorCode != 0)
            {
                Utils.Print("舍弃失败。");

                return discardsPerformed;
            }

            discardsPerformed++;
        }

        return discardsPerformed;
    }

    private List<InventoryItemInfo> BuildInventoryItemInfo(InventoryManager* manager, uint itemId)
    {
        List<InventoryItemInfo> infos = [];

        var itemsToDiscard = _configuration.ItemsToDiscard;

        // dont ask
        if (_targetInventoryType is InventoryType.Invalid or <= InventoryType.Inventory4)
        {
            foreach (var container in InventoryContainerToLookUp)
            {
                BuildInfo(container);
            }
        }
        else
        {
            BuildInfo(_targetInventoryType);
        }

        return infos;

        void BuildInfo(InventoryType container)
        {
            var inventoryContainer = manager->GetInventoryContainer(container);

            for (var i = 0; i < inventoryContainer->Size; i++)
            {
                ref var item = ref inventoryContainer->Items[i];

                var shouldAdd = (itemId == 0 && itemsToDiscard.Contains(item.ItemId)) || (itemId != 0 && item.ItemId == itemId);

                if (shouldAdd)
                {
                    infos.Add(new (item.ItemId, item.Slot, item.Quantity, item.GetInventoryType()));
                }
            }
        }
    }

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

    private record InventoryItemInfo(uint Id, short Slot, int Quantity, InventoryType Type);

    private static readonly InventoryType[] InventoryContainerToLookUp =
        [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
}