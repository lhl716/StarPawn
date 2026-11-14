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
            if (!string.IsNullOrEmpty(d.ManualFor) || d.IsBook)
            {
                // Revision 05: a physical book opened with the native Read action, not the magnifier.
                item.shortDescription = d.Description;
                Native05.InitBook(item, d);
            }
            if (d.InternalWidthCells <= 0 || d.InternalHeightCells <= 0) return;
            // Interior grids come from ITEM_LAYOUT_05 (e.g. organizer 8x6 for eight 2x3 books).
            var inventory = new GameGridInventory(d.InternalWidthCells, d.InternalHeightCells);
            var window = new PixelWindow(true, d.Name, WindowStyle.Classic);
            if (!window.Attach(inventory)) throw new InvalidOperationException(TextResolver.Get("sp5.container.attach", "This container's interior is unavailable."));
            item.SetContentWindow(window);
            ContainerHelper.InitContainerItem(inventory, item, null, AllowedChildren(d));
            // The native container serializes its actual children and their unique IDs.
            // Containers must be emptied before moving or selling, so children are never orphaned.
            var previousRemove = item.mayInventoryRemoveItemFunc;
            item.mayInventoryRemoveItemFunc = (current, parent) => inventory.childItems.Count == 0 && (previousRemove == null || previousRemove(current, parent));
            item.shortDescription = d.Description;
        }

        // No nested containers and no decorative copies: only real items of the right kind.
        internal static List<string> AllowedChildren(ItemDefinition box)
        {
            IEnumerable<ItemDefinition> pick;
            switch (box.ArtModel)
            {
                case "gem_case": pick = Catalog.Definitions.Where(x => x.Role == "gem"); break;
                case "presentation_case": pick = Catalog.Definitions.Where(x => x.Role == "setting" || (BusinessLabels.Has(x, BusinessLabels.Luxury) && x.Material != "none" && x.Form != "bar")); break;
                case "jewelry_display": pick = Catalog.Definitions.Where(x => BusinessLabels.Has(x, BusinessLabels.Luxury) && x.InternalWidthCells == 0); break;
                case "materials_bin": pick = Catalog.Definitions.Where(x => x.Role == "lot" || x.Role == "mixed" || x.RecoveryClass == "ore" || x.RecoveryClass == "component"); break;
                case "card_holder": return new List<string> { "starpawn.savings_card", "starpawn.credit_card" };
                default: pick = Catalog.Definitions.Where(x => !string.IsNullOrEmpty(x.ManualFor) || x.Id == "inspection_guide"); break;
            }
            return pick.Where(x => x.InternalWidthCells == 0).Select(x => "starpawn." + x.Id).ToList();
        }

        internal static GameInventory Inventory(GameItem organizer)
        {
            if (organizer.contentWindow?.childElement is GameInventory inventory) return inventory;
            throw new InvalidOperationException("手册收纳内部空间不可用。");
        }
    }
}
