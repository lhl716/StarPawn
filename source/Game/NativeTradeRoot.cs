using System;
using System.Linq;
using Newtonsoft.Json;
using StarPawn.Core;

namespace StarPawn.Game
{
    // This journal is stored alongside the run, before a native trade can be confirmed.
    // It records intent, not a second instruction to charge the game's wallet.
    public sealed class NativeTradePending
    {
        public int SchemaVersion = 1;
        public int Slot;
        public string RunId = "";
        public string Status = "Prepared";
        public string QuoteId = "";
        public string AssetId = "";
        public string EncounterId = "";
        public string CounterpartyId = "";
        public string OperationId = "";
        public string BeforeState = "";
        public string NewOwner = "";
        public int NativeItemUniqueId;
        public NativeTradeDirection Direction;
        public int Price;
        public int CashBefore;
        public int CashAfter;
        public string Error = "";
        public RunState NextState = new RunState();
    }

    public sealed partial class ModEntry
    {
        private const string NativeTradePendingKey = "native-trade-root-pending-v1";
        private AssetQuote? nativeAssetQuote;
        private GameItem? nativeQuotedItem;
        private NativeTradePending? nativePending;

        private void InitNativeTrade()
        {
            NativeTradeBridge.Enable(log!, NativeQuoteProvider, NativeQuoteIsCurrent);
            NativeTradeBridge.TradeCompleted += CompleteNativeTrade;
            NativeTradeBridge.QuoteRejected += NativeQuoteRejected;
        }

        private void StopNativeTrade()
        {
            NativeTradeBridge.TradeCompleted -= CompleteNativeTrade;
            NativeTradeBridge.QuoteRejected -= NativeQuoteRejected;
            NativeTradeBridge.Disable();
            ClearNativeTradeSession();
        }

        private void ClearNativeTradeSession()
        {
            nativeAssetQuote = null;
            nativeQuotedItem = null;
            nativePending = null;
        }

        private void ReconcileNativeTradeOnLoad()
        {
            ClearNativeTradeSession();
            var raw = ModHelper.GetModData(ModId, NativeTradePendingKey, null);
            var recovery = NativeTradeBridge.GetReconciliationReceipt();
            if (string.IsNullOrEmpty(raw))
            {
                if (recovery != null) throw new InvalidOperationException("上一笔原生交易需要核对，回执已保留。");
                return;
            }
            var pending = JsonConvert.DeserializeObject<NativeTradePending>(raw);
            if (pending == null || pending.SchemaVersion != 1 || pending.Slot != currentSlot ||
                pending.RunId != state!.Catalog.RunId || string.IsNullOrEmpty(pending.QuoteId))
                throw new InvalidOperationException("原生交易待办记录与当前存档不一致，原数据已保留。");
            nativePending = pending;
            if (pending.Status == "Prepared" && recovery == null)
            {
                // Prepared quotes cannot pass the bridge's confirmed-trade guard.
                RemoveNativePending();
                log?.Log("SP_NATIVE_PENDING_CANCELLED: unconfirmed quote after load, slot=" + currentSlot);
                return;
            }
            log?.Error("SP_NATIVE_RECOVERY_REQUIRED: quote=" + pending.QuoteId + ", status=" + pending.Status);
            throw new InvalidOperationException("上一笔原生交易已进入提交阶段，需要核对现金、实物与账本；不会重复扣款。");
        }

        private string NativeCounterpartyId(GameItem item)
        {
            var assetId = NativeBridge.GetAssetId(item);
            if (string.IsNullOrEmpty(assetId)) throw new InvalidOperationException("实物缺少星港资产编号。");
            var customer = PlayerStore.HasInstance ? PlayerStore.Instance.currentClientInstance : null;
            if (customer == null)
            {
                if (GeneralHelper.IsItemOwned(item)) return "self:" + assetId;
                throw new InvalidOperationException("当前没有可绑定的交易客户。");
            }
            var blueprint = customer.GetClientBlueprint();
            if (blueprint == null || string.IsNullOrEmpty(blueprint.identifier))
                throw new InvalidOperationException("当前客户缺少稳定身份，暂不能建立报价。");
            // The persisted identity survives reload; the bridge additionally binds the
            // actual customer and item references for this particular live quote.
            return "native:" + Day() + ":" + blueprint.identifier + ":" + assetId;
        }

        private long? NativeQuoteProvider(GameItem item, NativeTradeContext context)
            => ReferenceEquals(item, nativeQuotedItem) && NativeQuoteIsCurrent(context) ? nativeAssetQuote!.Price : (long?)null;

