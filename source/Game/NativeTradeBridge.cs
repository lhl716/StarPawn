using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;

namespace StarPawn.Game
{
    public enum NativeTradeDirection { PlayerBuys, PlayerSells }

    public sealed class NativeTradeContext
    {
        public string QuoteId { get; internal set; } = "";
        public string AssetId { get; internal set; } = "";
        public GameItem Item { get; internal set; } = null!;
        public StoreClientInstance Customer { get; internal set; } = null!;
        public NativeTradeDirection Direction { get; internal set; }
        public long AssetVersion { get; internal set; }
        public long MarketVersion { get; internal set; }
        public int Price { get; internal set; }
        public bool Confirmed { get; internal set; }
        internal PlayerStore Store = null!;
        internal int Slot;
        internal bool Consumed;
    }

    public sealed class NativeTradeReceipt
    {
        public string QuoteId = "";
        public string AssetId = "";
        public NativeTradeDirection Direction;
        public int Price;
        public int CashBefore;
        public int CashAfter;
        public bool OwnershipAfter;
        public bool Success;
        public bool ReconciliationRequired;
        public string Error = "";
    }

    // The price substitution is at three known native call sites, never on the global
    // GameItem.GetNegociatedValue method used by donations, fines, and asset totals.
    public static class NativeTradeBridge
    {
        private static ModLog? log;
        private static Func<GameItem, NativeTradeContext, long?>? provider;
        private static Func<NativeTradeContext, bool>? versionValidator;
        private static NativeTradeContext? quote;
        private static NativeTradeContext? executing;
        private static bool enabled;
        private static bool halted;
        private static string lastRejection = "";
        private static readonly HashSet<string> VerifiedSites = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, GameItem> PendingOffers = new Dictionary<string, GameItem>(StringComparer.Ordinal);
        public static event Action<NativeTradeReceipt>? TradeCompleted;
        public static event Action<string>? QuoteRejected;
        public static NativeTradeReceipt? LastReceipt { get; private set; }
        public static NativeTradeContext? CurrentQuote => quote;

        public static void Enable(ModLog modLog, Func<GameItem, NativeTradeContext, long?> quoteProvider,
            Func<NativeTradeContext, bool> versionsStillValid)
        {
            log = modLog;
            provider = quoteProvider ?? throw new ArgumentNullException(nameof(quoteProvider));
            versionValidator = versionsStillValid ?? throw new ArgumentNullException(nameof(versionsStillValid));
            if (enabled) return;
            enabled = true;
            ModHook.OnGameLoadedInit += ClearRun;
            ModHook.OnNewGameStarted += NewRun;
        }

        public static void Disable()
        {
            if (enabled)
            {
                ModHook.OnGameLoadedInit -= ClearRun;
                ModHook.OnNewGameStarted -= NewRun;
            }
            enabled = false;
            quote = null;
            executing = null;
            provider = null;
            versionValidator = null;
        }

        private static void NewRun(string scenario) => ClearRun();
        private static void ClearRun()
        {
            quote = null;
            executing = null;
            halted = false;
            lastRejection = "";
            LastReceipt = null;
            PendingOffers.Clear();
        }

        public static bool TryPrepareQuote(GameItem item, NativeTradeDirection direction, long assetVersion,
            long marketVersion, out NativeTradeContext? context, out string error)
        {
            context = null;
            error = "";
            if (!enabled || !NativeBridge.IsReady || provider == null || HasUnreconciledTrade()) { error = "交易桥尚未就绪或需要核对上一笔交易。"; return false; }
            if (!NativeBridge.IsStarPawnAsset(item) || NativeBridge.IsInCustody(item) || item.unitCount != 1)
            { error = "只支持未托管的单件 StarPawn 资产。"; return false; }
            var store = PlayerStore.Instance;
            if (store.currentClientInstance == null || !ReferenceEquals(store.GetCurrentlyNegociatedItem(), item))
            { error = "请先将该物品放到当前客户的交易台上。"; return false; }
            var candidate = new NativeTradeContext {
                QuoteId = "sp-trade-" + Guid.NewGuid().ToString("N"), AssetId = NativeBridge.GetAssetId(item),
                Item = item, Customer = store.currentClientInstance, Store = store, Slot = store.saveSlotId,
                Direction = direction, AssetVersion = assetVersion, MarketVersion = marketVersion
            };
            if (!ValidateContext(candidate, false, out error)) return false;
            try
            {
                var value = provider(item, candidate);
                if (!value.HasValue || value.Value <= 0 || value.Value > int.MaxValue)
                { error = "估价服务没有返回有效的最终交易金额。"; return false; }
                candidate.Price = (int)value.Value;
                if (!BudgetAndCashValid(candidate, out error)) return false;
                quote = candidate;
                context = candidate;
                lastRejection = "";
                RefreshNativeUI();
                return true;
            }
            catch (Exception exception) { error = "报价计算失败：" + exception.Message; return false; }
        }

