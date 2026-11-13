using System;
using System.Collections.Generic;
using HarmonyLib;

namespace StarPawn.Game
{
    public sealed class NativeCardInfo
    {
        public string Kind = "";
        public string AccountId = "";
        public int Serial;
        public string RunId = "";
    }

    // Cards are account references, never Catalog assets or containers of money.
    public static class NativeCards
    {
        public const string SavingsDefinition = "starpawn.savings_card";
        public const string CreditDefinition = "starpawn.credit_card";
        private const string KindTag = "starpawn.card_kind";
        private const string AccountTag = "starpawn.card_account";
        private const string SerialTag = "starpawn.card_serial";
        private const string RunTag = "starpawn.card_run";
        private static readonly Dictionary<string, GameItem> Retained = new Dictionary<string, GameItem>(StringComparer.Ordinal);
        private static ModLog? log;
        private static Action<NativeCardInfo>? inspect;
        private static bool enabled;

        public static void Enable(ModLog modLog, Action<NativeCardInfo> onInspect)
        {
            log = modLog;
            inspect = onInspect ?? throw new ArgumentNullException(nameof(onInspect));
            if (enabled) return;
            enabled = true;
            ModHook.OnModItemDirectoryInit += RegisterCards;
            ModHook.OnGameLoadedInit += ClearSession;
            ModHook.OnNewGameStarted += NewGame;
        }

        public static void Disable()
        {
            if (enabled)
            {
                ModHook.OnModItemDirectoryInit -= RegisterCards;
                ModHook.OnGameLoadedInit -= ClearSession;
                ModHook.OnNewGameStarted -= NewGame;
            }
            enabled = false;
            inspect = null;
            Retained.Clear();
        }

        private static void NewGame(string scenario) => ClearSession();
        private static void ClearSession() => Retained.Clear();

        private static void RegisterCards(ModItemDirectory directory)
        {
            directory.Add(SavingsDefinition, () => CreateCard("savings"));
            directory.Add(CreditDefinition, () => CreateCard("credit"));
        }

        private static GameItem CreateCard(string kind)
        {
            var item = ItemDirectory.CreateEmptyItem(kind == "savings" ? SavingsDefinition : CreditDefinition);
            item.name = kind == "savings" ? "星港银行储蓄卡" : "星港银行信用卡";
            item.shortDescription = "实名账户凭证 · 用放大镜查看账户 · 不可出售或加工";
            item.SetSpriteAndShapeFromMod("starpawn", kind == "savings" ? "bank_card" : "credit_card");
            item.SetShape(new GridShapeBuilder().SetData(new byte[] { 1, 1 }, 2));
            item.SetValue(0);
            item.unitCount = 1;
            item.mayStackItemFunc = (first, second) => false;
            // The main stock inventory is a verified persistent ordinary container.
            // Do not let cards enter machines, customer tables, barter, or trash slots.
            item.mayInventoryAddItemFunc = (current, inventory) => IsOrdinaryInventory(inventory);
            item.mayInventoryRemoveItemFunc = (current, inventory) =>
                GeneralHelper.IsItemOwned(current) && IsOrdinaryInventory(inventory);
            item.ModifyTag(KindTag, tag => tag.SetString(kind));
            InspectableHelper.InitInspectableItem(item);
            ModHelper.SetSource(item, "starpawn", "StarPawn");
            return item;
        }

        private static bool IsOrdinaryInventory(GameInventory inventory)
            => PlayerStore.HasInstance && EmporiumEntry.Instance != null && ReferenceEquals(inventory, EmporiumEntry.Instance.invElement);

        public static bool IsCard(GameItem item)
            => item != null && (item.identifier == SavingsDefinition || item.identifier == CreditDefinition);

        public static NativeCardInfo? ReadCard(GameItem item)
        {
            if (!IsCard(item)) return null;
            try
            {
                var info = new NativeCardInfo {
                    Kind = item.GetTagReadonly(KindTag).GetString() ?? "",
                    AccountId = item.GetTagReadonly(AccountTag).GetString() ?? "",
                    Serial = item.GetTagReadonly(SerialTag).GetInt(),
                    RunId = item.GetTagReadonly(RunTag).GetString() ?? ""
                };
                if ((info.Kind != "savings" && info.Kind != "credit") || string.IsNullOrWhiteSpace(info.AccountId) ||
                    string.IsNullOrWhiteSpace(info.RunId) || info.Serial < 1 ||
                    item.identifier != (info.Kind == "savings" ? SavingsDefinition : CreditDefinition)) return null;
                return info;
            }
            catch { return null; }
        }