        private bool NativeQuoteIsCurrent(NativeTradeContext context)
        {
            if (!SameRun() || nativeAssetQuote == null || !ReferenceEquals(nativeQuotedItem, context.Item)) return false;
            var q = nativeAssetQuote;
            var direction = context.Direction == NativeTradeDirection.PlayerBuys ? "buy" : "sell";
            if (q.AssetId != context.AssetId || q.AssetVersion != context.AssetVersion || q.MarketVersion != context.MarketVersion ||
                q.CounterpartyId != NativeCounterpartyId(context.Item) || q.Direction != direction) return false;
            var asset = Catalog.GetAsset(state!.Catalog, q.AssetId);
            if (asset.Status != "active" || (asset.OwnerId == "player") != (direction == "sell") ||
                asset.NativeItemUniqueId != NativeBridge.GetNativeUniqueId(context.Item)) return false;
            if (!ValuationV2.IsCurrent(state.Catalog, q, Day(), Market().Version, direction)) return false;
            if (context.Confirmed && (nativePending == null || nativePending.Status != "Executing" ||
                nativePending.QuoteId != context.QuoteId || nativePending.BeforeState != JsonConvert.SerializeObject(state))) return false;
            return true;
        }

        private void PlaceAndPrepareNativeTrade(string assetId)
        {
            if (!EnsureRun() || state == null || !NativeBridge.IsReady) return;
            CancelPreparedNativeTrade();
            var item = NativeBridge.FindAsset(assetId) ?? throw new InvalidOperationException("没有找到这件实物。");
            var asset = Catalog.GetAsset(state.Catalog, assetId);
            var client = PlayerStore.Instance.currentClientInstance;
            var owned = GeneralHelper.IsItemOwned(item);
            if (client == null || (owned ? !client.GetClientBlueprint().IsClientBuying() : !client.GetClientBlueprint().IsClientSelling()))
                throw new InvalidOperationException("请等待对应的买家或卖家来到柜台。");
            if (asset.Status != "active" || (asset.OwnerId == "player") != owned ||
                asset.NativeItemUniqueId != NativeBridge.GetNativeUniqueId(item) || NativeBridge.IsInCustody(item))
                throw new InvalidOperationException("实物身份、产权或托管状态不允许交易。");
            var tray = EmporiumEntry.Instance.backInvinvElement;
            if (tray.childItems.Any(other => !ReferenceEquals(other, item)))
                throw new InvalidOperationException("请先移走交易台上的其他物品，再单独议价。");
            if (!NativeBridge.TryMove(item, tray, out var error)) throw new InvalidOperationException(error);
            PlayerStore.Instance.ExamineItem(item);
            PrepareNativeTrade(assetId);
        }

