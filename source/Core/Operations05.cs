using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Revision 05 operations: jewelry services, physical recovery jobs, custom orders,
// inventory layout migration, footprint hit geometry and five-page handbooks.
namespace StarPawn.Core
{
    public sealed class RecipeDefinition
    {
        public string Id = "", Kind = "", Machine = "";
        public List<string> Inputs = new List<string>(), Equipment = new List<string>();
        public string OutputBar = "";                 // casting / alloy output definition
        public int InputLimit = 1, EnergyUnits = 1, DurationDays = 1; public long ServiceFee = 1;
        public int GoldBps = 10000, SilverBps = 10000, CopperBps = 10000;
    }

    public sealed class JobOutput { public string DefinitionId = ""; public long Gold, Silver, Copper; public string ExistingAssetId = ""; public string Action = "create"; public string SocketId = ""; public string ParentAssetId = ""; }

    public sealed class WorkJob
    {
        public string JobId = "", Kind = "", RecipeId = "", OwnerId = "player", OrderId = "";
        public List<string> InputAssetIds = new List<string>();
        public List<JobOutput> Outputs = new List<JobOutput>();
        public long Fee; public int Energy; public int StartDay, ReadyDay;
        public string Status = "running";             // running | ready | claimed
        public long LossGold, LossSilver, LossCopper; // carried by the residue output
    }

    public sealed class JewelryOrder
    {
        public string OrderId = "", CustomerId = "", Kind = "service";   // service (customer materials) | commission (shop materials)
        public string SettingAssetId = ""; public List<string> StoneAssetIds = new List<string>();
        public long Price, Deposit, ServiceCostEarned; public int AcceptedDay, DeadlineDay;
        public string Status = "accepted";            // accepted | in_progress | ready | delivered | cancelled
        public string JobId = "";
    }

    public sealed class MigrationReceipt { public string MigrationId = "", InventoryId = ""; public int Day; public int Moved, Kept, Overflowed; public bool Completed; }
    public sealed class OverflowClaim { public string AssetId = "", FromInventoryId = "", MigrationId = ""; public bool Claimed; }

    public sealed class OperationResult
    {
        public bool Success; public string ErrorKey = "", Error = ""; public long CashDelta; public string EntityId = "";
        public List<string> CreatedAssetIds = new List<string>(); public List<string> ChangedAssetIds = new List<string>();
        public static OperationResult Fail(string key, string english) => new OperationResult { ErrorKey = key, Error = TextResolver.Get(key, english) };
    }

    public static class Operations05
    {
        public static void ValidateRules(UnifiedRules r)
        {
            if (r == null || r.Events == null || r.Recipes == null || r.IntentProfiles == null || r.IntentProfiles.Count == 0) throw new InvalidOperationException("Missing unified rules.");
            if (r.PawnMinLtvBps <= 0 || r.PawnMinLtvBps > r.PawnDefaultLtvBps || r.PawnDefaultLtvBps > r.PawnMaxLtvBps || r.PawnMaxLtvBps > 10000) throw new InvalidOperationException("Invalid pawn policy.");
            if (r.CommissionFeeCap < 0 || r.CommissionBaseFee < 0 || r.CommissionFeeBps < 0 || r.OrderDepositBps < 0 || r.OrderDepositBps > 10000) throw new InvalidOperationException("Invalid fee policy.");
            if (r.IntentProfiles.Any(p => p.Leave < 0 || p.Sale < 0 || p.Pawn < 0 || p.Leave + p.Sale + p.Pawn <= 0)) throw new InvalidOperationException("Invalid intent weights.");
            if (r.Events.Select(e => e.Id).Distinct().Count() != r.Events.Count || r.Recipes.Select(e => e.Id).Distinct().Count() != r.Recipes.Count) throw new InvalidOperationException("Duplicate event or recipe.");
            if (r.Recipes.Any(x => x.GoldBps < 0 || x.GoldBps > 10000 || x.SilverBps < 0 || x.SilverBps > 10000 || x.CopperBps < 0 || x.CopperBps > 10000 || x.DurationDays < 1 || x.EnergyUnits < 0 || x.ServiceFee < 0)) throw new InvalidOperationException("Invalid recipe.");
        }

        // ---------------------------------------------------------------- element content
        public static long Gold(AssetRecord a) => a.GoldMicrograms >= 0 ? a.GoldMicrograms : Catalog.GetDefinition(a.DefinitionId).GoldMicrograms;
        public static long Silver(AssetRecord a) => a.SilverMicrograms >= 0 ? a.SilverMicrograms : Catalog.GetDefinition(a.DefinitionId).SilverMicrograms;
        public static long Copper(AssetRecord a) => a.CopperMicrograms >= 0 ? a.CopperMicrograms : Catalog.GetDefinition(a.DefinitionId).CopperMicrograms;

