using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StarPawn.Game
{
    public sealed class NativeItemDefinition
    {
        public string DefinitionId = "";
        public string DisplayName = "";
        public string VariantId = "";
        public string SpriteKey = "";
        public long BaseValue;
        public int WidthCells = 1;
        public int HeightCells = 1;
    }

    public sealed class NativeCashResult
    {
        public bool Success;
        public bool AlreadyApplied;
        public int Before;
        public int After;
        public string Error = "";
    }

    // No game implementation is copied here. These are checked adapters over local Rev13 APIs.
    public static class NativeBridge
    {
        public const string AssetIdTag = "starpawn.asset_id";
        public const string DefinitionTag = "starpawn.definition_id";
        public const string VariantTag = "starpawn.variant_id";
        public const string CustodyTag = "starpawn.custody_contract";
        private const string ModId = "starpawn";
        private const string WalletKey = "native-wallet-journal-v1";
        private static readonly Dictionary<string, NativeItemDefinition> Definitions = new Dictionary<string, NativeItemDefinition>(StringComparer.Ordinal);
        // A failed insertion retains the original instance for recovery instead of silently discarding it.
        private static readonly Dictionary<string, GameItem> PendingItems = new Dictionary<string, GameItem>(StringComparer.Ordinal);
        private static readonly Dictionary<string, DetachedNativeItem> DetachedItems = new Dictionary<string, DetachedNativeItem>(StringComparer.Ordinal);
        private static ModItemDirectory? directory;
        private static ModLog? log;
        private static Action<string>? inspect;
        private static bool enabled;
        private static bool runReady;
        private static bool cashBusy;
        private static PlayerStore? runStore;

        public static bool IsReady => enabled && runReady && PlayerStore.HasInstance && ReferenceEquals(runStore, PlayerStore.Instance);
        public static bool HasPendingPhysicalChanges => PendingItems.Count != 0 || DetachedItems.Count != 0;
        public static event Action<int>? NightlySettlementRequested;

        public static void Enable(ModLog modLog, Action<string> onInspect)
        {
            log = modLog;
            inspect = onInspect;
            if (enabled) return;
            enabled = true;
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnGameLoadedInit += BeginLoad;
            ModHook.OnGameLoadedLate += CompleteLoad;
            ModHook.OnNewGameStarted += NewGame;
            ModHook.OnHandlingNightlyServicesLate += NightlyServices;
        }

        public static void Disable()
        {
            if (!enabled) return;
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnGameLoadedInit -= BeginLoad;
            ModHook.OnGameLoadedLate -= CompleteLoad;
            ModHook.OnNewGameStarted -= NewGame;
            ModHook.OnHandlingNightlyServicesLate -= NightlyServices;
            enabled = false;
            runReady = false;
            runStore = null;
            inspect = null;
            directory = null;
            PendingItems.Clear();
            DetachedItems.Clear();
        }

        public static void RegisterDefinitions(IEnumerable<NativeItemDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                if (definition == null || !definition.DefinitionId.StartsWith("starpawn.", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(definition.DisplayName) || string.IsNullOrWhiteSpace(definition.SpriteKey) ||
                    string.IsNullOrWhiteSpace(definition.VariantId) || definition.BaseValue < 0 || definition.BaseValue > int.MaxValue ||
                    definition.WidthCells < 1 || definition.WidthCells > 8 || definition.HeightCells < 1 || definition.HeightCells > 8)
                    throw new ArgumentException("Invalid native item definition.");
                if (Definitions.ContainsKey(definition.DefinitionId)) continue;
                var copy = new NativeItemDefinition {
                    DefinitionId = definition.DefinitionId, DisplayName = definition.DisplayName,
                    VariantId = definition.VariantId, SpriteKey = definition.SpriteKey, BaseValue = definition.BaseValue,
                    WidthCells = definition.WidthCells, HeightCells = definition.HeightCells
                };
                Definitions.Add(copy.DefinitionId, copy);
                if (directory != null) RegisterItem(directory, copy);
            }
        }

        private static void RegisterItems(ModItemDirectory target)
        {
            directory = target;
            foreach (var definition in Definitions.Values) RegisterItem(target, definition);
        }

        private static void RegisterItem(ModItemDirectory target, NativeItemDefinition definition)
        {
            target.Add(definition.DefinitionId, () => CreateItem(definition));
        }

        private static GameItem CreateItem(NativeItemDefinition definition)
        {
            var item = ItemDirectory.CreateEmptyItem(definition.DefinitionId);
            item.name = definition.DisplayName;
            item.shortDescription = "StarPawn 资产 · 可使用放大镜鉴定";
            item.SetSpriteAndShapeFromMod(ModId, definition.SpriteKey);
            var cells = new byte[definition.WidthCells * definition.HeightCells];
            for (var i = 0; i < cells.Length; i++) cells[i] = 1;
            item.SetShape(new GridShapeBuilder().SetData(cells, definition.WidthCells));
            item.SetValue(definition.BaseValue);
            item.unitCount = 1;
            item.mayStackItemFunc = (first, second) => false;
            item.mayInventoryRemoveItemFunc = (current, inventory) => !IsInCustody(current);
            item.ModifyTag(AssetIdTag, tag => tag.SetString("sp-" + Guid.NewGuid().ToString("N")));
            item.ModifyTag(DefinitionTag, tag => tag.SetString(definition.DefinitionId));
            item.ModifyTag(VariantTag, tag => tag.SetString(definition.VariantId));
            InspectableHelper.InitInspectableItem(item);
            NativeEquipment.Initialize(item, definition.VariantId);
            ModHelper.SetSource(item, ModId, "StarPawn");
            return item;
        }

        private static void BeginLoad()
        {
            runReady = false;
            runStore = null;
            cashBusy = false;
            PendingItems.Clear();
            DetachedItems.Clear();
        }

        private static void CompleteLoad()
        {
            runStore = PlayerStore.HasInstance ? PlayerStore.Instance : null;
            runReady = runStore != null;
        }

        private static void NewGame(string scenario) { BeginLoad(); CompleteLoad(); }

        private static void NightlyServices()
        {
            if (IsReady) NightlySettlementRequested?.Invoke(StoreStation.GetDayCounter());
        }

        public static int GetGameDay() { RequireReady(); return StoreStation.GetDayCounter(); }
        public static int GetSaveSlot() { RequireReady(); return PlayerStore.Instance.saveSlotId; }
        public static int GetCash() { RequireReady(); return PlayerStore.Instance.GetCash(); }
        public static string GetAssetId(GameItem item) => item == null ? "" : item.GetTagReadonly(AssetIdTag).GetString() ?? "";
        public static string GetDefinitionId(GameItem item) => item == null ? "" : item.GetTagReadonly(DefinitionTag).GetString() ?? "";
        public static string GetVariantId(GameItem item) => item == null ? "" : item.GetTagReadonly(VariantTag).GetString() ?? "";
        public static bool IsStarPawnAsset(GameItem item) => item != null && (item.identifier ?? "").StartsWith("starpawn.", StringComparison.Ordinal) && !string.IsNullOrEmpty(GetAssetId(item));
        public static bool IsInCustody(GameItem item) => IsStarPawnAsset(item) && !string.IsNullOrEmpty(item.GetTagReadonly(CustodyTag).GetString());

        public static List<GameItem> GetOwnedAssets()
        {
            RequireReady();
            var result = new List<GameItem>();
            foreach (var item in EmporiumEntry.Instance.GetAllItems())
                if (IsStarPawnAsset(item) && GeneralHelper.IsItemOwned(item)) result.Add(item);
            return result;
        }

        public static GameItem? FindAsset(string assetId)
        {
            RequireReady();
            GameItem? result = null;
            foreach (var item in EmporiumEntry.Instance.GetAllItems())
            {
                if (!IsStarPawnAsset(item) || GetAssetId(item) != assetId) continue;
                if (result != null && !ReferenceEquals(result, item)) throw new InvalidOperationException("Duplicate native asset identity; operation stopped.");
                result = item;
            }
            return result;
        }

        public static void RefreshAppearance(string assetId, string textureKey)
        {
            var item = FindAsset(assetId);
            if (item == null) return;
            if (!textureKey.StartsWith(ModId + ":", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid asset texture scope.");
            var key = textureKey.Substring(ModId.Length + 1);
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModId);
            if (mod == null || !mod.Sprites.ContainsKey(key)) throw new InvalidOperationException("Missing native asset texture: " + key);
            // SetSprite preserves the existing inventory shape and the mod-sprite flag.
            // Re-running SetSpriteAndShapeFromMod here would resize an already placed item.
            item.SetSprite(ModId, key);
        }

        public static bool TrySpawnOwned(string definitionId, string assetId, out GameItem? item, out string error)
        {
            item = null;
            error = "";
            if (!IsReady) { error = "请先加载存档。"; return false; }
            if (string.IsNullOrWhiteSpace(assetId) || !Definitions.ContainsKey(definitionId)) { error = "资产或物品定义无效。"; return false; }
            var existing = FindAsset(assetId);
            if (existing != null)
            {
                item = existing;
                if (GetDefinitionId(existing) != definitionId || !GeneralHelper.IsItemOwned(existing)) { error = "资产已存在但归属或类型不匹配。"; return false; }
                return true;
            }
            if (!PendingItems.TryGetValue(assetId, out item))
            {
                item = ModHelper.SpawnItem(definitionId);
                item.ModifyTag(AssetIdTag, tag => tag.SetString(assetId));
                ModHelper.SetItemOwnerShip(item, true);
                PendingItems.Add(assetId, item);
            }
            if (GetDefinitionId(item) != definitionId) { error = "待放置资产类型不匹配。"; return false; }
            if (!TryMove(item, EmporiumEntry.Instance.invElement, out error)) return false;
            PendingItems.Remove(assetId);
            log?.Log("SP_NATIVE_SPAWN: asset=" + assetId + ", definition=" + definitionId);
            return true;
        }

        public static bool TryMove(GameItem item, GameInventory target, out string error)
        {
            error = "";
            if (!IsReady || item == null || target == null || !IsStarPawnAsset(item)) { error = "物品或目标库存不可用。"; return false; }
            if (item.unitCount != 1 || IsInCustody(item)) { error = "只允许移动单件且未托管的资产。"; return false; }
            var original = item.parentInventory;
            if (ReferenceEquals(original, target)) { PendingItems.Remove(GetAssetId(item)); return true; }
            if (!GraphUtils.CanAccept(target, item)) { error = "目标库存空间不足或不允许放入。"; return false; }
            var identity = GetAssetId(item);
            try
            {
                var moved = GraphUtils.TryAccept(target, item);
                if (moved == 1 && ReferenceEquals(item.parentInventory, target) && GetAssetId(item) == identity)
                { PendingItems.Remove(identity); return true; }
                error = "原生库存未确认完整转移。";
            }
            catch (Exception exception) { error = "库存转移异常：" + exception.Message; }
            // Native TryAccept can detach before insertion. Preserve the actual instance.
            if (item.parentInventory == null && original != null)
            {
                try { GraphUtils.TryAccept(original, item); }
                catch (Exception exception) { log?.Error("SP_NATIVE_MOVE_RECOVERY: " + exception.Message); }
            }
            if (item.parentInventory == null) PendingItems[identity] = item;
            return false;
        }

        public static bool TryMoveToMainInventory(string assetId, out string error)
        {
            var item = FindAsset(assetId);
            if (item == null) { error = "未找到实物资产。"; return false; }
            return TryMove(item, EmporiumEntry.Instance.invElement, out error);
        }

        public static int GetNativeUniqueId(GameItem item) => item == null ? -1 : item.uniqueId;

        public static bool TryRemoveOwned(string assetId, out GameItem? item, out string error)
        {
            item = null;
            error = "";
            if (!IsReady) { error = "存档尚未就绪。"; return false; }
            if (DetachedItems.TryGetValue(assetId, out var previous))
            {
                item = previous.Item;
                if (ReferenceEquals(previous.Store, PlayerStore.Instance) && item.parentInventory == null) return true;
                error = "资产移除状态需要核对。"; return false;
            }
            item = FindAsset(assetId);
            if (item == null || !GeneralHelper.IsItemOwned(item) || IsInCustody(item) || item.unitCount != 1 ||
                item.parentInventory == null || item.parentInventory.IsRemoveLocked() || !item.MayRemove())
            { error = "物品不存在、不归玩家所有或当前不能移除。"; return false; }
            var original = item.parentInventory;
            var detached = new DetachedNativeItem { Item = item, Original = original, Store = PlayerStore.Instance };
            try
            {
                var removed = original.Expel(item);
                if (!removed || item.parentInventory != null)
                {
                    error = "原生库存没有确认移除。";
                    if (item.parentInventory == null)
                    {
                        DetachedItems[assetId] = detached;
                        if (!RestoreDetached(item, out var restoreError)) error += "；" + restoreError;
                    }
                    return false;
                }
                DetachedItems.Add(assetId, detached);
                return true;
            }
            catch (Exception exception)
            {
                error = "移除物品失败：" + exception.Message;
                if (item.parentInventory == null)
                {
                    DetachedItems[assetId] = detached;
                    if (!RestoreDetached(item, out var restoreError)) error += "；" + restoreError;
                }
                return false;
            }
        }

        public static bool RestoreDetached(GameItem item, out string error)
        {
            error = "";
            if (!IsReady || item == null || !IsStarPawnAsset(item)) { error = "待恢复物品不可用。"; return false; }
            var id = GetAssetId(item);
            if (!DetachedItems.TryGetValue(id, out var detached) || !ReferenceEquals(detached.Item, item) ||
                !ReferenceEquals(detached.Store, PlayerStore.Instance))
            { error = "此实例没有当前存档的移除记录。"; return false; }
            if (item.parentInventory == null)
            {
                if (!TryMove(item, detached.Original, out error) &&
                    !TryMove(item, EmporiumEntry.Instance.invElement, out error)) return false;
            }
            if (!GeneralHelper.IsItemOwned(item)) { error = "恢复后的物品归属不匹配。"; return false; }
            DetachedItems.Remove(id);
            PendingItems.Remove(id);
            return true;
        }

        public static bool DiscardDetached(GameItem item)
        {
            if (!IsReady || item == null || item.parentInventory != null) return false;
            var id = GetAssetId(item);
            if (!DetachedItems.TryGetValue(id, out var detached) || !ReferenceEquals(detached.Item, item) ||
                !ReferenceEquals(detached.Store, PlayerStore.Instance) || IsInCustody(item)) return false;
            item.Destroy();
            DetachedItems.Remove(id);
            PendingItems.Remove(id);
            return true;
        }

        public static bool CancelPending(string assetId)
        {
            if (!IsReady || DetachedItems.ContainsKey(assetId)) return false;
            if (!PendingItems.TryGetValue(assetId, out var item)) return true;
            if (item.parentInventory != null || IsInCustody(item)) return false;
            item.Destroy();
            PendingItems.Remove(assetId);
            return true;
        }

        // Minimal physical custody keeps the actual item in its saved container and locks it.
        // The contract/UI must say that the collateral occupies this space until released.
        public static bool TrySetCustody(string assetId, string contractId, out string error)
        {
            error = "";
            var item = FindAsset(assetId);
            if (item == null || !GeneralHelper.IsItemOwned(item) || string.IsNullOrWhiteSpace(contractId)) { error = "抵押物不存在或不归玩家所有。"; return false; }
            var current = item.GetTagReadonly(CustodyTag).GetString();
            if (!string.IsNullOrEmpty(current) && current != contractId) { error = "物品已被其他合同托管。"; return false; }
            item.ModifyTag(CustodyTag, tag => tag.SetString(contractId));
            GeneralHelper.LockItem(item);
            return item.GetTagReadonly(CustodyTag).GetString() == contractId;
        }

        public static bool TryReleaseCustody(string assetId, string contractId, out string error)
        {
            error = "";
            var item = FindAsset(assetId);
            if (item == null || item.GetTagReadonly(CustodyTag).GetString() != contractId) { error = "托管合同与实物不匹配。"; return false; }
            item.ModifyTag(CustodyTag, tag => tag.SetString(""));
            GeneralHelper.UnlockItem(item);
            item.mayInventoryRemoveItemFunc = (current, inventory) => !IsInCustody(current);
            return true;
        }

        internal static bool TryHandleInspection(GameItem item)
        {
            if (!IsReady || inspect == null || !IsStarPawnAsset(item)) return false;
            try { inspect(GetAssetId(item)); return true; }
            catch (Exception exception) { log?.Error("SP_NATIVE_INSPECT: " + exception); return false; }
        }

        public static bool OpenInspect(string assetId)
        {
            var item = FindAsset(assetId);
            return item != null && TryHandleInspection(item);
        }

        public static string GetCashOperationStatus(string operationId)
        {
            if (!IsReady) throw new InvalidOperationException("Wallet context is not ready.");
            return ReadWalletJournal().Operations.TryGetValue(operationId, out var receipt) ? receipt.Status : "None";
        }

        public static NativeCashResult ApplyCashOnce(string operationId, int delta, int expectedBefore, long sequence = 0)
        {
            var result = new NativeCashResult { Before = expectedBefore, After = expectedBefore };
            if (!IsReady || cashBusy) { result.Error = "钱包尚未就绪或正在执行操作。"; return result; }
            if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 200) { result.Error = "缺少有效资金操作标识。"; return result; }
            if (delta == int.MinValue) { result.Error = "金额超出支持范围。"; return result; }
            long proposed = (long)expectedBefore + delta;
            if (expectedBefore < 0 || proposed < 0 || proposed > int.MaxValue) { result.Error = "余额不足或金额溢出。"; return result; }
            cashBusy = true;
            try
            {
                var journal = ReadWalletJournal();
                if (journal.Operations.TryGetValue(operationId, out var prior))
                {
                    result.Before = prior.Before;
                    result.After = prior.After;
                    if (prior.Delta != delta || prior.Before != expectedBefore) { result.Error = "操作标识已用于另一笔金额。"; return result; }
                    if (prior.Status != "Applied") { result.Error = "此资金操作状态不确定，已暂停重复执行。"; return result; }
                    result.Success = true;
                    result.AlreadyApplied = true;
                    return result;
                }
                var separator = operationId.LastIndexOf(':');
                if (sequence == 0 && separator > 0)
                    long.TryParse(operationId.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
                var scope = separator > 0 ? operationId.Substring(0, separator) : "";
                if (sequence < 0 || (sequence > 0 && (scope.Length == 0 || operationId != scope + ":" + sequence.ToString(CultureInfo.InvariantCulture))))
                { result.Error = "资金操作序号与标识不匹配。"; return result; }
                if (sequence > 0)
                {
                    if (journal.SequenceScope.Length != 0 && journal.SequenceScope != scope)
                    { result.Error = "资金操作属于另一局序号空间。"; return result; }
                    if (sequence <= journal.HighWatermark)
                    { result.Error = "旧序号已越过高水位，拒绝重放；请使用核心已提交记录核对。"; return result; }
                    journal.SequenceScope = scope;
                }
                foreach (var record in journal.Operations.Values)
                    if (record.Status != "Applied") { result.Error = "存在待核对资金操作，钱包已暂停新业务。"; return result; }
                if (journal.Operations.Count >= 2048) { result.Error = "资金操作记录达到上限，需要受控归档。"; return result; }
                var before = PlayerStore.Instance.GetCash();
                if (before != expectedBefore) { result.Error = "原版现金已变化，请重新确认金额。"; return result; }
                var entry = new NativeWalletRecord { Before = before, After = (int)proposed, Delta = delta, Status = "Applying", Sequence = sequence };
                journal.Operations.Add(operationId, entry);
                WriteWalletJournal(journal);
                try
                {
                    if (delta < 0) PlayerStore.Instance.PayMoney(-delta, false);
                    else if (delta > 0) PlayerStore.Instance.ModCash(delta, false);
                    if (PlayerStore.Instance.GetCash() != entry.After) throw new InvalidOperationException("Native wallet did not match the expected balance.");
                    entry.Status = "Applied";
                    if (sequence > 0)
                    {
                        journal.HighWatermark = sequence;
                        CompactWalletJournal(journal);
                    }
                    WriteWalletJournal(journal);
                    result.Success = true;
                    result.Before = before;
                    result.After = entry.After;
                    return result;
                }
                catch (Exception exception)
                {
                    entry.Status = "Uncertain";
                    WriteWalletJournal(journal);
                    result.Error = "资金边界异常，已保留记录并暂停：" + exception.Message;
                    return result;
                }
            }
            catch (Exception exception) { result.Error = "资金记录不可用，原数据未清空：" + exception.Message; return result; }
            finally { cashBusy = false; }
        }

        private static NativeWalletJournal ReadWalletJournal()
        {
            var raw = ModHelper.GetModData(ModId, WalletKey, null);
            if (string.IsNullOrEmpty(raw)) return new NativeWalletJournal();
            var parsed = JObject.Parse(raw);
            var schema = parsed["SchemaVersion"];
            if (schema == null || schema.Type != JTokenType.Integer || schema.Value<int>() != 1)
                throw new InvalidOperationException("Unsupported or missing wallet journal schema.");
            var journal = JsonConvert.DeserializeObject<NativeWalletJournal>(raw);
            if (journal == null || journal.SchemaVersion != 1 || journal.Operations == null) throw new InvalidOperationException("Unsupported wallet journal.");
            if (journal.HighWatermark < 0 || journal.SequenceScope == null || (journal.HighWatermark > 0 && journal.SequenceScope.Length == 0))
                throw new InvalidOperationException("Invalid wallet sequence watermark.");
            foreach (var entry in journal.Operations)
            {
                var record = entry.Value;
                if (string.IsNullOrWhiteSpace(entry.Key) || record == null || record.Before < 0 || record.After < 0 || record.Sequence < 0 ||
                    (long)record.Before + record.Delta != record.After ||
                    (record.Status != "Applying" && record.Status != "Applied" && record.Status != "Uncertain"))
                    throw new InvalidOperationException("Invalid wallet journal record.");
            }
            return journal;
        }

        private static void CompactWalletJournal(NativeWalletJournal journal)
        {
            var completed = new List<KeyValuePair<string, NativeWalletRecord>>();
            foreach (var pair in journal.Operations)
                if (pair.Value.Status == "Applied" && pair.Value.Sequence > 0) completed.Add(pair);
            completed.Sort((left, right) => left.Value.Sequence.CompareTo(right.Value.Sequence));
            for (var i = 0; i < completed.Count - 256; i++) journal.Operations.Remove(completed[i].Key);
        }

        private static void WriteWalletJournal(NativeWalletJournal journal)
        {
            RequireReady();
            var raw = JsonConvert.SerializeObject(journal);
            ModHelper.SetModData(ModId, WalletKey, raw);
            if (ModHelper.GetModData(ModId, WalletKey, null) != raw) throw new InvalidOperationException("Mod data write did not read back.");
        }

        private static void RequireReady()
        {
            if (!IsReady) throw new InvalidOperationException("Wait for OnGameLoadedLate before native operations.");
        }

        internal static bool CanSaveNativeState()
        {
            if (!enabled || !PlayerStore.HasInstance) return true;
            var pending = HasPendingPhysicalChanges ||
                !string.IsNullOrEmpty(ModHelper.GetModData(ModId, "commerce-recovery-v1", null)) ||
                !string.IsNullOrEmpty(ModHelper.GetModData(ModId, "native-trade-recovery-v1", null));
            var trade = ModHelper.GetModData(ModId, "native-trade-root-pending-v1", null);
            if (!string.IsNullOrEmpty(trade))
            {
                try { pending |= JObject.Parse(trade)["Status"]?.Value<string>() != "Prepared"; }
                catch { pending = true; }
            }
            if (pending) log?.Warning("SP_SAVE_BLOCKED: unresolved StarPawn transaction; preserved previous disk save.");
            return !pending;
        }
    }

    public sealed class NativeWalletJournal
    {
        public int SchemaVersion = 1;
        public long HighWatermark;
        public string SequenceScope = "";
        public Dictionary<string, NativeWalletRecord> Operations = new Dictionary<string, NativeWalletRecord>(StringComparer.Ordinal);
    }

    public sealed class NativeWalletRecord
    {
        public int Before;
        public int After;
        public int Delta;
        public long Sequence;
        public string Status = "Applying";
    }

    internal sealed class DetachedNativeItem
    {
        public GameItem Item = null!;
        public GameInventory Original = null!;
        public PlayerStore Store = null!;
    }

    // The game's native loader applies attribute patches. Do not PatchAll a second time.
    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OnInspectItem))]
    internal static class StarPawnInspectionRoute
    {
        private static bool Prefix(GameItem __0) => !NativeBridge.TryHandleInspection(__0);
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class StarPawnCustodySaleGuard
    {
        private static bool Prefix(GameItem __0) => !NativeBridge.IsInCustody(__0);
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class StarPawnTransactionSaveGuard
    {
        private static bool Prefix() => NativeBridge.CanSaveNativeState();
    }
}