        public static GameItem? FindCurrent(string kind, string accountId, int serial, string runId)
        {
            if (!enabled || !NativeBridge.IsReady) return null;
            GameItem? found = null;
            foreach (var item in EmporiumEntry.Instance.GetAllItems())
            {
                if (!GeneralHelper.IsItemOwned(item) || !Matches(item, kind, accountId, serial, runId)) continue;
                if (found != null && !ReferenceEquals(found, item))
                    throw new InvalidOperationException("同一卡号存在重复实体卡，请先核对；不会再补发。");
                found = item;
            }
            return found;
        }

        public static bool HasCurrent(string kind, string accountId, int serial, string runId)
            => FindCurrent(kind, accountId, serial, runId) != null;

        private static bool Matches(GameItem item, string kind, string accountId, int serial, string runId)
        {
            var card = ReadCard(item);
            return card != null && card.Kind == kind && card.AccountId == accountId && card.Serial == serial && card.RunId == runId;
        }

        public static bool TryIssue(string kind, string accountId, int serial, string runId, out GameItem? item, out string error)
        {
            item = null; error = "";
            if (!enabled || !NativeBridge.IsReady) { error = "存档尚未就绪，实体卡稍后领取。"; return false; }
            if ((kind != "savings" && kind != "credit") || string.IsNullOrWhiteSpace(accountId) || serial < 1 || string.IsNullOrWhiteSpace(runId))
            { error = "账户或卡号引用无效。"; return false; }
            var key = kind + "\n" + accountId + "\n" + serial + "\n" + runId;
            try
            {
                item = FindCurrent(kind, accountId, serial, runId);
                if (item != null) { Retained.Remove(key); return true; }
                if (!Retained.TryGetValue(key, out item))
                {
                    item = ModHelper.SpawnItem(kind == "savings" ? SavingsDefinition : CreditDefinition);
                    if (item == null || !IsCard(item))
                    {
                        error = "银行卡物品尚未注册。";
                        ReleaseUnplaced(key, ref item);
                        return false;
                    }
                    Retained.Add(key, item);
                    item.ModifyTag(KindTag, tag => tag.SetString(kind));
                    item.ModifyTag(AccountTag, tag => tag.SetString(accountId));
                    item.ModifyTag(SerialTag, tag => tag.SetInt(serial));
                    item.ModifyTag(RunTag, tag => tag.SetString(runId));
                    ModHelper.SetItemOwnerShip(item, true);
                }
                if (!Matches(item, kind, accountId, serial, runId))
                { error = "待发银行卡身份不一致，已保留原物供核对。"; return false; }
                if (item.parentInventory != null)
                {
                    if (GeneralHelper.IsItemOwned(item) && IsOrdinaryInventory(item.parentInventory))
                    { Retained.Remove(key); return true; }
                    error = "银行卡已经连接到其他库存，请先核对位置；不会再次生成。";
                    return false;
                }
                var target = EmporiumEntry.Instance.invElement;
                if (!GraphUtils.CanAccept(target, item))
                {
                    error = "库存需要空出连续两格。账户已有效，实体卡可稍后领取。";
                    ReleaseUnplaced(key, ref item);
                    return false;
                }
                var accepted = GraphUtils.TryAccept(target, item);
                // Inspect actual attachment, not only the integer result; some callbacks
                // can throw after the item has already been attached successfully.
                if (ReferenceEquals(item.parentInventory, target) && GeneralHelper.IsItemOwned(item) && Matches(item, kind, accountId, serial, runId))
                {
                    Retained.Remove(key);
                    log?.Log("SP_CARD_ISSUED: kind=" + kind + ", serial=" + serial + ", native=" + item.uniqueId + ", accepted=" + accepted);
                    return true;
                }
                error = "原生库存没有确认完整接收；账户有效，实体卡暂待领取。";
                ReleaseUnplaced(key, ref item);
                return false;
            }
            catch (Exception exception)
            {
                if (item != null && GeneralHelper.IsItemOwned(item) && item.parentInventory != null &&
                    IsOrdinaryInventory(item.parentInventory) && Matches(item, kind, accountId, serial, runId))
                {
                    Retained.Remove(key);
                    log?.Warning("SP_CARD_ISSUE_RECOVERED: attached card verified after callback error: " + exception.Message);
                    return true;
                }
                error = "实体卡发行暂未完成：" + exception.Message;
                try { ReleaseUnplaced(key, ref item); }
                catch (Exception cleanup) { error += "；未放置的卡片已保留供核对：" + cleanup.Message; }
                log?.Warning("SP_CARD_ISSUE_PENDING: " + error);
                return false;
            }
        }