        // Free = owned, not pledged/serviced, not set inside another item. Only an embedded stone is "inside" something.
        private static bool Free(AssetRecord a) => a.Status == "active" && a.OwnerId == "player" && string.IsNullOrEmpty(a.CustodyRef);
        private static string NextId(CatalogState s, string prefix) => prefix + "-" + (s.Unified.NextSequence++).ToString(CultureInfo.InvariantCulture);
        private static int Scaled(int value, MarketDay m, string channel, string target, UnifiedRules r) => (int)Math.Max(1, Math.Ceiling(value * m.Multiplier(channel, new[] { target, "all" }, r) / 10000m));
        private static long ScaledFee(long value, MarketDay m, string target, UnifiedRules r) => (long)Math.Ceiling(value * m.Multiplier("processing_service_cost", new[] { target, "all" }, r) / 10000m);

        public static OperationResult AddEnergy(CatalogState state, string cellAssetId, int units)
        {
            var cell = Catalog.GetAsset(state, cellAssetId);
            if (!Free(cell) || Catalog.GetDefinition(cell.DefinitionId).Role != "energy_cell") return OperationResult.Fail("sp5.job.no_cell", "Use a charged energy cell you own.");
            cell.Status = "consumed"; cell.StateVersion++; state.Unified.EnergyUnits = checked(state.Unified.EnergyUnits + units);
            return new OperationResult { Success = true, ChangedAssetIds = { cellAssetId } };
        }

        // ---------------------------------------------------------------- physical recovery
        // Starts a job: inputs are reserved, fee and energy are committed once, and the complete
        // output plan is saved. Every element is conserved: outputs + residue == inputs.
        public static OperationResult StartRecovery(CatalogState state, string recipeId, IList<string> inputAssetIds, IEnumerable<string> ownedEquipmentRoles, MarketDay market, long cash, UnifiedRules rules)
        {
            Catalog.Validate(state);
            var recipe = rules.Recipes.FirstOrDefault(x => x.Id == recipeId);
            if (recipe == null) return OperationResult.Fail("sp5.job.unknown", "This process is not available.");
            if (inputAssetIds == null || inputAssetIds.Count == 0 || inputAssetIds.Count > recipe.InputLimit || inputAssetIds.Distinct().Count() != inputAssetIds.Count) return OperationResult.Fail("sp5.job.inputs", "Choose between one and the allowed number of inputs.");
            var inputs = inputAssetIds.Select(id => Catalog.GetAsset(state, id)).ToList();
            if (inputs.Any(a => !Free(a))) return OperationResult.Fail("sp5.job.not_free", "An input is pledged, in service, or not yours.");
            if (inputs.Any(a => !recipe.Inputs.Contains(Catalog.GetDefinition(a.DefinitionId).ArtModel) && !recipe.Inputs.Contains(Catalog.GetDefinition(a.DefinitionId).RecoveryClass))) return OperationResult.Fail("sp5.job.wrong_input", "This process cannot take that input.");
            var owned = new HashSet<string>(ownedEquipmentRoles, StringComparer.Ordinal);
            if (!owned.Contains(recipe.Machine) || recipe.Equipment.Any(e => !owned.Contains(e))) return OperationResult.Fail("sp5.job.equipment", "The required machine or equipment is missing.");
            var fee = ScaledFee(recipe.ServiceFee, market, recipe.Kind == "recovery" ? "recovery" : recipe.Kind, rules);
            var energy = Scaled(recipe.EnergyUnits, market, "processing_energy_cost", "all", rules);
            var days = Scaled(recipe.DurationDays, market, "processing_duration", "all", rules);
            if (cash < fee) return OperationResult.Fail("sp5.job.no_cash", "You do not have enough cash for the service fee.");
            if (state.Unified.EnergyUnits < energy) return OperationResult.Fail("sp5.job.no_energy", "Not enough stored energy. Add a charged cell.");
            long au = inputs.Sum(Gold), ag = inputs.Sum(Silver), cu = inputs.Sum(Copper);
            var bonus = market.Delta("recovery_efficiency", "recovery");
            long Out(long amount, int bps) => recipe.Kind == "recovery" || recipe.Kind == "refining" ? Math.Min(amount, ComponentValuation.Scale(amount, Math.Min(10000, bps + (recipe.Kind == "recovery" ? bonus : 0)))) : amount;
            var job = new WorkJob { JobId = NextId(state, "job"), Kind = recipe.Kind, RecipeId = recipe.Id, InputAssetIds = inputAssetIds.ToList(), Fee = fee, Energy = energy, StartDay = market.Day, ReadyDay = market.Day + days };
            if (recipe.Kind == "dismantle" || recipe.Kind == "sort")
            {
                // Partition the saved budget among registered components; nothing is lost or created.
                job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_gold_scrap", Gold = au });
                job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_silver_scrap", Silver = ag });
                job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_copper_scrap", Copper = cu });
                job.Outputs.Add(new JobOutput { DefinitionId = "sp5_inert_residue" });
            }
            else if (recipe.Kind == "recovery" || recipe.Kind == "refining")
            {
                long oau = Out(au, recipe.GoldBps), oag = Out(ag, recipe.SilverBps), ocu = Out(cu, recipe.CopperBps);
                if (oau > 0) job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_recovered_gold", Gold = oau });
                if (oag > 0) job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_recovered_silver", Silver = oag });
                if (ocu > 0) job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_recovered_copper", Copper = ocu });
                job.LossGold = au - oau; job.LossSilver = ag - oag; job.LossCopper = cu - ocu;
                job.Outputs.Add(new JobOutput { DefinitionId = "sp5_inert_residue", Gold = job.LossGold, Silver = job.LossSilver, Copper = job.LossCopper });
            }
            else if (recipe.Kind == "casting")
            {
                job.Outputs.Add(new JobOutput { DefinitionId = recipe.OutputBar, Gold = au, Silver = ag, Copper = cu });
            }
            else if (recipe.Kind == "alloy")
            {
                // 18K = 750 Au : 250 Cu; 925 = 925 Ag : 75 Cu. Uses only what is present; the remainder returns as lots.
                var gold = recipe.OutputBar.Contains("gold");
                long precious = gold ? au : ag, pPart = gold ? 750 : 925, cPart = 1000 - pPart;
                var unit = Math.Min(precious / pPart, cu / cPart);
                if (unit <= 0) return OperationResult.Fail("sp5.job.no_charge", "The charge does not contain enough metal for this alloy.");
                long usedP = unit * pPart, usedC = unit * cPart;
                job.Outputs.Add(new JobOutput { DefinitionId = recipe.OutputBar, Gold = gold ? usedP : 0, Silver = gold ? 0 : usedP, Copper = usedC });
                if (precious - usedP > 0 || (gold ? ag : au) > 0)
                    job.Outputs.Add(new JobOutput { DefinitionId = gold ? "sp5_lot_recovered_gold" : "sp5_lot_recovered_silver", Gold = gold ? au - usedP : au, Silver = gold ? ag : ag - usedP });
                if (cu - usedC > 0) job.Outputs.Add(new JobOutput { DefinitionId = "sp5_lot_recovered_copper", Copper = cu - usedC });
            }
            else return OperationResult.Fail("sp5.job.unknown", "This process is not available.");
            if (job.Outputs.Sum(o => o.Gold) != au || job.Outputs.Sum(o => o.Silver) != ag || job.Outputs.Sum(o => o.Copper) != cu) throw new InvalidOperationException("Recovery plan does not conserve its inputs.");
            foreach (var a in inputs) { a.CustodyRef = job.JobId; a.StateVersion++; }
            state.Unified.EnergyUnits -= energy;
            state.Unified.Jobs.Add(job);
            return new OperationResult { Success = true, CashDelta = -fee, EntityId = job.JobId, ChangedAssetIds = inputAssetIds.ToList() };
        }

