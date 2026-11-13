using System;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private void PresentNativeOffer(string assetId)
        {
            RequireCommerceReady();
            var client = PlayerStore.Instance.currentClientInstance;
            if (client == null || !client.GetClientBlueprint().IsClientSelling())
                throw new InvalidOperationException("先开始营业，等一位卖家来到柜台，再请他展示这件货品。");
            var old = Catalog.GetAsset(state!.Catalog, assetId);
            if (old.OwnerId == "player" || old.Status != "active" || !state.OfferAssetIds.Contains(assetId) || NativeBridge.FindAsset(assetId) != null)
                throw new InvalidOperationException("这件来客货品已经交付或不再可供展示。");
            var next = Clone();
            var asset = Catalog.GetAsset(next.Catalog, assetId);
            next.OfferAssetIds.Remove(assetId);
            RunCommerce(next, OperationId(next), "柜台展示 " + Catalog.PublicFacts(asset).Name, 0, assetId, changes => {
                // Reserve capacity with the ordinary reversible physical transaction.
                // The customer owns the item before it is presented in their tray.
                StageSpawn(next, asset, changes);
                var item = NativeBridge.FindAsset(assetId)!;
                changes.Undo.Add(() => ModHelper.SetItemOwnerShip(item, true));
                ModHelper.SetItemOwnerShip(item, false);
                if (!NativeBridge.TryMove(item, EmporiumEntry.Instance.frontInvinvElement, out var error))
                    throw new InvalidOperationException(error);
                Catalog.Transfer(next.Catalog, assetId, asset.OwnerId, NativeCounterpartyId(item));
                foreach (var encounter in next.Catalog.Encounters.Where(e => e.AssetId == assetId && !e.Completed))
                    InspectionV2.EndEncounter(next.Catalog, encounter.EncounterId);
            });
            notice = "卖家已把货品放到前台。用放大镜验货，再选择放到交易台并报价，或办理典当。";
            log?.Log("SP_NATIVE_OFFER: asset=" + assetId + ", native=" + asset.NativeItemUniqueId);
            CloseWindow();
        }

        private void ValidatePhysicalAssets()
        {
            foreach (var asset in state!.Catalog.Assets.Where(a => a.OwnerId == "player" && a.Status == "active" || a.Status == "escrow"))
            {
                var item = NativeBridge.FindAsset(asset.AssetId);
                if (item == null || !GeneralHelper.IsItemOwned(item) || item.unitCount != 1 ||
                    NativeBridge.GetNativeUniqueId(item) != asset.NativeItemUniqueId ||
                    NativeBridge.GetDefinitionId(item) != "starpawn." + asset.DefinitionId)
                    throw new InvalidOperationException("资产与存档实物不一致：" + asset.AssetId);
                var contract = state.Economy.PawnContracts.SingleOrDefault(p => p.Status == "Active" && p.AssetId == asset.AssetId);
                if ((asset.Status == "escrow") != (contract != null) ||
                    (item.GetTagReadonly(NativeBridge.CustodyTag).GetString() ?? "") != (contract?.Id ?? ""))
                    throw new InvalidOperationException("托管合同与存档实物不一致：" + asset.AssetId);
            }
        }
    }
}