        private static void ReleaseUnplaced(string key, ref GameItem? item)
        {
            if (item == null) return;
            // Never destroy an attached card on an ambiguous inventory result.
            if (item.parentInventory != null) return;
            item.Destroy();
            Retained.Remove(key);
            item = null;
        }

        internal static bool HandleInspection(GameItem item)
        {
            if (!enabled || !IsCard(item)) return false;
            if (!NativeBridge.IsReady || !GeneralHelper.IsItemOwned(item)) return true;
            var card = ReadCard(item);
            if (card == null) { log?.Warning("SP_CARD_INVALID: card identity could not be read."); return true; }
            try { inspect?.Invoke(card); }
            catch (Exception error) { log?.Error("SP_CARD_INSPECTION: " + error); }
            return true;
        }

        internal static bool ContainsCard(GraphNodeStorage? root)
        {
            if (root == null) return false;
            var visited = new HashSet<GraphNodeStorage>();
            var pending = new Stack<GraphNodeStorage>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var node = pending.Pop();
                if (!visited.Add(node)) continue;
                if (node is GameItem item && IsCard(item)) return true;
                foreach (var child in node.children) if (child != null) pending.Push(child);
            }
            return false;
        }

        internal static bool AllowTrade(GameItem item) => !enabled || !ContainsCard(item);

        internal static bool AllowTableTrade()
            => !enabled || (EmporiumEntry.Instance != null && !ContainsCard(EmporiumEntry.Instance.backInvinvElement) &&
                !ContainsCard(EmporiumEntry.Instance.frontInvinvElement));

        internal static bool AllowCraft(List<GameItem> items)
        {
            if (!enabled) return true;
            foreach (var item in items) if (ContainsCard(item)) return false;
            return true;
        }

        internal static bool AllowTarget(GameItem source, GameItem target)
        {
            if (!enabled) return true;
            if (ContainsCard(source)) return false;
            return !ContainsCard(target) || (source != null && source.IsTag("MAGNIFYING_GLASS"));
        }
    }

    // Positional arguments are intentional: local Rev13 names this parameter
    // gameItem on several targets, and item on others. Compile checks cannot bind Harmony.
    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OnInspectItem))]
    internal static class StarPawnCardInspection { private static bool Prefix(GameItem __0) => !NativeCards.HandleInspection(__0); }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BuyItem))]
    internal static class StarPawnCardBuyGuard { private static bool Prefix(GameItem __0) => NativeCards.AllowTrade(__0); }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class StarPawnCardSellGuard { private static bool Prefix(GameItem __0) => NativeCards.AllowTrade(__0); }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanSellThisItem))]
    internal static class StarPawnCardCanSellGuard
    { private static bool Prefix(GameItem __0, ref bool __result) { if (NativeCards.AllowTrade(__0)) return true; __result=false; return false; } }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanBuyItemFromTable))]
    internal static class StarPawnCardCanBuyGuard
    { private static bool Prefix(PlayerStore __instance, ref bool __result) { if (NativeCards.AllowTrade(__instance.GetCurrentlyNegociatedItem())) return true; __result=false; return false; } }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BuyAllItemFromTable))]
    internal static class StarPawnCardBuyAllGuard { private static bool Prefix() => NativeCards.AllowTableTrade(); }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellAllItemFromTable))]
    internal static class StarPawnCardSellAllGuard { private static bool Prefix() => NativeCards.AllowTableTrade(); }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.AcceptBarter))]
    internal static class StarPawnCardBarterGuard { private static bool Prefix() => NativeCards.AllowTableTrade(); }
    [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.CanCraft), new Type[] { typeof(List<GameItem>) })]
    internal static class StarPawnCardCanCraftGuard
    { private static bool Prefix(List<GameItem> __0, ref bool __result) { if (NativeCards.AllowCraft(__0)) return true; __result=false; return false; } }
    [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.Craft), new Type[] { typeof(List<GameItem>) })]
    internal static class StarPawnCardCraftGuard
    { private static bool Prefix(List<GameItem> __0, ref List<GameItem> __result) { if (NativeCards.AllowCraft(__0)) return true; __result=new List<GameItem>(); return false; } }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.CanTarget))]
    internal static class StarPawnCardCanTargetGuard
    { private static bool Prefix(GameItem __instance, GameItem __0, ref bool __result) { if (NativeCards.AllowTarget(__instance,__0)) return true; __result=false; return false; } }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.Target))]
    internal static class StarPawnCardTargetGuard
    {
        private static bool Prefix(GameItem __instance, GameItem __0)
        {
            if (!NativeCards.AllowTarget(__instance,__0)) return false;
            if (__instance.IsTag("MAGNIFYING_GLASS") && NativeCards.HandleInspection(__0)) return false;
            return true;
        }
    }
}