        public static void AdvanceJobs(CatalogState state, int day)
        {
            foreach (var job in state.Unified.Jobs.Where(j => j.Status == "running" && day >= j.ReadyDay)) job.Status = "ready";
        }

        // Collects a finished job once. canPlace decides whether the real inventory has room;
        // if not, the job simply stays ready (charged work never disappears).
        public static OperationResult ClaimJob(CatalogState state, string jobId, Func<IReadOnlyList<string>, bool> canPlace)
        {
            var job = state.Unified.Jobs.FirstOrDefault(j => j.JobId == jobId);
            if (job == null) return OperationResult.Fail("sp5.job.unknown", "This process is not available.");
            if (job.Status == "claimed") return new OperationResult { Success = true, EntityId = jobId };
            if (job.Status != "ready") return OperationResult.Fail("sp5.job.not_ready", "This job is still running.");
            var creates = job.Outputs.Where(o => o.Action == "create" && (o.Gold + o.Silver + o.Copper > 0 || o.DefinitionId != "sp5_inert_residue" || job.Kind != "sort" && job.Kind != "dismantle")).ToList();
            if (!canPlace(creates.Select(o => o.DefinitionId).ToList())) return OperationResult.Fail("sp5.job.full", "No room for the output. It stays ready to collect.");
            var result = new OperationResult { Success = true, EntityId = jobId };
            foreach (var id in job.InputAssetIds)
            {
                var a = Catalog.GetAsset(state, id);
                if (job.Kind == "jewelry_mount" || job.Kind == "jewelry_demount" || job.Kind == "jewelry_repair") { a.CustodyRef = ""; a.StateVersion++; result.ChangedAssetIds.Add(id); }
                else { a.Status = "consumed"; a.CustodyRef = ""; a.StateVersion++; result.ChangedAssetIds.Add(id); }
            }
            foreach (var o in job.Outputs)
            {
                if (o.Action == "create")
                {
                    if (!creates.Contains(o)) continue;
                    var created = Catalog.CreateAsset(state, o.DefinitionId, job.OwnerId);
                    created.GoldMicrograms = o.Gold; created.SilverMicrograms = o.Silver; created.CopperMicrograms = o.Copper;
                    created.SourceJobId = job.JobId; created.SourceAssetIds.AddRange(job.InputAssetIds); // provenance, not containment
                    result.CreatedAssetIds.Add(created.AssetId);
                }
                else if (o.Action == "embed")
                {
                    var stone = Catalog.GetAsset(state, o.ExistingAssetId);
                    stone.Status = "embedded"; stone.ParentAssetId = o.ParentAssetId; stone.SocketId = o.SocketId; stone.StateVersion++;
                }
                else if (o.Action == "release")
                {
                    var stone = Catalog.GetAsset(state, o.ExistingAssetId);
                    stone.Status = "active"; stone.ParentAssetId = ""; stone.SocketId = ""; stone.StateVersion++; result.CreatedAssetIds.Add(stone.AssetId);
                }
                else if (o.Action == "repair")
                {
                    var item = Catalog.GetAsset(state, o.ExistingAssetId);
                    var d = Catalog.GetDefinition(item.DefinitionId);
                    foreach (var cause in d.Hotspots.Where(h => h.Repairable).Select(h => h.CauseId).Distinct()) if (!item.ResolvedCauseIds.Contains(cause)) item.ResolvedCauseIds.Add(cause);
                    item.RepairHistory.Add("job:" + job.JobId); item.StateVersion++;
                }
            }
            job.Status = "claimed";
            return result;
        }