        private void PrepareNativeTrade(string assetId)
        {
            if (!EnsureRun() || state == null) return;
            if (nativePending != null && nativePending.Status != "Prepared")
                throw new InvalidOperationException("上一笔原生交易需要核对。");
            CancelPreparedNativeTrade();
            var item = NativeBridge.FindAsset(assetId) ?? throw new InvalidOperationException("没有找到这件实物。");
            if (!ReferenceEquals(PlayerStore.Instance.GetCurrentlyNegociatedItem(), item) || PlayerStore.Instance.currentClientInstance == null)
                throw new InvalidOperationException("请将这件商品放在当前客户的交易台上。");
            var asset = Catalog.GetAsset(state.Catalog, assetId);
            var direction = GeneralHelper.IsItemOwned(item) ? NativeTradeDirection.PlayerSells : NativeTradeDirection.PlayerBuys;
            if ((asset.OwnerId == "player") != (direction == NativeTradeDirection.PlayerSells) || asset.Status != "active" ||
                asset.NativeItemUniqueId != NativeBridge.GetNativeUniqueId(item))
                throw new InvalidOperationException("实物身份或产权与星港记录不一致，请先核对。");
            var counterparty = NativeCounterpartyId(item);
            var active = state.Catalog.Encounters.FirstOrDefault(e => !e.Completed && e.AssetId == assetId && e.CounterpartyId == counterparty);
            var encounter = InspectionV2.Open(state.Catalog, assetId,
                active?.EncounterId ?? counterparty + ":deal:" + asset.StateVersion, counterparty);
            selectedAsset = assetId; selectedEncounter = encounter.EncounterId; selectedView = encounter.ViewId;
            Persist();
            var market = Market();
            nativeAssetQuote = QuoteByMarket(state.Catalog, encounter.EncounterId,
                direction == NativeTradeDirection.PlayerBuys ? "buy" : "sell", market);
            nativeQuotedItem = item;
            if (!NativeTradeBridge.TryPrepareQuote(item, direction, asset.StateVersion, market.Version, out var context, out var error) || context == null)
            {
                nativeAssetQuote = null; nativeQuotedItem = null;
                throw new InvalidOperationException(error);
            }
            try
            {
                var before = JsonConvert.SerializeObject(state);
                var next = Clone();
                var owner = direction == NativeTradeDirection.PlayerBuys ? "player" : counterparty;
                Catalog.Transfer(next.Catalog, assetId, asset.OwnerId, owner);
                InspectionV2.EndEncounter(next.Catalog, encounter.EncounterId);
                var operationId = OperationId(next);
                var delta = direction == NativeTradeDirection.PlayerBuys ? -(long)context.Price : context.Price;
                Record(next, operationId, direction == NativeTradeDirection.PlayerBuys ? "柜台购入" : "柜台出售", delta, assetId);
                Validate(next);
                nativePending = new NativeTradePending {
                    Slot = currentSlot, RunId = state.Catalog.RunId, QuoteId = context.QuoteId,
                    AssetId = assetId, EncounterId = encounter.EncounterId, CounterpartyId = counterparty,
                    NativeItemUniqueId = NativeBridge.GetNativeUniqueId(item), Direction = direction,
                    OperationId = operationId, BeforeState = before, NextState = next, NewOwner = owner,
                    Price = context.Price, CashBefore = Cash(), CashAfter = checked(Cash() + (int)delta)
                };
                SaveNativePending();
            }
            catch
            {
                NativeTradeBridge.CancelQuote("");
                nativeAssetQuote = null; nativeQuotedItem = null;
                throw;
            }
            var pending = nativePending;
            var q = nativeAssetQuote!;
            var ui = BeginPage("柜台成交 · " + Catalog.PublicFacts(asset).Name, "当前客户、这件实物与本次证据共同确定成交价。");
            Text(ui,24,80,550,80,(direction == NativeTradeDirection.PlayerBuys ? "向当前客户购入" : "向当前客户出售") +
                "\n确认金额 " + pending.Price + " Cr\n引用 " + q.AppliedCauses.Count + " 个独立原因",16);
            Text(ui,24,175,550,68,"成交后现金 " + pending.CashAfter + " Cr\n" + q.MaterialDescription +
                "\n价格或客户变化时，需要重新报价。",12);
            Button(ui,24,267,258,32,"确认成交 " + pending.Price,()=>SubmitNativeTrade(pending.QuoteId));
            Button(ui,298,267,276,32,"取消并继续验货",()=>{CancelPreparedNativeTrade();OpenNativeAsset(assetId);});
            Footer(ui);Show(ui);
            log?.Log("SP_NATIVE_QUOTE: id=" + pending.QuoteId + ", asset=" + assetId + ", price=" + pending.Price + ", direction=" + direction);
        }

        private void SubmitNativeTrade(string quoteId)
        {
            var pending = nativePending;
            var context = NativeTradeBridge.CurrentQuote;
            if (pending == null || pending.Status != "Prepared" || pending.QuoteId != quoteId ||
                context == null || context.QuoteId != quoteId || !NativeQuoteIsCurrent(context) ||
                pending.BeforeState != JsonConvert.SerializeObject(state))
            {
                CancelPreparedNativeTrade();
                throw new InvalidOperationException("报价或业务记录已变化，请重新确认。");
            }
            pending.CashBefore = Cash();
            pending.CashAfter = checked(pending.CashBefore + (pending.Direction == NativeTradeDirection.PlayerBuys ? -pending.Price : pending.Price));
            pending.Status = "Executing";
            try
            {
                SaveNativePending();
                if (NativeTradeBridge.ConfirmQuote(quoteId,out var error) && NativeTradeBridge.CommitConfirmed(out error))
                {
                    notice = "已与当前客户成交 " + pending.Price + " Cr，现金与实物产权已记账。";
                    OpenOwned();
                    return;
                }
                // A native precondition can decline before reaching its BuyItem/SellItem
                // boundary. Cancel the quote first, so a delayed native dialog cannot retry.
                NativeTradeBridge.CancelQuote("");
                if (nativePending == null) { notice = error; OpenOwned(); return; }
                if (NativeTradeBridge.GetReconciliationReceipt() == null && NativeTradeUnchanged(pending))
                {
                    RemoveNativePending(); nativeAssetQuote = null; nativeQuotedItem = null;
                    notice = error; OpenOwned(); return;
                }
                BlockNativeTrade(pending,error);
            }
            catch (Exception e)
            {
                NativeTradeBridge.CancelQuote("");
                if (nativePending != null) BlockNativeTrade(nativePending,e.Message);
                throw;
            }
        }

