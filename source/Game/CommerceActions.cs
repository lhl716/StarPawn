using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed class CommerceRecovery
    {
        public int SchemaVersion = 1;
        public string OperationId = "";
        public string Phase = "Prepared";
        public int CashBefore;
        public long CashDelta;
        public string Description = "";
        public RunState Proposed = null!;
    }

    public sealed partial class ModEntry
    {
        private const string CommerceRecoveryKey = "commerce-recovery-v1";

        private sealed class PhysicalChanges
        {
            internal readonly List<Action> Undo = new List<Action>();
            internal readonly List<Action> Finish = new List<Action>();
            internal void Rollback()
            {
                var errors = new List<Exception>();
                for (var i = Undo.Count - 1; i >= 0; i--)
                    try { Undo[i](); } catch (Exception error) { errors.Add(error); }
                if (errors.Count != 0) throw new AggregateException("实物补偿未完全完成。", errors);
            }
            internal void Complete() { foreach (var action in Finish) action(); }
        }

        private bool HasCommerceRecovery() => !string.IsNullOrEmpty(ModHelper.GetModData(ModId, CommerceRecoveryKey, null));

        private void SyncNativeAppearance(AssetRecord asset)
        {
            if (asset.Status == "consumed") return;
            NativeBridge.RefreshAppearance(asset.AssetId, Catalog.TextureFor(asset, asset.WindowOpened ? "window" : "front"));
        }

        private void RequireCommerceReady()
        {
            if (!EnsureRun() || state == null || !NativeBridge.IsReady) throw new InvalidOperationException("当前存档尚未就绪。");
            if (HasCommerceRecovery() || NativeBridge.HasPendingPhysicalChanges)
            { blocked = true; throw new InvalidOperationException("上一笔业务需要核对，已暂停新业务并保留原记录。"); }
        }

        private void WriteCommerceRecovery(CommerceRecovery recovery)
        {
            var raw = JsonConvert.SerializeObject(recovery);
            ModHelper.SetModData(ModId, CommerceRecoveryKey, raw);
            if (ModHelper.GetModData(ModId, CommerceRecoveryKey, null) != raw)
                throw new InvalidOperationException("事务准备记录读回失败。");
        }

        private void ClearCommerceRecovery()
        {
            ModHelper.RemoveModData(ModId, CommerceRecoveryKey);
            if (HasCommerceRecovery()) throw new InvalidOperationException("事务完成记录未能清除。");
        }

        // Persist a proposed state before touching inventory or cash. If either boundary
        // becomes uncertain, retain that proposal and stop; never invent a cash reversal.
        private void RunCommerce(RunState next, string operationId, string description, long cashDelta,
            string assetId, Action<PhysicalChanges> stage)
        {
            var previous = state;
            var before = Cash();
            if (cashDelta < -int.MaxValue || cashDelta > int.MaxValue || (long)before + cashDelta < 0 || (long)before + cashDelta > int.MaxValue)
                throw new InvalidOperationException("现金不足或金额超出原版范围。");
            var recovery = new CommerceRecovery { OperationId = operationId, CashBefore = before, CashDelta = cashDelta, Description = description, Proposed = next };
            WriteCommerceRecovery(recovery);
            var changes = new PhysicalChanges();
            try
            {
                stage(changes);
                Validate(next);
                recovery.Phase = "PhysicalStaged";
                WriteCommerceRecovery(recovery);
                Commit(next, operationId, description, cashDelta, assetId);
                recovery.Phase = "Committed";
                WriteCommerceRecovery(recovery);
                changes.Complete();
                if (NativeBridge.HasPendingPhysicalChanges) throw new InvalidOperationException("实物提交仍有未完成转移。");
                ClearCommerceRecovery();
            }
            catch (Exception failure)
            {
                var safeRollback = ReferenceEquals(previous, state) && Cash() == before;
                try { safeRollback &= NativeBridge.GetCashOperationStatus(operationId) == "None"; }
                catch { safeRollback = false; }
                if (safeRollback)
                {
                    try
                    {
                        changes.Rollback();
                        if (NativeBridge.HasPendingPhysicalChanges) throw new InvalidOperationException("原生库存仍有待恢复实物。");
                        ClearCommerceRecovery();
                    }
                    catch (Exception compensation) { blocked = true; throw new AggregateException("业务未完成，实物需要人工核对；请保留当前存档。", failure, compensation); }
                }
                else blocked = true;
                throw;
            }
        }

        private GameItem RequireNativeOwned(AssetRecord asset)
        {
            if (asset.Status != "active" || asset.OwnerId != "player") throw new InvalidOperationException("资产已转移、托管或消耗。");
            var item = NativeBridge.FindAsset(asset.AssetId);
            if (item == null || !GeneralHelper.IsItemOwned(item) || NativeBridge.IsInCustody(item) ||
                NativeBridge.GetDefinitionId(item) != "starpawn." + asset.DefinitionId || item.unitCount != 1)
                throw new InvalidOperationException("登记资产与当前实物不一致，未执行操作。");
            return item;
        }

        private void StageSpawn(RunState next, AssetRecord asset, PhysicalChanges changes)
        {
            if (NativeBridge.FindAsset(asset.AssetId) != null) throw new InvalidOperationException("该资产已有实物，拒绝重复生成。");
            changes.Undo.Add(() => {
                var present = NativeBridge.FindAsset(asset.AssetId);
                if (present != null)
                {
                    if (!NativeBridge.TryRemoveOwned(asset.AssetId, out var removed, out var removeError) || removed == null)
                        throw new InvalidOperationException(removeError);
                    if (!NativeBridge.DiscardDetached(removed)) throw new InvalidOperationException("新生成物品无法撤销。");
                }
                if (!NativeBridge.CancelPending(asset.AssetId)) throw new InvalidOperationException("待入库实物无法撤销。");
            });
            if (!NativeBridge.TrySpawnOwned("starpawn." + asset.DefinitionId, asset.AssetId, out var item, out var error) || item == null)
                throw new InvalidOperationException(error);
            asset.NativeItemUniqueId = NativeBridge.GetNativeUniqueId(item);
            SyncNativeAppearance(asset);
        }

        private void StageDetach(AssetRecord asset, PhysicalChanges changes)
        {
            if (!NativeBridge.TryRemoveOwned(asset.AssetId, out var item, out var error) || item == null)
                throw new InvalidOperationException(error);
            changes.Undo.Add(() => { if (!NativeBridge.RestoreDetached(item, out var restoreError)) throw new InvalidOperationException(restoreError); });
            changes.Finish.Add(() => { if (!NativeBridge.DiscardDetached(item)) throw new InvalidOperationException("交付后的实物未能完成移除，需要核对。"); });
            asset.NativeItemUniqueId = -1;
        }

        private AssetQuote ValidateCommerceQuote(AssetQuote candidate, string direction)
        {
            if (candidate == null) throw new InvalidOperationException("缺少报价。");
            var market = Market();
            if (!ValuationV2.IsCurrent(state!.Catalog, candidate, Day(), market.Version, direction))
                throw new InvalidOperationException("物品、证据、交易方向或行情已变化，请重新报价。");
            var actual = QuoteByMarket(state.Catalog, candidate.EncounterId, direction, market);
            if (actual.Price != candidate.Price || actual.AssetId != candidate.AssetId || actual.CounterpartyId != candidate.CounterpartyId)
                throw new InvalidOperationException("报价金额与当前估价不一致，请重新确认。");
            if (actual.Price <= 0 || actual.Price > int.MaxValue) throw new InvalidOperationException("此物品没有有效的交易金额。");
            return actual;
        }

        private void PurchaseAsset(AssetQuote candidate)
        {
            RequireCommerceReady();
            var quote = ValidateCommerceQuote(candidate, "buy");
            var old = Catalog.GetAsset(state!.Catalog, quote.AssetId);
            if (old.OwnerId == "player" || old.Status != "active" || !state.OfferAssetIds.Contains(old.AssetId))
                throw new InvalidOperationException("该来客商品已不在本次可售清单。");
            if (NativeBridge.FindAsset(old.AssetId) != null)
                throw new InvalidOperationException("这是原生柜台实物，请使用原生交易确认流程。");
            var next = Clone();
            Catalog.Transfer(next.Catalog, old.AssetId, old.OwnerId, "player");
            InspectionV2.EndEncounter(next.Catalog, quote.EncounterId);
            next.OfferAssetIds.Remove(old.AssetId);
            var asset = Catalog.GetAsset(next.Catalog, old.AssetId);
            RunCommerce(next, OperationId(next), "购入 " + Catalog.GetDefinition(asset.DefinitionId).Name, -quote.Price, asset.AssetId,
                changes => StageSpawn(next, asset, changes));
            notice = "商品已进入原版库存，支付 " + quote.Price + " Cr。";
        }

        private void SellAsset(AssetQuote candidate)
        {
            RequireCommerceReady();
            var quote = ValidateCommerceQuote(candidate, "sell");
            var old = Catalog.GetAsset(state!.Catalog, quote.AssetId);
            var item = RequireNativeOwned(old);
            if (PlayerStore.Instance.currentClientInstance != null && ReferenceEquals(PlayerStore.Instance.GetCurrentlyNegociatedItem(), item))
                throw new InvalidOperationException("该物品正在原生柜台议价，请与柜台客户成交或先收回库存。");
            var next = Clone();
            Catalog.Transfer(next.Catalog, old.AssetId, "player", "starpawn-buyer:" + quote.CounterpartyId);
            InspectionV2.EndEncounter(next.Catalog, quote.EncounterId);
            next.OfferAssetIds.Remove(old.AssetId);
            var asset = Catalog.GetAsset(next.Catalog, old.AssetId);
            RunCommerce(next, OperationId(next), "售出 " + Catalog.GetDefinition(asset.DefinitionId).Name, quote.Price, asset.AssetId,
                changes => StageDetach(asset, changes));
            notice = "实物已交付，收到 " + quote.Price + " Cr。";
        }

        private void ProcessAsset(string assetId, string action)
        {
            RequireCommerceReady();
            var old = Catalog.GetAsset(state!.Catalog, assetId);
            var item = RequireNativeOwned(old);
            if (PlayerStore.Instance.currentClientInstance != null && ReferenceEquals(PlayerStore.Instance.GetCurrentlyNegociatedItem(), item))
                throw new InvalidOperationException("加工前请先从原生议价台收回这件物品。");
            var next = Clone();
            var operation = OperationId(next);
            AssetActionResult result;
            switch (action)
            {
                case "repair": result = Catalog.Repair(next.Catalog, assetId, operation, Cash()); break;
                case "window": result = Catalog.OpenJadeWindow(next.Catalog, assetId, Cash()); break;
                case "cut_pendants": result = Catalog.CutJade(next.Catalog, assetId, "pendants", Cash()); break;
                case "cut_cabochon": result = Catalog.CutJade(next.Catalog, assetId, "cabochon", Cash()); break;
                case "material": case "authenticity": case "edition": result = Catalog.IssueCertificate(next.Catalog, assetId, action, Day(), Cash()); break;
                case "read": result = Catalog.ReadBook(next.Catalog, assetId); break;
                default: throw new InvalidOperationException("未知加工服务。");
            }
            if (!result.Changed) { notice = result.Message; return; }
            var asset = Catalog.GetAsset(next.Catalog, assetId);
            RunCommerce(next, operation, "资产服务 · " + action, -result.CashCost, assetId, changes => {
                // Every output has to fit with the original still present. This conservative
                // capacity check permits complete rollback without destroying the original.
                foreach (var childId in result.CreatedAssetIds) StageSpawn(next, Catalog.GetAsset(next.Catalog, childId), changes);
                if (asset.Status == "consumed") StageDetach(asset, changes);
            });
            // A visual refresh must not turn a committed service into a retriable payment.
            try { SyncNativeAppearance(asset); }
            catch (Exception error) { notice = result.Message + "；实物外观刷新失败：" + error.Message; return; }
            notice = result.Message;
        }

        private AssetQuote CollateralQuote(RunState next, string assetId)
        {
            var asset = Catalog.GetAsset(next.Catalog, assetId);
            if (asset.Status != "active") throw new InvalidOperationException("该资产已托管或已消耗。");
            var existing = next.Catalog.Encounters.FirstOrDefault(e => e.AssetId == assetId && !e.Completed && e.EncounterId == selectedEncounter)
                ?? next.Catalog.Encounters.LastOrDefault(e => e.AssetId == assetId && !e.Completed);
            var encounter = existing ?? InspectionV2.Open(next.Catalog, assetId, "collateral:" + assetId, "collateral:" + asset.OwnerId);
            InspectionV2.SelectAll(next.Catalog, encounter.EncounterId);
            return QuoteByMarket(next.Catalog, encounter.EncounterId, "sell", EconomyService.GetMarketSnapshot(next.Economy, Day(), rules));
        }

        private void ExecuteEconomy(EconomyCommand command)
        {
            RequireCommerceReady();
            if (command == null) throw new ArgumentNullException(nameof(command));
            var next = Clone();
            var operation = OperationId(next);
            command.TransactionId = operation;
            command.Sequence = checked(next.Economy.LastCommandSequence + 1);
            command.GameDay = Day();
            command.AvailableCash = Cash();
            if (command.Kind == EconomyAction.CreatePawn)
            {
                var asset = Catalog.GetAsset(next.Catalog, command.AssetId);
                if (command.PlayerIsLender)
                {
                    RequireCustomerCollateral(next, asset);
                    CancelPreparedNativeTrade();
                }
                else
                {
                    var item = RequireNativeOwned(asset);
                    if (PlayerStore.Instance.currentClientInstance != null && ReferenceEquals(PlayerStore.Instance.GetCurrentlyNegociatedItem(), item))
                        throw new InvalidOperationException("托管前请先将这件物品从原生议价台收回库存。");
                }
                // Ignore UI-supplied collateral valuation and recompute from the same asset,
                // all established evidence, certificates, and today's shared market.
                command.CollateralValue = CollateralQuote(next, command.AssetId).Price;
            }
            var result = EconomyService.Apply(next.Economy, command, rules);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            if (result.Duplicate) { notice = "该业务已经执行，未再次扣款。"; return; }
            RunCommerce(next, operation, "金融业务 · " + command.Kind, result.CashDelta, command.AssetId,
                changes => StageTransitions(next, result.AssetTransitions, changes));
            notice = "业务完成：" + command.Kind + "；现金 " + (result.CashDelta >= 0 ? "+" : "") + result.CashDelta + " Cr。";
            SyncCards(command.Kind);
        }

        private void StageTransitions(RunState next, List<AssetTransition> transitions, PhysicalChanges changes)
        {
            foreach (var transition in transitions)
            {
                var asset = Catalog.GetAsset(next.Catalog, transition.AssetId);
                var contract = next.Economy.PawnContracts.FirstOrDefault(p => p.Id == transition.ContractId && p.AssetId == asset.AssetId)
                    ?? state?.Economy.PawnContracts.FirstOrDefault(p => p.Id == transition.ContractId && p.AssetId == asset.AssetId)
                    ?? throw new InvalidOperationException("托管转换缺少匹配合同。");
                var incoming = transition.Kind == "EscrowFromPlayer" || transition.Kind == "EscrowFromCustomer";
                if (incoming)
                {
                    if (asset.Status != "active") throw new InvalidOperationException("抵押物已不处于可托管状态。");
                    if (transition.Kind == "EscrowFromPlayer") RequireNativeOwned(asset);
                    else
                    {
                        var offered = RequireCustomerCollateral(next, asset);
                        if (offered == null) StageSpawn(next, asset, changes);
                        else
                        {
                            var original = offered.parentInventory;
                            // Move this exact customer instance into saved custody. On failure,
                            // restore its tray and ownership before publishing any cash/state.
                            changes.Undo.Add(() => {
                                if (!NativeBridge.TryMove(offered, original, out var error)) throw new InvalidOperationException(error);
                                ModHelper.SetItemOwnerShip(offered, false);
                            });
                            ModHelper.SetItemOwnerShip(offered, true);
                            if (!NativeBridge.TryMove(offered, EmporiumEntry.Instance.invElement, out var moveError))
                                throw new InvalidOperationException(moveError);
                        }
                    }
                    changes.Undo.Add(() => {
                        var item = NativeBridge.FindAsset(asset.AssetId);
                        if (item != null && NativeBridge.IsInCustody(item) && !NativeBridge.TryReleaseCustody(asset.AssetId, contract.Id, out var error))
                            throw new InvalidOperationException(error);
                    });
                    if (!NativeBridge.TrySetCustody(asset.AssetId, contract.Id, out var custodyError)) throw new InvalidOperationException(custodyError);
                    asset.Status = "escrow";
                    asset.StateVersion = checked(asset.StateVersion + 1);
                    next.OfferAssetIds.Remove(asset.AssetId);
                    continue;
                }
                if (asset.Status != "escrow") throw new InvalidOperationException("合同托管状态与资产不一致。");
                var physical = NativeBridge.FindAsset(asset.AssetId);
                if (physical == null || !GeneralHelper.IsItemOwned(physical) || !NativeBridge.IsInCustody(physical))
                    throw new InvalidOperationException("合同抵押实物缺失或归属异常。");
                changes.Undo.Add(() => { if (!NativeBridge.TrySetCustody(asset.AssetId, contract.Id, out var error)) throw new InvalidOperationException(error); });
                if (!NativeBridge.TryReleaseCustody(asset.AssetId, contract.Id, out var releaseError)) throw new InvalidOperationException(releaseError);
                switch (transition.Kind)
                {
                    case "ReturnToPlayer":
                        if (contract.PlayerIsLender || asset.OwnerId != "player") throw new InvalidOperationException("赎当方向不匹配。");
                        break;
                    case "ForfeitToPlayer":
                        if (!contract.PlayerIsLender || asset.OwnerId == "player") throw new InvalidOperationException("绝当方向不匹配。");
                        asset.OwnerId = "player";
                        break;
                    case "ReturnToCustomer":
                        if (!contract.PlayerIsLender || asset.OwnerId == "player") throw new InvalidOperationException("顾客赎当方向不匹配。");
                        StageDetach(asset, changes);
                        break;
                    case "ForfeitToCustomer":
                        if (contract.PlayerIsLender || asset.OwnerId != "player") throw new InvalidOperationException("抵押物交付方向不匹配。");
                        StageDetach(asset, changes);
                        asset.OwnerId = "pawn-lender:" + contract.Id;
                        break;
                    default: throw new InvalidOperationException("未知托管转换。");
                }
                asset.Status = "active";
                asset.StateVersion = checked(asset.StateVersion + 1);
            }
        }

        private GameItem? RequireCustomerCollateral(RunState run, AssetRecord asset)
        {
            if (asset.OwnerId == "player" || asset.Status != "active")
                throw new InvalidOperationException("顾客抵押物归属或状态不匹配。");
            var item = NativeBridge.FindAsset(asset.AssetId);
            if (item == null)
            {
                if (!run.OfferAssetIds.Contains(asset.AssetId)) throw new InvalidOperationException("这件抵押物已不在来客清单中。");
                return null;
            }
            var client = PlayerStore.Instance.currentClientInstance;
            if (client == null || !client.GetClientBlueprint().IsClientSelling() || GeneralHelper.IsItemOwned(item) ||
                NativeBridge.IsInCustody(item) || item.unitCount != 1 ||
                NativeBridge.GetDefinitionId(item) != "starpawn." + asset.DefinitionId ||
                NativeBridge.GetNativeUniqueId(item) != asset.NativeItemUniqueId || asset.OwnerId != NativeCounterpartyId(item) ||
                !(ReferenceEquals(item.parentInventory, EmporiumEntry.Instance.frontInvinvElement) ||
                  ReferenceEquals(item.parentInventory, EmporiumEntry.Instance.backInvinvElement)))
                throw new InvalidOperationException("抵押物必须由当前卖家在柜台展示，且与登记原件一致。");
            return item;
        }

        private void CompleteNightSettlement(RunState next, List<AssetTransition> transitions)
        {
            if (!SameRun() || HasCommerceRecovery()) throw new InvalidOperationException("夜间结算上下文不可用。");
            var previous = state;
            var changes = new PhysicalChanges();
            var recovery = new CommerceRecovery { OperationId = "night:" + next.Catalog.RunId + ":" + next.Economy.LastClosedDay,
                CashBefore = Cash(), Proposed = next, Description = "夜间利息与抵押物结算" };
            WriteCommerceRecovery(recovery);
            try
            {
                StageTransitions(next, transitions, changes);
                Validate(next);
                WriteCommerceRecovery(recovery);
                state = next;
                Persist();
                changes.Complete();
                if (NativeBridge.HasPendingPhysicalChanges) throw new InvalidOperationException("日结实物提交仍有未完成转移。");
                ClearCommerceRecovery();
            }
            catch
            {
                if (ReferenceEquals(state, previous))
                {
                    try
                    {
                        changes.Rollback();
                        if (NativeBridge.HasPendingPhysicalChanges) throw new InvalidOperationException("日结实物仍待恢复。");
                        ClearCommerceRecovery();
                    }
                    catch { blocked = true; }
                }
                else blocked = true;
                throw;
            }
        }

        // Compatibility entry point: this owns the whole settlement boundary, including
        // persistence; callers should use CompleteNightSettlement directly.
        private void ApplyTransitions(RunState next, List<AssetTransition> transitions) => CompleteNightSettlement(next, transitions);

        private void PreparePawn(string assetId, bool playerIsLender)
        {
            RequireCommerceReady();
            var preview = Clone();
            var quote = CollateralQuote(preview, assetId);
            var maximum = Math.Min(rules.MaximumPawnPrincipal, checked((long)decimal.Floor((decimal)quote.Price * rules.PawnLoanToValueBps / 10000m)));
            if (maximum <= 0) throw new InvalidOperationException("此物品当前没有可放款价值。");
            var ui = BeginPage(playerIsLender ? "典当 · 店主放款" : "典当 · 玩家借款", "原物托管并占用库存空间；期满未赎即按合同转移所有权。");
            Text(ui, 24, 90, 550, 100, "抵押物：" + Catalog.GetDefinition(Catalog.GetAsset(preview.Catalog, assetId).DefinitionId).Name +
                "\n核定估值 " + quote.Price + " Cr，最高本金 " + maximum + " Cr\n期限 " + rules.DefaultPawnDays + " 天，日费用 " + (rules.PawnInterestBps / 100m).ToString("0.##") + "%", 13);
            if (!state!.Economy.PawnLicensed) Button(ui, 24, 230, 265, 32, "先办理典当许可 · " + rules.PawnLicenseCost, OpenPawnLedger);
            else Button(ui, 24, 230, 265, 32, (playerIsLender ? "支付放款 " : "领取借款 ") + maximum, () => {
                    ExecuteEconomy(new EconomyCommand { Kind = EconomyAction.CreatePawn, AssetId = assetId, PlayerIsLender = playerIsLender,
                        Amount = maximum, TermDays = rules.DefaultPawnDays });
                    OpenPawnLedger();
                });
            Footer(ui); Show(ui);
        }
    }
}