        public static bool ConfirmQuote(string quoteId, out string error)
        {
            error = "";
            if (quote == null || quote.QuoteId != quoteId || !ValidateContext(quote, false, out error) || !BudgetAndCashValid(quote, out error))
            { if (string.IsNullOrEmpty(error)) error = "报价不存在或已失效，请重新报价。"; Reject(error); return false; }
            quote.Confirmed = true;
            RefreshNativeUI();
            return true;
        }

        public static bool CommitConfirmed(out string error)
        {
            error = "";
            if (quote == null || !ValidateContext(quote, true, out error) || !BudgetAndCashValid(quote, out error))
            { if (string.IsNullOrEmpty(error)) error = "请先确认当前报价。"; Reject(error); return false; }
            if (!SitesReady()) { error = "原生交易补丁未通过调用点验证，已停止成交。"; Reject(error); return false; }
            var id = quote.QuoteId;
            LastReceipt = null;
            if (quote.Direction == NativeTradeDirection.PlayerBuys) PlayerStore.Instance.BuyItemFromTable();
            else PlayerStore.Instance.SellItemFromTable();
            if (LastReceipt != null && LastReceipt.QuoteId == id)
            { error = LastReceipt.Error; return LastReceipt.Success && !LastReceipt.ReconciliationRequired; }
            error = "原版客户规则未完成成交；物品检查或确认对话可能仍待处理。";
            return false;
        }

        public static void CancelQuote(string reason)
        {
            quote = null;
            if (!string.IsNullOrEmpty(reason)) Reject(reason);
            RefreshNativeUI();
        }

        private static bool ValidateContext(NativeTradeContext context, bool requireConfirmed, out string error)
        {
            error = "";
            if (!enabled || !NativeBridge.IsReady || HasUnreconciledTrade() || context.Consumed || (requireConfirmed && !context.Confirmed))
            { error = "报价未确认、已结束或交易桥暂停。"; return false; }
            var store = PlayerStore.Instance;
            if (!ReferenceEquals(context.Store, store) || context.Slot != store.saveSlotId ||
                !ReferenceEquals(context.Customer, store.currentClientInstance) ||
                !ReferenceEquals(context.Item, store.GetCurrentlyNegociatedItem()) ||
                NativeBridge.GetAssetId(context.Item) != context.AssetId || context.Item.unitCount != 1 ||
                !context.Item.IsRootedInBackInv() || NativeBridge.IsInCustody(context.Item))
            { error = "交易对象、客户或物品位置已变化，请重新报价。"; return false; }
            var owned = GeneralHelper.IsItemOwned(context.Item);
            if ((context.Direction == NativeTradeDirection.PlayerSells) != owned)
            { error = "物品归属与交易方向不一致。"; return false; }
            var client = context.Customer.GetClientBlueprint();
            if (client == null || (context.Direction == NativeTradeDirection.PlayerBuys ? !client.IsClientSelling() : !client.IsClientBuying()))
            { error = "当前客户不支持此交易方向。"; return false; }
            try
            {
                if (versionValidator == null || !versionValidator(context))
                { error = "物品状态或市场版本已变化，请重新确认新的报价。"; return false; }
            }
            catch (Exception exception) { error = "无法验证报价版本：" + exception.Message; return false; }
            return true;
        }

