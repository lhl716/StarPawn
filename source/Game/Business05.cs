using System;
using System.Collections.Generic;
using System.Linq;
using StarPawn.Core;

// Revision 05 business flow on the mod side: native commission items, settlement
// bookkeeping, the follow-up sale / pawn / leave conversation on the SAME item,
// saved daily markets, jobs, and the versioned inventory-shape migration.
namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private string activeVisit = "";

        private static string L(string key, string english) => TextResolver.Get(key, english);

        private void EnableRevision05()
        {
            Native05.Log = log;
            Native05.CorrectValueProvider = CommissionCorrectValue;
            Native05.CreateCommissionItem = CreateCommissionItem;
            Native05.OpenBook = (item, page) => { if (EnsureRun()) RunAction(generation, () => OpenBookItem(item, page)); };
            StarPawnEstimateObserver.Estimated = OnNativeEstimate;
            StarPawnCommissionFeeCap.Adjust = CapCommissionFee;
        }

        private void DisableRevision05()
        {
            Native05.CorrectValueProvider = null; Native05.CreateCommissionItem = null; Native05.OpenBook = null;
            StarPawnEstimateObserver.Estimated = null; StarPawnCommissionFeeCap.Adjust = null;
            Native05.UnregisterCareer(ModId);
        }

        // Saved daily market: one record per day, generated once from the 0.3 base snapshot.
        private MarketDay Market05()
        {
            var baseline = Market();
            return MarketService.EnsureDay(state!.Catalog, Day(), baseline.GoldSellPerGram, baseline.GoldBuyPerGram, baseline.SilverSellPerGram, baseline.SilverBuyPerGram,
                state.Economy.MarketEventsOptedIn, UnifiedConfig.Rules);
        }

        // Called once after a successful load: history-only XP, promotion books, overflow, shape migration.
        private void AfterLoad05()
        {
            if (state == null) return;
            Career.MigrateLegacyExperience(state.Catalog);
            Market05();
            Operations05.AdvanceJobs(state.Catalog, Day());
            VisitService.ExpireOffers(state.Catalog, Day());
            MigrateShapes();
            ClaimPromotionBooks();
            Persist();
        }

        private void Nightly05(int day)
        {
            if (state == null) return;
            Operations05.AdvanceJobs(state.Catalog, day + 1);
            VisitService.ExpireOffers(state.Catalog, day + 1);
            ClaimPromotionBooks();
        }

        // ------------------------------------------------------------ native commissions
        private GameItem? CreateCommissionItem(string group)
        {
            if (state == null) return null;
            var members = Catalog.Definitions.Where(d => Native05.GroupOf(d) == group).ToList();
            var families = members.GroupBy(Appraisal.Family).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
            if (families.Count == 0) return null;
            var family = families[(int)(MarketService.Hash(state.Catalog.RunId + ":commission:" + Day() + ":" + state.Catalog.NextAssetSequence) % (uint)families.Count)];
            var profile = Appraisal.ChooseProfile(state.Catalog.RunId, Day() * 31 + (int)(state.Catalog.NextAssetSequence % 31), family);
            var customer = "appraisal:" + state.Catalog.RunId + ":" + state.Catalog.NextAssetSequence;
            var asset = Catalog.CreateAsset(state.Catalog, profile.Id, customer);
            var item = ModHelper.SpawnItem("starpawn." + profile.Id);
            item.ModifyTag(NativeBridge.AssetIdTag, tag => tag.SetString(asset.AssetId));
            ModHelper.SetItemOwnerShip(item, false);
            asset.NativeItemUniqueId = NativeBridge.GetNativeUniqueId(item);
            var profileKinds = new[] { "ordinary", "ordinary", "urgent_cash", "sentimental", "dealer", "collector" };
            var kind = profileKinds[(int)(MarketService.Hash(asset.AssetId + ":profile") % (uint)profileKinds.Length)];
            var visit = VisitService.Open(state.Catalog, asset.AssetId, customer, "appraisal", kind, MarketService.Hash(asset.AssetId + ":honest") % 5 == 0, Market05(), Native05.NativeRank(), UnifiedConfig.Rules);
            activeVisit = visit.VisitId;
            Persist();
            log?.Log("SP5_COMMISSION: group=" + group + " asset=" + asset.AssetId + " profile=" + profile.Id + " customer=" + kind);
            return item;
        }

        // The private answer for native settlement, from the commission's saved market.
        private long CommissionCorrectValue(GameItem item)
        {
            if (state == null) return 0;
            var assetId = NativeBridge.GetAssetId(item);
            if (string.IsNullOrEmpty(assetId) || !state.Catalog.Assets.Any(a => a.AssetId == assetId)) return 0;
            return VisitService.CorrectValue(state.Catalog, assetId);
        }

        private void OnNativeEstimate(GameItem item, long estimate)
        {
            if (state == null) return;
            var assetId = NativeBridge.GetAssetId(item);
            var visit = state.Catalog.Unified.Visits.LastOrDefault(v => v.AssetId == assetId && v.Path == "appraisal");
            if (visit == null) return;
            if (estimate >= 0) VisitService.Submit(state.Catalog, visit.VisitId, estimate, UnifiedConfig.Rules); // bookkeeping only: native already settled
            VisitService.ResolveIntent(state.Catalog, visit.VisitId, Market05(), state.Economy.PawnLicensed, Cash(), UnifiedConfig.Rules);
            Persist();
            if (visit.Intent != "leave" && visit.Stage == "followup") { activeVisit = visit.VisitId; RunAction(generation, () => OpenVisit(visit.VisitId)); }
            log?.Log("SP5_SETTLED: visit=" + visit.VisitId + " grade=" + visit.Grade + " intent=" + visit.Intent);
        }

        private void CapCommissionFee(GameItem item, int before, int nativeFee)
        {
            if (state == null || nativeFee <= 0) return;
            var visit = state.Catalog.Unified.Visits.LastOrDefault(v => v.AssetId == NativeBridge.GetAssetId(item) && v.Path == "appraisal");
            if (visit == null) return;
            var cap = (int)Math.Min(UnifiedConfig.Rules.CommissionFeeCap, Career.Fee(visit.CorrectValue, Math.Max(1, visit.Grade < 0 ? Career.Grade(visit.SubmittedValue, visit.CorrectValue) : visit.Grade), UnifiedConfig.Rules));
            if (nativeFee <= cap) return;
            var result = NativeBridge.ApplyCashOnce("sp5-fee-cap:" + visit.VisitId, cap - nativeFee, before + nativeFee);
            log?.Log("SP5_FEE_CAP: visit=" + visit.VisitId + " native=" + nativeFee + " capped=" + cap + " ok=" + result.Success + " " + result.Error);
        }

        // ------------------------------------------------------------ follow-up conversation
        private void OpenVisit(string visitId)
        {
            if (state == null) return;
            var v = VisitService.Get(state.Catalog, visitId);
            var asset = Catalog.GetAsset(state.Catalog, v.AssetId);
            var d = Catalog.GetDefinition(asset.DefinitionId);
            var m = Market05();
            if (v.Stage == "followup") VisitService.Reprice(state.Catalog, v, m, UnifiedConfig.Rules);
            var ui = BeginPage(Catalog.PublicFacts(asset).Name, L("sp5.visit.subtitle", "Same customer, same item"));
            var line = L(VisitService.FollowUpLineKey(v), "");
            Text(ui, 24, 80, 550, 46, line, 15);
            var q = ComponentValuation.Quote(state.Catalog, asset, m, v.Intent == "request_pawn" ? ComponentValuation.PawnCollateral : ComponentValuation.MaterialBuyback, UnifiedConfig.Rules);
            Text(ui, 24, 132, 550, 110, Breakdown(q, d), 12);
            if (v.Stage != "followup")
            {
                Text(ui, 24, 250, 550, 40, L("sp5.visit.closed", "This conversation has ended."), 13);
                Footer(ui); Show(ui); return;
            }
            if (v.Intent == "offer_sale")
            {
                Text(ui, 24, 250, 550, 24, string.Format(L("sp5.visit.ask", "Asking price: {0} Cr (Buy from customer)"), v.OfferAmount), 14);
                Button(ui, 24, 290, 260, 30, string.Format(L("sp5.visit.buy", "Buy for {0} Cr"), v.OfferAmount), () => AcceptVisitSale(v.VisitId));
            }
            else
            {
                Text(ui, 24, 250, 550, 36, string.Format(L("sp5.visit.pawn_terms", "Loan {0} Cr at {1}% of the lending basis {2} Cr, {3} days, daily fee {4}%."),
                    v.OfferAmount, v.PawnLtvBps / 100, v.CollateralBasis, rules.DefaultPawnDays, (rules.PawnInterestBps / 100m).ToString("0.##")), 13);
                if (!string.IsNullOrEmpty(v.BlockedReasonKey)) { Text(ui, 24, 288, 550, 22, L(v.BlockedReasonKey, ""), 12); ui.Last.color = "#984b45"; }
                if (!state.Economy.PawnLicensed) Button(ui, 24, 316, 260, 30, L("sp5.visit.need_license", "Get a pawn licence first"), OpenPawnLedger);
                else Button(ui, 24, 316, 260, 30, string.Format(L("sp5.visit.lend", "Lend {0} Cr against it"), v.OfferAmount), () => AcceptVisitPawn(v.VisitId));
            }
            Button(ui, 300, v.Intent == "offer_sale" ? 290 : 316, 260, 30, L("sp5.visit.decline", "No, thank you"), () => { VisitService.Decline(state!.Catalog, v.VisitId); Persist(); notice = L("sp5.line.leave.plain", ""); OpenHome(); });
            Footer(ui); Show(ui);
        }

        private string Breakdown(ComponentQuote q, ItemDefinition d)
        {
            var s = new System.Text.StringBuilder();
            if (q.Metal.Length > 0)
            {
                s.AppendLine(ValuationV2.DescribeMetal(q) + "  ·  " + string.Format(L("sp5.quote.rate", "{0} Cr per pure gram"), q.MetalRate));
                s.AppendLine(string.Format(L("sp5.quote.metal", "Metal {0} Cr"), q.MetalValue));
            }
            if (q.CraftBasis > 0 || q.CraftDeduction > 0) s.AppendLine(string.Format(L("sp5.quote.craft", "Workmanship {0} Cr (reduction {1})"), q.CraftValue, q.CraftDeduction));
            if (q.StonesCounted > 0) s.AppendLine(string.Format(L("sp5.quote.stones", "Stones {0} Cr ({1} counted{2})"), q.StoneValue, q.StonesCounted, q.StonesVerified ? "" : L("sp5.quote.untested", ", identity not tested")));
            if (q.PremiumValue > 0) s.AppendLine(string.Format(L("sp5.quote.premium", "Collector premium {0} Cr"), q.PremiumValue));
            foreach (var line in q.Lines) s.AppendLine("· " + line.Label + "  −" + line.Amount);
            s.Append(string.Format(L("sp5.quote.total", "Estimate {0} Cr"), q.Total));
            return s.ToString();
        }

        private void AcceptVisitSale(string visitId)
        {
            RequireCommerceReady();
            var v = VisitService.Get(state!.Catalog, visitId);
            var item = NativeBridge.FindAsset(v.AssetId) ?? throw new InvalidOperationException(L("sp5.visit.item_changed", "The item is no longer available."));
            var next = Clone();
            var r = VisitService.AcceptSale(next.Catalog, visitId, Day(), Cash());
            if (!r.Success) { notice = r.Error; OpenVisit(visitId); return; }
            var asset = Catalog.GetAsset(next.Catalog, v.AssetId);
            RunCommerce(next, OperationId(next), "sp5 buy from customer " + v.AssetId, r.CashDelta, v.AssetId, changes =>
            {
                var original = item.parentInventory;
                changes.Undo.Add(() => { ModHelper.SetItemOwnerShip(item, false); if (!NativeBridge.TryMove(item, original, out var e)) throw new InvalidOperationException(e); });
                ModHelper.SetItemOwnerShip(item, true);
                if (!NativeBridge.TryMove(item, EmporiumEntry.Instance.invElement, out var error)) throw new InvalidOperationException(error);
                asset.NativeItemUniqueId = NativeBridge.GetNativeUniqueId(item);
            });
            notice = string.Format(L("sp5.visit.bought", "Bought. Paid {0} Cr."), -r.CashDelta);
            OpenHome();
        }

        private void AcceptVisitPawn(string visitId)
        {
            var v = VisitService.Get(state!.Catalog, visitId);
            var prep = VisitService.PreparePawn(state.Catalog, visitId, Day(), Operations05.LendableCash(Cash(), state.Catalog), rules.DefaultPawnDays);
            if (!prep.Success) { notice = prep.Error; OpenVisit(visitId); return; }
            pawnFromVisit = visitId;
            try { ExecuteEconomy(prep.PawnCommand!); }
            finally { pawnFromVisit = ""; }
            var contract = state!.Economy.PawnContracts.LastOrDefault(c => c.AssetId == v.AssetId && c.Status == "Active");
            if (contract != null) { VisitService.ConfirmPawn(state.Catalog, visitId, contract.Id); Persist(); }
            OpenPawnLedger();
        }


        // ------------------------------------------------------------ promotion books
        private void ClaimPromotionBooks()
        {
            if (state == null || !PlayerStore.HasInstance) return;
            var owned = state.Catalog.Assets.Where(a => a.OwnerId == "player" && a.Status != "consumed").Select(a => a.DefinitionId).ToList();
            var next = Clone();
            var grant = Career.ClaimEntitlements(next.Catalog, Native05.NativeRank(), owned);
            if (grant.Count == 0 && next.Catalog.Unified.GrantedEntitlements.Count == state.Catalog.Unified.GrantedEntitlements.Count) return;
            var assets = grant.Select(id => Catalog.CreateAsset(next.Catalog, id)).ToList();
            RunCommerce(next, OperationId(next), "sp5 promotion handbooks", 0, assets.FirstOrDefault()?.AssetId ?? "", changes => { foreach (var a in assets) StageSpawn(next, a, changes); });
            if (assets.Count > 0) notice = string.Format(L("sp5.books.granted", "{0} handbook volumes arrived with your promotion."), assets.Count);
        }

        // ------------------------------------------------------------ inventory shape migration
        private void MigrateShapes()
        {
            if (state == null) return;
            foreach (var item in NativeBridge.GetOwnedAssets())
            {
                var assetId = NativeBridge.GetAssetId(item);
                if (!LayoutMigration.NeedsMigration(state.Catalog, assetId) || !Catalog.TryGetDefinition(NativeBridge.GetVariantId(item), out var d)) continue;
                var receipt = LayoutMigration.Begin(state.Catalog, assetId, Day());
                var plan = new MigrationPlan();
                if (Native05.TryReadShape(item, out var w, out var h) && w == d.WidthCells && h == d.HeightCells) { plan.Kept = 1; LayoutMigration.Complete(state.Catalog, receipt, plan, assetId); continue; }
                if (NativeBridge.IsInCustody(item)) continue; // pledged/serviced: migrated after release, never moved now
                if (!NativeBridge.TryRemoveOwned(assetId, out var detached, out var error) || detached == null) { log?.Log("SP5_MIGRATE_SKIP: " + assetId + " " + error); continue; }
                Native05.ApplyShape(detached, d.WidthCells, d.HeightCells);
                if (NativeBridge.RestoreDetached(detached, out error)) { plan.Moved = 1; LayoutMigration.Complete(state.Catalog, receipt, plan, assetId); continue; }
                // No room anywhere: keep the old shape where it was and list it as a visible overflow claim.
                if (w > 0) Native05.ApplyShape(detached, w, h); else Native05.ApplyShape(detached, 1, 1);
                if (!NativeBridge.RestoreDetached(detached, out error)) { blocked = true; log?.Error("SP5_MIGRATE_RESTORE_FAILED: " + assetId + " " + error); return; }
                plan.Overflow.Add(assetId); LayoutMigration.Complete(state.Catalog, receipt, plan, "main");
                log?.Log("SP5_MIGRATE_OVERFLOW: " + assetId);
            }
        }

        private void RetryOverflow(string assetId)
        {
            var claim = state!.Catalog.Unified.Overflow.FirstOrDefault(o => o.AssetId == assetId && !o.Claimed);
            var item = NativeBridge.FindAsset(assetId);
            if (claim == null || item == null || !Catalog.TryGetDefinition(NativeBridge.GetVariantId(item), out var d)) return;
            Native05.TryReadShape(item, out var w, out var h);
            if (!NativeBridge.TryRemoveOwned(assetId, out var detached, out var error) || detached == null) { notice = error; return; }
            Native05.ApplyShape(detached, d.WidthCells, d.HeightCells);
            if (NativeBridge.RestoreDetached(detached, out _)) { LayoutMigration.Claim(state.Catalog, assetId); notice = L("sp5.overflow.done", "The item now uses its full size."); }
            else { Native05.ApplyShape(detached, Math.Max(1, w), Math.Max(1, h)); NativeBridge.RestoreDetached(detached, out _); notice = L("sp5.overflow.full", "Still not enough room. Free some space and try again."); }
            Persist();
        }

        // ------------------------------------------------------------ books opened by Read
        private void OpenBookItem(GameItem item, int page)
        {
            var d = Catalog.GetDefinition(NativeBridge.GetVariantId(item));
            if (string.IsNullOrEmpty(d.ManualFor)) { OpenReading(NativeBridge.GetAssetId(item)); return; }
            OpenHandbook(d.ManualFor, page, d.Id);
        }
    }
}