        // ---------------------------------------------------------------- jewelry services
        public static IReadOnlyList<AssetRecord> Stones(CatalogState state, string settingAssetId) => state.Assets.Where(a => a.ParentAssetId == settingAssetId && a.Status == "embedded").OrderBy(a => a.SocketId, StringComparer.Ordinal).ToList();
        public static IReadOnlyList<string> Sockets(ItemDefinition setting) => Enumerable.Range(0, setting.SocketCount).Select(i => "socket" + i.ToString(CultureInfo.InvariantCulture)).ToList();

        public static string CompatibilityError(CatalogState state, AssetRecord setting, IList<AssetRecord> stones)
        {
            var d = Catalog.GetDefinition(setting.DefinitionId);
            if (d.Role != "setting" || d.SocketCount <= 0) return "sp5.jewel.not_setting";
            if (Stones(state, setting.AssetId).Count > 0) return "sp5.jewel.occupied";
            if (stones.Count != d.SocketCount) return "sp5.jewel.count";            // one real stone per socket, never a paid-for replacement
            var defs = stones.Select(s => Catalog.GetDefinition(s.DefinitionId)).ToList();
            if (defs.Any(x => x.Role != "gem" || !d.CompatibleStones.Contains(x.ArtModel))) return "sp5.jewel.incompatible";
            if (defs.Select(x => x.GemFamily).Distinct().Count() != 1) return "sp5.jewel.mixed";
            return "";
        }

        private static OperationResult StartJewelry(CatalogState state, string kind, AssetRecord setting, IList<AssetRecord> stones, IEnumerable<string> ownedEquipmentRoles, long baseFee, int baseEnergy, MarketDay market, long cash, UnifiedRules rules, string ownerOfWork, Func<WorkJob, bool> plan)
        {
            var owned = new HashSet<string>(ownedEquipmentRoles, StringComparer.Ordinal);
            if (!owned.Contains("jeweler_bench") || !owned.Contains("jeweler_toolkit")) return OperationResult.Fail("sp5.jewel.no_bench", "This work needs the jeweler's bench and toolkit.");
            var fee = ScaledFee(baseFee, market, "jewelry_repair", rules);
            var energy = Scaled(baseEnergy, market, "processing_energy_cost", "all", rules);
            if (cash < fee) return OperationResult.Fail("sp5.job.no_cash", "You do not have enough cash for the service fee.");
            if (state.Unified.EnergyUnits < energy) return OperationResult.Fail("sp5.job.no_energy", "Not enough stored energy. Add a charged cell.");
            var job = new WorkJob { JobId = NextId(state, "job"), Kind = kind, Fee = fee, Energy = energy, StartDay = market.Day, ReadyDay = market.Day + Scaled(1, market, "processing_duration", "all", rules), OwnerId = ownerOfWork };
            job.InputAssetIds.Add(setting.AssetId); job.InputAssetIds.AddRange(stones.Select(s => s.AssetId));
            if (!plan(job)) return OperationResult.Fail("sp5.jewel.unsupported", "This piece cannot take that service as it is.");
            setting.CustodyRef = job.JobId; setting.StateVersion++;
            foreach (var s in stones) { s.CustodyRef = job.JobId; s.StateVersion++; }
            state.Unified.EnergyUnits -= energy; state.Unified.Jobs.Add(job);
            return new OperationResult { Success = true, CashDelta = -fee, EntityId = job.JobId, ChangedAssetIds = job.InputAssetIds.ToList() };
        }

        private static bool Workable(AssetRecord a, string owner) => a.Status == "active" && a.OwnerId == owner && string.IsNullOrEmpty(a.CustodyRef) && string.IsNullOrEmpty(a.ParentAssetId);