        private static bool BudgetAndCashValid(NativeTradeContext context, out string error)
        {
            error = "";
            var cash = context.Store.GetCash();
            if (context.Direction == NativeTradeDirection.PlayerBuys)
            {
                if (cash < context.Price) { error = "现金不足，请重新确认交易。"; return false; }
            }
            else
            {
                if ((long)cash + context.Price > int.MaxValue) { error = "现金金额超出原版支持范围。"; return false; }
                var client = context.Customer.GetClientBlueprint();
                if (client.useClientBudget && client.GetBudget() < context.Price)
                { error = "客户预算低于已确认金额，请重新报价；不会静默降低到账金额。"; return false; }
            }
            return true;
        }

        internal static bool GuardAvailability(GameItem item, NativeTradeDirection direction)
        {
            if (!enabled || !NativeBridge.IsStarPawnAsset(item)) return true;
            if (quote == null || !ReferenceEquals(quote.Item, item) || quote.Direction != direction ||
                !ValidateContext(quote, true, out _) || !BudgetAndCashValid(quote, out _) || !SitesReady()) return false;
            return true;
        }

        internal static bool BeginTrade(GameItem item, NativeTradeDirection direction, out NativeTradeExecution? state)
        {
            state = null;
            if (!enabled || !NativeBridge.IsStarPawnAsset(item)) return true;
            if (executing != null) { Reject("正在提交上一笔交易，请勿重复点击。"); return false; }
            if (quote == null || !ReferenceEquals(quote.Item, item) || quote.Direction != direction)
            { Reject("StarPawn 物品需要先确认当前报价。"); return false; }
            var error = "";
            if (!SitesReady() || !ValidateContext(quote, true, out error) || !BudgetAndCashValid(quote, out error))
            { Reject(string.IsNullOrEmpty(error) ? "原生价格调用点未验证。" : error); return false; }
            executing = quote;
            state = new NativeTradeExecution { Context = quote, CashBefore = PlayerStore.Instance.GetCash(), OwnedBefore = GeneralHelper.IsItemOwned(item) };
            return true;
        }

        // Called only from the three explicitly rewritten native trade sites below.
        public static long ValueAtTradeSite(GameItem item)
        {
            if (!enabled || !NativeBridge.IsStarPawnAsset(item)) return item.GetNegociatedValue();
            var active = executing ?? quote;
            if (active != null && ReferenceEquals(active.Item, item) && !active.Consumed)
            {
                if (ReferenceEquals(active, executing)) return active.Price;
                if (ValidateContext(active, true, out _) && BudgetAndCashValid(active, out _)) return active.Price;
            }
            // Guards reject this path; throwing avoids charging a fallback amount if another
            // patch unexpectedly calls the rewritten native method without its prefix.
            throw new InvalidOperationException("No valid StarPawn quote at native trade call site.");
        }

        internal static void OverrideDisplay(GameItem item, ref int result)
        {
            if (quote != null && ReferenceEquals(quote.Item, item) && ValidateContext(quote, false, out _) && BudgetAndCashValid(quote, out _))
                result = quote.Price;
        }

        internal static void OverrideBudgetCheck(StoreClient client, GameItem item, ref bool result)
        {
            if (quote != null && quote.Direction == NativeTradeDirection.PlayerSells &&
                ReferenceEquals(quote.Item, item) && ReferenceEquals(quote.Customer.GetClientBlueprint(), client) &&
                ValidateContext(quote, true, out _))
                result = !client.useClientBudget || client.GetBudget() >= quote.Price;
        }

