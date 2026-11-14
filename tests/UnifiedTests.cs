// Revision 05 regression suite. Pure business logic against the REAL packaged content.
// It does not load the game, Unity or Harmony; see docs/ENGINEERING_REPORT_05.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using StarPawn.Core;

internal static class UnifiedTests
{
    private static int count;
    private static string root = "";
    private static List<ItemDefinition> defs = null!;
    private static UnifiedRules rules = null!;
    private static string Snap(object v) => JsonConvert.SerializeObject(v);
    private static T Reload<T>(T v) where T : class => JsonConvert.DeserializeObject<T>(Snap(v))!;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T expected, T actual, string message = "value") { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(message + ": expected=" + expected + ", actual=" + actual); }
    private static void Test(string name, Action action)
    {
        Catalog.Configure(defs); InspectionV2.ConfigureRules(new InspectionRules { ExperiencePerDiscovery = 0 }); UnifiedConfig.Configure(Reload(rules));
        action(); count++; Console.WriteLine("PASS " + name);
    }
    private static ItemDefinition D(string id) => Catalog.GetDefinition(id);
    private static CatalogState State(string run = "unified") => Catalog.CreateState(run);
    private static MarketDay Market(CatalogState s, int day = 1, bool events = false) => MarketService.EnsureDay(s, day, 80, 65, 8, 6, events, UnifiedConfig.Rules);
    private static MarketDay With(MarketDay m, params (string key, int bps)[] channels) { var c = Reload(m); foreach (var (k, b) in channels) c.Channels.Add(new ChannelValue { Key = k, DeltaBps = b }); return c; }
    private static readonly string[] Bench = { "jeweler_bench", "jeweler_toolkit" };
    private static readonly string[] Plant = { "recovery_station", "refining_furnace", "process_cartridge", "crucible", "ingot_mold" };
    private static List<Dictionary<string, string>> Csv(string rel)
    {
        var lines = File.ReadAllLines(Path.Combine(root, rel)); var head = lines[0].TrimStart('﻿').Split(',');
        return lines.Skip(1).Where(l => l.Length > 0).Select(l => { var p = l.Split(','); return head.Select((h, i) => (h, v: i < p.Length ? p[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
    }

    public static int Main(string[] args)
    {
        try
        {
            root = args[0];
            var pack = JsonConvert.DeserializeObject<ContentPack>(File.ReadAllText(Path.Combine(root, "mod/Content/items.json")))!;
            var loc = JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(Path.Combine(root, "mod/Content/Localization/zh-CN.json")))!;
            defs = ContentPacks.ValidateAndMerge(new[] { pack }, new InspectionRules { ExperiencePerDiscovery = 0 }, loc, key => File.Exists(Path.Combine(root, "mod/Textures/Paper/" + key.Split(':')[1] + ".png")) && File.Exists(Path.Combine(root, "mod/Textures/Items/" + key.Split(':')[1] + ".png")));
            rules = JsonConvert.DeserializeObject<UnifiedRules>(File.ReadAllText(Path.Combine(root, "mod/Content/unified-rules.json")), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error })!;
            Run();
            Console.WriteLine("UNIFIED TESTS PASSED: " + count + "; real packaged content + core business logic, NOT an in-game or Unity test");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void Run()
    {
        // ---------------------------------------------------------------- footprints and containers
        Test("every runtime definition has an explicit footprint that matches ITEM_LAYOUT_05", () =>
        {
            var layout = File.ReadAllLines(Path.Combine(root, "art/ITEM_LAYOUT_05.csv")).Skip(1).Where(l => l.Length > 0).ToDictionary(l => l.Split(',')[0], l => l);
            var cover = Csv("art/RUNTIME_COVERAGE_05.csv").ToDictionary(r => r["current_id"], r => r);
            var raw = File.ReadAllText(Path.Combine(root, "mod/Content/items.json"));
            Eq(defs.Count, System.Text.RegularExpressions.Regex.Matches(raw, "\"WidthCells\":").Count, "A definition relies on the default 1x1 width");
            Eq(defs.Count, System.Text.RegularExpressions.Regex.Matches(raw, "\"HeightCells\":").Count, "A definition relies on the default 1x1 height");
            foreach (var d in defs)
            {
                Check(cover.ContainsKey(d.Id) && layout.ContainsKey(d.ArtModel), "No manifest row for " + d.Id);
                Eq(d.ArtModel, cover[d.Id]["art_asset_key"], "Coverage map disagrees for " + d.Id);
                Eq(cover[d.Id]["width"] + "x" + cover[d.Id]["height"], d.WidthCells + "x" + d.HeightCells, "Footprint of " + d.Id);
            }
            Check(defs.Where(d => d.IsJadeRough).All(d => d.WidthCells == 3 && d.HeightCells == 2), "Rough jade must be 3x2");
            Check(defs.Where(d => d.IsBook && !string.IsNullOrEmpty(d.ManualFor)).All(d => d.WidthCells == 2 && d.HeightCells == 3), "Manuals must be 2x3");
            Check(defs.Where(d => d.Form == "bracelet").All(d => d.WidthCells == 2 && d.HeightCells == 2) && defs.Any(d => d.Form == "bracelet"), "Bracelets must be 2x2");
            Check(defs.Where(d => d.Form == "necklace").All(d => d.WidthCells == 1 && d.HeightCells == 3) && defs.Any(d => d.Form == "necklace"), "Necklaces must be 1x3");
        });
        Test("containers really fit what they claim", () =>
        {
            void Fits(string id, int w, int h, int n, int iw, int ih)
            {
                var d = D(id); Eq(iw + "x" + ih, d.InternalWidthCells + "x" + d.InternalHeightCells, id + " interior");
                var plan = LayoutMigration.Plan(iw, ih, Enumerable.Range(0, n).Select(i => new PlacedItem { Id = "i" + i, X = 99, Y = 99, W = 1, H = 1, NewW = w, NewH = h }));
                Eq(0, plan.Overflow.Count, id + " cannot hold " + n + " items of " + w + "x" + h);
            }
            Fits("sp3_manual_organizer", 2, 3, 8, 8, 6); Fits("sp5_manual_organizer_large", 2, 3, 12, 8, 9); Fits("sp5_card_holder", 2, 1, 2, 4, 1);
            Fits("sp5_presentation_case", 1, 3, 4, 4, 3); Fits("sp5_gem_case", 1, 1, 16, 4, 4); Fits("sp5_jewelry_display", 1, 3, 6, 6, 4);
            Check(LayoutMigration.Plan(8, 6, Enumerable.Range(0, 9).Select(i => new PlacedItem { Id = "i" + i, X = 99, Y = 99, NewW = 2, NewH = 3 })).Overflow.Count == 1, "A ninth book must not fit an eight-book organizer");
        });

        // ---------------------------------------------------------------- inventory hit geometry (separate from defect hotspots)
        Test("selection covers every occupied cell, including transparent art, and nothing outside", () =>
        {
            foreach (var (w, h) in new[] { (1, 1), (3, 2), (2, 3), (2, 2), (1, 3) })
                foreach (var cell in new[] { 64f, 37.5f, 96f })
                    for (var turns = 0; turns < 4; turns++)
                        foreach (var flip in new[] { false, true })
                        {
                            var cells = FootprintGeometry.Filled(w, h); int rw = turns % 2 == 0 ? w : h, rh = turns % 2 == 0 ? h : w;
                            for (var cy = 0; cy < rh; cy++) for (var cx = 0; cx < rw; cx++)
                                foreach (var (fx, fy) in new[] { (.5f, .5f), (.02f, .02f), (.98f, .02f), (.02f, .98f), (.98f, .98f), (.5f, .01f), (.01f, .5f) })
                                    Check(FootprintGeometry.Contains(cells, w, h, turns, flip, cell, (cx + fx) * cell, (cy + fy) * cell), "Occupied cell not selectable " + w + "x" + h);
                            foreach (var (x, y) in new[] { (-0.5f, 1f), (1f, -0.5f), (rw * cell + 0.5f, 1f), (1f, rh * cell + 0.5f), (rw * cell, rh * cell) })
                                Check(!FootprintGeometry.Contains(cells, w, h, turns, flip, cell, x, y), "Point outside the footprint selected the item");
                            Eq(w * h, FootprintGeometry.CellRects(cells, w, h, turns, flip, cell).Count, "Hit plate count");
                        }
        });
        Test("a non-rectangular shape uses only its filled cells at every rotation and flip", () =>
        {
            var l = new[] { true, false, true, false, true, true }; // 2 wide x 3 high "L"
            for (var turns = 0; turns < 4; turns++) foreach (var flip in new[] { false, true })
            {
                var rects = FootprintGeometry.CellRects(l, 2, 3, turns, flip, 64); Eq(4, rects.Count, "L shape cell count");
                int rw = turns % 2 == 0 ? 2 : 3, rh = turns % 2 == 0 ? 3 : 2, hit = 0;
                for (var y = 0; y < rh; y++) for (var x = 0; x < rw; x++) if (FootprintGeometry.Contains(l, 2, 3, turns, flip, 64, x * 64 + 32, y * 64 + 32)) hit++;
                Eq(4, hit, "Empty bounding-box cells must not capture the pointer");
            }
            Check(!FootprintGeometry.Contains(l, 2, 3, 0, false, 64, 96, 32) && FootprintGeometry.Contains(l, 2, 3, 0, true, 64, 96, 32), "Flip must mirror the occupied cells");
        });
        Test("a click on the whole item does not reveal a defect: discovery still needs the authored hotspot", () =>
        {
            var s = State(); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p3", "customer"); var e = InspectionV2.Open(s, a.AssetId, "e", "customer");
            Eq("miss", InspectionV2.ClickAt(s, "e", .05f, .05f).Kind, "A corner of the canvas found a defect");
            var h = D(a.DefinitionId).Hotspots[0];
            Eq("discovered", InspectionV2.ClickAt(s, "e", h.U, h.V).Kind, "The authored hotspot centre must still work");
            Eq("miss", InspectionV2.ClickAt(s, "e", h.U + h.Width, h.V + h.Height).Kind, "Hotspot bounds were enlarged");
        });

        // ---------------------------------------------------------------- discovery feedback and misses
        Test("first correct discovery plays feedback once; repeats, reloads and other views never repeat it", () =>
        {
            var s = State(); var a = Catalog.CreateAsset(s, "jade_rough_cracked", "customer", 77); a.WindowOpened = true;
            InspectionV2.Open(s, a.AssetId, "e", "customer");
            var first = InspectionV2.Discover(s, "e", "rough_crack"); Check(first.Kind == "discovered" && first.PlayFeedback, "No feedback on first finding");
            var again = InspectionV2.Discover(s, "e", "rough_crack"); Check(again.Kind == "repeated" && !again.PlayFeedback, "Repeat played feedback");
            s = Reload(s); Catalog.Validate(s);
            Check(!InspectionV2.Discover(s, "e", "rough_crack").PlayFeedback, "Reload replayed feedback");
            Eq(0, s.Experience, "A clue awarded independent experience");
            InspectionV2.Open(s, a.AssetId, "e", "customer", "window");
            Check(InspectionV2.Discover(s, "e", "window_crack").PlayFeedback, "A different observation in another view is a new finding");
            var q = ValuationV2.Quote(s, "e", 1, 1, 80, 65, 8, 6); Eq(1, q.Lines.Count, "Two views of one crack must be one economic cause"); Eq(65L, q.EvidenceDeduction, "Shared cause deducted twice");
        });
        Test("two consecutive misses end claims; a new finding resets, an old one does not; persisted across reload", () =>
        {
            var s = State(); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p3", "customer"); var e = InspectionV2.Open(s, a.AssetId, "e", "customer");
            InspectionV2.Miss(s, "e"); InspectionV2.Discover(s, "e", "metal_wear"); Eq(0, e.ConsecutiveMisses, "A new finding must reset the streak");
            InspectionV2.Miss(s, "e"); InspectionV2.Discover(s, "e", "metal_wear"); Eq(1, e.ConsecutiveMisses, "A repeated finding must not reset the streak");
            s = Reload(s); Catalog.Validate(s); Eq(1, InspectionV2.GetEncounter(s, "e").ConsecutiveMisses, "Streak lost on reload");
            var locked = InspectionV2.Miss(s, "e"); Check(locked.Locked && locked.Kind == "locked", "Second miss must end claims");
            Eq("locked", InspectionV2.Discover(s, "e", "metal_split").Kind, "A locked encounter accepted a claim");
            Check(Reload(s).Encounters[0].Locked, "Lock lost on reload");
        });
        Test("studying your own stock never impersonates a customer walking out", () =>
        {
            var s = State(); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p3"); var e = InspectionV2.Open(s, a.AssetId, "study", "player");
            for (var i = 0; i < 5; i++) Eq("study_miss", InspectionV2.Miss(s, "study").Kind, "Study miss");
            Check(!e.Locked && e.ConsecutiveMisses == 0, "Personal study was locked");
            Check(InspectionV2.Discover(s, "study", "metal_wear").PlayFeedback, "Study can still find a defect");
        });
        Test("evidence toggles are inert: confirmed findings always apply", () =>
        {
            var s = State(); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p3", "customer"); InspectionV2.Open(s, a.AssetId, "e", "customer");
            InspectionV2.Discover(s, "e", "metal_wear"); var before = ValuationV2.Quote(s, "e", 1, 1, 80, 65, 8, 6).Price;
#pragma warning disable CS0618
            InspectionV2.SelectEvidence(s, "e", "metal_wear", false); InspectionV2.SelectAll(s, "e");
#pragma warning restore CS0618
            Eq(before, ValuationV2.Quote(s, "e", 1, 1, 80, 65, 8, 6).Price, "A toggle restored value");
        });

        // ---------------------------------------------------------------- component valuation
        Test("cosmetic damage reduces workmanship only and can never consume the metal value", () =>
        {
            var s = State(); var m = Market(s);
            var clean = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0", "customer"); var worn = Catalog.CreateAsset(s, "sp3_gold_ring_0_p3", "customer");
            var q0 = ComponentValuation.Quote(s, clean, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            worn.DiscoveredEvidenceIds.AddRange(new[] { "metal_wear", "metal_split" });
            var q = ComponentValuation.Quote(s, worn, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Eq(ValuationV2.MetalValue(6000, 9990, 80), q.MetalValue, "Metal value"); Eq(q0.MetalValue, q.MetalValue, "Scratches changed the gold value");
            Eq(155L, q.CraftBasis, "Workmanship basis"); Eq(38L + 77L, q.CraftDeduction, "Workmanship deduction"); Eq(155L - 115L, q.CraftValue, "Remaining workmanship");
            Eq(q.MetalValue + q.CraftValue, q.Total, "Total is metal + remaining workmanship"); Check(q.Lines.All(l => l.Component == "workmanship"), "Cosmetic line charged to another component");
            // Heavier damage than the workmanship is worth still leaves all of the metal.
            var d = Reload(D("sp3_gold_ring_0_p3")); foreach (var h in d.Hotspots) h.ValueDeduction = 5000;
            Catalog.Configure(defs.Select(x => x.Id == d.Id ? d : x));
            var capped = ComponentValuation.Quote(s, worn, m, ComponentValuation.MaterialBuyback, UnifiedConfig.Rules);
            Eq(0L, capped.CraftValue, "Workmanship cannot go negative"); Eq(ValuationV2.MetalValue(6000, 9990, 65), capped.Total, "Clean confirmed gold fell below its buyback value");
        });
        Test("the handoff example: 600 gold + 200 workmanship, a 40 scratch leaves 600 + 160 = 760", () =>
        {
            var d = Reload(D("sp3_gold_ring_0_p1")); d.WeightMilligrams = 7500; d.DeclaredPurityBasisPoints = d.ActualPurityBasisPoints = 10000; d.CraftValue = 200; d.Hotspots[0].ValueDeduction = 40;
            Catalog.Configure(defs.Select(x => x.Id == d.Id ? d : x));
            var s = State(); var m = Market(s); var a = Catalog.CreateAsset(s, d.Id, "customer"); InspectionV2.Open(s, a.AssetId, "e", "customer");
            Eq(800L, ValuationV2.Quote(s, "e", 1, m.Version, 80, 65, 8, 6).Price, "Before the finding");
            InspectionV2.Discover(s, "e", "metal_wear");
            var q = ValuationV2.Quote(s, "e", 1, m.Version, 80, 65, 8, 6);
            Eq(600L, q.MaterialValue, "Gold"); Eq(160L, q.CraftValue, "Workmanship"); Eq(760L, q.Price, "Estimate");
        });
        Test("unknown purity is not leaked; a justified finding revalues the metal component", () =>
        {
            var s = State(); var m = Market(s);
            var fake = Catalog.CreateAsset(s, "gold_bracelet_plated", "customer"); var real = Catalog.CreateAsset(s, "gold_bracelet", "customer");
            foreach (var context in new[] { ComponentValuation.BuyFromCustomer, ComponentValuation.MaterialBuyback, ComponentValuation.PawnCollateral })
                Eq(ComponentValuation.Quote(s, real, m, context, UnifiedConfig.Rules).Total, ComponentValuation.Quote(s, fake, m, context, UnifiedConfig.Rules).Total, "Hidden purity leaked through " + context);
            InspectionV2.Open(s, fake.AssetId, "e", "customer");
            var q = ValuationV2.Quote(s, "e", 1, m.Version, 80, 65, 8, 6); Check(!q.RecoveryValueConfirmed && q.MaterialDescription.Contains("not tested"), "Untested purity presented as fact");
            Check(ComponentValuation.Quote(s, fake, m, ComponentValuation.TrueValue, UnifiedConfig.Rules).Total < q.Price, "The private answer must know the truth");
            InspectionV2.Discover(s, "e", "plating_edge"); InspectionV2.Discover(s, "e", "plating_seam");
            var after = ValuationV2.Quote(s, "e", 1, m.Version, 80, 65, 8, 6);
            Eq(0L, after.MaterialValue, "Plated body kept a gold value"); Eq(0L, after.RecoveryValue, "Recovery value survived"); Check(after.PurityConfirmed, "Finding did not establish material");
            Check(after.AppliedCauses.Count(c => c == "base_material_mismatch") == 1, "Two plating clues are one cause");
        });
        Test("a missing link reduces actual metal mass at unchanged purity", () =>
        {
            var d = Reload(D("sp3_gold_ring_0_p1")); d.Hotspots[0].Component = "metal_mass"; d.Hotspots[0].MassLossMilligrams = 500; d.Hotspots[0].ValueDeduction = 0;
            Catalog.Configure(defs.Select(x => x.Id == d.Id ? d : x));
            var s = State(); var m = Market(s); var a = Catalog.CreateAsset(s, d.Id, "customer"); a.DiscoveredEvidenceIds.Add("metal_wear");
            var q = ComponentValuation.Quote(s, a, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Eq(5500L, q.EligibleMilligrams, "Eligible mass"); Eq(9990, q.PurityBasisPoints, "Purity changed"); Eq(155L, q.CraftValue, "Workmanship charged for missing metal");
            Eq(ValuationV2.MetalValue(5500, 9990, 80), q.MetalValue, "Metal value of the remaining mass");
        });

        // ---------------------------------------------------------------- market, labels and events
        Test("the daily market is saved once: reopening, reloading and retrying cannot reroll it", () =>
        {
            var s = State("market"); var seen = new List<string>();
            for (var day = 0; day < 120; day++) { var m = Market(s, day, true); Check(m.EventIds.Count <= 3, "More than three concurrent events"); seen.Add(Snap(m)); Eq(Snap(m), Snap(Market(s, day, true)), "Same day rerolled"); }
            var copy = Reload(s); Eq(seen[119], Snap(Market(copy, 119, true)), "Reload rerolled the market");
            Check(seen.Any(x => x.Contains("gold_shipment") || x.Contains("safe_haven") || x.Contains("credit_squeeze") || x.Contains("jade_festival")), "No event ever started");
            var history = new Dictionary<string, int>();
            foreach (var m in s.Unified.MarketDays) foreach (var id in m.EventIds) history[id] = m.Day;
            var quiet = State("quiet"); for (var day = 0; day < 60; day++) Eq(0, Market(quiet, day, false).EventIds.Count, "An opted-out save received an event");
            try { Market(s, 3, true); } catch (InvalidOperationException) { return; }
            Check(s.Unified.MarketDays.Any(m => m.Day == 3) , "A trimmed past day must not be regenerated");
        });
        Test("a gold shock moves gold through the metal component only; luxury demand moves workmanship only", () =>
        {
            var s = State(); var m = Market(s);
            var ring = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0"); var silver = Catalog.CreateAsset(s, "sp3_silver_ring_0_p0"); var bar = Catalog.CreateAsset(s, "sp3_gold_bar_0_p0");
            var baseQ = ComponentValuation.Quote(s, ring, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            var shock = MarketService.EnsureDay(State("shock"), 1, 80, 65, 8, 6, false, UnifiedConfig.Rules); shock.GoldSell = 68; shock.GoldBuy = 55; // -15% as produced by gold_shipment
            var q = ComponentValuation.Quote(s, ring, shock, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Check(q.MetalValue < baseQ.MetalValue, "Gold jewelry ignored the gold price"); Eq(baseQ.CraftValue, q.CraftValue, "A gold shock changed workmanship");
            Eq(ComponentValuation.Quote(s, silver, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules).Total, ComponentValuation.Quote(s, silver, shock, ComponentValuation.SellToCustomer, UnifiedConfig.Rules).Total, "Silver moved with gold");
            var demand = With(m, ("category_demand:precious_metal_goods", 1000), ("category_demand:gold_goods", 800));
            var d = ComponentValuation.Quote(s, ring, demand, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Eq(baseQ.MetalValue, d.MetalValue, "Luxury demand multiplied gold mass"); Eq(ComponentValuation.Scale(155, 11800), d.CraftBasis, "Two matching rows must apply once each, summed");
            Eq(ComponentValuation.Quote(s, bar, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules).Total, ComponentValuation.Quote(s, bar, demand, ComponentValuation.SellToCustomer, UnifiedConfig.Rules).Total, "Bullion gained a jewelry premium");
        });
        Test("event rows from the real rules reach the saved market channels", () =>
        {
            var s = State("rows"); s.Unified.WorldEvents.Add(new ActiveWorldEvent { EventId = "gold_shipment", StartDay = 5, EndDay = 6 }); s.Unified.WorldEvents.Add(new ActiveWorldEvent { EventId = "credit_squeeze", StartDay = 5, EndDay = 6 });
            var m = Market(s, 5); Eq(ComponentValuation.Scale(80, 8500), m.GoldSell, "gold_shipment must lower the shared gold quote by 15%"); Eq(8L, m.SilverSell, "Silver is untouched");
            Eq(-1000, m.Delta("new_pawn_ltv", "new_agreements"), "Credit squeeze LTV row"); Eq(1500, m.Delta("intent_shift", "request_pawn"), "Credit squeeze intent shift");
            Eq(80L, Market(s, 7).GoldSell, "Effect must end with the event");
        });
        Test("business labels are explicit, filterable and never inferred from names", () =>
        {
            Check(BusinessLabels.Has(D("sp3_gold_ring_0_p0"), BusinessLabels.Luxury), "Gold jewelry is Luxury");
            Check(BusinessLabels.Has(D("sp3_gold_bar_0_p0"), BusinessLabels.Materials) && BusinessLabels.Has(D("sp3_gold_bar_0_p0"), BusinessLabels.Investment) && !BusinessLabels.Has(D("sp3_gold_bar_0_p0"), BusinessLabels.Luxury), "Bullion is Materials + Investment");
            Check(BusinessLabels.Has(D("jade_rough"), BusinessLabels.Materials) && BusinessLabels.Has(D("sp5_scrap_board_p0"), BusinessLabels.Electronics) && BusinessLabels.Has(D("sp5_scrap_board_p0"), BusinessLabels.Materials), "Materials mapping");
            Check(BusinessLabels.Has(D("painting"), BusinessLabels.Collectibles) && BusinessLabels.Has(D("console"), BusinessLabels.Electronics) && BusinessLabels.Has(D("cartridge"), BusinessLabels.Entertainment), "Collectible/Electronics/Entertainment mapping");
            Check(BusinessLabels.ForNative("hand").SequenceEqual(new[] { "Weapons", "Augments" }) && BusinessLabels.ForNative("hand", true).Contains("Collectibles"), "Native hands: Weapons + Augments (+ Collectibles when rare)");
            Check(BusinessLabels.ForNative("plasma_fuel").SequenceEqual(new[] { "Materials", "Energy" }) && BusinessLabels.ForNative("golden hand").Count == 0 && BusinessLabels.ForNative("ribwich").Count == 0, "No label from a name");
            foreach (var label in BusinessLabels.Filterable.Where(l => l != "Weapons")) Check(BusinessLabels.Filter(defs, label).Any(), "Empty filter " + label);
            Check(defs.Where(d => d.ShopOnly && d.Role != "energy_cell").All(d => d.Labels.Count == 0), "Tools and manuals are not merchandise categories");
            Check(D("gold_bracelet_plated").Labels.SequenceEqual(D("gold_bracelet").Labels), "A label must not reveal plating");
        });

        // ---------------------------------------------------------------- native career and commissions
        Test("commission eligibility follows the native rank schedule without touching trade", () =>
        {
            Check(Career.CanCommission(D("sp3_gold_ring_0_p0"), 0) && Career.CanCommission(D("sp3_silver_bar_0_p0"), 0), "Rank 0: ordinary gold and silver");
            Check(!Career.CanCommission(D("sp3_jade_pendant_p0"), 0) && Career.CanCommission(D("sp3_jade_pendant_p0"), 1), "Rank 1: finished jade");
            Check(!Career.CanCommission(D("jade_rough"), 1) && Career.CanCommission(D("jade_rough"), 2) && Career.CanCommission(D("sp5_gem_ruby_oval_nat_p0"), 2) && Career.CanCommission(D("sp5_scrap_board_p0"), 2), "Rank 2: rough, loose gems, recovery");
            Check(!Career.CanCommission(D("sp5_jewelry_ring_au18k_p1"), 2) && Career.CanCommission(D("sp5_jewelry_ring_au18k_p1"), 3), "Rank 3: mounted jewelry");
            Check(defs.Where(d => d.ShopOnly).All(d => !Career.CanCommission(d, 3)), "Tools and manuals are never commissions");
            var s = State(); var m = Market(s); var a = Catalog.CreateAsset(s, "jade_rough", "customer", 5);
            try { VisitService.Open(s, a.AssetId, "customer", "appraisal", "ordinary", false, m, 1, UnifiedConfig.Rules); throw new Exception("Low rank accepted a rough-jade commission"); } catch (InvalidOperationException) { }
            Check(VisitService.Open(s, a.AssetId, "customer", "sale", "ordinary", false, m, 0, UnifiedConfig.Rules).Path == "sale", "Direct trade must not need appraiser rank");
        });
        Test("a commission settles once at the saved market; accuracy bands and the fee cap hold", () =>
        {
            Eq(3, Career.Grade(100, 100)); Eq(3, Career.Grade(95, 100)); Eq(3, Career.Grade(105, 100)); Eq(2, Career.Grade(94, 100)); Eq(2, Career.Grade(115, 100)); Eq(1, Career.Grade(70, 100)); Eq(0, Career.Grade(69, 100)); Eq(0, Career.Grade(131, 100));
            var s = State(); var m = Market(s); var a = Catalog.CreateAsset(s, "sp3_gold_bar_0_p0", "customer");
            var v = VisitService.Open(s, a.AssetId, "customer", "appraisal", "ordinary", false, m, 0, UnifiedConfig.Rules);
            Eq(v.VisitId, VisitService.Open(s, a.AssetId, "customer", "appraisal", "dealer", true, Market(s), 0, UnifiedConfig.Rules).VisitId, "Reopening created a second commission");
            Eq(v.CorrectValue, VisitService.CorrectValue(s, a.AssetId), "Native callback answer");
            var r = VisitService.Submit(s, v.VisitId, v.CorrectValue, UnifiedConfig.Rules); Check(r.Success && r.CashDelta > 0 && r.CashDelta <= UnifiedConfig.Rules.CommissionFeeCap, "Fee missing or above the cap");
            Check(v.CorrectValue > 2000 && r.CashDelta == UnifiedConfig.Rules.CommissionFeeCap, "A high-value bar must hit the bounded fee, not a raw percentage");
            s = Reload(s); var again = VisitService.Submit(s, v.VisitId, 1, UnifiedConfig.Rules); Check(again.Success && again.CashDelta == 0, "Second submission paid again"); Eq(3, VisitService.Get(s, v.VisitId).Grade, "Saved grade was overwritten");
            // A later market move cannot invalidate the submitted answer.
            var before = VisitService.CorrectValue(s, a.AssetId); MarketService.EnsureDay(s, 2, 120, 100, 8, 6, false, UnifiedConfig.Rules); Eq(before, VisitService.CorrectValue(s, a.AssetId), "Commission answer drifted with the market");
            var bad = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0", "c2"); var v2 = VisitService.Open(s, bad.AssetId, "c2", "appraisal", "ordinary", false, Market(s, 2), 0, UnifiedConfig.Rules);
            var refused = VisitService.Submit(s, v2.VisitId, 1, UnifiedConfig.Rules); Check(refused.CashDelta == 0 && VisitService.Get(s, v2.VisitId).Resolution == "refused", "A wild estimate earned a fee");
        });
        Test("promotion handbooks are granted once per rank, never on reload, never duplicated", () =>
        {
            var s = State(); var first = Career.ClaimEntitlements(s, 0, new string[0]);
            Check(first.Count == 10 && first.All(id => D(id).ManualFor == "metals"), "Rank 0 grants the ten metal volumes");
            Eq(0, Career.ClaimEntitlements(Reload(s), 0, new string[0]).Count, "Reload granted books again");
            var s2 = Reload(s); var third = Career.ClaimEntitlements(s2, 3, new[] { "sp31_manual_jade_pendant" });
            Check(!third.Contains("sp31_manual_jade_pendant") && third.Contains("sp31_manual_rough_sand") && third.Contains("sp5_manual_mounts_a") && third.All(id => D(id).ManualFor != "metals"), "A higher-rank save receives only its missing entitlements once");
            Eq(0, Career.ClaimEntitlements(s2, 3, new string[0]).Count, "Entitlements repeated");
            s.Experience = 70; Career.MigrateLegacyExperience(s); s.Experience = 0; Career.MigrateLegacyExperience(s); Eq(70, s.Unified.LegacyExperience, "Legacy XP must be kept as history exactly once");
        });

        // ---------------------------------------------------------------- appraisal -> sale / pawn / leave
        Test("customer intent is one saved draw: reopening and reloading cannot change it", () =>
        {
            var s = State("intent"); var m = Market(s); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0", "customer");
            var v = VisitService.Open(s, a.AssetId, "customer", "appraisal", "urgent_cash", false, m, 0, UnifiedConfig.Rules);
            Check(VisitService.ResolveIntent(s, v.VisitId, m, true, 99999, UnifiedConfig.Rules).Draw < 0, "Intent revealed before settlement");
            VisitService.Submit(s, v.VisitId, v.CorrectValue, UnifiedConfig.Rules);
            var before = Snap(Reload(s));                                      // reload BEFORE the draw
            var drawn = Snap(VisitService.ResolveIntent(s, v.VisitId, m, true, 99999, UnifiedConfig.Rules));
            var other = JsonConvert.DeserializeObject<CatalogState>(before)!; Eq(drawn, Snap(VisitService.ResolveIntent(other, v.VisitId, Market(other), true, 99999, UnifiedConfig.Rules)), "Reload before the draw changed the outcome");
            var after = Reload(s);                                             // reload AFTER the draw
            Eq(drawn, Snap(VisitService.ResolveIntent(after, v.VisitId, Market(after), false, 0, UnifiedConfig.Rules)), "Reload after the draw re-rolled or re-priced it");
            Eq(100, v.WeightLeave + v.WeightSale + v.WeightPawn, "Weights not normalised once");
        });
        Test("intent weights follow the profiles; ineligible pawn moves to leave; a credit squeeze shifts toward pawn", () =>
        {
            int[] Tally(string profile, bool licensed, MarketDay? forced = null)
            {
                var t = new int[3];
                for (var i = 0; i < 1500; i++)
                {
                    var s = State("p" + profile + i); var m = forced ?? Market(s); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0", "c");
                    var v = VisitService.Open(s, a.AssetId, "c", "appraisal", profile, false, m, 0, UnifiedConfig.Rules); VisitService.Submit(s, v.VisitId, v.CorrectValue, UnifiedConfig.Rules);
                    VisitService.ResolveIntent(s, v.VisitId, m, licensed, 99999, UnifiedConfig.Rules); t[v.Intent == "leave" ? 0 : v.Intent == "offer_sale" ? 1 : 2]++;
                }
                return t;
            }
            foreach (var p in UnifiedConfig.Rules.IntentProfiles)
            {
                var t = Tally(p.Id, true);
                Check(Math.Abs(t[0] / 15.0 - p.Leave) < 5 && Math.Abs(t[1] / 15.0 - p.Sale) < 5 && Math.Abs(t[2] / 15.0 - p.Pawn) < 5, "Distribution for " + p.Id + ": " + string.Join("/", t));
            }
            var unlicensed = Tally("urgent_cash", false); Eq(0, unlicensed[2], "Pawn offered without a pawn business"); Check(Math.Abs(unlicensed[0] / 15.0 - 65) < 5, "Ineligible pawn weight must move to leave, not be rerolled");
            var squeeze = With(MarketService.EnsureDay(State("sq"), 1, 80, 65, 8, 6, false, UnifiedConfig.Rules), ("intent_shift:request_pawn", 1500));
            var shifted = Tally("ordinary", true, squeeze); Check(Math.Abs(shifted[2] / 15.0 - 35) < 5 && Math.Abs(shifted[0] / 15.0 - 35) < 5, "Credit squeeze must move 15 points from leave to pawn: " + string.Join("/", shifted));
        });
        CustomerVisit Followup(CatalogState s, string wanted, string def = "sp3_gold_ring_0_p3", bool assay = true)
        {
            for (var i = 0; i < 400; i++)
            {
                var customer = "cust-" + i; var a = Catalog.CreateAsset(s, def, customer); if (assay) a.Measurements.Add("assay");
                var m = Market(s); var v = VisitService.Open(s, a.AssetId, customer, "appraisal", wanted == "request_pawn" ? "urgent_cash" : "dealer", false, m, 0, UnifiedConfig.Rules);
                VisitService.Submit(s, v.VisitId, v.CorrectValue, UnifiedConfig.Rules); VisitService.ResolveIntent(s, v.VisitId, m, true, 99999, UnifiedConfig.Rules);
                if (v.Intent == wanted) return v;
            }
            throw new Exception("No " + wanted + " intent in 400 visits");
        }
        Test("a sale follow-up transfers the SAME item; refusal, low cash and expiry are safe", () =>
        {
            var s = State("sale"); var v = Followup(s, "offer_sale"); var assets = s.Assets.Count; var a = Catalog.GetAsset(s, v.AssetId);
            InspectionV2.Open(s, a.AssetId, v.EncounterId, v.CustomerId); InspectionV2.Discover(s, v.EncounterId, "metal_wear");
            var old = v.OfferAmount; VisitService.Reprice(s, v, Market(s), UnifiedConfig.Rules); Check(v.OfferAmount < old, "The follow-up price ignored a recorded finding");
            var poor = VisitService.AcceptSale(s, v.VisitId, 1, v.OfferAmount - 1); Check(!poor.Success && poor.ErrorKey == "sp5.visit.no_cash" && v.Stage == "followup" && a.OwnerId == v.CustomerId, "Insufficient cash changed state");
            var ok = VisitService.AcceptSale(s, v.VisitId, 1, v.OfferAmount); Check(ok.Success && ok.CashDelta == -v.OfferAmount, "Purchase payment");
            Eq("player", a.OwnerId, "Ownership"); Eq(assets, s.Assets.Count, "A copy was spawned for the follow-up"); Check(a.DiscoveredEvidenceIds.Contains("metal_wear"), "Observations lost");
            Check(!VisitService.AcceptSale(s, v.VisitId, 1, 99999).Success, "The same offer was accepted twice");
            var d = Followup(s, "offer_sale"); var fee = d.Fee; VisitService.Decline(s, d.VisitId); Check(d.Resolution == "declined" && d.FeePaid && d.Fee == fee && Catalog.GetAsset(s, d.AssetId).OwnerId == d.CustomerId, "Declining undid the appraisal or moved the item");
            var late = Followup(s, "offer_sale"); var gone = VisitService.AcceptSale(s, late.VisitId, late.OfferExpiryDay + 1, 99999); Check(!gone.Success && late.Resolution == "expired", "An expired offer was honoured");
        });
        Test("a pawn follow-up signs one contract on the same item through the ledger", () =>
        {
            var s = State("pawn"); var eco = EconomyService.CreateNewState(); var er = new EconomyRules { PawnLicenseCost = 0 }; long cash = 50000; var seq = 0;
            EconomyResult Do(EconomyCommand c) { c.AvailableCash = cash; c.Sequence = eco.LastCommandSequence + 1; c.TransactionId = "t" + (++seq); var r = EconomyService.Apply(eco, c, er); if (r.Success) cash += r.CashDelta; return r; }
            Check(Do(new EconomyCommand { Kind = EconomyAction.AcquirePawnLicense, GameDay = 1 }).Success, "License");
            var v = Followup(s, "request_pawn"); Eq(5000, v.PawnLtvBps, "New offers start at 50% LTV"); Eq(ComponentValuation.Scale(v.CollateralBasis, 5000), v.OfferAmount, "Principal = basis x LTV"); Eq("", v.BlockedReasonKey, "Assayed gold needs no further test");
            Check(!VisitService.PreparePawn(s, v.VisitId, 1, v.OfferAmount - 1, 7).Success, "Lent more than lendable cash");
            var prep = VisitService.PreparePawn(s, v.VisitId, 1, cash, 7); Check(prep.Success && prep.PawnCommand != null, "Prepare"); var before = cash;
            var signed = Do(prep.PawnCommand!); Check(signed.Success, signed.Error); Eq(before - v.OfferAmount, cash, "Principal debited once");
            VisitService.ConfirmPawn(s, v.VisitId, signed.EntityId); var contract = eco.PawnContracts.Single();
            Check(contract.CustomerId == v.CustomerId && contract.AppraisalRef == v.VisitId && contract.LoanToValueBps == 5000 && contract.AssetId == v.AssetId && contract.PlayerIsLender, "Contract record incomplete");
            Check(signed.AssetTransitions.Single().Kind == "EscrowFromCustomer" && EconomyService.IsAssetInEscrow(eco, v.AssetId), "Collateral not reserved");
            var twice = Do(new EconomyCommand { Kind = EconomyAction.CreatePawn, GameDay = 1, Amount = 10, AssetId = v.AssetId, CollateralValue = 100 }); Check(!twice.Success, "The same collateral was pledged twice");
            Check(!VisitService.PreparePawn(s, v.VisitId, 1, cash, 7).Success, "A closed visit produced a second loan");
            // Renewal is an amendment of the same contract.
            var renew = Do(new EconomyCommand { Kind = EconomyAction.RenewPawn, GameDay = 3, ContractId = contract.Id, TermDays = 7 }); Check(renew.Success, renew.Error);
            Eq(1, eco.PawnContracts.Count, "Renewal created a second contract"); Eq(1, eco.PawnContracts[0].Amendments.Count, "Amendment not recorded"); var principal = eco.PawnContracts[0].Principal; Eq(v.OfferAmount, principal, "Renewal changed the signed principal");
            // Redemption through the deadline day; only one terminal action wins.
            var due = eco.PawnContracts[0].DueDay; EconomyService.SettleDay(eco, due - 1, er);
            var redeem = Do(new EconomyCommand { Kind = EconomyAction.RedeemPawn, GameDay = due, ContractId = contract.Id }); Check(redeem.Success && redeem.CashDelta >= principal && redeem.AssetTransitions.Single().Kind == "ReturnToCustomer", "Redemption on the deadline day");
            var settle = EconomyService.SettleDay(eco, due, er); Check(settle.AssetTransitions.Count == 0 && eco.PawnContracts[0].Status == "Redeemed", "A redeemed contract also defaulted");
            Check(!Do(new EconomyCommand { Kind = EconomyAction.RedeemPawn, GameDay = due, ContractId = contract.Id }).Success, "Duplicate redemption payout");
        });
        Test("default forfeits once at the closing boundary; signed terms ignore later market and rule changes", () =>
        {
            var s = State("default"); var eco = EconomyService.CreateNewState(); var er = new EconomyRules { PawnLicenseCost = 0 }; long cash = 50000; var seq = 0;
            EconomyResult Do(EconomyCommand c) { c.AvailableCash = cash; c.Sequence = eco.LastCommandSequence + 1; c.TransactionId = "t" + (++seq); var r = EconomyService.Apply(eco, c, er); if (r.Success) cash += r.CashDelta; return r; }
            Do(new EconomyCommand { Kind = EconomyAction.AcquirePawnLicense, GameDay = 1 });
            var v = Followup(s, "request_pawn"); var signed = Do(VisitService.PreparePawn(s, v.VisitId, 1, cash, 3).PawnCommand!); VisitService.ConfirmPawn(s, v.VisitId, signed.EntityId);
            var c0 = Snap(eco.PawnContracts[0]); er.PawnInterestBps = 500; er.PawnLoanToValueBps = 3000; MarketService.EnsureDay(s, 2, 30, 20, 8, 6, false, UnifiedConfig.Rules);
            var c1 = eco.PawnContracts[0]; Check(Snap(c1).Contains("\"Principal\":" + v.OfferAmount) && c1.DailyInterestBps == 60 && c1.LoanToValueBps == 5000, "A signed contract was repriced: " + c0);
            var forfeits = 0; for (var day = 1; day <= c1.DueDay + 2; day++) forfeits += EconomyService.SettleDay(eco, day, er).AssetTransitions.Count(t => t.Kind == "ForfeitToPlayer");
            Eq(1, forfeits, "Forfeiture must transfer ownership exactly once"); Eq("Forfeited", eco.PawnContracts[0].Status, "Status");
            Check(!Do(new EconomyCommand { Kind = EconomyAction.RedeemPawn, GameDay = c1.DueDay + 2, ContractId = c1.Id }).Success, "Redeemed after default");
        });
        Test("unverified gold cannot secure its claimed content; a credit squeeze only tightens new offers", () =>
        {
            var s = State("unverified"); var known = Followup(s, "request_pawn", "sp3_gold_bar_0_p0", true); var unknown = Followup(s, "request_pawn", "sp3_gold_bar_0_p0", false);
            Eq("sp5.pawn.needs_assay", unknown.BlockedReasonKey, "The lower offer must name the missing test"); Check(unknown.OfferAmount < known.OfferAmount && unknown.OfferAmount > 0, "Unverified collateral lent at the verified basis");
            var m = With(Market(s), ("new_pawn_ltv:new_agreements", -1000)); var old = known.PawnLtvBps; VisitService.Reprice(s, unknown, m, UnifiedConfig.Rules);
            Eq(4000, unknown.PawnLtvBps, "Credit squeeze: ten points more conservative"); Eq(old, known.PawnLtvBps, "An already quoted offer changed without a reprice");
            var floor = With(Market(s), ("new_pawn_ltv:new_agreements", -9000)); VisitService.Reprice(s, unknown, floor, UnifiedConfig.Rules); Eq(3000, unknown.PawnLtvBps, "LTV must stay within 30..65%");
        });
        Test("direct sale and pawn visits skip the paid commission and value the same item", () =>
        {
            var s = State("direct"); var m = Market(s); var a = Catalog.CreateAsset(s, "sp3_gold_ring_0_p0", "c"); a.Measurements.Add("assay");
            var v = VisitService.Open(s, a.AssetId, "c", "pawn", "ordinary", false, m, 0, UnifiedConfig.Rules); Check(v.Stage == "followup" && v.Intent == "request_pawn" && v.Draw < 0, "A direct pawn visit drew an intent");
            Check(!VisitService.Submit(s, v.VisitId, 100, UnifiedConfig.Rules).Success && v.Fee == 0, "A direct visit charged an appraisal fee");
            VisitService.Reprice(s, v, m, UnifiedConfig.Rules); Check(v.OfferAmount > 0 && VisitService.PreparePawn(s, v.VisitId, 1, 99999, 7).PawnCommand!.AssetId == a.AssetId, "Direct pawn terms");
            VisitService.Walkout(s, v.VisitId); Check(v.Resolution == "walkout" && a.OwnerId == "c", "A walkout moved the item");
        });

        // ---------------------------------------------------------------- jewelry
        (CatalogState s, AssetRecord setting, AssetRecord stone) Mounted(string stoneDef = "sp5_gem_ruby_oval_nat_p0", string owner = "player")
        {
            var s = State("jewel" + stoneDef + owner); s.Unified.EnergyUnits = 20; var m = Market(s);
            var setting = Catalog.CreateAsset(s, "sp5_setting_ring_au18k", owner); var stone = Catalog.CreateAsset(s, stoneDef, owner);
            var job = Operations05.StartMount(s, setting.AssetId, new[] { stone.AssetId }, Bench, m, 100, UnifiedConfig.Rules, owner); Check(job.Success, job.Error);
            Operations05.AdvanceJobs(s, 2); Check(Operations05.ClaimJob(s, job.EntityId, _ => true).Success, "Claim mount");
            return (s, setting, stone);
        }
        Test("mounting keeps the real setting and stone identities; metal, workmanship and stone are separate", () =>
        {
            var (s, setting, stone) = Mounted(); var m = Market(s, 2);
            Check(stone.Status == "embedded" && stone.ParentAssetId == setting.AssetId && s.Assets.Count == 2, "Mount created a wrapper or a copy");
            var q = ComponentValuation.Quote(s, setting, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Eq(ValuationV2.MetalValue(4000, 7500, m.GoldSell), q.MetalValue, "Gold is the setting mass only, never the stone-inclusive weight"); Eq(12L, q.CraftValue, "Workmanship"); Eq(90L, q.StoneValue, "Stone 1.000 ct x 90");
            Eq(q.MetalValue + 12 + 90, q.Total, "No wrapper value on top of the children"); Eq(1, q.StonesCounted, "One real stone per socket");
            var gems = With(m, ("gem_price:ruby", -1200)); var g = ComponentValuation.Quote(s, setting, gems, ComponentValuation.SellToCustomer, UnifiedConfig.Rules);
            Eq(ComponentValuation.Scale(90, 8800), g.StoneValue, "A gem event applies once, to the stone"); Eq(q.MetalValue, g.MetalValue, "A gem event changed the gold"); Eq(q.CraftValue, g.CraftValue, "A gem event changed workmanship");
            Check(!Operations05.StartMount(s, setting.AssetId, new[] { Catalog.CreateAsset(s, "sp5_gem_ruby_oval_nat_p0").AssetId }, Bench, m, 100, UnifiedConfig.Rules).Success, "A second stone was forced into a full socket");
        });
        Test("an empty setting is not a missing-stone finding; demount returns the SAME stone; nothing recreates a stone", () =>
        {
            var s0 = State("empty"); var m0 = Market(s0); var empty = Catalog.CreateAsset(s0, "sp5_setting_ring_au18k");
            var eq = ComponentValuation.Quote(s0, empty, m0, ComponentValuation.SellToCustomer, UnifiedConfig.Rules); Check(eq.StoneValue == 0 && eq.Lines.Count == 0 && eq.CraftDeduction == 0, "An empty service setting was treated as damaged");
            Check(!Operations05.StartDemount(s0, empty.AssetId, Bench, m0, 100, UnifiedConfig.Rules).Success, "A missing stone was 'recovered'");
            var (s, setting, stone) = Mounted(); var m = Market(s, 2); var id = stone.AssetId;
            Check(!Operations05.StartDemount(s, setting.AssetId, new[] { "jeweler_bench" }, m, 100, UnifiedConfig.Rules).Success, "Demount without the toolkit");
            var job = Operations05.StartDemount(s, setting.AssetId, Bench, m, 100, UnifiedConfig.Rules); Check(job.Success, job.Error); Operations05.AdvanceJobs(s, 3);
            var claim = Operations05.ClaimJob(s, job.EntityId, _ => true); Check(claim.CreatedAssetIds.SequenceEqual(new[] { id }) && stone.Status == "active" && stone.ParentAssetId == "" && s.Assets.Count == 2, "Demount must release the same stone, not a new one");
            Eq(0, Operations05.ClaimJob(s, job.EntityId, _ => true).CreatedAssetIds.Count, "Claiming twice released a second stone");
        });
        Test("socket rules: compatible family and shape, one stone per socket, matched stones for pairs", () =>
        {
            var s = State("sockets"); s.Unified.EnergyUnits = 20; var m = Market(s);
            var ring = Catalog.CreateAsset(s, "sp5_setting_ring_au18k"); var pearl = Catalog.CreateAsset(s, "sp5_gem_pearl_round_nat_p0");
            Eq("sp5.jewel.incompatible", Operations05.StartMount(s, ring.AssetId, new[] { pearl.AssetId }, Bench, m, 100, UnifiedConfig.Rules).ErrorKey, "A pearl was set in the solitaire ring");
            var studs = Catalog.CreateAsset(s, "sp5_setting_earrings_ag925"); var d1 = Catalog.CreateAsset(s, "sp5_gem_diamond_round_nat_p0"); var d2 = Catalog.CreateAsset(s, "sp5_gem_diamond_round_nat_p0"); var r1 = Catalog.CreateAsset(s, "sp5_gem_ruby_cushion_nat_p0");
            Eq("sp5.jewel.count", Operations05.StartMount(s, studs.AssetId, new[] { d1.AssetId }, Bench, m, 100, UnifiedConfig.Rules).ErrorKey, "Earrings need two real stones");
            Eq("sp5.jewel.mixed", Operations05.StartMount(s, studs.AssetId, new[] { d1.AssetId, r1.AssetId }, Bench, m, 100, UnifiedConfig.Rules).ErrorKey, "Mixed families in a pair");
            var job = Operations05.StartMount(s, studs.AssetId, new[] { d1.AssetId, d2.AssetId }, Bench, m, 100, UnifiedConfig.Rules); Check(job.Success, job.Error); Operations05.AdvanceJobs(s, 2); Operations05.ClaimJob(s, job.EntityId, _ => true);
            Check(Operations05.Stones(s, studs.AssetId).Select(x => x.AssetId).OrderBy(x => x).SequenceEqual(new[] { d1.AssetId, d2.AssetId }.OrderBy(x => x)) && d1.SocketId != d2.SocketId, "Two distinct stones in two sockets");
            foreach (var row in Csv("docs/JEWELRY_SETTINGS_04.csv")) { var form = row["category"].Replace("jewelry_", ""); foreach (var metal in new[] { "au18k", "ag925" }) Eq(int.Parse(row["socket_count"]), D("sp5_setting_" + form + "_" + metal).SocketCount, "Socket count of " + form); }
        });
        Test("a laboratory report establishes identity; disclosed lab-grown is not counterfeit; a probe proves no origin", () =>
        {
            var (s, setting, stone) = Mounted("sp5_gem_ruby_oval_lab_p0"); var m = Market(s, 2);
            var claimed = ComponentValuation.Quote(s, setting, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules); Eq(90L, claimed.StoneValue, "Untested claim"); Check(!claimed.StonesVerified, "An untested stone shown as verified");
            var probe = Catalog.CreateAsset(s, "sp5_tool_gem_probe"); stone.Measurements.Add("gem_probe"); Check(Appraisal.Reading(stone, "gem_probe").Contains("does not confirm natural origin"), "The probe claimed to prove origin");
            Check(ComponentValuation.Quote(s, setting, m, ComponentValuation.PawnCollateral, UnifiedConfig.Rules).StoneValue < 10, "An unverified claim secured a natural-gem premium");
            var lab = Operations05.LabReport(s, stone.AssetId, 2, m, 100, UnifiedConfig.Rules); Check(lab.Success && lab.CashDelta == -6, "Lab fee");
            var known = ComponentValuation.Quote(s, setting, m, ComponentValuation.SellToCustomer, UnifiedConfig.Rules); Eq(36L, known.StoneValue, "Laboratory-grown band is 40% of natural"); Check(known.StonesVerified && s.Assets.Count == 3, "A report must not create a stone");
            Check(!Operations05.LabReport(s, stone.AssetId, 2, m, 100, UnifiedConfig.Rules).Success, "A second report was sold for the same stone");
        });
        Test("custom orders: customer property stays theirs, the deposit is held, and delivery pays the balance once", () =>
        {
            var s = State("order"); s.Unified.EnergyUnits = 20; var m = Market(s);
            var setting = Catalog.CreateAsset(s, "sp5_setting_ring_au18k", "lady-wang"); var stone = Catalog.CreateAsset(s, "sp5_gem_ruby_oval_nat_p0", "lady-wang");
            var accept = Operations05.AcceptOrder(s, "lady-wang", "service", setting.AssetId, new[] { stone.AssetId }, 120, 1, 5, UnifiedConfig.Rules);
            Check(accept.Success && accept.CashDelta == 36, "Deposit = floor(120 x 30%)"); Eq(36L, s.Unified.HeldCustomerDeposits, "Held deposit"); Eq(964L, Operations05.LendableCash(1000, s), "A held deposit was counted as lendable cash");
            Check(setting.OwnerId == "lady-wang" && stone.OwnerId == "lady-wang" && setting.CustodyRef == accept.EntityId, "Customer goods changed owner or were not reserved");
            Check(!Operations05.StartRecovery(s, "sort_metal_goods", new[] { setting.AssetId }, Plant, m, 100, UnifiedConfig.Rules).Success, "Customer property entered recovery");
            Check(!Operations05.AcceptOrder(s, "lady-wang", "service", setting.AssetId, new[] { stone.AssetId }, 50, 1, 5, UnifiedConfig.Rules).Success, "The same parts were reserved for a second order");
            var work = Operations05.StartOrderWork(s, accept.EntityId, Bench, m, 100, UnifiedConfig.Rules); Check(work.Success, work.Error);
            Check(!Operations05.DeliverOrder(s, accept.EntityId).Success, "Delivered before the piece was ready");
            Operations05.AdvanceJobs(s, 2); Operations05.ClaimJob(s, work.EntityId, _ => true); Check(Operations05.MarkOrderReady(s, accept.EntityId).Success, "Ready");
            var deliver = Operations05.DeliverOrder(s, accept.EntityId); Check(deliver.Success && deliver.CashDelta == 84, "Balance = price - deposit"); Eq(0L, s.Unified.HeldCustomerDeposits, "Deposit not released at delivery");
            Eq(0L, Operations05.DeliverOrder(s, accept.EntityId).CashDelta, "A second delivery paid again"); Check(setting.OwnerId == "lady-wang" && stone.Status == "embedded" && stone.OwnerId == "lady-wang", "The customer did not receive their own mounted stone");
            // Cancel before work: full refund, parts released.
            var s2 = State("cancel"); var a2 = Catalog.CreateAsset(s2, "sp5_setting_ring_ag925"); var g2 = Catalog.CreateAsset(s2, "sp5_gem_diamond_round_nat_p0");
            var o2 = Operations05.AcceptOrder(s2, "mr-li", "commission", a2.AssetId, new[] { g2.AssetId }, 200, 1, 4, UnifiedConfig.Rules); Eq(60L, o2.CashDelta, "Commission deposit");
            var cancel = Operations05.CancelOrder(s2, o2.EntityId); Check(cancel.Success && cancel.CashDelta == -60 && s2.Unified.HeldCustomerDeposits == 0 && a2.CustodyRef == "" && g2.CustodyRef == "", "Cancellation before work must refund in full and release the parts");
            Check(!Operations05.AcceptOrder(s2, "mr-li", "commission", a2.AssetId, new[] { Catalog.CreateAsset(s2, "sp5_gem_ruby_oval_nat_p0", "someone-else").AssetId }, 200, 1, 4, UnifiedConfig.Rules).Success, "A commission reserved a stone the shop does not own");
        });

        // ---------------------------------------------------------------- physical recovery
        Test("recovery conserves every element: outputs + residue = inputs; no gold appears from nowhere", () =>
        {
            var s = State("recover"); s.Unified.EnergyUnits = 50; var m = Market(s);
            var boards = Enumerable.Range(0, 3).Select(_ => Catalog.CreateAsset(s, "sp5_scrap_board_p0")).ToList();
            long au = boards.Sum(Operations05.Gold), ag = boards.Sum(Operations05.Silver), cu = boards.Sum(Operations05.Copper);
            var start = Operations05.StartRecovery(s, "recover_components", boards.Select(b => b.AssetId).ToList(), Plant, m, 100, UnifiedConfig.Rules); Check(start.Success && start.CashDelta == -2, start.Error);
            Eq(46, s.Unified.EnergyUnits, "Energy consumed once"); Check(boards.All(b => b.CustodyRef == start.EntityId), "Inputs not reserved");
            Check(!Operations05.StartRecovery(s, "recover_components", new[] { boards[0].AssetId }, Plant, m, 100, UnifiedConfig.Rules).Success, "An input was used by two jobs");
            Check(!Operations05.ClaimJob(s, start.EntityId, _ => true).Success, "Collected before the job finished");
            s = Reload(s); Operations05.AdvanceJobs(s, 2);
            Check(!Operations05.ClaimJob(s, start.EntityId, _ => false).Success && s.Unified.Jobs[0].Status == "ready" && s.Assets.Count == 3, "A full inventory must leave the output ready, not lose it");
            var claim = Operations05.ClaimJob(s, start.EntityId, _ => true); Check(claim.Success, claim.Error);
            var outs = claim.CreatedAssetIds.Select(id => Catalog.GetAsset(s, id)).ToList();
            Eq(au, outs.Sum(Operations05.Gold), "Gold not conserved"); Eq(ag, outs.Sum(Operations05.Silver), "Silver not conserved"); Eq(cu, outs.Sum(Operations05.Copper), "Copper not conserved");
            var gold = outs.Single(o => o.DefinitionId == "sp5_lot_recovered_gold"); Eq(ComponentValuation.Scale(au, 8500), Operations05.Gold(gold), "Recovered gold = budget x recipe efficiency");
            Check(Operations05.Gold(outs.Single(o => o.DefinitionId == "sp5_inert_residue")) == au - Operations05.Gold(gold), "The loss must be carried by the residue");
            Check(s.Assets.Where(a => boards.Select(b => b.AssetId).Contains(a.AssetId)).All(a => a.Status == "consumed"), "Inputs survived"); var n = s.Assets.Count;
            Check(Operations05.ClaimJob(Reload(s), start.EntityId, _ => true).CreatedAssetIds.Count == 0 && Operations05.ClaimJob(s, start.EntityId, _ => true).CreatedAssetIds.Count == 0 && s.Assets.Count == n, "Reload or repeated collection duplicated outputs");
            Check(ComponentValuation.Quote(s, gold, m, ComponentValuation.MaterialBuyback, UnifiedConfig.Rules).Total == ValuationV2.MetalValue(Operations05.Gold(gold) / 1000, 10000, 65), "A lot is valued by its recorded content");
        });
        Test("dismantling partitions a device's finite budget; an event changes new jobs only", () =>
        {
            var s = State("dismantle"); s.Unified.EnergyUnits = 50; var m = Market(s); var console = Catalog.CreateAsset(s, "console");
            Check(!Operations05.StartRecovery(s, "dismantle_device", new[] { console.AssetId }, new[] { "refining_furnace" }, m, 100, UnifiedConfig.Rules).Success, "Ran without the recovery station");
            var job = Operations05.StartRecovery(s, "dismantle_device", new[] { console.AssetId }, Plant, m, 100, UnifiedConfig.Rules); Check(job.Success, job.Error); var ready = s.Unified.Jobs[0].ReadyDay; var energy = s.Unified.Jobs[0].Energy;
            var ration = With(Market(s), ("processing_energy_cost:all", 3000), ("processing_duration:all", 5000), ("processing_service_cost:all", 1000));
            var second = Operations05.StartRecovery(s, "dismantle_device", new[] { Catalog.CreateAsset(s, "handheld").AssetId }, Plant, ration, 100, UnifiedConfig.Rules); Check(second.Success, second.Error);
            Check(s.Unified.Jobs[1].Energy > energy && s.Unified.Jobs[1].ReadyDay > ready && -second.CashDelta > -job.CashDelta, "Rationing and freight must raise energy, time and fee of NEW jobs");
            Check(s.Unified.Jobs[0].ReadyDay == ready && s.Unified.Jobs[0].Energy == energy, "An accepted job was repriced by a later event");
            Operations05.AdvanceJobs(s, ready); var outs = Operations05.ClaimJob(s, job.EntityId, _ => true).CreatedAssetIds.Select(id => Catalog.GetAsset(s, id)).ToList();
            Eq(120000L, outs.Sum(Operations05.Gold), "Device gold budget"); Eq(60000000L, outs.Sum(Operations05.Copper), "Device copper budget");
            s.Unified.EnergyUnits = 0; Eq("sp5.job.no_energy", Operations05.StartRecovery(s, "recover_sorted", new[] { outs[0].AssetId }, Plant, m, 100, UnifiedConfig.Rules).ErrorKey, "Machine energy is finite");
        });
        Test("alloying uses only the metal present and returns the remainder", () =>
        {
            var s = State("alloy"); s.Unified.EnergyUnits = 50; var m = Market(s);
            var gold = Catalog.CreateAsset(s, "sp5_lot_recovered_gold"); gold.GoldMicrograms = 9000000; gold.SilverMicrograms = 0; gold.CopperMicrograms = 0;
            var copper = Catalog.CreateAsset(s, "sp5_lot_recovered_copper"); copper.CopperMicrograms = 2000000; copper.GoldMicrograms = 0; copper.SilverMicrograms = 0;
            var job = Operations05.StartRecovery(s, "alloy_gold18k", new[] { gold.AssetId, copper.AssetId }, Plant, m, 100, UnifiedConfig.Rules); Check(job.Success, job.Error);
            Operations05.AdvanceJobs(s, 9); var outs = Operations05.ClaimJob(s, job.EntityId, _ => true).CreatedAssetIds.Select(id => Catalog.GetAsset(s, id)).ToList();
            var bar = outs.Single(o => o.DefinitionId == "sp5_alloy_gold18k_bar"); Eq(6000000L, Operations05.Gold(bar), "18K uses 3 parts gold per part copper"); Eq(2000000L, Operations05.Copper(bar), "Copper in the bar");
            Eq(9000000L, outs.Sum(Operations05.Gold), "Gold conserved"); Eq(2000000L, outs.Sum(Operations05.Copper), "Copper conserved"); Eq(3000000L, Operations05.Gold(outs.Single(o => o.DefinitionId == "sp5_lot_recovered_gold")), "Unused gold must come back");
            var lone = Catalog.CreateAsset(s, "sp5_lot_recovered_gold"); lone.GoldMicrograms = 500; lone.SilverMicrograms = 0; lone.CopperMicrograms = 0;
            Eq("sp5.job.no_charge", Operations05.StartRecovery(s, "alloy_gold18k", new[] { lone.AssetId }, Plant, m, 100, UnifiedConfig.Rules).ErrorKey, "An alloy was made without copper");
        });

        // ---------------------------------------------------------------- migration and old saves
        Test("layout migration repacks a full inventory without overlap, loss or copies", () =>
        {
            var items = new List<PlacedItem>(); var id = 0;                              // a full 10x6 grid of old one-cell items
            for (var y = 0; y < 6; y++) for (var x = 0; x < 10; x++) { var k = id++; var (w, h) = new[] { (1, 1), (3, 2), (2, 3), (2, 2), (1, 3) }[k % 5]; items.Add(new PlacedItem { Id = "it" + k, X = x, Y = y, W = 1, H = 1, NewW = k % 7 == 0 ? w : 1, NewH = k % 7 == 0 ? h : 1 }); }
            var plan = LayoutMigration.Plan(10, 6, items);
            Eq(60, plan.Placements.Count + plan.Overflow.Count, "Items lost or duplicated"); Check(plan.Placements.Select(p => p.Id).Concat(plan.Overflow).Distinct().Count() == 60, "An item appears twice");
            var used = new bool[10, 6];
            foreach (var p in plan.Placements) for (var x = p.X; x < p.X + p.W; x++) for (var y = p.Y; y < p.Y + p.H; y++) { Check(x < 10 && y < 6 && !used[x, y], "Overlap or out of bounds after migration"); used[x, y] = true; }
            Check(plan.Overflow.Count > 0 && plan.Kept > 40, "A full grid must overflow visibly rather than overlap");
            Eq(Snap(plan), Snap(LayoutMigration.Plan(10, 6, items)), "The plan is not deterministic");
            var roomy = LayoutMigration.Plan(12, 12, items); Eq(0, roomy.Overflow.Count, "Unneeded overflow"); Check(roomy.Placements.Where(p => { var o = items.First(i => i.Id == p.Id); return o.NewW == o.W && o.NewH == o.H; }).All(p => p.X == items.First(i => i.Id == p.Id).X && p.Y == items.First(i => i.Id == p.Id).Y), "Unchanged items were moved");
        });
        Test("migration is versioned, resumable and leaves overflow as persistent claims on the same objects", () =>
        {
            var s = State("migrate"); Check(LayoutMigration.NeedsMigration(s, "main"), "Fresh state needs migration");
            var plan = LayoutMigration.Plan(2, 3, new[] { new PlacedItem { Id = "book-a", NewW = 2, NewH = 3, W = 1, H = 1 }, new PlacedItem { Id = "book-b", X = 1, NewW = 2, NewH = 3, W = 1, H = 1 } });
            var receipt = LayoutMigration.Begin(s, "main", 4); s = Reload(s);                                  // interrupted before completion
            Check(LayoutMigration.NeedsMigration(s, "main") && s.Unified.Migrations.Count == 1, "An interrupted migration must resume, not restart");
            receipt = LayoutMigration.Begin(s, "main", 5); Eq(1, s.Unified.Migrations.Count, "Resume created a second receipt"); LayoutMigration.Complete(s, receipt, plan, "main"); LayoutMigration.Complete(s, receipt, plan, "main");
            Check(!LayoutMigration.NeedsMigration(s, "main") && s.Unified.Overflow.Count == 1 && s.Unified.Overflow[0].AssetId == "book-b", "Overflow claim");
            s = Reload(s); Check(LayoutMigration.Claim(s, "book-b") && !LayoutMigration.Claim(s, "book-b"), "An overflow claim must be redeemable exactly once");
        });
        Test("a 0.3.1 save without Revision 05 state still loads; identities, evidence and legacy XP survive", () =>
        {
            var s = State("old"); var a = Catalog.CreateAsset(s, "gold_bracelet_plated", "customer"); a.DiscoveredEvidenceIds.Add("plating_edge"); a.RewardedEvidenceIds.Add("plating_edge"); s.Experience = 60;
            var e = InspectionV2.Open(s, a.AssetId, "old-deal", "customer"); e.SelectedEvidenceIds.Add("plating_edge");
            var json = Newtonsoft.Json.Linq.JObject.Parse(Snap(s)); json.Remove("Unified"); foreach (var asset in (Newtonsoft.Json.Linq.JArray)json["Assets"]!) foreach (var f in new[] { "SocketId", "CustodyRef", "GoldMicrograms", "SilverMicrograms", "CopperMicrograms" }) ((Newtonsoft.Json.Linq.JObject)asset).Remove(f);
            var old = JsonConvert.DeserializeObject<CatalogState>(json.ToString())!; Catalog.Validate(old);
            Check(old.Unified != null && old.Unified.Visits.Count == 0 && old.Assets[0].AssetId == a.AssetId && old.Assets[0].GoldMicrograms == -1, "Old save lost data or gained defaults that change truth");
            Career.MigrateLegacyExperience(old); Eq(60, old.Unified.LegacyExperience, "Legacy XP"); Eq(60, old.Experience, "Legacy XP field rewritten");
            Check(!InspectionV2.Discover(old, "old-deal", "plating_edge").PlayFeedback, "An old finding replayed feedback after migration");
        });

        // ---------------------------------------------------------------- handbooks and copy
        Test("every physical book has at most five pages; the opening shares page one with real content", () =>
        {
            var manuals = defs.Where(d => d.IsBook && !string.IsNullOrEmpty(d.ManualFor)).ToList(); Check(manuals.Count >= 35, "Manuals missing");
            foreach (var manual in manuals)
            {
                var pages = Handbooks.Pages(manual); Check(pages.Count >= 1 && pages.Count <= 5, manual.Id + " has " + pages.Count + " pages");
                Check(pages[0].HasIntro && pages.Count(p => p.HasIntro) == 1, "Intro placement in " + manual.Id);
                if (manual.HandbookTemplates.Count > 0)
                {
                    Check(pages[0].Kind == "checks" && pages[0].Entries.Count >= 1, "Page one of " + manual.Id + " is a mandatory instructions-only page");
                    Check(pages.Where(p => p.Kind == "checks").All(p => p.Entries.Count >= 1 && p.Entries.Count <= 2), "A page must teach one or two checks");
                    Eq(manual.HandbookTemplates.Count, pages.SelectMany(p => p.Entries).Select(x => x.Evidence.TemplateId).Distinct().Count(), "The five-page cap dropped an example in " + manual.Id);
                }
            }
            Check(Handbooks.Pages(D("sp3_manual_metals")).All(p => p.Kind == "index") && Handbooks.Pages(D("sp3_manual_metals")).Sum(p => p.Volumes.Count) == 10, "An anthology keeps its index, not a hidden long book");
        });
        Test("handbook routing depends on the public family, never on hidden defects or origin", () =>
        {
            Eq(Appraisal.HandbookFor(D("gold_bracelet")).Id, Appraisal.HandbookFor(D("gold_bracelet_plated")).Id, "Plating changed the handbook");
            Eq(Appraisal.HandbookFor(D("sp5_gem_ruby_oval_nat_p0")).Id, Appraisal.HandbookFor(D("sp5_gem_ruby_oval_lab_p0")).Id, "Gem origin changed the handbook");
            Eq("sp5_manual_gems_a", Appraisal.HandbookFor(D("sp5_gem_ruby_oval_nat_p2")).Id, "Ruby routes to its own volume"); Eq("sp5_manual_mounts_a", Appraisal.HandbookFor(D("sp5_jewelry_ring_au18k_p1")).Id, "Ring routes to the ring volume");
            Check(Appraisal.Handbook(D("sp31_manual_gold_ring")).All(x => Appraisal.Family(x.Example).Contains("gold_ring")), "A ring volume shows another category's templates");
            Check(D("sp5_gem_ruby_oval_nat_p0").Name == D("sp5_gem_ruby_oval_lab_p0").Name && D("sp5_gem_ruby_oval_nat_p0").TextureKey == D("sp5_gem_ruby_oval_lab_p0").TextureKey, "Name or art reveals a stone's origin");
        });
        Test("Revision 05 copy is keyed in both languages and one source never has two translations", () =>
        {
            var zh = JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(Path.Combine(root, "mod/Content/Localization/zh-CN.json")))!; var en = JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(Path.Combine(root, "mod/Content/Localization/en.json")))!;
            var zk = zh.Entries.ToDictionary(x => x.Key, x => x); var ek = en.Entries.ToDictionary(x => x.Key, x => x);
            var keys = zk.Keys.Where(k => k.StartsWith("sp5.") || k.StartsWith("content.sp5_") || k.StartsWith("template.sp5_")).ToList(); Check(keys.Count > 500, "Revision 05 keys missing");
            foreach (var k in keys) Check(ek.ContainsKey(k) && ek[k].Text.Length > 0 && zk[k].Text.Length > 0 && ek[k].Text != zk[k].Text, "Missing translation for " + k);
            Check(ek.Keys.All(zk.ContainsKey), "English overlay has an unknown key");
            ContentPacks.ValidateLocalization(zh); TextResolver.Configure(new LocalizationPack { Language = "en", Entries = zh.Entries.Select(x => ek.ContainsKey(x.Key) ? new LocalizedText { Key = x.Key, Source = x.Source, Text = ek[x.Key].Text } : x).ToList() });
            Eq("Mark: {0} | Assay: not tested", TextResolver.Get("sp5.material.marked"), "English copy"); TextResolver.Configure(zh); Check(TextResolver.Get("sp5.material.marked").Contains("未检测"), "Chinese copy retained");
            var source = File.ReadAllText(Path.Combine(root, "source/Core/Unified05.cs")) + File.ReadAllText(Path.Combine(root, "source/Core/Operations05.cs"));
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source, "\"(sp5\\.[a-z0-9_.]+)\"")) if (!match.Groups[1].Value.EndsWith(".")) Check(zk.ContainsKey(match.Groups[1].Value) || match.Groups[1].Value == "sp5.visit.grade", "Core uses an unlocalized key: " + match.Groups[1].Value);
        });

        // ---------------------------------------------------------------- finance and balance
        Test("balance guards: no same-day deposit/borrow profit, bounded pawn lending, held deposits excluded", () =>
        {
            var fin = JsonConvert.DeserializeObject<EconomyRules>(File.ReadAllText(Path.Combine(root, "mod/Content/finance-rules.json")), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace })!; // same as the game loader EconomyService.Validate(EconomyService.CreateNewState(), fin);
            Check(fin.BankInterestBps < fin.LoanInterestBps && fin.BankInterestBps < fin.CreditInterestBps && fin.BankInterestBps < fin.PawnInterestBps, "A deposit rate at or above a borrowing rate allows a guaranteed loop");
            Eq(5000, fin.PawnLoanToValueBps, "New pawn offers start at 50%"); Check(fin.PawnMinLoanToValueBps == 3000 && fin.PawnMaxLoanToValueBps == 6500, "Pawn policy bounds 30..65%");
            var eco = EconomyService.CreateNewState(); var r = EconomyService.Apply(eco, new EconomyCommand { Kind = EconomyAction.AcquirePawnLicense, GameDay = 1, AvailableCash = 9999, Sequence = 1, TransactionId = "a" }, fin); Check(r.Success, r.Error);
            var over = EconomyService.Apply(eco, new EconomyCommand { Kind = EconomyAction.CreatePawn, GameDay = 1, AvailableCash = 9999, Sequence = 2, TransactionId = "b", Amount = 660, CollateralValue = 1000, AssetId = "x", LoanToValueBps = 7000 }, fin); Check(!over.Success, "Lent above the 65% policy bound");
            var def = EconomyService.Apply(eco, new EconomyCommand { Kind = EconomyAction.CreatePawn, GameDay = 1, AvailableCash = 9999, Sequence = 2, TransactionId = "c", Amount = 501, CollateralValue = 1000, AssetId = "x" }, fin); Check(!def.Success, "The default policy lent above 50%");
            // Early game: a 6 g ring is worth far more than its appraisal fee, and the fee never exceeds the cap.
            var s = State("balance"); var m = Market(s); foreach (var id in new[] { "sp3_gold_ring_0_p0", "sp3_gold_bar_0_p0", "sp5_jewelry_ring_au18k_p1", "jade_rough" })
            { var q = ComponentValuation.Quote(s, Catalog.CreateAsset(s, id, "c", 3), m, ComponentValuation.TrueValue, UnifiedConfig.Rules).Total; var fee = Career.Fee(q, 3, UnifiedConfig.Rules); Check(fee > 0 && fee <= 60 && fee <= Math.Max(8, q / 3), "Commission fee out of balance for " + id + ": " + fee + " on " + q); }
            var urgent = UnifiedConfig.Rules.IntentProfiles.First(p => p.Id == "urgent_cash"); Check(urgent.Pawn > urgent.Sale && urgent.Sale > urgent.Leave, "Urgent customers must mostly want cash");
        });
    }
}