        // Mount: the SAME setting and stone identities; no wrapper item and no duplicate loose stones.
        public static OperationResult StartMount(CatalogState state, string settingAssetId, IList<string> stoneAssetIds, IEnumerable<string> equipment, MarketDay market, long cash, UnifiedRules rules, string owner = "player")
        {
            Catalog.Validate(state);
            var setting = Catalog.GetAsset(state, settingAssetId);
            var stones = stoneAssetIds.Distinct().Select(id => Catalog.GetAsset(state, id)).ToList();
            if (stones.Count != stoneAssetIds.Count || !Workable(setting, owner) || stones.Any(s => !Workable(s, owner))) return OperationResult.Fail("sp5.job.not_free", "An input is pledged, in service, or not yours.");
            var error = CompatibilityError(state, setting, stones);
            if (error.Length > 0) return OperationResult.Fail(error, "These stones do not fit this setting.");
            var sockets = Sockets(Catalog.GetDefinition(setting.DefinitionId));
            return StartJewelry(state, "jewelry_mount", setting, stones, equipment, 5, 2, market, cash, rules, owner, job =>
            {
                for (var i = 0; i < stones.Count; i++) job.Outputs.Add(new JobOutput { Action = "embed", ExistingAssetId = stones[i].AssetId, ParentAssetId = setting.AssetId, SocketId = sockets[i] });
                return true;
            });
        }

        // Demount: releases exactly the stones that are present. A missing stone cannot be recovered.
        public static OperationResult StartDemount(CatalogState state, string settingAssetId, IEnumerable<string> equipment, MarketDay market, long cash, UnifiedRules rules, string owner = "player")
        {
            Catalog.Validate(state);
            var setting = Catalog.GetAsset(state, settingAssetId);
            if (!Workable(setting, owner)) return OperationResult.Fail("sp5.job.not_free", "An input is pledged, in service, or not yours.");
            var stones = Stones(state, settingAssetId);
            if (stones.Count == 0) return OperationResult.Fail("sp5.jewel.empty", "There is no stone in this setting.");
            return StartJewelry(state, "jewelry_demount", setting, new List<AssetRecord>(), equipment, 3, 1, market, cash, rules, owner, job =>
            {
                foreach (var s in stones) job.Outputs.Add(new JobOutput { Action = "release", ExistingAssetId = s.AssetId });
                return true;
            });
        }

        // Repair resolves supported mechanical causes of the setting only; gem facts are untouched.
        public static OperationResult StartMountRepair(CatalogState state, string settingAssetId, IEnumerable<string> equipment, MarketDay market, long cash, UnifiedRules rules, string owner = "player")
        {
            Catalog.Validate(state);
            var setting = Catalog.GetAsset(state, settingAssetId);
            if (!Workable(setting, owner)) return OperationResult.Fail("sp5.job.not_free", "An input is pledged, in service, or not yours.");
            var d = Catalog.GetDefinition(setting.DefinitionId);
            if (!d.Hotspots.Any(h => h.Repairable && !setting.ResolvedCauseIds.Contains(h.CauseId))) return OperationResult.Fail("sp5.jewel.nothing", "Nothing here can be repaired at the bench.");
            return StartJewelry(state, "jewelry_repair", setting, new List<AssetRecord>(), equipment, 4, 1, market, cash, rules, owner, job => { job.Outputs.Add(new JobOutput { Action = "repair", ExistingAssetId = setting.AssetId }); return true; });
        }

        public static OperationResult LabReport(CatalogState state, string stoneAssetId, int day, MarketDay market, long cash, UnifiedRules rules)
        {
            var stone = Catalog.GetAsset(state, stoneAssetId);
            var d = Catalog.GetDefinition(stone.DefinitionId);
            if (d.Role != "gem") return OperationResult.Fail("sp5.lab.not_gem", "The laboratory only identifies loose or mounted stones.");
            if (ComponentValuation.GemIdentityKnown(stone)) return OperationResult.Fail("sp5.lab.done", "This stone already has a current report.");
            var fee = (long)Math.Ceiling(rules.GemLabFee * market.Multiplier("assay_service_cost", new[] { "gem_lab" }, rules) / 10000m);
            if (cash < fee) return OperationResult.Fail("sp5.job.no_cash", "You do not have enough cash for the service fee.");
            stone.StateVersion++;
            stone.Certificates.Add(new AssetCertificate { CertificateId = NextId(state, "lab"), AssetId = stone.AssetId, Scope = "gem_identity", Conclusion = d.GemOrigin, IssuedDay = day, AssetVersion = stone.StateVersion, Issuer = "gem_lab" });
            return new OperationResult { Success = true, CashDelta = -fee, ChangedAssetIds = { stoneAssetId } };
        }

