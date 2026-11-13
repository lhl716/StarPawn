using System;
using System.Collections.Generic;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    internal static class NativeEquipment
    {
        internal static Action<string, string>? MeasureRequested;

        internal static void Initialize(GameItem item, string definitionId)
        {
            var d = Catalog.GetDefinition(definitionId);
            if (!string.IsNullOrEmpty(d.InstrumentKind))
            {
                item.shortDescription = d.Description;
                item.EnableTag("CAN_USE_ON_NON_OWNED_TAG", false);
                item.maySelectSlotItemFunc = (tool, inventory, slot) => true;
                item.canSelectSlotItemFunc = (tool, inventory, slot) => true;
                item.mayThisTargetItemFunc = (tool, target) => NativeBridge.IsStarPawnAsset(target) && GeneralHelper.IsItemOwned(tool) && !NativeBridge.IsInCustody(tool) && Appraisal.CanMeasure(Catalog.GetDefinition(NativeBridge.GetVariantId(target)),d.InstrumentKind);
                item.canThisTargetItemFunc = item.mayThisTargetItemFunc;
                item.onThisTargetItemFunc = (tool, target) => MeasureRequested?.Invoke(NativeBridge.GetAssetId(tool), NativeBridge.GetAssetId(target));
            }
            if (!string.IsNullOrEmpty(d.ManualFor)) item.shortDescription = "鉴定说明手册 · 使用放大镜翻阅固定缺陷图鉴";
            if (!d.IsOrganizer) return;
            var inventory = new GameGridInventory(4, 2);
            var window = new PixelWindow(true, "图鉴", WindowStyle.Classic);
            if (!window.Attach(inventory)) throw new InvalidOperationException("无法连接手册收纳内部空间。");
            item.SetContentWindow(window);
            var allowed = Catalog.Definitions.Where(x => !string.IsNullOrEmpty(x.ManualFor) || x.Id == "inspection_guide").Select(x => "starpawn." + x.Id).ToList();
            ContainerHelper.InitContainerItem(inventory, item, null, allowed);
            // The native container serializes its actual children and their unique IDs.
            // Do not let a filled cabinet leave the store with unpriced child assets.
            var previousRemove = item.mayInventoryRemoveItemFunc;
            item.mayInventoryRemoveItemFunc = (current, parent) => inventory.childItems.Count == 0 && (previousRemove == null || previousRemove(current, parent));
            item.shortDescription = "八格鉴定手册收纳 · 收纳真实书本；搬动或出售前先取空";
        }

        internal static GameInventory Inventory(GameItem organizer)
        {
            if (organizer.contentWindow?.childElement is GameInventory inventory) return inventory;
            throw new InvalidOperationException("手册收纳内部空间不可用。");
        }
    }
}
