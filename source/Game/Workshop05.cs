using System;
using System.Collections.Generic;
using System.Linq;
using StarPawn.Core;

// Revision 05 workshop: jewelry bench (mount, demount, repair, laboratory), physical
// recovery jobs, energy, and custom orders. Every operation goes through RunCommerce so
// cash, the catalog and native inventory change together or not at all.
namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private readonly List<string> workshopPick = new List<string>();

        private IEnumerable<string> OwnedEquipment() => state!.Catalog.Assets
            .Where(a => a.OwnerId == "player" && a.Status == "active" && string.IsNullOrEmpty(a.CustodyRef))
            .Select(a => Catalog.GetDefinition(a.DefinitionId).Role).Where(r => r.Length > 0).Distinct();

        // Applies a pure operation's asset changes to the native inventory in one commerce transaction.
        private void ApplyOperation(string description, Func<RunState, OperationResult> operation, Func<IReadOnlyList<string>, bool>? capacity = null)
        {
            RequireCommerceReady();
            var next = Clone();
            var result = operation(next);
            if (!result.Success) { notice = result.Error; return; }
            RunCommerce(next, OperationId(next), description, result.CashDelta, result.EntityId, changes =>
            {
                foreach (var id in result.ChangedAssetIds.Concat(result.CreatedAssetIds).Distinct())
                {
                    var after = Catalog.GetAsset(next.Catalog, id);
                    var native = NativeBridge.FindAsset(id);
                    var shouldExist = after.Status == "active" && after.OwnerId == "player";
                    if (native != null && !shouldExist && GeneralHelper.IsItemOwned(native)) StageDetach(after, changes);   // consumed or set into a setting
                    else if (native == null && shouldExist && string.IsNullOrEmpty(after.CustodyRef)) StageSpawn(next, after, changes); // released stone or new output
                }
                foreach (var id in result.ChangedAssetIds.Where(x => Catalog.GetAsset(next.Catalog, x).Status == "active")) { var a = Catalog.GetAsset(next.Catalog, id); if (NativeBridge.FindAsset(id) != null) NativeBridge.RefreshAppearance(id, JewelryArt.TextureFor(next.Catalog, a)); }
            });
            notice = description;
        }

        // The real capacity check is StageSpawn inside RunCommerce: if any output does not fit,
        // the whole transaction rolls back, so the job stays "ready" and nothing is charged twice.
        private bool CanPlace(IReadOnlyList<string> definitionIds) => NativeBridge.IsReady;

        private void OpenWorkshop05(string selectedRecipe)
        {
            if (!EnsureRun() || state == null) return;
            Operations05.AdvanceJobs(state.Catalog, Day());
            var m = Market05();
            var ui = BeginPage(L("sp5.workshop.title", "Workshop"), string.Format(L("sp5.workshop.energy", "Stored energy: {0} units"), state.Catalog.Unified.EnergyUnits));
            var y = 76f;
            foreach (var job in state.Catalog.Unified.Jobs.Where(j => j.Status != "claimed").Take(4))
            {
                Text(ui, 24, y, 380, 22, L("sp5.job.kind." + job.Kind, job.Kind) + " · " + (job.Status == "ready" ? L("sp5.job.ready", "Ready to collect") : string.Format(L("sp5.job.until", "Ready on day {0}"), job.ReadyDay)), 11);
                if (job.Status == "ready") { var id = job.JobId; Button(ui, 420, y, 150, 22, L("sp5.job.collect", "Collect"), () => { ApplyOperation(L("sp5.job.collected", "Output collected."), n => Operations05.ClaimJob(n.Catalog, id, CanPlace)); OpenWorkshop05(""); }); }
                y += 26;
            }
            var owned = state.Catalog.Assets.Where(a => a.OwnerId == "player" && a.Status == "active" && string.IsNullOrEmpty(a.CustodyRef)).ToList();
            Text(ui, 24, y + 4, 550, 20, L("sp5.workshop.pick", "Choose items, then a process:"), 12); y += 26;
            foreach (var a in owned.Where(a => { var d = Catalog.GetDefinition(a.DefinitionId); return d.Role == "gem" || d.Role == "setting" || d.Role == "lot" || d.Role == "mixed" || d.Role == "energy_cell" || !string.IsNullOrEmpty(d.RecoveryClass); }).Take(6))
            {
                var picked = workshopPick.Contains(a.AssetId); var id = a.AssetId;
                Button(ui, 24, y, 260, 20, (picked ? "● " : "○ ") + Catalog.GetDefinition(a.DefinitionId).Name, () => { if (!workshopPick.Remove(id)) workshopPick.Add(id); OpenWorkshop05(selectedRecipe); });
                y += 22;
            }
            var pick = workshopPick.Where(id => owned.Any(a => a.AssetId == id)).ToList();
            var bx = 300f; var by = 150f; var equipment = OwnedEquipment().ToList();
            void Act(string key, string english, Func<RunState, OperationResult> op) { Button(ui, bx, by, 270, 22, L(key, english), () => { ApplyOperation(L(key, english), op); workshopPick.Clear(); OpenWorkshop05(""); }); by += 25; }
            var settings = pick.Where(id => Catalog.GetDefinition(Catalog.GetAsset(state.Catalog, id).DefinitionId).Role == "setting").ToList();
            var stones = pick.Where(id => Catalog.GetDefinition(Catalog.GetAsset(state.Catalog, id).DefinitionId).Role == "gem").ToList();
            if (settings.Count == 1 && stones.Count > 0) Act("sp5.workshop.mount", "Set the chosen stones", n => Operations05.StartMount(n.Catalog, settings[0], stones, equipment, m, Cash(), UnifiedConfig.Rules));
            if (settings.Count == 1 && stones.Count == 0)
            {
                Act("sp5.workshop.demount", "Take the stones out", n => Operations05.StartDemount(n.Catalog, settings[0], equipment, m, Cash(), UnifiedConfig.Rules));
                Act("sp5.workshop.repair", "Repair the setting", n => Operations05.StartMountRepair(n.Catalog, settings[0], equipment, m, Cash(), UnifiedConfig.Rules));
            }
            if (stones.Count == 1 && settings.Count == 0) Act("sp5.workshop.lab", "Send to the laboratory", n => Operations05.LabReport(n.Catalog, stones[0], Day(), m, Cash(), UnifiedConfig.Rules));
            var cell = pick.FirstOrDefault(id => Catalog.GetDefinition(Catalog.GetAsset(state.Catalog, id).DefinitionId).Role == "energy_cell");
            if (cell != null) Act("sp5.workshop.cell", "Load the energy cell", n => Operations05.AddEnergy(n.Catalog, cell, 10));
            foreach (var recipe in UnifiedConfig.Rules.Recipes.Where(r => pick.Count > 0 && pick.Count <= r.InputLimit && pick.All(id => { var d = Catalog.GetDefinition(Catalog.GetAsset(state.Catalog, id).DefinitionId); return r.Inputs.Contains(d.ArtModel) || r.Inputs.Contains(d.RecoveryClass); })).Take(4))
            {
                var r = recipe;
                Act("sp5.recipe." + r.Id, r.Id, n => Operations05.StartRecovery(n.Catalog, r.Id, pick, equipment, m, Cash(), UnifiedConfig.Rules));
            }
            if (pick.Count == 1)
            {
                var a = Catalog.GetAsset(state.Catalog, pick[0]);
                var sale = ComponentValuation.Quote(state.Catalog, a, m, ComponentValuation.MaterialBuyback, UnifiedConfig.Rules).Total;
                Text(ui, bx, by + 4, 270, 40, string.Format(L("sp5.workshop.compare", "Sell as-is now: {0} Cr. Recovery keeps only the metal already inside."), sale), 10);
            }
            Button(ui, 24, 330, 160, 24, L("sp5.orders.open", "Custom orders"), OpenOrders05);
            Footer(ui); Show(ui);
        }

        private void OpenOrders05()
        {
            if (!EnsureRun() || state == null) return;
            var ui = BeginPage(L("sp5.orders.title", "Custom orders"), string.Format(L("sp5.orders.held", "Customer deposits held: {0} Cr (not yours to spend)"), state.Catalog.Unified.HeldCustomerDeposits));
            var y = 80f; var m = Market05(); var equipment = OwnedEquipment().ToList();
            foreach (var o in state.Catalog.Unified.Orders.Where(o => o.Status != "delivered" && o.Status != "cancelled").Take(6))
            {
                var id = o.OrderId;
                Text(ui, 24, y, 300, 36, string.Format(L("sp5.orders.row", "{0} · price {1} Cr, deposit {2} Cr, due day {3} · {4}"), o.CustomerId, o.Price, o.Deposit, o.DeadlineDay, L("sp5.orders.status." + o.Status, o.Status)), 10);
                if (o.Status == "accepted") Button(ui, 330, y, 120, 22, L("sp5.orders.start", "Start work"), () => { ApplyOperation(L("sp5.orders.started", "Work started."), n => Operations05.StartOrderWork(n.Catalog, id, equipment, m, Cash(), UnifiedConfig.Rules)); OpenOrders05(); });
                if (o.Status == "in_progress") Button(ui, 330, y, 120, 22, L("sp5.orders.ready", "Mark ready"), () => { ApplyOperation(L("sp5.orders.readied", "Ready for the customer."), n => Operations05.MarkOrderReady(n.Catalog, id)); OpenOrders05(); });
                if (o.Status == "ready") Button(ui, 330, y, 120, 22, L("sp5.orders.deliver", "Hand over"), () => { ApplyOperation(L("sp5.orders.delivered", "Delivered and paid."), n => Operations05.DeliverOrder(n.Catalog, id)); OpenOrders05(); });
                Button(ui, 456, y, 116, 22, L("sp5.orders.cancel", "Cancel"), () => { ApplyOperation(L("sp5.orders.cancelled", "Order cancelled; deposit refunded as agreed."), n => Operations05.CancelOrder(n.Catalog, id)); OpenOrders05(); });
                y += 40;
            }
            if (y == 80f) Text(ui, 24, y, 550, 40, L("sp5.orders.none", "No open orders."), 13);
            Button(ui, 24, 330, 160, 24, L("sp5.workshop.open", "Workshop"), () => OpenWorkshop05(""));
            Footer(ui); Show(ui);
        }
    }
}