        internal static void EndTrade(NativeTradeExecution? state, Exception? exception)
        {
            if (state == null) return;
            var context = state.Context;
            var after = context.Store.GetCash();
            var owned = GeneralHelper.IsItemOwned(context.Item);
            var expected = (long)state.CashBefore + (context.Direction == NativeTradeDirection.PlayerBuys ? -context.Price : context.Price);
            var receipt = new NativeTradeReceipt {
                QuoteId = context.QuoteId, AssetId = context.AssetId, Direction = context.Direction,
                Price = context.Price, CashBefore = state.CashBefore, CashAfter = after, OwnershipAfter = owned
            };
            receipt.Success = exception == null && after == expected && owned == (context.Direction == NativeTradeDirection.PlayerBuys);
            receipt.ReconciliationRequired = !receipt.Success && (after != state.CashBefore || owned != state.OwnedBefore);
            receipt.Error = receipt.Success ? "" : "原生成交结果未完全匹配已确认金额和归属，请核对；不会自动重试或反向改款。";
            if (exception != null) receipt.Error += " " + exception.Message;
            context.Consumed = true;
            context.Confirmed = false;
            executing = null;
            quote = null;
            LastReceipt = receipt;
            if (receipt.ReconciliationRequired) halted = true;
            log?.Log("SP_NATIVE_TRADE: id=" + receipt.QuoteId + ", success=" + receipt.Success + ", cash=" + receipt.CashBefore + "->" + receipt.CashAfter);
            try { TradeCompleted?.Invoke(receipt); }
            catch (Exception callbackError)
            {
                halted = true;
                receipt.ReconciliationRequired = true;
                receipt.Error = "原生成交已执行，但核心状态通知失败：" + callbackError.Message;
                log?.Error(receipt.Error);
            }
            if (receipt.ReconciliationRequired)
                ModHelper.SetModData("starpawn", "native-trade-recovery-v1", JsonConvert.SerializeObject(receipt));
            RefreshNativeUI();
        }

        private static bool HasUnreconciledTrade()
        {
            return halted || (NativeBridge.IsReady && !string.IsNullOrEmpty(ModHelper.GetModData("starpawn", "native-trade-recovery-v1", null)));
        }

        public static NativeTradeReceipt? GetReconciliationReceipt()
        {
            if (!NativeBridge.IsReady) return null;
            var raw = ModHelper.GetModData("starpawn", "native-trade-recovery-v1", null);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<NativeTradeReceipt>(raw);
        }

        // The caller must first reconcile its own persisted ledger and physical ownership.
        // This only acknowledges a receipt; it never rewrites money or item ownership.
        public static bool AcknowledgeReconciliation(string quoteId)
        {
            var receipt = GetReconciliationReceipt();
            if (receipt == null || receipt.QuoteId != quoteId) return false;
            ModHelper.RemoveModData("starpawn", "native-trade-recovery-v1");
            halted = false;
            return true;
        }

        internal static bool GuardBulk()
        {
            if (!enabled || !NativeBridge.IsReady) return true;
            foreach (var item in EmporiumEntry.Instance.backInvinvElement.childItems)
            {
                if (!NativeBridge.IsStarPawnAsset(item)) continue;
                Reject("交易台含 StarPawn 资产，请逐件确认报价后成交。");
                return false;
            }
            return true;
        }

        private static void Reject(string reason)
        {
            if (lastRejection == reason) return;
            lastRejection = reason;
            log?.Warning("SP_NATIVE_QUOTE_REJECTED: " + reason);
            try { QuoteRejected?.Invoke(reason); }
            catch (Exception exception) { log?.Error("SP_NATIVE_QUOTE_NOTICE: " + exception.Message); }
        }

        private static void RefreshNativeUI()
        {
            if (NativeBridge.IsReady && NegociationUIManager.Instance != null) NegociationUIManager.Instance.RefreshUI();
        }

        private static bool SitesReady() => VerifiedSites.Contains("BuyItem") && VerifiedSites.Contains("SellItem") && VerifiedSites.Contains("CanBuyItemFromTable");

        internal static IEnumerable<CodeInstruction> RewritePriceCall(IEnumerable<CodeInstruction> instructions, string site)
        {
            var original = AccessTools.Method(typeof(GameItem), nameof(GameItem.GetNegociatedValue));
            var replacement = AccessTools.Method(typeof(NativeTradeBridge), nameof(ValueAtTradeSite));
            var list = new List<CodeInstruction>(instructions);
            var count = 0;
            foreach (var instruction in list)
            {
                if (!instruction.Calls(original)) continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                count++;
            }
            if (count == 1) VerifiedSites.Add(site);
            else VerifiedSites.Remove(site);
            return list;
        }

