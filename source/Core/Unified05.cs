using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Revision 05: one valuation service, one market/effect pipeline, the native appraisal
// career, and the appraisal -> sale / pawn / leave customer loop. Pure logic: no Unity
// or game types. Everything that must survive a reload lives in UnifiedState.
namespace StarPawn.Core
{
    public sealed class UnifiedState
    {
        public int Version = 5;
        public List<CustomerVisit> Visits = new List<CustomerVisit>();
        public List<MarketDay> MarketDays = new List<MarketDay>();
        public List<ActiveWorldEvent> WorldEvents = new List<ActiveWorldEvent>();
        public List<EventHistory> EventHistory = new List<EventHistory>();
        public long MarketVersion;
        public List<string> GrantedEntitlements = new List<string>();
        public List<WorkJob> Jobs = new List<WorkJob>();
        public List<JewelryOrder> Orders = new List<JewelryOrder>();
        public long HeldCustomerDeposits;
        public int EnergyUnits;
        public long NextSequence = 1;
        public List<MigrationReceipt> Migrations = new List<MigrationReceipt>();
        public List<OverflowClaim> Overflow = new List<OverflowClaim>();
        public int LegacyExperience = -1; // 0.3 click-XP retained as history only; -1 = not migrated yet
    }

    // ------------------------------------------------------------------ labels
    public static class BusinessLabels
    {
        public const string Luxury = "Luxury", Collectibles = "Collectibles", Materials = "Materials", Weapons = "Weapons",
            Electronics = "Electronics", Entertainment = "Entertainment", Investment = "Investment", Energy = "Energy", Augments = "Augments";
        public static readonly string[] Filterable = { Luxury, Collectibles, Materials, Weapons, Electronics, Entertainment };