        // ---------------------------------------------------------------- custom orders
        // Deposit = floor(Price * 30%). It is a held customer liability, never lendable cash or profit.
        public static OperationResult AcceptOrder(CatalogState state, string customerId, string kind, string settingAssetId, IList<string> stoneAssetIds, long price, int day, int deadlineDay, UnifiedRules rules)
        {
            Catalog.Validate(state);
            if ((kind != "service" && kind != "commission") || price <= 0 || deadlineDay <= day) return OperationResult.Fail("sp5.order.terms", "The order needs a price and a deadline after today.");
            var owner = kind == "service" ? customerId : "player";
            var items = new[] { settingAssetId }.Concat(stoneAssetIds).Select(id => Catalog.GetAsset(state, id)).ToList();
            // No speculative reservations: every input must exist and be free now.
            if (items.Any(a => !Workable(a, owner))) return OperationResult.Fail("sp5.order.inputs", "Every part for this order must be on hand and free.");
            var error = CompatibilityError(state, items[0], items.Skip(1).ToList());
            if (error.Length > 0) return OperationResult.Fail(error, "These stones do not fit this setting.");
            var order = new JewelryOrder { OrderId = NextId(state, "order"), CustomerId = customerId, Kind = kind, SettingAssetId = settingAssetId, StoneAssetIds = stoneAssetIds.ToList(),
                Price = price, Deposit = ComponentValuation.Scale(price, rules.OrderDepositBps), AcceptedDay = day, DeadlineDay = deadlineDay };
            foreach (var a in items) { a.CustodyRef = order.OrderId; a.StateVersion++; }
            state.Unified.HeldCustomerDeposits = checked(state.Unified.HeldCustomerDeposits + order.Deposit);
            state.Unified.Orders.Add(order);
            return new OperationResult { Success = true, CashDelta = order.Deposit, EntityId = order.OrderId };
        }

        public static OperationResult StartOrderWork(CatalogState state, string orderId, IEnumerable<string> equipment, MarketDay market, long cash, UnifiedRules rules)
        {
            var order = state.Unified.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null || order.Status != "accepted") return OperationResult.Fail("sp5.order.state", "This order cannot start work now.");
            var owner = order.Kind == "service" ? order.CustomerId : "player";
            var items = new[] { order.SettingAssetId }.Concat(order.StoneAssetIds).Select(id => Catalog.GetAsset(state, id)).ToList();
            foreach (var a in items) a.CustodyRef = "";
            var result = StartMount(state, order.SettingAssetId, order.StoneAssetIds, equipment, market, cash, rules, owner);
            if (!result.Success) { foreach (var a in items) a.CustodyRef = order.OrderId; return result; }
            state.Unified.Jobs.First(j => j.JobId == result.EntityId).OrderId = order.OrderId;
            order.JobId = result.EntityId; order.Status = "in_progress"; order.ServiceCostEarned = -result.CashDelta;
            return result;
        }

