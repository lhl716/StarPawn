using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StarPawn.Core;

internal static class ContentTests
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual, string message) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(message + ": expected=" + expected + ", actual=" + actual); }
    private static void Throws(Action action, string message) { try { action(); } catch (InvalidOperationException) { return; } catch (ArgumentException) { return; } catch (OverflowException) { return; } throw new Exception(message); }
    private static string Saved(CatalogState state) => JsonSerializer.Serialize(state, Json);
    private static CatalogState Reload(CatalogState state) => JsonSerializer.Deserialize<CatalogState>(Saved(state), Json)!;
    private static (CatalogState State, AssetRecord Asset, InspectionEncounter Encounter) Sample(string definition = "gold_bracelet_plated", string owner = "player")
    {
        var state = Catalog.CreateState("content-test");
        var asset = Catalog.CreateAsset(state, definition, owner, 123456);
        var encounter = InspectionV2.Open(state, asset.AssetId, "deal-1", "customer-a");
        return (state, asset, encounter);
    }
    private static AssetQuote Quote(CatalogState state, string id = "deal-1", string direction = "buy", long version = 1, int day = 0, long goldBuy = 90)
        => ValuationV2.Quote(state, id, day, version, 100, goldBuy, 5, 4, direction);
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }

    private static void Main(string[] args)
    {
        Test("all content definitions instantiate with unique physical IDs", () => {
            var state = Catalog.CreateState("all-content");
            foreach (var d in Catalog.Definitions) { var a = Catalog.CreateAsset(state, d.Id); Check(Catalog.PublicFacts(a).Name.Length > 0, "Missing public name"); Check(Catalog.TextureFor(a).StartsWith("starpawn:"), "Nonlocal texture key"); }
            Equal(state.Assets.Count, state.Assets.Select(a => a.AssetId).Distinct().Count(), "Duplicate asset identities");
            Check(state.Assets.Count >= 24, "Content pack incomplete"); Catalog.Validate(Reload(state));
        });
        Test("two consecutive misses lock customer and item across reopen and save", () => {
            var s = Sample(); InspectionV2.ClickAt(s.State, "deal-1", 0, 0); Equal(1, s.Encounter.ConsecutiveMisses, "First miss");
            var result = InspectionV2.ClickAt(s.State, "deal-1", 0, 0); Check(result.Locked && result.Kind == "locked" && result.Message.Length > 0, "Lock must use in-world feedback");
            var state = Reload(s.State); var reopened = InspectionV2.Open(state, s.Asset.AssetId, "new-ui-id", "customer-a");
            Equal("deal-1", reopened.EncounterId, "Reminted window must reuse persistent encounter"); Check(reopened.Locked, "Reload cleared patience");
            Equal("locked", InspectionV2.Discover(state, "deal-1", "plating_edge").Kind, "Locked inspector accepted discovery"); Equal(0, state.Experience, "Locked XP leak");
        });
        Test("new discovery restores patience but repeated evidence does not", () => {
            var s = Sample(); InspectionV2.Miss(s.State, "deal-1"); InspectionV2.Discover(s.State, "deal-1", "plating_edge"); Equal(0, s.Encounter.ConsecutiveMisses, "New evidence failed to reset streak");
            InspectionV2.Miss(s.State, "deal-1"); Equal("repeated", InspectionV2.Discover(s.State, "deal-1", "plating_edge").Kind, "Repeat not identified");
            Equal(1, s.Encounter.ConsecutiveMisses, "Repeat restored patience"); Equal(0, s.State.Experience, "Repeat awarded XP"); InspectionV2.Miss(s.State, "deal-1"); Check(s.Encounter.Locked, "Repeat protected from second miss");
        });
        Test("asset and customer encounter identities cannot be rebound", () => {
            var s = Sample(); var other = Catalog.CreateAsset(s.State, "gold_bracelet");
            Throws(() => InspectionV2.Open(s.State, other.AssetId, "deal-1", "customer-a"), "Encounter rebound to asset");
            Throws(() => InspectionV2.Open(s.State, s.Asset.AssetId, "deal-1", "customer-b"), "Encounter rebound to customer");
            var newCustomer = InspectionV2.Open(s.State, s.Asset.AssetId, "deal-2", "customer-b"); Check(!newCustomer.Locked, "A new customer inherits unrelated patience");
        });
        Test("normalized art hotspots match inspected plated bracelet and miss empty space", () => {
            var s = Sample(); Equal("discovered", InspectionV2.ClickAt(s.State, "deal-1", .277f, .275f).Kind, "Left real image defect missed");
            Equal("discovered", InspectionV2.ClickAt(s.State, "deal-1", .736f, .293f).Kind, "Right real image defect missed");
            Equal("discovered", InspectionV2.ClickAt(s.State, "deal-1", .499f, .184f).Kind, "Clasp real image defect missed");
            Equal("miss", InspectionV2.ClickAt(s.State, "deal-1", .5f, .5f).Kind, "Empty center must not be a defect");
        });
        Test("experience is per physical asset and evidence across buyback", () => {
            var s = Sample(); InspectionV2.Discover(s.State, "deal-1", "plating_edge"); InspectionV2.EndEncounter(s.State, "deal-1");
            Catalog.Transfer(s.State, s.Asset.AssetId, "player", "customer-a"); Catalog.Transfer(s.State, s.Asset.AssetId, "customer-a", "player");
            InspectionV2.Open(s.State, s.Asset.AssetId, "deal-2", "customer-a"); InspectionV2.Discover(s.State, "deal-2", "plating_edge"); Equal(0, s.State.Experience, "Buyback XP duplication");
            var second = Catalog.CreateAsset(s.State, "gold_bracelet_plated"); InspectionV2.Open(s.State, second.AssetId, "deal-3", "customer-a"); InspectionV2.Discover(s.State, "deal-3", "plating_edge"); Equal(0, s.State.Experience, "Different physical copy must reward discovery");
        });
        Test("gold weight and craft are separate with real recycling floor", () => {
            var s = Sample("gold_bracelet_worn"); foreach (var h in Catalog.GetDefinition(s.Asset.DefinitionId).Hotspots) InspectionV2.Discover(s.State, "deal-1", h.Id);
            var buy = Quote(s.State, goldBuy:100); var sell = Quote(s.State, direction:"sell", goldBuy:100);
            Equal(1798L, ValuationV2.MetalValue(18000, 9990, 100), "Milligram purity conversion incorrect");
            Equal(1798L, sell.MaterialValue, "Buyback metal value"); Check(sell.Price >= 1798L && sell.Price == 1798L + sell.CraftValue, "Cosmetic findings must not pierce the metal value; remaining workmanship is added on top"); Check(buy.Price >= sell.Price, "Buy and sell direction spread inverted");
            Equal(1798L, buy.MaterialValue, "Raw material value"); Equal(360L, buy.CraftPremium, "Craft premium"); Check(buy.MaterialDescription.Contains("18 g") && buy.MaterialDescription.Contains("99.90%"), "Metal mass/purity invisible");
        });
        Test("unknown plated truth stays hidden and confirmed fake has no gold floor", () => {
            var fake = Sample(); var genuine = Sample("gold_bracelet"); Equal(Quote(genuine.State).Price, Quote(fake.State).Price, "Unknown hidden purity changed quote");
            Equal(9990, Catalog.PublicFacts(fake.Asset).PurityBasisPoints, "Hidden purity leaked in facts"); Check(!Quote(fake.State).RecoveryValueConfirmed, "Claimed recovery mislabeled confirmed");
            InspectionV2.Discover(fake.State, "deal-1", "plating_edge"); Equal(0L, Quote(fake.State).RecoveryValue, "Fake floor survived discovery"); Equal(0, Catalog.PublicFacts(fake.Asset).PurityBasisPoints, "Known material not projected");
            InspectionV2.Discover(fake.State, "deal-1", "clasp_break"); Equal(0L, Quote(fake.State).Price, "Fake clamped to claimed gold floor");
        });
        Test("quote stamps invalidate on asset, selected evidence, day and market", () => {
            var s = Sample(); var quote = Quote(s.State); Check(ValuationV2.IsCurrent(s.State, quote, 0, 1, "buy"), "Fresh quote invalid");
            Check(!ValuationV2.IsCurrent(s.State, quote, 1, 1, "buy") && !ValuationV2.IsCurrent(s.State, quote, 0, 2, "buy") && !ValuationV2.IsCurrent(s.State, quote, 0, 1, "sell"), "Quote reused across external snapshot");
            InspectionV2.Discover(s.State, "deal-1", "plating_edge"); Check(!ValuationV2.IsCurrent(s.State, quote, 0, 1, "buy"), "Old quote survived evidence");
            quote = Quote(s.State);
            quote = Quote(s.State); Catalog.Transfer(s.State, s.Asset.AssetId, "player", "customer-b"); Check(!ValuationV2.IsCurrent(s.State, quote, 0, 1, "buy"), "Old quote survived ownership");
        });
        Test("repair preserves identity, deduplicates cost and invalidates valuation", () => {
            var s = Sample("gold_bracelet_worn"); foreach (var h in Catalog.GetDefinition(s.Asset.DefinitionId).Hotspots) InspectionV2.Discover(s.State, "deal-1", h.Id);
            var quote = Quote(s.State); var id = s.Asset.AssetId; var repaired = Catalog.Repair(s.State, id, "repair-1", 100);
            Check(repaired.Changed && repaired.CashCost == 60, "Repair missing fee or changes"); Equal(id, s.Asset.AssetId, "Repair recreated asset"); Equal(2, s.Asset.ResolvedCauseIds.Count, "Repair causes not deduplicated");
            Check(!ValuationV2.IsCurrent(s.State, quote, 0, 1, "buy"), "Repair did not invalidate quote"); Equal(0L, Quote(s.State).EvidenceDeduction, "Repaired defects still deducted");
            var replay = Catalog.Repair(s.State, id, "repair-1", 0); Check(!replay.Changed && replay.CashCost == 0, "Repeated repair double charged"); Equal(0, s.State.Experience, "Repair reset discovery XP");
        });
        Test("repair payment failure leaves complete state unchanged", () => {
            var s = Sample("console_damaged"); InspectionV2.Discover(s.State, "deal-1", "console_port"); var before = Saved(s.State);
            Throws(() => Catalog.Repair(s.State, s.Asset.AssetId, "repair", 0), "Unfunded repair accepted"); Equal(before, Saved(s.State), "Unfunded repair partially mutated");
        });
        Test("workshop repair updates art without awarding undiscovered evidence", () => {
            var s = Sample("gold_bracelet_worn"); InspectionV2.Discover(s.State, "deal-1", "surface_wear"); Catalog.Repair(s.State, s.Asset.AssetId, "all-workshop", 100);
            Equal("starpawn:gold_bracelet_clean", Catalog.TextureFor(s.Asset), "Fixed object retains damaged art"); Equal(0, Catalog.ActiveHotspots(s.Asset).Count, "Fixed defects remain clickable"); Equal(0, s.State.Experience, "Workshop awarded unseen evidence XP"); Equal(1, s.Asset.DiscoveredEvidenceIds.Count, "Workshop manufactured observation history");
            var fake = Sample(); InspectionV2.Discover(fake.State, "deal-1", "clasp_break"); Catalog.Repair(fake.State, fake.Asset.AssetId, "clasp-workshop", 100);
            Equal("starpawn:gold_bracelet_plated_repaired", Catalog.TextureFor(fake.Asset), "Counterfeit repair chose genuine gold art"); Equal(2, Catalog.ActiveHotspots(fake.Asset).Count, "Repair erased unrepairable counterfeit signs");
        });
        Test("silver jewellery and bullion share real material economics", () => {
            var bracelet = Sample("silver_bracelet_worn"); InspectionV2.Discover(bracelet.State, "deal-1", "silver_clasp"); var quote = Quote(bracelet.State, direction:"sell");
            Check(quote.Price >= ValuationV2.MetalValue(22000, 9250, 4), "Silver recycling floor violated"); Check(quote.MaterialDescription.Contains("22 g"), "Silver gram mass missing");
            var bar = Sample("silver_bar_suspect"); var before = Quote(bar.State); Check(!before.RecoveryValueConfirmed && before.RecoveryValue > 0, "Missing declared bullion estimate"); InspectionV2.Discover(bar.State, "deal-1", "silver_bar_seam"); Equal(0L, Quote(bar.State).RecoveryValue, "Fake silver retains real silver floor");
        });
        Test("seeded jade survives save and alternative views with fixed child lineage", () => {
            var s = Sample("jade_rough_cracked"); var beforeFacts = Catalog.PublicFacts(s.Asset); Check(beforeFacts.JadeDescription.Contains("尚未"), "Jade interior leaked");
            var copy = Reload(s.State); Catalog.OpenJadeWindow(s.State, s.Asset.AssetId, 100); Check(Catalog.PublicFacts(s.Asset).JadeDescription.Contains("开窗可见"), "Window did not reveal state");
            Equal(Catalog.GetAsset(copy, s.Asset.AssetId).JadeGrade, s.Asset.JadeGrade, "Window rerolled quality");
            var a = Catalog.CutJade(s.State, s.Asset.AssetId, "pendants", 100); var b = Catalog.CutJade(copy, s.Asset.AssetId, "pendants", 100); Equal(string.Join(",", a.CreatedAssetIds), string.Join(",", b.CreatedAssetIds), "Reload changed generated identities");
            Equal("consumed", s.Asset.Status, "Cut input retained for resale"); Equal(s.Asset.JadeYieldCount, a.CreatedAssetIds.Count, "Cut output count rerolled");
            foreach (var childId in a.CreatedAssetIds) { var child = Catalog.GetAsset(s.State, childId); Equal(s.Asset.AssetId, child.ParentAssetId, "Missing parent lineage"); Equal(Catalog.GetAsset(copy, childId).QualityBasisPoints, child.QualityBasisPoints, "Reload rerolled child quality"); }
            var count = s.State.Assets.Count; Throws(() => Catalog.CutJade(s.State, s.Asset.AssetId, "pendants", 100), "Consumed rough cut twice"); Equal(count, s.State.Assets.Count, "Duplicate cut outputs"); Catalog.Validate(Reload(s.State));
        });
        Test("jade processing failures are atomic", () => {
            var s = Sample("jade_rough"); var before = Saved(s.State); Throws(() => Catalog.OpenJadeWindow(s.State, s.Asset.AssetId, 0), "Unfunded window accepted"); Equal(before, Saved(s.State), "Partial window mutation");
            Throws(() => Catalog.CutJade(s.State, s.Asset.AssetId, "pendants", 0), "Unfunded cut accepted"); Equal(before, Saved(s.State), "Partial cut mutation");
            s.State.NextAssetSequence = long.MaxValue; before = Saved(s.State); Throws(() => Catalog.CutJade(s.State, s.Asset.AssetId, "pendants", 100), "Overflow cut accepted"); Equal(before, Saved(s.State), "Overflow consumed input");
        });
        Test("escrow prevents change and sale quotes", () => {
            var s = Sample("jade_rough"); s.Asset.Status = "escrow"; var before = Saved(s.State);
            Throws(() => Catalog.OpenJadeWindow(s.State, s.Asset.AssetId, 100), "Escrow window allowed"); Throws(() => Catalog.CutJade(s.State, s.Asset.AssetId, "pendants", 100), "Escrow cut allowed");
            Throws(() => Catalog.Transfer(s.State, s.Asset.AssetId, "player", "buyer"), "Escrow transfer allowed"); Throws(() => Quote(s.State), "Escrow sale quote allowed"); Equal(before, Saved(s.State), "Escrow modified");
        });
        Test("publication work, edition and physical copy stay distinct", () => {
            var s = Sample("book_reprint"); var second = Catalog.CreateAsset(s.State, "book_reprint"); var original = Catalog.CreateAsset(s.State, "book");
            Check(second.AssetId != s.Asset.AssetId, "Books reused physical ID"); Equal(Catalog.PublicFacts(original).WorkId, Catalog.PublicFacts(second).WorkId, "Same work identity differs");
            Equal("2241 初版", Catalog.PublicFacts(s.Asset).Edition, "Hidden reprint edition leaked"); InspectionV2.Discover(s.State, "deal-1", "reprint_mark"); Equal("2246 重印版", Catalog.PublicFacts(s.Asset).Edition, "Observed edition did not reveal");
            var xp = s.State.Experience; Check(Catalog.ReadBook(s.State, s.Asset.AssetId).Message.Length > 20, "No reading text"); Check(!Catalog.ReadBook(s.State, s.Asset.AssetId).Changed, "Repeat reading mutates state"); Equal(xp, s.State.Experience, "Reading farms inspection XP");
        });
        Test("certificates bind scope and asset and invalidate stale quote", () => {
            var s = Sample(); var quote = Quote(s.State); var cert = Catalog.IssueCertificate(s.State, s.Asset.AssetId, "material", 2, 100);
            Check(cert.Changed && cert.CashCost == 25, "Certificate fee missing"); Check(!ValuationV2.IsCurrent(s.State, quote, 0, 1, "buy"), "Certification did not invalidate old price"); Equal(0, Catalog.PublicFacts(s.Asset).PurityBasisPoints, "Certificate material not known");
            Equal(s.Asset.AssetId, s.Asset.Certificates.Single().AssetId, "Certificate not bound to item"); Equal("material", s.Asset.Certificates.Single().Scope, "Certificate scope broadened");
            Check(!Catalog.IssueCertificate(s.State, s.Asset.AssetId, "material", 2, 0).Changed, "Same certificate double charged"); Throws(() => Catalog.IssueCertificate(s.State, s.Asset.AssetId, "edition", 2, 100), "Invalid scope accepted"); Catalog.Validate(Reload(s.State));
        });
        Test("corrupt state, wrong evidence and duplicate native binding fail closed", () => {
            var s = Sample(); var original = Saved(s.State); s.State.SchemaVersion = 99; Throws(() => Catalog.Validate(s.State), "Future catalog accepted"); s.State.SchemaVersion = 1;
            s.Asset.RewardedEvidenceIds.Add("not-defined"); Throws(() => Catalog.Validate(s.State), "Unknown rewarded evidence accepted"); s.Asset.RewardedEvidenceIds.Clear();
            var other = Catalog.CreateAsset(s.State, "book"); s.Asset.NativeItemUniqueId = other.NativeItemUniqueId = 77; Throws(() => Catalog.Validate(s.State), "One native item bound twice");
            var clean = JsonSerializer.Deserialize<CatalogState>(original, Json)!; Catalog.Validate(clean);
        });
        Test("corrupt asset ancestry cycles fail validation", () => {
            var state = Catalog.CreateState("cycle"); var a = Catalog.CreateAsset(state, "jade_pendant"); var b = Catalog.CreateAsset(state, "jade_pendant"); a.ParentAssetId = b.AssetId; b.ParentAssetId = a.AssetId;
            Throws(() => Catalog.Validate(state), "Cyclic parent asset records accepted");
        });
        Test("completed encounter cannot be reused and cannot quote", () => {
            var s = Sample(); InspectionV2.EndEncounter(s.State, "deal-1"); Throws(() => InspectionV2.Open(s.State, s.Asset.AssetId, "deal-1", "customer-a"), "Completed deal reopened"); Throws(() => Quote(s.State), "Completed deal quoted");
        });
        Test("content packs reject missing art, duplicate IDs, absent text keys and bad geometry", () => {
            var items = JsonSerializer.Deserialize<List<ItemDefinition>>(JsonSerializer.Serialize(Catalog.Definitions, Json), Json)!;
            var pack = new ContentPack { Items = items }; var localization = ContentPacks.CreateDefaultLocalization(items); var rules = new InspectionRules();
            Equal(items.Count, ContentPacks.ValidateAndMerge(new[] { pack }, rules, localization, _ => true).Count, "Valid content pack rejected");
            Throws(() => ContentPacks.ValidateAndMerge(new[] { pack }, rules, localization, _ => false), "Missing image accepted");
            Throws(() => ContentPacks.ValidateAndMerge(new[] { pack, pack }, rules, localization), "Duplicate pack accepted");
            var key = items[0].NameKey; items[0].NameKey = "missing.name"; Throws(() => ContentPacks.ValidateAndMerge(new[] { pack }, rules, localization), "Missing localized text accepted"); items[0].NameKey = key;
            var hotspot = items.SelectMany(d => d.Hotspots).First(); hotspot.U = 2; Throws(() => ContentPacks.ValidateAndMerge(new[] { pack }, rules, localization), "Off-image hotspot accepted");
        });
        Test("localization uses stable keys and one-pass inline substitution without gameplay changes", () => {
            var state = Catalog.CreateState("localization"); var before = Saved(state);
            TextResolver.Configure(new LocalizationPack { Language = "en", Entries = new List<LocalizedText> { new LocalizedText { Key = "ui.bank", Source = "银行", Text = "Bank" }, new LocalizedText { Key = "ui.bank_title", Source = "星港银行", Text = "Starport Bank" } } });
            Equal("Starport Bank · Bank", TextResolver.LocalizeInline("星港银行 · 银行"), "Overlapping inline translations not matched longest-first"); Equal("Bank", TextResolver.Get("ui.bank"), "Text key lookup failed"); Equal(before, Saved(state), "Language change creates gameplay obligation");
            TextResolver.Configure(ContentPacks.CreateDefaultLocalization(Catalog.Definitions));
        });
        Console.WriteLine("CONTENT TESTS PASSED: " + passed);
        if (args.Length > 0) ExportContract(args[0]);
        if (args.Length > 1 && args[1] == "export") ExportConfiguration(Path.GetDirectoryName(args[0])!);
    }

    private static void ExportContract(string path)
    {
        var text = new StringBuilder();
        text.AppendLine("StarPawn content / inspection contract (pure netstandard2.1; all class members are public serializable fields)");
        text.AppendLine("UI must display Catalog.PublicFacts(asset), never hidden truth fields on ItemDefinition / AssetRecord. Quote.MaterialValue and CraftPremium split material and workmanship. RecoveryValueConfirmed=false means a claimed estimate, never a verified guarantee.");
        text.AppendLine("All coordinates are normalized in the actual visible image rectangle, x left-to-right, y top-to-bottom. Letterbox margins must not be mapped into the artwork.");
        text.AppendLine("Only actual deal termination calls EndEncounter; closing the inspector preserves customer+asset encounter, misses, lock and selected evidence. Auto-discovery is restricted to observable current view.");
        text.AppendLine("All CashCost actions require Changed=true then a single atomic root-state debit+save. Root must clone state or rollback on native debit/persistence failure. Transactions use AssetQuote snapshot and IsCurrent before commit.");
        foreach (var type in new[] { typeof(Catalog), typeof(InspectionV2), typeof(ValuationV2) })
        {
            text.AppendLine(); text.AppendLine(type.FullName);
            foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(m => !m.IsSpecialName))
                text.AppendLine(method.ReturnType.Name + " " + method.Name + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name + (p.IsOptional ? " = " + (p.DefaultValue ?? "null") : ""))) + ")");
        }
        text.AppendLine(); text.AppendLine("CONTENT AND ART KEYS");
        foreach (var d in Catalog.Definitions)
        {
            text.AppendLine(d.Id + " | " + d.Name + " | " + d.TextureKey + (d.WindowTextureKey.Length > 0 ? " | " + d.WindowTextureKey : "") + (d.RepairedTextureKey.Length > 0 ? " | repaired=" + d.RepairedTextureKey : ""));
            foreach (var h in d.Hotspots) text.AppendLine("  " + h.Id + " | " + h.Label + " | " + h.ViewId + " | u=" + h.U + ",v=" + h.V + ",w=" + h.Width + ",h=" + h.Height + " | cause=" + h.CauseId);
        }
        File.WriteAllText(path, text.ToString());
    }

    private static void ExportConfiguration(string deliveryFolder)
    {
        var folder = Path.Combine(deliveryFolder, "mod", "Content"); Directory.CreateDirectory(folder); Directory.CreateDirectory(Path.Combine(folder, "Localization"));
        var formatted = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        void Write(string name, object value) => File.WriteAllText(Path.Combine(folder, name), JsonSerializer.Serialize(value, formatted));
        var pack = new ContentPack { Items = Catalog.Definitions.ToList() }; var localization = ContentPacks.CreateDefaultLocalization(pack.Items);
        var knownSources = new HashSet<string>(localization.Entries.Select(e => e.Source), StringComparer.Ordinal);
        foreach (var filename in Directory.GetFiles(Path.Combine(deliveryFolder, "source"), "*.cs", SearchOption.AllDirectories).Where(p => !p.EndsWith(Path.DirectorySeparatorChar + "Inspection.cs", StringComparison.Ordinal)))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(filename), "\"(?:\\\\.|[^\"\\\\])*\""))
            {
                var literal = match.Value.Substring(1, match.Length - 2);
                if (!Regex.IsMatch(literal, "[\u3400-\u9fff]")) continue;
                string source; try { source = Regex.Unescape(literal); } catch (ArgumentException) { continue; }
                if (source.Length == 0 || !knownSources.Add(source)) continue;
                uint hash = 2166136261; foreach (var c in source) hash = unchecked((hash ^ c) * 16777619);
                localization.Entries.Add(new LocalizedText { Key = "ui." + hash.ToString("x8"), Source = source, Text = source });
            }
        }
        ContentPacks.ValidateAndMerge(new[] { pack }, new InspectionRules(), localization);
        Write("items.json", pack); Write("inspection-rules.json", new InspectionRules()); Write("finance-rules.json", new EconomyRules()); Write("settings.json", new ContentSettings()); Write("Localization/zh-CN.json", localization);
        var english = new LocalizationPack { Language = "en", Entries = localization.Entries.Where(e => e.Source == "星港大厅" || e.Source == "我的货物" || e.Source == "收起").Select(e => new LocalizedText { Key = e.Key, Source = e.Source, Text = e.Source == "星港大厅" ? "Starport desk" : e.Source == "我的货物" ? "My items" : "Close" }).ToList() };
        Write("Localization/en.json", english);
        var docs = Path.Combine(deliveryFolder, "docs"); Directory.CreateDirectory(docs);
        var example = JsonSerializer.Deserialize<ItemDefinition>(JsonSerializer.Serialize(Catalog.GetDefinition("silver_bracelet"), Json), Json)!;
        example.Id = "silver_bracelet_engraved_example"; example.Name = "月轨银镯 · 纪念刻字版"; example.NameKey = "content.silver_bracelet_engraved_example.name"; example.DescriptionKey = "content.silver_bracelet_engraved_example.description"; example.CraftValue = 155;
        File.WriteAllText(Path.Combine(docs, "example-extension.json"), JsonSerializer.Serialize(new ContentPack { PackId = "starpawn.example.engraved", Items = new List<ItemDefinition> { example } }, formatted));
        File.WriteAllText(Path.Combine(docs, "example-extension-localization.json"), JsonSerializer.Serialize(ContentPacks.CreateDefaultLocalization(new[] { example }), formatted));
        Console.WriteLine("EXPORTED " + pack.Items.Count + " definitions and " + localization.Entries.Count + " localization keys to " + folder);
    }
}