        // Native goods are mapped by their registered item type, never by a translated
        // name, a substring or a sprite colour. Adding a label here changes no native price.
        private static readonly Dictionary<string, string[]> Native = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "plasma_fuel", new[] { Materials, Energy } },
            { "hand", new[] { Weapons, Augments } },
        };

        public static IReadOnlyList<string> For(ItemDefinition definition) => definition.Labels.AsReadOnly();
        public static string Primary(ItemDefinition definition) => !string.IsNullOrEmpty(definition.PrimaryLabel) ? definition.PrimaryLabel : definition.Labels.FirstOrDefault() ?? "";
        public static bool Has(ItemDefinition definition, string label) => definition.Labels.Contains(label);
        public static IReadOnlyList<string> ForNative(string nativeItemId, bool rareConfiguration = false)
        {
            if (nativeItemId == null || !Native.TryGetValue(nativeItemId, out var labels)) return Array.Empty<string>();
            return rareConfiguration && nativeItemId == "hand" ? labels.Concat(new[] { Collectibles }).ToArray() : labels;
        }
        public static IEnumerable<ItemDefinition> Filter(IEnumerable<ItemDefinition> definitions, string label) => definitions.Where(d => Has(d, label));

        // Demand targets used by event rows. Explicit per definition facts, one entry per target.
        public static IReadOnlyList<string> DemandTargets(ItemDefinition d, bool certified)
        {
            var t = new List<string>();
            if (d.Material == "gold" || d.Material == "silver") { if (d.Form != "bar") t.Add("precious_metal_goods"); }
            if (d.Material == "gold" && d.Form != "bar") t.Add("gold_goods");
            if (!string.IsNullOrEmpty(d.JadeTier)) t.Add("jade_workmanship");
            if (Has(d, Electronics)) t.Add("electronics");
            if (Has(d, Collectibles)) t.Add("collectibles");
            if (d.Role == "setting" && d.SocketCount > 0) t.Add("mounted_jewelry");
            if (certified) t.Add("certified_goods");
            return t;
        }
    }

    // ------------------------------------------------------------------ market and events
    public sealed class UnifiedRules
    {
        public int RulesVersion = 5;
        public long CopperSellPer100g = 6, CopperBuyPer100g = 4;
        public int EventStartChancePercent = 35, MaxConcurrentEvents = 3, MaxExtraEventVisitorsPerDay = 2, MarketHistoryDays = 30;
        public int ChannelMinBps = 2500, ChannelMaxBps = 30000;
        // Commission fee policy for StarPawn families only; vanilla commissions are untouched.
        public long CommissionBaseFee = 5; public int CommissionFeeBps = 300; public long CommissionFeeCap = 60;
        // Pawn offers continue from valuation.
        public int PawnDefaultLtvBps = 5000, PawnMinLtvBps = 3000, PawnMaxLtvBps = 6500, PawnUnverifiedHaircutBps = 6000, PawnCraftLiquidationBps = 5000;
        public int OfferValidDays = 1;
        public int ResalePriceBasisPoints = 8500;
        public int CustomerAskBasisPoints = 9000;       // what a follow-up seller asks, relative to the routine estimate
        public int OrderDepositBps = 3000;
        public int GemLabGrownBps = 4000, GemImitationBps = 500;
        public long GemLabFee = 6;
        public List<WorldEventDefinition> Events = new List<WorldEventDefinition>();
        public List<RecipeDefinition> Recipes = new List<RecipeDefinition>();
        public List<IntentProfile> IntentProfiles = new List<IntentProfile>
        {
            new IntentProfile { Id = "ordinary", Leave = 50, Sale = 30, Pawn = 20 },
            new IntentProfile { Id = "urgent_cash", Leave = 15, Sale = 35, Pawn = 50 },
            new IntentProfile { Id = "sentimental", Leave = 55, Sale = 5, Pawn = 40 },
            new IntentProfile { Id = "dealer", Leave = 15, Sale = 75, Pawn = 10 },
            new IntentProfile { Id = "collector", Leave = 70, Sale = 15, Pawn = 15 },
        };
    }

    public sealed class IntentProfile { public string Id = ""; public int Leave, Sale, Pawn; }
    public sealed class EventRow { public string Channel = "", Target = "", Application = "multiplier_delta"; public int DeltaBps; }
    public sealed class WorldEventDefinition { public string Id = ""; public int DurationDays = 2, CooldownDays = 10, UnlockDay = 8; public List<EventRow> Rows = new List<EventRow>(); }
    public sealed class ActiveWorldEvent { public string EventId = ""; public int StartDay, EndDay; }
    public sealed class EventHistory { public string EventId = ""; public int LastEndDay; }
    public sealed class ChannelValue { public string Key = ""; public int DeltaBps; }

    public sealed class MarketDay
    {
        public int Day; public long Version;
        public long GoldSell, GoldBuy, SilverSell, SilverBuy, CopperSell, CopperBuy;
        public List<ChannelValue> Channels = new List<ChannelValue>();
        public List<string> EventIds = new List<string>();
        public int Delta(string channel, string target) { var k = channel + ":" + target; var c = Channels.FirstOrDefault(x => x.Key == k); return c == null ? 0 : c.DeltaBps; }
        // Union of rows, each applied once even when an item matches several targets.
        public int Multiplier(string channel, IEnumerable<string> targets, UnifiedRules rules)
        {
            var sum = 10000; foreach (var t in targets.Distinct()) sum += Delta(channel, t);
            return Math.Max(rules.ChannelMinBps, Math.Min(rules.ChannelMaxBps, sum));
        }
    }

    public static class MarketService
    {
        // Idempotent per day: reopening news, reloading or sleeping again returns the saved record.
        public static MarketDay EnsureDay(CatalogState state, int day, long goldSell, long goldBuy, long silverSell, long silverBuy, bool eventsOptedIn, UnifiedRules rules)
        {
            if (day < 0 || goldBuy < 0 || goldSell < goldBuy || silverBuy < 0 || silverSell < silverBuy) throw new ArgumentException("Invalid base market.");
            var u = state.Unified;
            var existing = u.MarketDays.FirstOrDefault(m => m.Day == day);
            if (existing != null) return existing;
            if (u.MarketDays.Any(m => m.Day > day)) throw new InvalidOperationException("The market cannot be generated for a past day.");
            foreach (var done in u.WorldEvents.Where(e => e.EndDay < day).ToList())
            {
                u.WorldEvents.Remove(done);
                var h = u.EventHistory.FirstOrDefault(x => x.EventId == done.EventId);
                if (h == null) u.EventHistory.Add(new EventHistory { EventId = done.EventId, LastEndDay = done.EndDay }); else h.LastEndDay = done.EndDay;
            }
            if (eventsOptedIn && u.WorldEvents.Count < rules.MaxConcurrentEvents && rules.Events.Count > 0)
            {
                var roll = Hash(state.RunId + ":event:" + day.ToString(CultureInfo.InvariantCulture));
                if (roll % 100 < (uint)rules.EventStartChancePercent)
                {
                    var eligible = rules.Events.Where(e => day >= e.UnlockDay && u.WorldEvents.All(a => a.EventId != e.Id)
                        && u.EventHistory.All(h => h.EventId != e.Id || day > h.LastEndDay + e.CooldownDays)).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
                    if (eligible.Count > 0)
                    {
                        var pick = eligible[(int)((roll / 100) % (uint)eligible.Count)];
                        u.WorldEvents.Add(new ActiveWorldEvent { EventId = pick.Id, StartDay = day, EndDay = day + Math.Max(1, pick.DurationDays) - 1 });
                    }
                }
            }
            var record = new MarketDay { Day = day, Version = checked(++u.MarketVersion) };
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var active in u.WorldEvents.Where(e => e.StartDay <= day && e.EndDay >= day).OrderBy(e => e.EventId, StringComparer.Ordinal))
            {
                var definition = rules.Events.FirstOrDefault(e => e.Id == active.EventId);
                if (definition == null) continue; // an optional pack was removed; its saved event simply has no rows
                record.EventIds.Add(active.EventId);
                foreach (var row in definition.Rows) { var key = row.Channel + ":" + row.Target; totals[key] = (totals.TryGetValue(key, out var v) ? v : 0) + row.DeltaBps; }
            }
            foreach (var pair in totals.OrderBy(p => p.Key, StringComparer.Ordinal)) record.Channels.Add(new ChannelValue { Key = pair.Key, DeltaBps = pair.Value });
            long Apply(long price, string metal) => Math.Max(0, checked((long)decimal.Floor((decimal)price * Math.Max(rules.ChannelMinBps, Math.Min(rules.ChannelMaxBps, 10000 + record.Delta("commodity_price", metal))) / 10000m)));
            record.GoldSell = Apply(goldSell, "Au"); record.GoldBuy = Math.Min(record.GoldSell, Apply(goldBuy, "Au"));
            record.SilverSell = Apply(silverSell, "Ag"); record.SilverBuy = Math.Min(record.SilverSell, Apply(silverBuy, "Ag"));
            record.CopperSell = Apply(rules.CopperSellPer100g, "Cu"); record.CopperBuy = Math.Min(record.CopperSell, Apply(rules.CopperBuyPer100g, "Cu"));
            u.MarketDays.Add(record);
            while (u.MarketDays.Count > rules.MarketHistoryDays) u.MarketDays.RemoveAt(0);
            return record;
        }

        public static MarketDay? Find(CatalogState state, int day) => state.Unified.MarketDays.FirstOrDefault(m => m.Day == day);
        internal static uint Hash(string value) { uint h = 2166136261; foreach (var c in value) h = unchecked((h ^ c) * 16777619); h ^= h >> 15; h = unchecked(h * 2246822519); h ^= h >> 13; return h; }
    }

    // ------------------------------------------------------------------ component valuation
    public sealed class QuoteLine
    {
        public string CauseId = "", Component = "", LabelKey = "", Label = "";
        public long Amount; // credits removed from that component (>= 0)
    }

    public sealed class ComponentQuote
    {
        public string Context = "";
        public string Metal = "";                 // gold / silver / ""
        public long EligibleMilligrams; public int PurityBasisPoints; public bool PurityConfirmed; public long MetalRate;
        public long MetalValue;                   // never reduced by cosmetic findings
        public long CraftBasis, CraftDeduction, CraftValue;
        public long StoneValue; public int StonesCounted, StonesMissing; public bool StonesVerified = true;
        public long PremiumValue;                 // justified edition / provenance premium, shown separately
        public long Total;
        public List<QuoteLine> Lines = new List<QuoteLine>();
        public List<string> AppliedCauses = new List<string>();
    }

    public static class ComponentValuation
    {
        public const string BuyFromCustomer = "buy_from_customer", SellToCustomer = "sell_to_customer", MaterialBuyback = "material_buyback",
            PawnCollateral = "pawn_collateral", TrueValue = "true_value";

        public static string ComponentOf(ItemDefinition d, HotspotDefinition h)
        {
            if (!string.IsNullOrEmpty(h.Component)) return h.Component;
            if (h.RevealsMaterial) return "metal_purity";
            if (h.RevealsAuthenticity) return "premium";
            return d.Material == "gold" || d.Material == "silver" ? "workmanship" : "condition";
        }

        public static bool MaterialKnown(AssetRecord asset, ItemDefinition d) =>
            asset.Measurements.Contains("assay") || asset.Certificates.Any(c => c.Scope == "material") ||
            d.Hotspots.Any(h => h.RevealsMaterial && asset.DiscoveredEvidenceIds.Contains(h.Id));
        public static bool AuthenticityKnown(AssetRecord asset, ItemDefinition d) =>
            asset.Certificates.Any(c => c.Scope == "authenticity" || c.Scope == "edition") ||
            d.Hotspots.Any(h => h.RevealsAuthenticity && asset.DiscoveredEvidenceIds.Contains(h.Id));
        public static bool GemIdentityKnown(AssetRecord asset) => asset.Certificates.Any(c => c.Scope == "gem_identity");

        // context TrueValue uses the saved truth (for the private commission answer only).
        public static ComponentQuote Quote(CatalogState state, AssetRecord asset, MarketDay market, string context, UnifiedRules rules)
        {
            var d = Catalog.GetDefinition(asset.DefinitionId);
            var truth = context == TrueValue;
            var q = new ComponentQuote { Context = context };
            var certified = asset.Certificates.Count > 0;
            var targets = BusinessLabels.DemandTargets(d, certified);
            var demand = market.Multiplier("category_demand", targets, rules);
            // Findings that count: every confirmed, unresolved cause (truth: every active cause).
            var active = d.Hotspots.Where(h => !asset.ResolvedCauseIds.Contains(h.CauseId) && (truth || asset.DiscoveredEvidenceIds.Contains(h.Id))).ToList();
            var causes = active.GroupBy(h => h.CauseId, StringComparer.Ordinal).ToList(); // several views of one crack = one cause

            var precious = d.Material == "gold" || d.Material == "silver";
            var materialKnown = truth || MaterialKnown(asset, d);
            if (precious)
            {
                q.Metal = d.Material;
                var loss = causes.Where(c => c.Any(h => ComponentOf(d, h) == "metal_mass")).Sum(c => c.Max(h => h.MassLossMilligrams));
                q.EligibleMilligrams = Math.Max(0, d.WeightMilligrams - loss);
                q.PurityBasisPoints = materialKnown ? d.ActualPurityBasisPoints : d.DeclaredPurityBasisPoints;
                q.PurityConfirmed = materialKnown;
                var ask = d.Material == "gold" ? market.GoldSell : market.SilverSell;
                var bid = d.Material == "gold" ? market.GoldBuy : market.SilverBuy;
                q.MetalRate = context == SellToCustomer || context == TrueValue ? ask : bid;
                if (context == BuyFromCustomer) q.MetalRate = ask; // 0.3 behaviour retained: stock is bought at the day's selling rate
                q.MetalValue = ValuationV2.MetalValue(q.EligibleMilligrams, q.PurityBasisPoints, q.MetalRate);
                if (context == PawnCollateral && !materialKnown) q.MetalValue = Scale(q.MetalValue, rules.PawnUnverifiedHaircutBps);
                foreach (var c in causes.Where(c => c.Any(h => ComponentOf(d, h) == "metal_mass")))
                    q.Lines.Add(Line(c, "metal_mass", ValuationV2.MetalValue(c.Max(h => h.MassLossMilligrams), q.PurityBasisPoints, q.MetalRate)));
                // A proven non-precious body keeps only its saved object value, shown as workmanship.
                var basis = materialKnown && d.ActualPurityBasisPoints == 0 ? d.ActualBaseValue : d.CraftValue;
                q.CraftBasis = Scale(Scale(basis, asset.QualityBasisPoints), demand);
            }
            else if (d.Role == "lot")
            {
                // Measured recovered material: valued by its recorded content, nothing else.
                var rate = context == SellToCustomer || context == TrueValue || context == BuyFromCustomer;
                q.MetalValue = checked(ValuationV2.MetalValue(Operations05.Gold(asset) / 1000, 10000, rate ? market.GoldSell : market.GoldBuy)
                    + ValuationV2.MetalValue(Operations05.Silver(asset) / 1000, 10000, rate ? market.SilverSell : market.SilverBuy)
                    + ValuationV2.MetalValue(Operations05.Copper(asset) / 100000, 10000, rate ? market.CopperSell : market.CopperBuy));
                q.PurityConfirmed = true;
            }
            else if (d.Role == "gem")
            {
                q.StoneValue = StoneValue(asset, d, market, rules, truth, out var verified); q.StonesCounted = 1; q.StonesVerified = verified;
            }
            else
            {
                var authentic = truth || AuthenticityKnown(asset, d);
                var body = authentic ? d.ActualBaseValue : d.BaseValue;
                // A supported premium is the part of the claim that evidence can remove.
                var premium = authentic ? 0 : Math.Max(0, d.BaseValue - Math.Min(d.BaseValue, d.ActualBaseValue));
                var jade = JadeMultiplier(asset, d, market, rules, truth);
                q.CraftBasis = Scale(Scale(Scale(body - premium, asset.QualityBasisPoints), demand), jade);
                q.PremiumValue = Scale(Scale(premium, asset.QualityBasisPoints), BusinessLabels.Has(d, BusinessLabels.Collectibles) ? market.Multiplier("category_demand", new[] { "collectibles" }, rules) : 10000);
            }
            // Cosmetic / structural findings are confined to workmanship and capped by it.
            long remaining = q.CraftBasis;
            foreach (var c in causes)
            {
                var component = ComponentOf(d, c.First());
                if (component != "workmanship" && component != "condition") continue;
                var amount = Math.Min(remaining, c.Max(h => h.ValueDeduction));
                remaining -= amount; q.CraftDeduction += amount; q.Lines.Add(Line(c, "workmanship", amount));
            }
            q.CraftValue = remaining;
            if (context == MaterialBuyback) q.CraftValue = Scale(q.CraftValue, rules.ResalePriceBasisPoints);
            if (context == PawnCollateral) q.CraftValue = Scale(q.CraftValue, rules.PawnCraftLiquidationBps);
            // Real embedded stones are separate children; the setting never counts them as gold.
            if (d.Role == "setting")
            {
                var stones = state.Assets.Where(a => a.ParentAssetId == asset.AssetId && a.Status == "embedded").ToList();
                foreach (var stone in stones)
                {
                    var sd = Catalog.GetDefinition(stone.DefinitionId);
                    var value = StoneValue(stone, sd, market, rules, truth, out var verified);
                    if (context == PawnCollateral && !verified) value = Scale(value, rules.GemImitationBps); // an unverified claim cannot secure a natural-gem premium
                    q.StoneValue = checked(q.StoneValue + value); q.StonesCounted++; q.StonesVerified &= verified;
                }
                q.StonesMissing = Math.Max(0, d.SocketCount - stones.Count);
            }
            foreach (var c in causes) q.AppliedCauses.Add(c.Key);
            q.Total = checked(q.MetalValue + q.CraftValue + q.StoneValue + q.PremiumValue);
            return q;
        }

        public static long StoneValue(AssetRecord stone, ItemDefinition d, MarketDay market, UnifiedRules rules, bool truth, out bool verified)
        {
            verified = truth || GemIdentityKnown(stone);
            var origin = verified ? d.GemOrigin : d.GemClaim;
            var factor = origin == "imitation" ? rules.GemImitationBps : origin == "laboratory_grown" ? rules.GemLabGrownBps : 10000;
            var index = market.Multiplier("gem_price", new[] { d.GemFamily }, rules);
            var value = Scale(Scale(checked(d.GemMilliCarats * d.GemUnitValue / 1000), factor), index);
            var active = d.Hotspots.Where(h => !stone.ResolvedCauseIds.Contains(h.CauseId) && (truth || stone.DiscoveredEvidenceIds.Contains(h.Id)))
                .GroupBy(h => h.CauseId, StringComparer.Ordinal).Sum(c => c.Max(h => h.ValueDeduction));
            return Math.Max(0, value - Math.Min(value, active));
        }

        private static int JadeMultiplier(AssetRecord asset, ItemDefinition d, MarketDay market, UnifiedRules rules, bool truth)
        {
            if (!string.IsNullOrEmpty(d.JadeTier)) return market.Multiplier("jade_tier_value", new[] { d.JadeTier }, rules);
            if (!d.IsJadeRough) return 10000;
            // Sealed rough never leaks its grade through a price: use the average band until the window is open.
            if (truth || asset.WindowOpened) return market.Multiplier("jade_tier_value", new[] { new[] { "", "dou", "nuo", "bing" }[Math.Max(1, Math.Min(3, asset.JadeGrade))] }, rules);
            var average = (market.Delta("jade_tier_value", "dou") + market.Delta("jade_tier_value", "nuo") + market.Delta("jade_tier_value", "bing")) / 3;
            return Math.Max(rules.ChannelMinBps, Math.Min(rules.ChannelMaxBps, 10000 + average));
        }

        private static QuoteLine Line(IGrouping<string, HotspotDefinition> cause, string component, long amount)
        {
            var h = cause.First();
            return new QuoteLine { CauseId = cause.Key, Component = component, LabelKey = h.LabelKey, Label = TextResolver.Get(h.LabelKey, h.Label), Amount = amount };
        }

        internal static long Scale(long amount, int basisPoints) => checked((long)decimal.Floor((decimal)amount * basisPoints / 10000m));
    }

    // ------------------------------------------------------------------ art for assembled jewelry
    public static class JewelryArt
    {
        private static readonly Dictionary<string, string> Missing = new Dictionary<string, string>(StringComparer.Ordinal)
            { { "ring", "missing_center" }, { "pendant", "missing_insert" }, { "earrings", "missing_right" }, { "bracelet", "missing_left" }, { "necklace", "missing_drop" } };

        // A setting shows its REAL embedded stone; with none embedded, the setting's empty art.
        public static string TextureFor(CatalogState state, AssetRecord asset, string view = "front")
        {
            var d = Catalog.GetDefinition(asset.DefinitionId);
            if (d.Role != "setting" || d.SocketCount <= 0 || string.IsNullOrEmpty(d.SettingForm)) return Catalog.TextureFor(asset, view);
            var stones = state.Assets.Where(a => a.ParentAssetId == asset.AssetId && a.Status == "embedded").ToList();
            if (stones.Count == 0) return "starpawn:sp5_setting_" + d.SettingForm + "_" + Metal(d);
            var stone = Catalog.GetDefinition(stones[0].DefinitionId).ArtModel.Substring(4);
            var repaired = d.Hotspots.Where(h => h.Repairable).All(h => asset.ResolvedCauseIds.Contains(h.CauseId));
            var art = string.IsNullOrEmpty(d.ArtState) || (repaired && d.Hotspots.All(h => h.Repairable)) ? "intact" : d.ArtState;
            if (stones.Count < d.SocketCount) art = Missing[d.SettingForm];
            return "starpawn:sp5_j_" + d.SettingForm + "_" + Metal(d) + "_" + stone + "_" + art;
        }

        private static string Metal(ItemDefinition d) => d.Material == "gold" ? "au18k" : "ag925";
    }

    // ------------------------------------------------------------------ native career
    public static class Career
    {
        public const int Junior = 0, Appraiser = 1, Senior = 2, Master = 3;

        // Paid commissions for a family need this native rank. Trade, reading and
        // ordinary repair are not gated by it.
        public static bool CanCommission(ItemDefinition d, int nativeRank) => d.CommissionRank >= 0 && nativeRank >= d.CommissionRank && !d.ShopOnly;

        // Mirrors the audited native precision bands for dialogue and the mod fee only;
        // native settlement remains the authority for career progress.
        public static int Grade(long submitted, long correct)
        {
            if (submitted < 0 || correct <= 0) return 0;
            var ratio = (decimal)submitted / correct;
            if (ratio >= 0.95m && ratio <= 1.05m) return 3;
            if (ratio >= 0.85m && ratio <= 1.15m) return 2;
            if (ratio >= 0.70m && ratio <= 1.30m) return 1;
            return 0;
        }

        // Native percentage fees were not balanced for jewelry prices; StarPawn families use a bounded fee.
        public static long Fee(long correctValue, int grade, UnifiedRules rules)
        {
            if (grade <= 0) return 0;
            var fee = checked(rules.CommissionBaseFee + ComponentValuation.Scale(correctValue, rules.CommissionFeeBps));
            fee = Math.Min(rules.CommissionFeeCap, fee);
            return grade == 3 ? fee : grade == 2 ? fee * 3 / 4 : fee / 2;
        }

        // Physical handbook entitlements granted once per promotion (manual definitions carry CommissionRank as their grant rank).
        public static IReadOnlyList<string> ClaimEntitlements(CatalogState state, int nativeRank, IEnumerable<string> alreadyOwnedDefinitionIds)
        {
            var owned = new HashSet<string>(alreadyOwnedDefinitionIds, StringComparer.Ordinal);
            var grant = new List<string>();
            foreach (var manual in Catalog.Definitions.Where(d => d.IsBook && d.HandbookTemplates.Count > 0 && d.CommissionRank >= 0 && d.CommissionRank <= nativeRank).OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                var key = "rank:" + manual.CommissionRank.ToString(CultureInfo.InvariantCulture) + ":" + manual.Id;
                if (state.Unified.GrantedEntitlements.Contains(key)) continue;
                state.Unified.GrantedEntitlements.Add(key);
                if (!owned.Contains(manual.Id)) grant.Add(manual.Id);
            }
            return grant;
        }

        public static void MigrateLegacyExperience(CatalogState state)
        {
            if (state.Unified.LegacyExperience >= 0) return;
            state.Unified.LegacyExperience = state.Experience; // history only: never converted into native promotions
        }
    }

    // ------------------------------------------------------------------ customer visit
    public sealed class CustomerVisit
    {
        public string VisitId = "", AssetId = "", CustomerId = "", EncounterId = "";
        public string Path = "appraisal";          // appraisal | sale | pawn
        public string Profile = "ordinary";
        public bool Dishonest;
        public int Day; public long MarketVersion;
        public long CorrectValue;                  // private settlement answer for the saved market
        public string Stage = "inspecting";        // inspecting | settled | followup | closed
        public long SubmittedValue = -1; public int Grade = -1; public long Fee; public bool FeePaid;
        public int WeightLeave, WeightSale, WeightPawn; public int Draw = -1;
        public string Intent = "";                 // leave | offer_sale | request_pawn
        public bool IntentRevealed;
        public long OfferAmount; public long CollateralBasis; public int PawnLtvBps; public int PawnTermDays; public int OfferExpiryDay;
        public string BlockedReasonKey = "";
        public string Resolution = "";             // sold | pawned | declined | left | walkout | expired | refused
        public string ContractId = "";
    }

    public sealed class VisitResult
    {
        public bool Success; public string ErrorKey = "", Error = "";
        public long CashDelta; public string LineKey = "", Line = "";
        public EconomyCommand? PawnCommand;
        public static VisitResult Fail(string key, string english) => new VisitResult { ErrorKey = key, Error = TextResolver.Get(key, english) };
    }

    public static class VisitService
    {
        public const int MaxVisits = 512;

        public static CustomerVisit Open(CatalogState state, string assetId, string customerId, string path, string profile, bool dishonest, MarketDay market, int nativeRank, UnifiedRules rules)
        {
            Catalog.Validate(state);
            if (path != "appraisal" && path != "sale" && path != "pawn") throw new ArgumentException("Unknown visit path.");
            var asset = Catalog.GetAsset(state, assetId);
            if (asset.OwnerId != customerId || customerId == "player") throw new InvalidOperationException("A visit concerns the customer's own item.");
            // Reopening a window or reloading returns the same visit: nothing is redrawn.
            var existing = state.Unified.Visits.FirstOrDefault(v => v.AssetId == assetId && v.CustomerId == customerId && v.Stage != "closed");
            if (existing != null) return existing;
            var d = Catalog.GetDefinition(asset.DefinitionId);
            if (path == "appraisal" && !Career.CanCommission(d, nativeRank)) throw new InvalidOperationException("This commission needs a higher appraiser rank.");
            if (rules.IntentProfiles.All(p => p.Id != profile)) throw new ArgumentException("Unknown customer profile.");
            if (state.Unified.Visits.Count >= MaxVisits) state.Unified.Visits.RemoveAll(v => v.Stage == "closed" && v.Day < market.Day - 30);
            if (state.Unified.Visits.Count >= MaxVisits) throw new InvalidOperationException("Visit history is full.");
            var visit = new CustomerVisit
            {
                VisitId = "visit-" + (state.Unified.NextSequence++).ToString(CultureInfo.InvariantCulture), AssetId = assetId, CustomerId = customerId,
                Path = path, Profile = profile, Dishonest = dishonest, Day = market.Day, MarketVersion = market.Version,
                CorrectValue = ComponentValuation.Quote(state, asset, market, ComponentValuation.TrueValue, rules).Total,
                Stage = path == "appraisal" ? "inspecting" : "followup", Intent = path == "sale" ? "offer_sale" : path == "pawn" ? "request_pawn" : "",
                IntentRevealed = path != "appraisal"
            };
            visit.EncounterId = "enc-" + visit.VisitId;
            if (path != "appraisal")
            {
                // A direct sale/pawn visit is already a negotiation: price it now from the same valuation.
                visit.OfferExpiryDay = market.Day + Math.Max(0, rules.OfferValidDays - 1);
                state.Unified.Visits.Add(visit);
                Reprice(state, visit, market, rules);
                return visit;
            }
            state.Unified.Visits.Add(visit);
            return visit;
        }

        public static CustomerVisit Get(CatalogState state, string visitId) => state.Unified.Visits.FirstOrDefault(v => v.VisitId == visitId) ?? throw new InvalidOperationException("Unknown visit.");

        // The native callback asks for the private correct value of the saved commission market.
        public static long CorrectValue(CatalogState state, string assetId)
        {
            var visit = state.Unified.Visits.LastOrDefault(v => v.AssetId == assetId && v.Path == "appraisal");
            if (visit == null) throw new InvalidOperationException("No commission for this item.");
            return visit.CorrectValue;
        }

        // Settles the paid commission exactly once. A reload or a second click returns the saved result.
        public static VisitResult Submit(CatalogState state, string visitId, long estimate, UnifiedRules rules)
        {
            var visit = Get(state, visitId);
            if (visit.Path != "appraisal") return VisitResult.Fail("sp5.visit.not_commission", "This customer did not ask for a paid appraisal.");
            if (estimate < 0) return VisitResult.Fail("sp5.visit.bad_estimate", "Enter an estimate of zero or more.");
            if (visit.Stage != "inspecting") return new VisitResult { Success = true, CashDelta = 0, LineKey = "sp5.visit.already_settled", Line = TextResolver.Get("sp5.visit.already_settled", "The appraisal fee was already settled.") };
            visit.SubmittedValue = estimate;
            visit.Grade = Career.Grade(estimate, visit.CorrectValue);
            visit.Fee = Career.Fee(visit.CorrectValue, visit.Grade, rules);
            visit.FeePaid = visit.Fee > 0;
            visit.Stage = "settled";
            if (visit.Grade == 0) { visit.Resolution = "refused"; visit.Stage = "closed"; }
            var key = "sp5.visit.grade" + visit.Grade.ToString(CultureInfo.InvariantCulture);
            return new VisitResult { Success = true, CashDelta = visit.Fee, LineKey = key, Line = TextResolver.Get(key, new[] {
                "\"That cannot be right. I will ask someone else.\"", "\"Hm. Roughly what I heard elsewhere.\"",
                "\"That sounds fair. Thank you.\"", "\"Exactly what I needed to know. Thank you.\"" }[visit.Grade]) };
        }

        // One saved weighted draw after settlement. Ineligible routes move their weight to "leave"; nothing is rerolled.
        public static CustomerVisit ResolveIntent(CatalogState state, string visitId, MarketDay market, bool pawnLicensed, long shopCash, UnifiedRules rules)
        {
            var visit = Get(state, visitId);
            if (visit.Path != "appraisal" || visit.Stage != "settled" || visit.Draw >= 0) return visit;
            var asset = Catalog.GetAsset(state, visit.AssetId);
            var d = Catalog.GetDefinition(asset.DefinitionId);
            var profile = rules.IntentProfiles.First(p => p.Id == visit.Profile);
            int leave = profile.Leave, sale = profile.Sale, pawn = profile.Pawn;
            // Bounded, data-driven event shifts (e.g. a credit squeeze moves weight toward pawn requests).
            var toPawn = market.Delta("intent_shift", "request_pawn") / 100; var toSale = market.Delta("intent_shift", "offer_sale") / 100;
            pawn += toPawn; sale += toSale; leave -= toPawn + toSale;
            var collateral = ComponentValuation.Quote(state, asset, market, ComponentValuation.PawnCollateral, rules).Total;
            var pawnOk = pawnLicensed && asset.Status == "active" && collateral > 0 && !d.ShopOnly && string.IsNullOrEmpty(asset.ParentAssetId);
            if (!pawnOk) { leave += Math.Max(0, pawn); pawn = 0; }
            var ask = AskFor(state, asset, market, rules);
            if (ask <= 0) { leave += Math.Max(0, sale); sale = 0; }
            leave = Math.Max(0, leave); sale = Math.Max(0, sale); pawn = Math.Max(0, pawn);
            if (leave + sale + pawn == 0) leave = 1;
            visit.WeightLeave = leave; visit.WeightSale = sale; visit.WeightPawn = pawn;
            visit.Draw = (int)(MarketService.Hash(state.RunId + ":intent:" + visit.VisitId) % (uint)(leave + sale + pawn));
            visit.Intent = visit.Draw < leave ? "leave" : visit.Draw < leave + sale ? "offer_sale" : "request_pawn";
            visit.IntentRevealed = true;
            visit.OfferExpiryDay = market.Day + Math.Max(0, rules.OfferValidDays - 1);
            if (visit.Intent == "leave") { visit.Resolution = "left"; visit.Stage = "closed"; }
            else { visit.Stage = "followup"; Reprice(state, visit, market, rules); }
            return visit;
        }

        // Follow-up terms always come from the common valuation of the SAME asset and its recorded findings.
        public static void Reprice(CatalogState state, CustomerVisit visit, MarketDay market, UnifiedRules rules)
        {
            if (visit.Stage != "followup") return;
            var asset = Catalog.GetAsset(state, visit.AssetId);
            if (visit.Intent == "offer_sale") visit.OfferAmount = AskFor(state, asset, market, rules);
            if (visit.Intent == "request_pawn")
            {
                visit.CollateralBasis = ComponentValuation.Quote(state, asset, market, ComponentValuation.PawnCollateral, rules).Total;
                var ltv = rules.PawnDefaultLtvBps + market.Delta("new_pawn_ltv", "new_agreements");
                visit.PawnLtvBps = Math.Max(rules.PawnMinLtvBps, Math.Min(rules.PawnMaxLtvBps, ltv));
                visit.OfferAmount = ComponentValuation.Scale(visit.CollateralBasis, visit.PawnLtvBps);
                var d = Catalog.GetDefinition(asset.DefinitionId);
                visit.BlockedReasonKey = (d.Material == "gold" || d.Material == "silver") && !ComponentValuation.MaterialKnown(asset, d) ? "sp5.pawn.needs_assay" : "";
            }
        }

        private static long AskFor(CatalogState state, AssetRecord asset, MarketDay market, UnifiedRules rules)
            => ComponentValuation.Scale(ComponentValuation.Quote(state, asset, market, ComponentValuation.MaterialBuyback, rules).Total, rules.CustomerAskBasisPoints);

        public static VisitResult AcceptSale(CatalogState state, string visitId, int day, long shopCash)
        {
            var visit = Get(state, visitId);
            if (visit.Stage != "followup" || visit.Intent != "offer_sale") return VisitResult.Fail("sp5.visit.no_offer", "There is no sale offer to accept.");
            if (day > visit.OfferExpiryDay) { Close(visit, "expired"); return VisitResult.Fail("sp5.visit.expired", "The customer has already left with the item."); }
            if (shopCash < visit.OfferAmount) return VisitResult.Fail("sp5.visit.no_cash", "You do not have enough cash for this purchase.");
            var asset = Catalog.GetAsset(state, visit.AssetId);
            if (asset.Status != "active" || asset.OwnerId != visit.CustomerId) return VisitResult.Fail("sp5.visit.item_changed", "The item is no longer available.");
            Catalog.Transfer(state, visit.AssetId, visit.CustomerId, "player"); // the SAME asset; nothing is spawned
            Close(visit, "sold");
            return new VisitResult { Success = true, CashDelta = -visit.OfferAmount };
        }

        // Returns the economy command to sign; ConfirmPawn records the contract once the ledger accepted it.
        public static VisitResult PreparePawn(CatalogState state, string visitId, int day, long shopLendableCash, int termDays)
        {
            var visit = Get(state, visitId);
            if (visit.Stage != "followup" || visit.Intent != "request_pawn") return VisitResult.Fail("sp5.visit.no_offer", "There is no pawn request to accept.");
            if (day > visit.OfferExpiryDay) { Close(visit, "expired"); return VisitResult.Fail("sp5.visit.expired", "The customer has already left with the item."); }
            if (visit.OfferAmount <= 0) return VisitResult.Fail("sp5.pawn.no_value", "There is no lendable value in this item as it stands.");
            if (shopLendableCash < visit.OfferAmount) return VisitResult.Fail("sp5.visit.no_cash_pawn", "You do not have enough lendable cash for this loan.");
            var asset = Catalog.GetAsset(state, visit.AssetId);
            if (asset.Status != "active" || asset.OwnerId != visit.CustomerId) return VisitResult.Fail("sp5.visit.item_changed", "The item is no longer available.");
            visit.PawnTermDays = termDays;
            return new VisitResult { Success = true, PawnCommand = new EconomyCommand {
                Kind = EconomyAction.CreatePawn, GameDay = day, AvailableCash = shopLendableCash, Amount = visit.OfferAmount, AssetId = visit.AssetId,
                CollateralValue = visit.CollateralBasis, TermDays = termDays, PlayerIsLender = true, CustomerId = visit.CustomerId, AppraisalRef = visit.VisitId, LoanToValueBps = visit.PawnLtvBps } };
        }

        public static void ConfirmPawn(CatalogState state, string visitId, string contractId)
        {
            var visit = Get(state, visitId);
            if (visit.Stage == "closed") return;
            visit.ContractId = contractId; Close(visit, "pawned");
        }

        // Declining a follow-up never undoes the completed appraisal fee.
        public static void Decline(CatalogState state, string visitId)
        {
            var visit = Get(state, visitId);
            if (visit.Stage != "closed") Close(visit, visit.Stage == "followup" ? "declined" : "left");
        }

        public static void Walkout(CatalogState state, string visitId) { var visit = Get(state, visitId); if (visit.Stage != "closed") Close(visit, "walkout"); }

        public static void ExpireOffers(CatalogState state, int day)
        {
            foreach (var visit in state.Unified.Visits.Where(v => v.Stage != "closed" && day > Math.Max(v.OfferExpiryDay, v.Day))) Close(visit, "expired");
        }

        private static void Close(CustomerVisit visit, string resolution) { visit.Resolution = resolution; visit.Stage = "closed"; }

        // Short contextual dialogue. Different customers answer a finding differently.
        public static string FollowUpLineKey(CustomerVisit visit)
        {
            if (visit.Intent == "offer_sale") return visit.Profile == "dealer" ? "sp5.line.sale.dealer" : visit.SubmittedValue >= 0 && visit.Grade >= 2 ? "sp5.line.sale.surprised" : "sp5.line.sale.plain";
            if (visit.Intent == "request_pawn") return visit.Profile == "sentimental" ? "sp5.line.pawn.sentimental" : "sp5.line.pawn.cash";
            return visit.Profile == "collector" ? "sp5.line.leave.collector" : "sp5.line.leave.plain";
        }

        public static string FindingLineKey(CustomerVisit visit, string component)
        {
            if (visit.Dishonest) return component == "metal_purity" ? "sp5.line.finding.dishonest_material" : "sp5.line.finding.dishonest";
            switch (visit.Profile)
            {
                case "dealer": return "sp5.line.finding.dealer";
                case "collector": return "sp5.line.finding.collector";
                case "urgent_cash": return "sp5.line.finding.urgent";
                default: return component == "metal_purity" ? "sp5.line.finding.truthful_material" : "sp5.line.finding.truthful";
            }
        }
    }
}