        private bool NativeTradeUnchanged(NativeTradePending pending)
        {
            var item = NativeBridge.FindAsset(pending.AssetId);
            return item != null && NativeBridge.GetNativeUniqueId(item) == pending.NativeItemUniqueId &&
                Cash() == pending.CashBefore && GeneralHelper.IsItemOwned(item) == (pending.Direction == NativeTradeDirection.PlayerSells) &&
                pending.BeforeState == JsonConvert.SerializeObject(state);
        }

        private void CompleteNativeTrade(NativeTradeReceipt receipt)
        {
            var pending = nativePending;
            if (pending == null || pending.Status != "Executing" || pending.QuoteId != receipt.QuoteId)
            {
                blocked = true;
                throw new InvalidOperationException("原生成交回执缺少匹配的星港待办，已暂停业务。");
            }
            if (!receipt.Success || receipt.ReconciliationRequired)
            {
                if (!receipt.ReconciliationRequired && NativeTradeUnchanged(pending))
                { RemoveNativePending(); nativeAssetQuote = null; nativeQuotedItem = null; notice = receipt.Error; return; }
                BlockNativeTrade(pending,receipt.Error);
                throw new InvalidOperationException(receipt.Error);
            }
            if (!SameRun() || pending.AssetId != receipt.AssetId || pending.Direction != receipt.Direction ||
                pending.Price != receipt.Price || pending.CashBefore != receipt.CashBefore || pending.CashAfter != receipt.CashAfter ||
                receipt.OwnershipAfter != (pending.Direction == NativeTradeDirection.PlayerBuys) ||
                pending.BeforeState != JsonConvert.SerializeObject(state))
            {
                BlockNativeTrade(pending,"成交回执与待办快照不一致。");
                throw new InvalidOperationException(pending.Error);
            }
            try
            {
                Validate(pending.NextState);
                // The native method has already changed cash and physical ownership.
                // Only publish the staged catalog/ledger. Never call Commit here.
                state = pending.NextState;
                Persist();
                RemoveNativePending();
                nativeAssetQuote = null; nativeQuotedItem = null;
                notice = "柜台成交完成，已记录现金与实物产权。";
                log?.Log("SP_NATIVE_CORE_COMMIT: operation=" + pending.OperationId + ", quote=" + receipt.QuoteId +
                    ", cash=" + receipt.CashBefore + "->" + receipt.CashAfter + ", owner=" + pending.NewOwner);
            }
            catch (Exception e)
            {
                nativePending = pending;
                BlockNativeTrade(pending,e.Message);
                throw;
            }
        }

        private void SaveNativePending()
        {
            if (!SameRun() || nativePending == null) throw new InvalidOperationException("无法在变化后的存档中记录交易待办。");
            var raw = JsonConvert.SerializeObject(nativePending);
            ModHelper.SetModData(ModId,NativeTradePendingKey,raw);
            if (ModHelper.GetModData(ModId,NativeTradePendingKey,null) != raw)
                throw new InvalidOperationException("交易待办写入核对失败。");
        }

        private void RemoveNativePending()
        {
            ModHelper.RemoveModData(ModId,NativeTradePendingKey);
            if (!string.IsNullOrEmpty(ModHelper.GetModData(ModId,NativeTradePendingKey,null)))
                throw new InvalidOperationException("交易待办清理核对失败。");
            nativePending = null;
        }

        private void CancelPreparedNativeTrade()
        {
            if (nativePending != null && nativePending.Status != "Prepared")
                throw new InvalidOperationException("已提交的原生交易需要核对，不能直接取消。");
            NativeTradeBridge.CancelQuote("");
            if (nativePending != null) RemoveNativePending();
            nativeAssetQuote = null; nativeQuotedItem = null;
        }

        private void BlockNativeTrade(NativeTradePending pending,string error)
        {
            pending.Status = "Review"; pending.Error = error;
            try
            {
                // Preserve the journal even when normal business has been blocked.
                if (PlayerStore.HasInstance && ReferenceEquals(currentStore,PlayerStore.Instance) && currentSlot == PlayerStore.Instance.saveSlotId)
                    ModHelper.SetModData(ModId,NativeTradePendingKey,JsonConvert.SerializeObject(pending));
            }
            finally
            {
                blocked = true;
                notice = "原生交易结果需要核对，已保留待办与回执并暂停业务。";
                log?.Error("SP_NATIVE_CORE_BLOCKED: quote=" + pending.QuoteId + ", " + error);
            }
        }

        private void NativeQuoteRejected(string error) { notice = error; log?.Warning("SP_NATIVE_ROOT_QUOTE: " + error); }
    }
}
