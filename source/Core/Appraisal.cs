using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StarPawn.Core
{
    public sealed class HandbookEntry
    {
        public ItemDefinition Example = null!;
        public HotspotDefinition Evidence = null!;
    }

    public static class Appraisal
    {
        public static readonly string[] InstrumentKinds = { "scale", "assay", "loupe", "jade_light", "uv", "gem_probe" };
        public static readonly string[] ManualCategories = { "metals", "jade", "antiques", "art", "electronics", "media", "books" };
        public const int MaxHandbookPages = 5;
        public const int HandbookExamplesPerPage = 2;
        public const int HandbookIndexRows = 5;

        public static string Category(ItemDefinition definition)
        {
            if (!string.IsNullOrEmpty(definition.ManualCategory)) return definition.ManualCategory;
            if (definition.Material == "gold" || definition.Material == "silver") return "metals";
            if (definition.Category.StartsWith("翡翠", StringComparison.Ordinal)) return "jade";
            switch (definition.Category)
            {
                case "古董": return "antiques";
                case "字画": return "art";
                case "主机": case "掌机": return "electronics";
                case "卡带": case "光盘": return "media";
                default: return "books";
            }
        }

        public static string Family(ItemDefinition definition) => string.IsNullOrEmpty(definition.FamilyId) ? definition.Id : definition.FamilyId;

        // Pure deterministic selection of a pre-authored profile. Saved DefinitionId is the
        // complete defect combination; opening UI, inspecting and loading never roll anything.
        public static ItemDefinition ChooseProfile(string runId, int day, IEnumerable<ItemDefinition> candidates)
        {
            if (string.IsNullOrEmpty(runId) || day < 0) throw new ArgumentException("A run and day are required.");
            var choices = candidates.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (choices.Count == 0 || choices.Select(Family).Distinct().Count() != 1) throw new ArgumentException("Choose from one product family.");
            uint hash = 2166136261;
            foreach (var c in runId + ":" + day.ToString(CultureInfo.InvariantCulture) + ":" + Family(choices[0])) hash = unchecked((hash ^ c) * 16777619);
            return choices[(int)(hash % (uint)choices.Count)];
        }

        public static IReadOnlyList<HandbookEntry> Handbook(string category)
        {
            if (!ManualCategories.Contains(category)) throw new ArgumentException("Unknown manual category.");
            return Catalog.Definitions.Where(d => !d.ShopOnly && Category(d) == category)
                .SelectMany(d => d.Hotspots.Select(h => new HandbookEntry { Example = d, Evidence = h }))
                .GroupBy(e => string.IsNullOrEmpty(e.Evidence.TemplateId) ? e.Evidence.CauseId : e.Evidence.TemplateId)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.First()).ToList().AsReadOnly();
        }

        public static IReadOnlyList<ItemDefinition> HandbookVolumes(string category) => Catalog.Definitions
            .Where(d => d.ManualFor == category && d.HandbookTemplates.Count > 0)
            .OrderBy(d => d.Id, StringComparer.Ordinal).ToList().AsReadOnly();

        public static bool IsHandbookCollection(ItemDefinition definition) =>
            !string.IsNullOrEmpty(definition.ManualFor) && definition.HandbookTemplates.Count == 0 &&
            HandbookVolumes(definition.ManualFor).Count > 0;

        public static IReadOnlyList<HandbookEntry> Handbook(ItemDefinition manual)
        {
            var entries = Handbook(manual.ManualFor);
            return manual.HandbookTemplates.Count == 0 ? entries : entries
                .Where(e => manual.HandbookTemplates.Contains(e.Evidence.TemplateId)).ToList().AsReadOnly();
        }

        // Includes the opening explanation page; there is no hidden overflow section.
        public static int HandbookPageCount(ItemDefinition manual) => IsHandbookCollection(manual)
            ? Math.Max(1, (HandbookVolumes(manual.ManualFor).Count + HandbookIndexRows - 1) / HandbookIndexRows)
            : 1 + (Handbook(manual).Count + HandbookExamplesPerPage - 1) / HandbookExamplesPerPage;

        // Routing depends only on the public product family, never its hidden defects.
        public static ItemDefinition HandbookFor(ItemDefinition item)
        {
            var category = Category(item);
            return HandbookVolumes(category).FirstOrDefault(d => d.HandbookFamilies.Contains(Family(item))) ??
                Catalog.Definitions.First(d => d.ManualFor == category && d.HandbookTemplates.Count == 0);
        }

        public static bool CanReadHandbook(ItemDefinition owned, ItemDefinition requested) =>
            owned.IsBook && !string.IsNullOrEmpty(owned.ManualFor) && owned.ManualFor == requested.ManualFor &&
            (owned.HandbookTemplates.Count == 0 || owned.Id == requested.Id);

        public static string InstrumentName(string kind)
        {
            switch (kind)
            {
                case "scale": return "精密电子秤";
                case "assay": return "贵金属成色分析仪";
                case "loupe": return "十倍珠宝镜";
                case "jade_light": return "强光透玉灯";
                case "uv": return "紫外检测灯";
                case "gem_probe": return TextResolver.Get("sp5.tool.gem_probe", "Gem screening probe");
                default: throw new ArgumentException("Unknown instrument.");
            }
        }

        public static bool CanMeasure(ItemDefinition definition, string kind)
        {
            if (definition.ShopOnly) return false;
            switch (kind)
            {
                case "scale": return definition.WeightMilligrams > 0;
                case "assay": return definition.Material == "gold" || definition.Material == "silver";
                case "loupe": return true;
                case "jade_light": return definition.IsJadeRough || Category(definition) == "jade";
                case "uv": return Category(definition) == "jade" || Category(definition) == "antiques" || Category(definition) == "art" || definition.Role == "gem";
                case "gem_probe": return definition.Role == "gem" && definition.GemFamily != "pearl";
                default: return false;
            }
        }

        public static string Reading(AssetRecord asset, string kind)
        {
            var d = Catalog.GetDefinition(asset.DefinitionId);
            if (!asset.Measurements.Contains(kind)) return "尚未检测。";
            if (kind == "scale") return "净重 " + (d.WeightMilligrams / 1000m).ToString("0.###", CultureInfo.InvariantCulture) + " 克；成色需另行检测。";
            if (kind == "assay") return "实测贵金属含量 " + (d.ActualPurityBasisPoints / 100m).ToString("0.00", CultureInfo.InvariantCulture) + "% · " + (d.WeightMilligrams / 1000m).ToString("0.###", CultureInfo.InvariantCulture) + " 克。此读数已纳入材料估价。";
            if (kind == "jade_light")
            {
                if (!d.IsJadeRough) return "透光观察已记录；留意裂隙、颜色聚集和棉絮，对照翡翠手册判断。";
                // Skin observation provides a broad, deliberately overlapping signal. It
                // never reveals grade/yield or changes the already persisted cutting seed.
                var signal = (unchecked((uint)asset.JadeSeed) % 3 + (uint)asset.JadeGrade) % 3;
                return new[] { "局部透光弱，皮壳厚度也可能遮挡内部。", "边缘有青色光晕，内部连续性仍不确定。", "细窄色带透光，需开窗进一步观察。" }[signal] + "产量须切割后确认。";
            }
            if (kind == "gem_probe")
                return d.GemOrigin == "imitation"
                    ? TextResolver.Get("sp5.probe.refer", "Screening: refer for laboratory testing.")
                    : TextResolver.Get("sp5.probe.pass", "Screening: no obvious substitute detected. This does not confirm natural origin or treatment.");
            var evidence = d.Hotspots.Where(h => h.InstrumentKind == kind && asset.DiscoveredEvidenceIds.Contains(h.Id) && !asset.ResolvedCauseIds.Contains(h.CauseId))
                .GroupBy(h => h.CauseId, StringComparer.Ordinal).Select(g => g.First().Label).Distinct().ToList();
            return evidence.Count == 0 ? (kind == "uv" ? "未见本手册列出的异常荧光；不能据此证明真品。" : "镜下未见本手册列出的细部异常；仍可检查其他部位。") : "记录：" + string.Join("、", evidence) + "。请结合手册和其他证据判断。";
        }

        public static AssetActionResult Measure(CatalogState state, string encounterId, string instrumentAssetId)
        {
            Catalog.Validate(state);
            var encounter = InspectionV2.GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) throw new InvalidOperationException("顾客已收回物品，本次不能继续检测。");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var tool = Catalog.GetAsset(state, instrumentAssetId);
            var kind = Catalog.GetDefinition(tool.DefinitionId).InstrumentKind;
            if (tool.OwnerId != "player" || tool.Status != "active" || !InstrumentKinds.Contains(kind)) throw new InvalidOperationException("需要持有未托管的对应检测仪器。");
            var d = Catalog.GetDefinition(asset.DefinitionId);
            if (asset.Status != "active" || !CanMeasure(d, kind)) throw new InvalidOperationException("此仪器不适用于这件物品。");
            var found = d.Hotspots.Where(h => h.InstrumentKind == kind && h.ViewId == encounter.ViewId && !asset.ResolvedCauseIds.Contains(h.CauseId) && !asset.DiscoveredEvidenceIds.Contains(h.Id)).ToList();
            var firstMeasurement = !asset.Measurements.Contains(kind);
            if (!firstMeasurement && found.Count == 0) return new AssetActionResult { Message = Reading(asset, kind) };
            var version = checked(asset.StateVersion + 1);
            var revision = checked(encounter.Revision + 1);
            if (firstMeasurement) asset.Measurements.Add(kind);
            foreach (var h in found)
            {
                asset.DiscoveredEvidenceIds.Add(h.Id);
                if (!asset.RewardedEvidenceIds.Contains(h.Id)) asset.RewardedEvidenceIds.Add(h.Id);
            }
            if (found.Count > 0) encounter.ConsecutiveMisses = 0;
            asset.StateVersion = version; encounter.Revision = revision;
            // A reading is an observation, never a second career reward.
            var result = new AssetActionResult { Changed = true, Message = Reading(asset, kind) };
            result.NewEvidenceIds.AddRange(found.Select(h => h.Id));
            return result;
        }

        public static void ValidateTemplates(IEnumerable<ItemDefinition> definitions)
        {
            var items = definitions.ToList();
            var ids = new HashSet<string>(items.Select(d => d.Id));
            foreach (var d in items)
            {
                if (d.HandbookTemplates == null || d.HandbookFamilies == null) throw new InvalidOperationException("Missing handbook lists.");
                if (!string.IsNullOrEmpty(d.ManualFor) && (!d.IsBook || !ManualCategories.Contains(d.ManualFor))) throw new InvalidOperationException("Invalid appraisal manual.");
                if (d.HandbookTemplates.Count > 0 && (string.IsNullOrEmpty(d.ManualFor) || d.HandbookFamilies.Count == 0 ||
                    d.HandbookTemplates.Count > (MaxHandbookPages - 1) * HandbookExamplesPerPage ||
                    d.HandbookTemplates.Distinct().Count() != d.HandbookTemplates.Count ||
                    d.HandbookFamilies.Distinct().Count() != d.HandbookFamilies.Count))
                    throw new InvalidOperationException("Invalid or oversized handbook volume.");
                if (d.IsJadeRough)
                    foreach (var output in new[] { d.JadePendantOutput, d.JadeCabochonOutput, d.JadeBangleOutput, d.JadeBeadsOutput }.Where(s => !string.IsNullOrEmpty(s)))
                        if (!ids.Contains(output) || items.Single(i => i.Id == output).IsJadeRough) throw new InvalidOperationException("Missing or recursive jade output.");
                foreach (var h in d.Hotspots)
                    if (!string.IsNullOrEmpty(h.InstrumentKind) && !InstrumentKinds.Contains(h.InstrumentKind)) throw new InvalidOperationException("Invalid evidence instrument.");
            }
            foreach (var g in items.SelectMany(d => d.Hotspots).Where(h => !string.IsNullOrEmpty(h.TemplateId)).GroupBy(h => h.TemplateId))
            {
                var first = g.First();
                if (string.IsNullOrWhiteSpace(first.Guidance)) throw new InvalidOperationException("A fixed defect template needs handbook guidance.");
                if (g.Any(h => h.Label != first.Label || h.Guidance != first.Guidance || h.Repairable != first.Repairable || h.RevealsMaterial != first.RevealsMaterial || h.RevealsAuthenticity != first.RevealsAuthenticity || h.InstrumentKind != first.InstrumentKind))
                    throw new InvalidOperationException("Defect template properties differ: " + g.Key);
            }
            foreach (var category in ManualCategories)
            {
                var products = items.Where(d => !d.ShopOnly && Category(d) == category).ToList();
                var expected = new HashSet<string>(products.SelectMany(d => d.Hotspots).Select(h => h.TemplateId));
                var volumes = items.Where(d => d.ManualFor == category && d.HandbookTemplates.Count > 0).ToList();
                if (volumes.Count == 0)
                {
                    if (items.Any(d => d.ManualFor == category) && expected.Count > (MaxHandbookPages - 1) * HandbookExamplesPerPage)
                        throw new InvalidOperationException("Long handbook must be split into volumes.");
                    continue;
                }
                if (volumes.Count > MaxHandbookPages * HandbookIndexRows)
                    throw new InvalidOperationException("Handbook collection index exceeds five pages.");
                var covered = volumes.SelectMany(d => d.HandbookTemplates).ToList();
                if (covered.Distinct().Count() != covered.Count || !expected.SetEquals(covered))
                    throw new InvalidOperationException("Handbook volumes omit or duplicate fixed templates.");
                foreach (var volume in volumes)
                    if (volume.HandbookFamilies.Any(f => !products.Any(d => Family(d) == f)))
                        throw new InvalidOperationException("Handbook refers to an unknown or foreign family.");
                foreach (var family in products.GroupBy(Family))
                {
                    var matches = volumes.Where(v => v.HandbookFamilies.Contains(family.Key)).ToList();
                    // An extension with no authored clues can use the collection index.
                    // Defect-bearing families still require one complete fixed volume.
                    if (matches.Count > 1 || (matches.Count == 0 && family.Any(d => d.Hotspots.Count > 0)) ||
                        (matches.Count == 1 && family.SelectMany(d => d.Hotspots).Any(h => !matches[0].HandbookTemplates.Contains(h.TemplateId))))
                        throw new InvalidOperationException("Every product family needs one complete matching handbook.");
                }
            }
        }
    }
}