        // Explicit sourcing only: no random-pool edits, no replacement of the customer's goods.
        public static bool TryPlaceNpcOffer(string definitionId, string assetId, bool sourcingOptIn, out GameItem? item, out string error)
        {
            item = null;
            error = "";
            if (!sourcingOptIn || !enabled || !NativeBridge.IsReady) { error = "请先明确开启 StarPawn 货源业务。"; return false; }
            var customer = PlayerStore.Instance.currentClientInstance;
            if (customer == null || !customer.GetClientBlueprint().IsClientSelling()) { error = "当前没有可提供商品的卖家。"; return false; }
            if (string.IsNullOrWhiteSpace(assetId) || !definitionId.StartsWith("starpawn.", StringComparison.Ordinal)) { error = "货品标识无效。"; return false; }
            if (NativeBridge.FindAsset(assetId) != null) { error = "该资产实物已存在，不能重复提供。"; return false; }
            if (!PendingOffers.TryGetValue(assetId, out item))
            {
                item = ModHelper.SpawnItem(definitionId);
                if (item == null || NativeBridge.GetDefinitionId(item) != definitionId) { error = "该物品定义尚未注册。"; return false; }
                item.ModifyTag(NativeBridge.AssetIdTag, tag => tag.SetString(assetId));
                ModHelper.SetItemOwnerShip(item, false);
                PendingOffers.Add(assetId, item);
            }
            if (NativeBridge.GetDefinitionId(item) != definitionId) { error = "待放置货品类型不匹配。"; return false; }
            if (!NativeBridge.TryMove(item, EmporiumEntry.Instance.frontInvinvElement, out error)) return false;
            PendingOffers.Remove(assetId);
            return true;
        }
    }

    internal sealed class NativeTradeExecution
    {
        public NativeTradeContext Context = null!;
        public int CashBefore;
        public bool OwnedBefore;
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BuyItem))]
    internal static class StarPawnNativeBuy
    {
        private static bool Prefix(GameItem __0, out NativeTradeExecution? __state) => NativeTradeBridge.BeginTrade(__0, NativeTradeDirection.PlayerBuys, out __state);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NativeTradeBridge.RewritePriceCall(instructions, "BuyItem");
        private static Exception? Finalizer(Exception? __exception, NativeTradeExecution? __state) { NativeTradeBridge.EndTrade(__state, __exception); return __exception; }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class StarPawnNativeSell
    {
        private static bool Prefix(GameItem __0, out NativeTradeExecution? __state) => NativeTradeBridge.BeginTrade(__0, NativeTradeDirection.PlayerSells, out __state);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NativeTradeBridge.RewritePriceCall(instructions, "SellItem");
        private static Exception? Finalizer(Exception? __exception, NativeTradeExecution? __state) { NativeTradeBridge.EndTrade(__state, __exception); return __exception; }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanBuyItemFromTable))]
    internal static class StarPawnNativeCanBuy
    {
        private static bool Prefix(PlayerStore __instance, ref bool __result)
        {
            var item = __instance.GetCurrentlyNegociatedItem();
            if (NativeTradeBridge.GuardAvailability(item, NativeTradeDirection.PlayerBuys)) return true;
            __result = false; return false;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => NativeTradeBridge.RewritePriceCall(instructions, "CanBuyItemFromTable");
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanSellThisItem))]
    internal static class StarPawnNativeCanSell
    {
        private static bool Prefix(GameItem __0, ref bool __result)
        {
            if (NativeTradeBridge.GuardAvailability(__0, NativeTradeDirection.PlayerSells)) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.GetFinalOfferValue))]
    internal static class StarPawnNativeQuoteDisplay
    {
        private static void Postfix(GameItem __0, ref int __result) => NativeTradeBridge.OverrideDisplay(__0, ref __result);
    }

    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.HasBudgetLeftToBuy))]
    internal static class StarPawnNativeBudgetDisplay
    {
        private static void Postfix(StoreClient __instance, GameItem __0, ref bool __result) => NativeTradeBridge.OverrideBudgetCheck(__instance, __0, ref __result);
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BuyAllItemFromTable))]
    internal static class StarPawnNativeBuyBulkGuard { private static bool Prefix() => NativeTradeBridge.GuardBulk(); }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellAllItemFromTable))]
    internal static class StarPawnNativeSellBulkGuard { private static bool Prefix() => NativeTradeBridge.GuardBulk(); }
}