        public static OperationResult MarkOrderReady(CatalogState state, string orderId)
        {
            var order = state.Unified.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null || order.Status != "in_progress") return OperationResult.Fail("sp5.order.state", "This order cannot start work now.");
            var job = state.Unified.Jobs.First(j => j.JobId == order.JobId);
            if (job.Status != "claimed") return OperationResult.Fail("sp5.job.not_ready", "This job is still running.");
            Catalog.GetAsset(state, order.SettingAssetId).CustodyRef = order.OrderId;
            order.Status = "ready";
            return new OperationResult { Success = true, EntityId = orderId };
        }

        // Delivery: collects the balance exactly once and releases the held deposit into revenue.
        public static OperationResult DeliverOrder(CatalogState state, string orderId)
        {
            var order = state.Unified.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return OperationResult.Fail("sp5.order.state", "This order cannot start work now.");
            if (order.Status == "delivered") return new OperationResult { Success = true, EntityId = orderId }; // no second payout
            if (order.Status != "ready") return OperationResult.Fail("sp5.order.not_ready", "The piece is not ready to hand over.");
            var setting = Catalog.GetAsset(state, order.SettingAssetId);
            setting.CustodyRef = "";
            if (setting.OwnerId != order.CustomerId)
            {
                Catalog.Transfer(state, setting.AssetId, "player", order.CustomerId);
                foreach (var stone in Stones(state, setting.AssetId)) { stone.OwnerId = order.CustomerId; stone.StateVersion++; }
            }
            state.Unified.HeldCustomerDeposits -= order.Deposit;
            order.Status = "delivered";
            return new OperationResult { Success = true, CashDelta = order.Price - order.Deposit, EntityId = orderId, ChangedAssetIds = { setting.AssetId } };
        }

        // Before work: full refund. After work began: refund less only the completed service cost, capped at the deposit.
        public static OperationResult CancelOrder(CatalogState state, string orderId)
        {
            var order = state.Unified.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null || order.Status == "delivered" || order.Status == "cancelled") return OperationResult.Fail("sp5.order.state", "This order cannot start work now.");
            if (order.Status == "in_progress" && state.Unified.Jobs.Any(j => j.JobId == order.JobId && j.Status != "claimed")) return OperationResult.Fail("sp5.order.busy", "Collect the bench work before cancelling.");
            var kept = order.Status == "accepted" ? 0 : Math.Min(order.Deposit, order.ServiceCostEarned);
            foreach (var id in new[] { order.SettingAssetId }.Concat(order.StoneAssetIds)) { var a = Catalog.GetAsset(state, id); if (a.CustodyRef == order.OrderId) { a.CustodyRef = ""; a.StateVersion++; } }
            state.Unified.HeldCustomerDeposits -= order.Deposit;
            order.Status = "cancelled";
            return new OperationResult { Success = true, CashDelta = -(order.Deposit - kept), EntityId = orderId };
        }

        public static long LendableCash(long shopCash, CatalogState state) => Math.Max(0, shopCash - state.Unified.HeldCustomerDeposits);
    }

    // ------------------------------------------------------------------ inventory layout migration
    public sealed class PlacedItem { public string Id = ""; public int X, Y, W, H, NewW, NewH; public bool Pinned; }
    public sealed class MigrationPlan { public List<PlacedItem> Placements = new List<PlacedItem>(); public List<string> Overflow = new List<string>(); public int Kept, Moved; }

    public static class LayoutMigration
    {
        public const string CurrentId = "layout05";

        // Repack one grid for the new footprints. Items keep their place when they still fit;
        // others move to the first free position (row-major); what cannot fit goes to overflow.
        // Never overlaps, never drops an item, never duplicates one.
        public static MigrationPlan Plan(int gridWidth, int gridHeight, IEnumerable<PlacedItem> items)
        {
            var list = items.Select(i => new PlacedItem { Id = i.Id, X = i.X, Y = i.Y, W = i.W, H = i.H, NewW = i.NewW, NewH = i.NewH, Pinned = i.Pinned }).ToList();
            if (list.Select(i => i.Id).Distinct().Count() != list.Count) throw new ArgumentException("Duplicate item in inventory.");
            var used = new bool[gridWidth, gridHeight];
            var plan = new MigrationPlan();
            bool Fits(int x, int y, int w, int h)
            {
                if (x < 0 || y < 0 || x + w > gridWidth || y + h > gridHeight) return false;
                for (var i = x; i < x + w; i++) for (var j = y; j < y + h; j++) if (used[i, j]) return false;
                return true;
            }
            void Mark(int x, int y, int w, int h) { for (var i = x; i < x + w; i++) for (var j = y; j < y + h; j++) used[i, j] = true; }
            var pending = new List<PlacedItem>();
            // Unchanged shapes first, then changed shapes at their old origin, in stable row-major order.
            foreach (var item in list.OrderBy(i => i.NewW == i.W && i.NewH == i.H ? 0 : 1).ThenBy(i => i.Y).ThenBy(i => i.X).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                if (Fits(item.X, item.Y, item.NewW, item.NewH)) { Mark(item.X, item.Y, item.NewW, item.NewH); item.W = item.NewW; item.H = item.NewH; plan.Placements.Add(item); plan.Kept++; }
                else pending.Add(item);
            }
            foreach (var item in pending.OrderByDescending(i => i.NewW * i.NewH).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                var placed = false;
                for (var y = 0; y < gridHeight && !placed; y++) for (var x = 0; x < gridWidth && !placed; x++)
                    if (Fits(x, y, item.NewW, item.NewH)) { Mark(x, y, item.NewW, item.NewH); item.X = x; item.Y = y; item.W = item.NewW; item.H = item.NewH; plan.Placements.Add(item); plan.Moved++; placed = true; }
                if (!placed) plan.Overflow.Add(item.Id);
            }
            return plan;
        }

        public static bool NeedsMigration(CatalogState state, string inventoryId) => !state.Unified.Migrations.Any(m => m.MigrationId == CurrentId && m.InventoryId == inventoryId && m.Completed);

        // Recorded before items move, completed after: an interrupted run resumes instead of repeating.
        public static MigrationReceipt Begin(CatalogState state, string inventoryId, int day)
        {
            var receipt = state.Unified.Migrations.FirstOrDefault(m => m.MigrationId == CurrentId && m.InventoryId == inventoryId);
            if (receipt == null) { receipt = new MigrationReceipt { MigrationId = CurrentId, InventoryId = inventoryId, Day = day }; state.Unified.Migrations.Add(receipt); }
            return receipt;
        }

        public static void Complete(CatalogState state, MigrationReceipt receipt, MigrationPlan plan, string inventoryId)
        {
            if (receipt.Completed) return;
            foreach (var id in plan.Overflow)
                if (!state.Unified.Overflow.Any(o => o.AssetId == id)) state.Unified.Overflow.Add(new OverflowClaim { AssetId = id, FromInventoryId = inventoryId, MigrationId = CurrentId });
            receipt.Kept = plan.Kept; receipt.Moved = plan.Moved; receipt.Overflowed = plan.Overflow.Count; receipt.Completed = true;
        }

        public static bool Claim(CatalogState state, string assetId)
        {
            var claim = state.Unified.Overflow.FirstOrDefault(o => o.AssetId == assetId && !o.Claimed);
            if (claim == null) return false;
            claim.Claimed = true; return true;
        }
    }

    // ------------------------------------------------------------------ footprint hit geometry
    // Inventory selection covers the item's occupied cells (including transparent art), and
    // nothing else. This is deliberately separate from appraisal defect hotspots.
    public static class FootprintGeometry
    {
        // cells: row-major occupancy of the UNROTATED shape (width x height). orientation: 0,1,2,3 quarter turns clockwise.
        // (localX, localY): pointer position relative to the top-left of the ROTATED bounding box, in pixels.
        public static bool Contains(bool[] cells, int width, int height, int orientation, bool flipped, float cellSize, float localX, float localY)
        {
            if (cells == null || width <= 0 || height <= 0 || cells.Length != width * height || cellSize <= 0) return false;
            var turns = ((orientation % 4) + 4) % 4;
            int rw = turns % 2 == 0 ? width : height, rh = turns % 2 == 0 ? height : width;
            if (localX < 0 || localY < 0 || localX >= rw * cellSize || localY >= rh * cellSize) return false;
            int cx = (int)(localX / cellSize), cy = (int)(localY / cellSize);
            // Undo rotation, then the horizontal flip, to find the source cell.
            int sx, sy;
            switch (turns)
            {
                case 0: sx = cx; sy = cy; break;
                case 1: sx = cy; sy = height - 1 - cx; break;
                case 2: sx = width - 1 - cx; sy = height - 1 - cy; break;
                default: sx = width - 1 - cy; sy = cx; break;
            }
            if (flipped) sx = width - 1 - sx;
            return cells[sy * width + sx];
        }

        public static bool[] Filled(int width, int height) { var c = new bool[width * height]; for (var i = 0; i < c.Length; i++) c[i] = true; return c; }

        // Pixel rectangles (x, y, w, h) of each occupied cell in the rotated box; the UI builds one hit plate per cell.
        public static List<float[]> CellRects(bool[] cells, int width, int height, int orientation, bool flipped, float cellSize)
        {
            var rects = new List<float[]>();
            var turns = ((orientation % 4) + 4) % 4;
            int rw = turns % 2 == 0 ? width : height, rh = turns % 2 == 0 ? height : width;
            for (var y = 0; y < rh; y++) for (var x = 0; x < rw; x++)
                if (Contains(cells, width, height, orientation, flipped, cellSize, (x + 0.5f) * cellSize, (y + 0.5f) * cellSize)) rects.Add(new[] { x * cellSize, y * cellSize, cellSize, cellSize });
            return rects;
        }
    }

    // ------------------------------------------------------------------ five-page handbooks
    public sealed class HandbookPage
    {
        public int Number;                       // 1-based
        public string Kind = "checks";           // checks | tools | index
        public bool HasIntro;                    // the opening shares page 1 with real content
        public List<HandbookEntry> Entries = new List<HandbookEntry>();
        public List<ItemDefinition> Volumes = new List<ItemDefinition>();
    }

    public static class Handbooks
    {
        public const int MaxPages = 5;

        // Every physical book, including introduction and index, has at most five pages.
        public static IReadOnlyList<HandbookPage> Pages(ItemDefinition manual)
        {
            var pages = new List<HandbookPage>();
            if (Appraisal.IsHandbookCollection(manual))
            {
                var volumes = Appraisal.HandbookVolumes(manual.ManualFor);
                for (var i = 0; i < volumes.Count; i += Appraisal.HandbookIndexRows)
                    pages.Add(new HandbookPage { Kind = "index", HasIntro = i == 0, Volumes = volumes.Skip(i).Take(Appraisal.HandbookIndexRows).ToList() });
            }
            else
            {
                var entries = Appraisal.Handbook(manual);
                for (var i = 0; i < entries.Count; i += Appraisal.HandbookExamplesPerPage)
                    pages.Add(new HandbookPage { Kind = "checks", HasIntro = i == 0, Entries = entries.Skip(i).Take(Appraisal.HandbookExamplesPerPage).ToList() });
                // Tools, limits and a pricing example get their own short page when there is room.
                if (pages.Count < MaxPages) pages.Add(new HandbookPage { Kind = "tools", HasIntro = pages.Count == 0 });
            }
            if (pages.Count == 0) pages.Add(new HandbookPage { Kind = "tools", HasIntro = true });
            if (pages.Count > MaxPages) throw new InvalidOperationException("Handbook " + manual.Id + " exceeds five pages; split it into volumes.");
            for (var i = 0; i < pages.Count; i++) pages[i].Number = i + 1;
            return pages.AsReadOnly();
        }

        public static int PageCount(ItemDefinition manual) => Pages(manual).Count;

        public static void ValidateAll()
        {
            foreach (var manual in Catalog.Definitions.Where(d => d.IsBook && !string.IsNullOrEmpty(d.ManualFor))) Pages(manual);
        }
    }
}
