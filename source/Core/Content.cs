using System;
using System.Collections.Generic;
using System.Linq;

namespace StarPawn.Core
{
    public sealed class CatalogState
    {
        public int SchemaVersion = 1;
        public string RunId = "";
        public long NextAssetSequence = 1;
        public int Experience;
        public List<AssetRecord> Assets = new List<AssetRecord>();
        public List<InspectionEncounter> Encounters = new List<InspectionEncounter>();
        // Revision 05 business state. Absent in older saves; the initializer keeps them loadable.
        public UnifiedState Unified = new UnifiedState();
    }

    public sealed class AssetRecord
    {
        public string AssetId = "";
        public string DefinitionId = "";
        public string OwnerId = "player";
        public string Status = "active"; // active / escrow / consumed; ownership is separate.
        public int StateVersion = 1;
        public int NativeItemUniqueId = -1;
        public string ParentAssetId = "";
        public List<string> DiscoveredEvidenceIds = new List<string>();
        public List<string> RewardedEvidenceIds = new List<string>();
        public List<string> ResolvedCauseIds = new List<string>();
        public List<string> RepairHistory = new List<string>();
        public List<AssetCertificate> Certificates = new List<AssetCertificate>();
        public int JadeSeed;
        public int JadeGrade;
        public int JadeYieldCount;
        public bool WindowOpened;
        public int QualityBasisPoints = 10000;
        public bool BookRead;
        // Measurements are established observations, persisted with this exact asset.
        public List<string> Measurements = new List<string>();
        // ---- Revision 05
        public string SocketId = "";            // set while Status == "embedded"
        public string CustodyRef = "";          // job / order holding this item ("" = none)
        public string SourceJobId = "";         // provenance of recovered output (not containment)
        public List<string> SourceAssetIds = new List<string>();
        public long GoldMicrograms = -1;        // measured lot content; -1 = use the definition's authored budget
        public long SilverMicrograms = -1;
        public long CopperMicrograms = -1;
    }

    public sealed class AssetCertificate
    {
        public string CertificateId = "";
        public string AssetId = "";
        public string Issuer = "星港检验所";
        public string Scope = "";
        public string Conclusion = "";
        public int IssuedDay;
        public int AssetVersion;
    }

    public sealed class HotspotDefinition
    {
        public string Id = "";
        public string CauseId = "";
        public string Label = "";
        public string LabelKey = "";
        public string ViewId = "front";
        public float U;
        public float V;
        public float Width = .10f;
        public float Height = .10f;
        public long ValueDeduction;
        public bool RevealsMaterial;
        public bool RevealsAuthenticity;
        public bool Repairable;
        public string TemplateId = "";
        public string Guidance = "";
        public string GuidanceKey = "";
        public string InstrumentKind = "";
        // Revision 05: the value component this cause changes. Empty = derived by
        // ComponentValuation.ComponentOf (never a blanket multiplier over the total).
        // workmanship | metal_purity | metal_mass | stone | premium | condition | none
        public string Component = "";
        public long MassLossMilligrams;
    }

    public sealed class ItemDefinition
    {
        public string Id = "";
        public string Name = "";
        public string NameKey = "";
        public string Category = "";
        public string Material = "none";
        public string TextureKey = "";
        public string WindowTextureKey = "";
        public string RepairedTextureKey = "";
        public string WorkId = "";
        public string EditionId = "";
        public string DeclaredEditionId = "";
        public string Description = "";
        public string DescriptionKey = "";
        public string ReadingText = "";
        public string ReadingKey = "";
        public long WeightMilligrams;
        public int DeclaredPurityBasisPoints;
        public int ActualPurityBasisPoints;
        public long CraftValue;
        public long BaseValue;
        public long ActualBaseValue;
        public long RepairCost;
        public long WindowCost;
        public long CutCost;
        public bool IsJadeRough;
        public bool IsBook;
        public bool IsGenuine = true;
        public string FamilyId = "";
        public string Form = "";
        public string ManualCategory = "";
        public string ManualFor = "";
        public List<string> HandbookTemplates = new List<string>();
        public List<string> HandbookFamilies = new List<string>();
        public string InstrumentKind = "";
        public bool ShopOnly;
        public bool IsOrganizer;
        public int WidthCells = 1;
        public int HeightCells = 1;
        // Relative grade weights are rolled only once, at creation. Legacy defaults are unchanged.
        public int JadeLowWeight = 1;
        public int JadeMediumWeight = 1;
        public int JadeHighWeight = 1;
        public string JadePendantOutput = "jade_pendant";
        public string JadeCabochonOutput = "jade_pendant";
        public string JadeBangleOutput = "";
        public string JadeBeadsOutput = "";
        public List<HotspotDefinition> Hotspots = new List<HotspotDefinition>();
        // ---- Revision 05 (all optional; old packs keep working)
        public List<string> Labels = new List<string>();     // Luxury, Collectibles, Materials, Weapons, ...
        public string PrimaryLabel = "";
        public string ArtModel = "";                          // art/ITEM_LAYOUT_05.csv asset_key
        public int CommissionRank = -1;                       // native appraiser rank needed for a paid commission; -1 = never commissioned
        public int InternalWidthCells;                        // containers only
        public int InternalHeightCells;
        public string Role = "";                              // gem | setting | assembly | scrap | ore | lot | bar | machine | tool | container | document
        public string GemFamily = "";                         // diamond | ruby | sapphire | emerald | pearl
        public string GemShape = "";
        public long GemMilliCarats;
        public long GemUnitValue;                             // fictional credits per carat before the day's gem index
        public string GemOrigin = "natural";                  // natural | laboratory_grown | cultured | imitation (saved truth)
        public string GemClaim = "natural";
        public string SettingForm = "";                       // ring | pendant | earrings | bracelet | necklace
        public int SocketCount;
        public List<string> CompatibleStones = new List<string>();
        public string JadeTier = "";                          // dou | nuo | bing for finished jade
        public long GoldMicrograms;                           // finite authored recovery budgets (fictional balance data)
        public long SilverMicrograms;
        public long CopperMicrograms;
        public string RecoveryClass = "";                     // device | component | sorted | ore | lot | bar | residue
        public string ArtState = "";                          // mounted-jewelry art state for this profile ("intact" or a template short name)
    }

    public sealed class AssetActionResult
    {
        public bool Changed;
        public string Message = "";
        public long CashCost;
        public List<string> CreatedAssetIds = new List<string>();
        public List<string> NewEvidenceIds = new List<string>();
    }

    public sealed class PublicAssetFacts
    {
        public string Name = "";
        public string Category = "";
        public string AssetId = "";
        public string WorkId = "";
        public string Edition = "";
        public bool EditionConfirmed;
        public long WeightMilligrams;
        public int PurityBasisPoints;
        public bool MaterialConfirmed;
        public string MaterialDescription = "";
        public string JadeDescription = "";
    }

    public static class Catalog
    {
        public const int MaxAssets = 4096;
        public const int MaxEncounters = 8192;
        private static Dictionary<string, ItemDefinition> definitions = BuildDefinitions();
        public static IReadOnlyList<ItemDefinition> Definitions => definitions.Values.ToList().AsReadOnly();

        public static CatalogState CreateState(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId) || runId.Length > 80) throw new ArgumentException("A bounded run ID is required.");
            return new CatalogState { RunId = runId };
        }

        public static ItemDefinition GetDefinition(string id)
        {
            if (id == null || !definitions.TryGetValue(id, out var definition)) throw new InvalidOperationException("Unknown content definition: " + id);
            return definition;
        }
        public static bool TryGetDefinition(string id, out ItemDefinition definition) => definitions.TryGetValue(id, out definition!);

        // Content packs may deserialize these DTOs at startup. Never reconfigure mid-run.
        public static void Configure(IEnumerable<ItemDefinition> items)
        {
            var replacement = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                ValidateDefinition(item);
                if (replacement.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate content definition.");
                replacement.Add(item.Id, item);
            }
            if (replacement.Count == 0) throw new InvalidOperationException("Empty content catalog.");
            definitions = replacement;
        }

        public static AssetRecord CreateAsset(CatalogState state, string definitionId, string ownerId = "player", int seed = 0)
        {
            Validate(state);
            var definition = GetDefinition(definitionId);
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner is required.");
            if (state.Assets.Count >= MaxAssets) throw new InvalidOperationException("Asset capacity reached; existing records retained.");
            var sequence = state.NextAssetSequence;
            var next = checked(sequence + 1);
            var generatedSeed = seed == 0 ? StableHash(state.RunId + ":" + sequence) : seed;
            var random = unchecked((uint)generatedSeed);
            random = NextRandom(random);
            var gradeRoll = random % (definition.JadeLowWeight + definition.JadeMediumWeight + definition.JadeHighWeight);
            var result = new AssetRecord {
                AssetId = "sp-" + state.RunId + "-" + sequence,
                DefinitionId = definitionId,
                OwnerId = ownerId,
                JadeSeed = definition.IsJadeRough ? generatedSeed : 0,
                JadeGrade = definition.IsJadeRough ? (gradeRoll < definition.JadeLowWeight ? 1 : gradeRoll < definition.JadeLowWeight + definition.JadeMediumWeight ? 2 : 3) : 0,
                JadeYieldCount = definition.IsJadeRough ? 1 + (int)(NextRandom(random) % 3) : 0
            };
            if (state.Assets.Any(a => a.AssetId == result.AssetId)) throw new InvalidOperationException("Asset sequence collides with an existing identity.");
            state.NextAssetSequence = next;
            state.Assets.Add(result);
            return result;
        }

        public static AssetRecord GetAsset(CatalogState state, string assetId)
        {
            if (state == null || state.Assets == null) throw new InvalidOperationException("Missing catalog state.");
            return state.Assets.FirstOrDefault(a => a.AssetId == assetId) ?? throw new InvalidOperationException("Unknown asset: " + assetId);
        }

        public static void Transfer(CatalogState state, string assetId, string expectedOwner, string newOwner)
        {
            Validate(state);
            var asset = GetAsset(state, assetId);
            if (asset.Status != "active" || asset.OwnerId != expectedOwner || string.IsNullOrWhiteSpace(newOwner))
                throw new InvalidOperationException("Asset ownership or custody changed.");
            var next = checked(asset.StateVersion + 1);
            asset.OwnerId = newOwner;
            asset.StateVersion = next;
        }

        public static string TextureFor(AssetRecord asset, string view = "front")
        {
            var definition = GetDefinition(asset.DefinitionId);
            if (view == "window" && asset.WindowOpened && !string.IsNullOrEmpty(definition.WindowTextureKey)) return definition.WindowTextureKey;
            if (view == "front" && !string.IsNullOrEmpty(definition.RepairedTextureKey) && definition.Hotspots.Any(h => h.Repairable) && definition.Hotspots.Where(h => h.Repairable).All(h => asset.ResolvedCauseIds.Contains(h.CauseId))) return definition.RepairedTextureKey;
            return definition.TextureKey;
        }

        // All ordinary UI must use this projection rather than the definition's hidden truth.
        public static PublicAssetFacts PublicFacts(AssetRecord asset)
        {
            var definition = GetDefinition(asset.DefinitionId);
            var discovered = definition.Hotspots.Where(h => asset.DiscoveredEvidenceIds.Contains(h.Id)).ToList();
            var materialKnown = asset.Measurements.Contains("assay") || asset.Certificates.Any(c => c.Scope == "material") || discovered.Any(h => h.RevealsMaterial);
            var editionKnown = asset.Certificates.Any(c => c.Scope == "edition" || c.Scope == "authenticity") || discovered.Any(h => h.RevealsAuthenticity);
            var purity = materialKnown ? definition.ActualPurityBasisPoints : definition.DeclaredPurityBasisPoints;
            var precious = definition.Material == "gold" || definition.Material == "silver";
            return new PublicAssetFacts {
                Name = definition.Name, Category = definition.Category, AssetId = asset.AssetId, WorkId = definition.WorkId,
                Edition = editionKnown || string.IsNullOrEmpty(definition.DeclaredEditionId) ? definition.EditionId : definition.DeclaredEditionId,
                EditionConfirmed = editionKnown, WeightMilligrams = definition.WeightMilligrams, PurityBasisPoints = purity, MaterialConfirmed = materialKnown,
                MaterialDescription = precious ? (materialKnown ? "已核实成色 " : "标称成色 ") + (purity / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "% · " + (definition.WeightMilligrams / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " 克" : definition.Category,
                JadeDescription = definition.IsJadeRough ? (asset.WindowOpened ? "开窗可见：" + new[] { "", "细碎棉絮", "青色水线", "通透翠色" }[asset.JadeGrade] + "；完整产量需切割后确认。" : "皮壳包裹，内部尚未可见。") : ""
            };
        }

        public static bool CanObserve(AssetRecord asset, string view)
        {
            return asset.Status != "consumed" && (view == "front" || (view == "window" && asset.WindowOpened && GetDefinition(asset.DefinitionId).IsJadeRough));
        }

        public static IReadOnlyList<HotspotDefinition> ActiveHotspots(AssetRecord asset, string view = "front")
        {
            if (!CanObserve(asset, view)) return Array.Empty<HotspotDefinition>();
            return GetDefinition(asset.DefinitionId).Hotspots.Where(h => h.ViewId == view && !asset.ResolvedCauseIds.Contains(h.CauseId)).ToList().AsReadOnly();
        }

        public static AssetActionResult Repair(CatalogState state, string assetId, string operationId, long availableCash)
        {
            Validate(state);
            var asset = RequireOwnedActive(state, assetId);
            if (string.IsNullOrWhiteSpace(operationId)) throw new ArgumentException("Repair operation ID is required.");
            if (asset.RepairHistory.Any(h => h.StartsWith(operationId + ":", StringComparison.Ordinal))) return new AssetActionResult { Message = "这次修复已经完成。" };
            var definition = GetDefinition(asset.DefinitionId);
            if (!definition.Hotspots.Any(h => h.Repairable && asset.DiscoveredEvidenceIds.Contains(h.Id) && !asset.ResolvedCauseIds.Contains(h.CauseId))) return new AssetActionResult { Message = "没有已确认且可修复的问题。" };
            // One paid workshop visit services the whole item; undiscovered fixes never grant XP.
            var causes = definition.Hotspots.Where(h => h.Repairable && !asset.ResolvedCauseIds.Contains(h.CauseId)).Select(h => h.CauseId).Distinct().ToList();
            if (availableCash < definition.RepairCost) throw new InvalidOperationException("现金不足以支付修复费。");
            var version = checked(asset.StateVersion + 1);
            if (asset.RepairHistory.Count >= 64) throw new InvalidOperationException("Repair history capacity reached.");
            asset.ResolvedCauseIds.AddRange(causes);
            asset.RepairHistory.Add(operationId + ":" + string.Join(",", causes));
            asset.StateVersion = version;
            return new AssetActionResult { Changed = true, CashCost = definition.RepairCost, Message = "师傅完成了整件检修，可修的损伤已处理。重新看看现在的品相吧。" };
        }

        public static AssetActionResult OpenJadeWindow(CatalogState state, string assetId, long availableCash)
        {
            Validate(state);
            var asset = RequireOwnedActive(state, assetId);
            var definition = GetDefinition(asset.DefinitionId);
            if (!definition.IsJadeRough) throw new InvalidOperationException("Only rough jade can be windowed.");
            if (asset.WindowOpened) return new AssetActionResult { Message = "这块原石已经开过窗。" };
            if (availableCash < definition.WindowCost) throw new InvalidOperationException("现金不足以支付开窗费。");
            var version = checked(asset.StateVersion + 1);
            asset.WindowOpened = true;
            asset.StateVersion = version;
            return new AssetActionResult { Changed = true, CashCost = definition.WindowCost, Message = "师傅磨开一角，灯下露出石料内部的颜色和纹理。" };
        }

        public static AssetActionResult CutJade(CatalogState state, string assetId, string cutMode, long availableCash)
        {
            Validate(state);
            var asset = RequireOwnedActive(state, assetId);
            var definition = GetDefinition(asset.DefinitionId);
            if (!definition.IsJadeRough) throw new InvalidOperationException("Only rough jade can be cut.");
            if (cutMode != "pendants" && cutMode != "cabochon" && cutMode != "bangle" && cutMode != "beads") throw new ArgumentException("Unknown jade cutting plan.");
            if (availableCash < definition.CutCost) throw new InvalidOperationException("现金不足以支付切割费。");
            var count = cutMode == "pendants" ? asset.JadeYieldCount : 1;
            if (state.Assets.Count + count > MaxAssets) throw new InvalidOperationException("Asset capacity reached.");
            checked { _ = state.NextAssetSequence + count; _ = asset.StateVersion + 1; }
            var outputId = cutMode == "pendants" ? definition.JadePendantOutput : cutMode == "cabochon" ? definition.JadeCabochonOutput : cutMode == "bangle" ? definition.JadeBangleOutput : definition.JadeBeadsOutput;
            if (string.IsNullOrEmpty(outputId)) throw new InvalidOperationException("这类原石不支持此切割方案。");
            if (cutMode == "bangle" && asset.JadeYieldCount < 2) throw new InvalidOperationException("开料师傅：这块料尺寸不足，取不出整只手镯，换个方案吧。");
            var outputDefinition = GetDefinition(outputId);
            var children = new List<AssetRecord>();
            for (var i = 0; i < count; i++)
            {
                var identity = "sp-" + state.RunId + "-" + (state.NextAssetSequence + i);
                if (state.Assets.Any(a => a.AssetId == identity)) throw new InvalidOperationException("Asset sequence collision.");
                children.Add(new AssetRecord { AssetId = identity, DefinitionId = outputDefinition.Id, ParentAssetId = asset.AssetId, OwnerId = "player", QualityBasisPoints = asset.JadeGrade * 3500 + (cutMode == "cabochon" ? 4000 : cutMode == "bangle" ? 2000 : 0) });
            }
            asset.Status = "consumed";
            asset.StateVersion++;
            state.NextAssetSequence += count;
            state.Assets.AddRange(children);
            return new AssetActionResult { Changed = true, CashCost = definition.CutCost, CreatedAssetIds = children.Select(c => c.AssetId).ToList(), Message = "师傅收好余料，将 " + count + " 件打磨好的成品放在了柜台上。" };
        }

        public static AssetActionResult ReadBook(CatalogState state, string assetId)
        {
            Validate(state);
            var asset = RequireOwnedActive(state, assetId);
            var definition = GetDefinition(asset.DefinitionId);
            if (!definition.IsBook) throw new InvalidOperationException("This item is not readable.");
            var changed = !asset.BookRead;
            asset.BookRead = true;
            return new AssetActionResult { Changed = changed, Message = definition.ReadingText };
        }

        public static AssetActionResult IssueCertificate(CatalogState state, string assetId, string scope, int gameDay, long availableCash)
        {
            Validate(state);
            var asset = RequireOwnedActive(state, assetId);
            var definition = GetDefinition(asset.DefinitionId);
            if (gameDay < 0) throw new ArgumentOutOfRangeException(nameof(gameDay));
            if (scope != "material" && scope != "authenticity" && scope != "edition") throw new ArgumentException("Unknown certificate scope.");
            if (scope == "material" && definition.Material != "gold" && definition.Material != "silver") throw new InvalidOperationException("This material service only certifies precious metal content.");
            if (scope == "edition" && string.IsNullOrEmpty(definition.EditionId)) throw new InvalidOperationException("This item has no publication edition.");
            if (asset.Certificates.Any(c => c.Scope == scope && c.AssetVersion == asset.StateVersion)) return new AssetActionResult { Message = "已有适用于当前状态的同范围证书。" };
            var fee = InspectionV2.Rules.CertificateFee;
            if (availableCash < fee) throw new InvalidOperationException("现金不足以支付检验费。");
            if (asset.Certificates.Count >= 32) throw new InvalidOperationException("Certificate capacity reached.");
            var version = checked(asset.StateVersion + 1);
            var conclusion = scope == "material" ? "贵金属成色 " + (definition.ActualPurityBasisPoints / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%" : scope == "edition" ? definition.EditionId : (definition.IsGenuine ? "登记范围内真品" : "登记范围内仿品");
            asset.Certificates.Add(new AssetCertificate { CertificateId = asset.AssetId + "-cert-" + (asset.Certificates.Count + 1), AssetId = asset.AssetId, Scope = scope, Conclusion = conclusion, IssuedDay = gameDay, AssetVersion = version });
            asset.StateVersion = version;
            return new AssetActionResult { Changed = true, CashCost = fee, Message = "星港检验所：" + conclusion + "。证书只对所列范围负责。" };
        }

        public static void Validate(CatalogState state)
        {
            if (state == null || state.SchemaVersion != 1 || string.IsNullOrWhiteSpace(state.RunId) || state.RunId.Length > 80 || state.NextAssetSequence < 1 || state.Experience < 0 || state.Assets == null || state.Encounters == null)
                throw new InvalidOperationException("Unsupported or damaged catalog state; retain the original payload.");
            if (state.Assets.Count > MaxAssets || state.Encounters.Count > MaxEncounters) throw new InvalidOperationException("Catalog capacity exceeded.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var nativeIds = new HashSet<int>();
            foreach (var asset in state.Assets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.AssetId) || !ids.Add(asset.AssetId) || string.IsNullOrWhiteSpace(asset.OwnerId) || asset.StateVersion < 1 || asset.NativeItemUniqueId < -1 || asset.QualityBasisPoints < 1 || asset.QualityBasisPoints > 20000)
                    throw new InvalidOperationException("Invalid asset identity or quality.");
                if (asset.Status != "active" && asset.Status != "escrow" && asset.Status != "consumed" && asset.Status != "embedded") throw new InvalidOperationException("Invalid asset status.");
                if (asset.Status == "embedded" && string.IsNullOrEmpty(asset.ParentAssetId)) throw new InvalidOperationException("An embedded stone needs its setting.");
                if (asset.NativeItemUniqueId >= 0 && asset.Status != "consumed" && !nativeIds.Add(asset.NativeItemUniqueId)) throw new InvalidOperationException("Multiple assets refer to one native item.");
                var definition = GetDefinition(asset.DefinitionId);
                if (asset.DiscoveredEvidenceIds == null || asset.RewardedEvidenceIds == null || asset.ResolvedCauseIds == null || asset.RepairHistory == null || asset.Certificates == null || asset.Measurements == null || asset.RepairHistory.Count > 64 || asset.Certificates.Count > 32)
                    throw new InvalidOperationException("Missing asset records.");
                ValidateUnique(asset.Measurements, Appraisal.InstrumentKinds);
                ValidateUnique(asset.DiscoveredEvidenceIds, definition.Hotspots.Select(h => h.Id));
                ValidateUnique(asset.RewardedEvidenceIds, definition.Hotspots.Select(h => h.Id));
                if (asset.RewardedEvidenceIds.Any(id => !asset.DiscoveredEvidenceIds.Contains(id))) throw new InvalidOperationException("Rewarded evidence must remain recorded.");
                ValidateUnique(asset.ResolvedCauseIds, definition.Hotspots.Where(h => h.Repairable).Select(h => h.CauseId).Distinct());
                if (definition.IsJadeRough && (asset.JadeGrade < 1 || asset.JadeGrade > 3 || asset.JadeYieldCount < 1 || asset.JadeYieldCount > 3)) throw new InvalidOperationException("Invalid persisted jade outcome.");
                var certificateIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var certificate in asset.Certificates)
                    if (certificate == null || string.IsNullOrWhiteSpace(certificate.CertificateId) || !certificateIds.Add(certificate.CertificateId) || certificate.AssetId != asset.AssetId || certificate.AssetVersion < 1 || certificate.AssetVersion > asset.StateVersion || certificate.IssuedDay < 0 || (certificate.Scope != "material" && certificate.Scope != "authenticity" && certificate.Scope != "edition")) throw new InvalidOperationException("Invalid certificate binding.");
            }
            foreach (var asset in state.Assets)
                if (!string.IsNullOrEmpty(asset.ParentAssetId) && (!ids.Contains(asset.ParentAssetId) || asset.ParentAssetId == asset.AssetId)) throw new InvalidOperationException("Invalid asset ancestry.");
            var index = state.Assets.ToDictionary(a => a.AssetId, StringComparer.Ordinal);
            foreach (var asset in state.Assets.Where(a => !string.IsNullOrEmpty(a.ParentAssetId)))
            {
                var seen = new HashSet<string>(StringComparer.Ordinal); var current = asset;
                while (!string.IsNullOrEmpty(current.ParentAssetId))
                {
                    if (!seen.Add(current.AssetId)) throw new InvalidOperationException("Cyclic asset ancestry.");
                    current = index[current.ParentAssetId];
                }
            }
            InspectionV2.ValidateEncounters(state);
        }

        internal static void ValidateUnique(IEnumerable<string> values, IEnumerable<string> allowed)
        {
            var known = new HashSet<string>(allowed, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values) if (!known.Contains(value) || !seen.Add(value)) throw new InvalidOperationException("Unknown or duplicate evidence/cause.");
        }

        private static AssetRecord RequireOwnedActive(CatalogState state, string assetId)
        {
            var asset = GetAsset(state, assetId);
            if (asset.OwnerId != "player" || asset.Status != "active") throw new InvalidOperationException("Only an owned, unencumbered asset can be changed.");
            return asset;
        }

        private static int StableHash(string value)
        {
            uint hash = 2166136261;
            foreach (var character in value) hash = unchecked((hash ^ character) * 16777619);
            return unchecked((int)hash);
        }

        private static uint NextRandom(uint value)
        {
            if (value == 0) value = 0x9E3779B9;
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            return value;
        }

        internal static void ValidateDefinition(ItemDefinition item)
        {
            if (item != null && (item.WidthCells < 1 || item.WidthCells > 4 || item.HeightCells < 1 || item.HeightCells > 4 || item.JadeLowWeight < 1 || item.JadeMediumWeight < 1 || item.JadeHighWeight < 1 || item.JadeLowWeight > 100 || item.JadeMediumWeight > 100 || item.JadeHighWeight > 100 || (!string.IsNullOrEmpty(item.InstrumentKind) && !Appraisal.InstrumentKinds.Contains(item.InstrumentKind))))
                throw new InvalidOperationException("Invalid equipment, inventory shape or jade grade weights.");
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.TextureKey) || item.Hotspots == null || item.WeightMilligrams < 0 || item.WeightMilligrams > 100000000 || item.BaseValue < 0 || item.ActualBaseValue < 0 || item.CraftValue < 0 || item.RepairCost < 0 || item.WindowCost < 0 || item.CutCost < 0 || item.ActualPurityBasisPoints < 0 || item.ActualPurityBasisPoints > 10000 || item.DeclaredPurityBasisPoints < 0 || item.DeclaredPurityBasisPoints > 10000)
                throw new InvalidOperationException("Invalid content definition.");
            var ids = new HashSet<string>();
            foreach (var h in item.Hotspots)
                if (h == null || string.IsNullOrWhiteSpace(h.Id) || !ids.Add(h.Id) || string.IsNullOrWhiteSpace(h.CauseId) || h.ValueDeduction < 0 || float.IsNaN(h.U) || float.IsNaN(h.V) || float.IsNaN(h.Width) || float.IsNaN(h.Height) || h.Width <= 0 || h.Height <= 0 || h.U - h.Width / 2 < 0 || h.U + h.Width / 2 > 1 || h.V - h.Height / 2 < 0 || h.V + h.Height / 2 > 1 || (h.ViewId != "front" && h.ViewId != "window")) throw new InvalidOperationException("Invalid normalized hotspot.");
        }

        private static HotspotDefinition H(string id, string cause, string label, float u, float v, long deduction, bool repairable = false, bool material = false, bool authenticity = false, string view = "front")
            => new HotspotDefinition { Id = id, CauseId = cause, Label = label, U = u, V = v, ValueDeduction = deduction, Repairable = repairable, RevealsMaterial = material, RevealsAuthenticity = authenticity, ViewId = view };

        private static Dictionary<string, ItemDefinition> BuildDefinitions()
        {
            var result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            void Add(ItemDefinition item) { ValidateDefinition(item); result.Add(item.Id, item); }
            ItemDefinition Make(string id, string name, string category, long value, string texture) => new ItemDefinition { Id = id, Name = name, Category = category, BaseValue = value, ActualBaseValue = value, TextureKey = "starpawn:" + texture, Description = "星港流通商品；以实物、证据和本日行情为准。" };
            ItemDefinition Metal(string id, string name, string material, long mg, int declared, int actual, long craft, string texture)
            {
                var d = Make(id, name, material == "gold" ? "黄金" : "白银", 0, texture); d.Material = material; d.WeightMilligrams = mg; d.DeclaredPurityBasisPoints = declared; d.ActualPurityBasisPoints = actual; d.CraftValue = craft; d.ActualBaseValue = 30; d.RepairCost = 60; d.IsGenuine = declared == actual; return d;
            }
            Add(Metal("gold_bracelet", "星环足金手镯", "gold", 18000, 9990, 9990, 360, "gold_bracelet_clean"));
            var worn = Metal("gold_bracelet_worn", "星环足金手镯", "gold", 18000, 9990, 9990, 360, "gold_bracelet_worn");
            worn.Hotspots.Add(H("surface_wear", "surface_damage", "表面磨损", .25f, .40f, 90, true));
            worn.Hotspots.Add(H("surface_seam", "surface_damage", "接缝刮痕", .72f, .40f, 90, true));
            worn.Hotspots.Add(H("clasp_break", "clasp_damage", "搭扣断裂", .50f, .18f, 180, true)); Add(worn);
            var plated = Metal("gold_bracelet_plated", "星环足金手镯", "gold", 18000, 9990, 0, 360, "gold_bracelet_plated");
            plated.Hotspots.Add(H("plating_edge", "base_material_mismatch", "镀层露底", .277f, .275f, 0, false, true));
            plated.Hotspots.Add(H("plating_seam", "base_material_mismatch", "接缝露底", .736f, .293f, 0, false, true));
            plated.Hotspots.Add(H("clasp_break", "clasp_damage", "搭扣断裂", .499f, .184f, 180, true));
            plated.Hotspots[0].Width = plated.Hotspots[1].Width = .075f;
            plated.Hotspots[2].Width = .12f;
            foreach (var h in plated.Hotspots) h.Height = .07f;
            Add(plated);
            Add(Metal("gold_bar", "星港认证金条 · 50 克", "gold", 50000, 9999, 9999, 35, "gold_bar_clean"));
            var bar = Metal("gold_bar_suspect", "星港认证金条 · 50 克", "gold", 50000, 9999, 0, 35, "gold_bar_suspect"); bar.Hotspots.Add(H("bar_seam", "bar_filler", "封边露出异色内芯", .72f, .64f, 0, false, true)); bar.Hotspots.Add(H("bar_stamp", "bar_filler", "压印边缘异常", .47f, .42f, 0, false, true)); Add(bar);
            Add(Metal("silver_cup", "月港银杯", "silver", 32000, 9250, 9250, 160, "silver_cup_clean"));
            var cup = Metal("silver_cup_worn", "月港银杯", "silver", 32000, 9250, 9250, 160, "silver_cup_worn"); cup.Hotspots.Add(H("cup_dent", "cup_damage", "杯壁凹陷", .60f, .57f, 100, true)); Add(cup);
            Add(Metal("silver_bracelet", "月轨银手镯", "silver", 22000, 9250, 9250, 130, "silver_bracelet_clean"));
            var silverBracelet = Metal("silver_bracelet_worn", "月轨银手镯", "silver", 22000, 9250, 9250, 130, "silver_bracelet_worn"); silverBracelet.Hotspots.Add(H("silver_clasp", "silver_clasp_damage", "银镯搭扣裂损", .50f, .20f, 95, true)); Add(silverBracelet);
            Add(Metal("silver_bar", "月港认证银条 · 100 克", "silver", 100000, 9990, 9990, 25, "silver_bar_clean"));
            var silverBar = Metal("silver_bar_suspect", "月港认证银条 · 100 克", "silver", 100000, 9990, 0, 25, "silver_bar_suspect"); silverBar.Hotspots.Add(H("silver_bar_seam", "silver_filler", "镀银封边露出铜色内芯", .70f, .60f, 0, false, true)); Add(silverBar);
            var rough = Make("jade_rough", "青岚原石", "翡翠原石", 260, "jade_rough_clean"); rough.IsJadeRough = true; rough.WindowTextureKey = "starpawn:jade_window_clean"; rough.WindowCost = 35; rough.CutCost = 80; Add(rough);
            var cracked = Make("jade_rough_cracked", "青岚原石", "翡翠原石", 260, "jade_rough_cracked"); cracked.IsJadeRough = true; cracked.WindowTextureKey = "starpawn:jade_window_cracked"; cracked.WindowCost = 35; cracked.CutCost = 80; cracked.Hotspots.Add(H("rough_crack", "jade_fissure", "皮壳裂隙", .62f, .45f, 65)); cracked.Hotspots.Add(H("window_crack", "jade_fissure", "开窗处延伸裂纹", .50f, .50f, 65, false, false, false, "window")); Add(cracked);
            Add(Make("jade_pendant", "青岚玉佩", "翡翠成品", 320, "jade_pendant_clean"));
            var dyed = Make("jade_pendant_dyed", "青岚玉佩", "翡翠成品", 320, "jade_pendant_dyed"); dyed.ActualBaseValue = 90; dyed.IsGenuine = false; dyed.Hotspots.Add(H("dye_pool", "jade_treatment", "裂隙染色聚集", .54f, .54f, 0, false, false, true)); Add(dyed);
            Add(Make("antique_vase", "远航纪年陶瓶", "古董", 720, "antique_vase_clean"));
            var vase = Make("antique_vase_repaired", "远航纪年陶瓶", "古董", 720, "antique_vase_repaired"); vase.Hotspots.Add(H("vase_restore", "restoration_history", "釉面修补边界", .63f, .57f, 280)); Add(vase);
            Add(Make("painting", "《港灯》原作", "字画", 900, "painting_clean"));
            var painting = Make("painting_forged", "《港灯》原作", "字画", 900, "painting_forged"); painting.IsGenuine = false; painting.ActualBaseValue = 100; painting.Hotspots.Add(H("seal_mismatch", "painting_authenticity", "印章笔画错位", .76f, .74f, 0, false, false, true)); painting.Hotspots.Add(H("print_dots", "painting_authenticity", "笔触中的规则印点", .39f, .40f, 0, false, false, true)); Add(painting);
            var console = Make("console", "轨道一号游戏机", "主机", 480, "console_clean"); console.WorkId = "orbit-console"; console.EditionId = "星港版第一型"; Add(console);
            var damagedConsole = Make("console_damaged", "轨道一号游戏机", "主机", 480, "console_damaged"); damagedConsole.WorkId = console.WorkId; damagedConsole.EditionId = console.EditionId; damagedConsole.RepairCost = 65; damagedConsole.Hotspots.Add(H("console_port", "console_port_damage", "接口触片弯折", .67f, .62f, 190, true)); damagedConsole.Hotspots.Add(H("console_screw", "console_service_history", "螺丝拆修痕迹", .28f, .42f, 55)); Add(damagedConsole);
            var handheld = Make("handheld", "口袋航线掌机", "掌机", 340, "handheld_clean"); handheld.WorkId = "pocket-star-routes"; handheld.EditionId = "星港便携版"; Add(handheld);
            var brokenHandheld = Make("handheld_damaged", "口袋航线掌机", "掌机", 340, "handheld_damaged"); brokenHandheld.WorkId = handheld.WorkId; brokenHandheld.EditionId = handheld.EditionId; brokenHandheld.RepairCost = 45; brokenHandheld.Hotspots.Add(H("handheld_port", "handheld_port_damage", "掌机接口触片弯折", .72f, .60f, 140, true)); Add(brokenHandheld);
            var cartridge = Make("cartridge", "《环轨快递》卡带", "卡带", 180, "cartridge_clean"); cartridge.WorkId = "orbital-courier"; cartridge.EditionId = "星港首发版"; Add(cartridge);
            var bootleg = Make("cartridge_bootleg", "《环轨快递》卡带", "卡带", 180, "cartridge_bootleg"); bootleg.WorkId = cartridge.WorkId; bootleg.EditionId = "非授权重制版"; bootleg.DeclaredEditionId = cartridge.EditionId; bootleg.ActualBaseValue = 35; bootleg.IsGenuine = false; bootleg.Hotspots.Add(H("label_print", "cartridge_edition", "标签印刷套色偏移", .49f, .40f, 0, false, false, true)); Add(bootleg);
            var disc = Make("disc", "《远星回声》光盘", "光盘", 120, "disc_clean"); disc.WorkId = "far-star-echoes"; disc.EditionId = "2247 完整版"; Add(disc);
            var scratched = Make("disc_scratched", "《远星回声》光盘", "光盘", 120, "disc_scratched"); scratched.WorkId = disc.WorkId; scratched.EditionId = disc.EditionId; scratched.RepairCost = 15; scratched.Hotspots.Add(H("disc_scratch", "disc_surface", "读取面径向划伤", .63f, .45f, 70, true)); Add(scratched);
            var book = Make("book", "《星港手记》签名初版", "书籍", 240, "book_clean"); book.IsBook = true; book.WorkId = "harbor-notes"; book.EditionId = "2241 初版"; book.ReadingText = "港口亮灯时，归航的人把故事留在当铺。书中的航线、旧招牌和手写批注，记录了星港的第一个冬天。"; Add(book);
            var reprint = Make("book_reprint", "《星港手记》签名初版", "书籍", 240, "book_reprint"); reprint.IsBook = true; reprint.WorkId = book.WorkId; reprint.EditionId = "2246 重印版"; reprint.DeclaredEditionId = book.EditionId; reprint.ReadingText = book.ReadingText; reprint.ActualBaseValue = 45; reprint.IsGenuine = false; reprint.Hotspots.Add(H("reprint_mark", "book_edition", "版权页重印标记", .59f, .69f, 0, false, false, true)); Add(reprint);
            var guide = Make("inspection_guide", "《星港验货手册》", "专业书籍", 75, "guide_book"); guide.IsBook = true; guide.WorkId = "inspection-handbook"; guide.EditionId = "2247 修订版"; guide.ReadingText = "先看轮廓、接口与印记，再找真正可见的异常。同一材质问题的多个线索只计价一次。顾客耐心有限，连续两次空点会结束本次检查。足金首饰的工艺可以受损，黄金材料仍有回收价值。"; Add(guide);
            ConfigureArt(result);
            return result;
        }

        private static void ConfigureArt(Dictionary<string, ItemDefinition> items)
        {
            void Bounds(string item, string evidence, float u, float v, float width, float height)
            {
                var h = items[item].Hotspots.Single(hotspot => hotspot.Id == evidence);
                h.U = u; h.V = v; h.Width = width; h.Height = height;
            }
            // Coordinates measured on final 1536x1024 raster artwork; all patches stay inside the object.
            Bounds("gold_bracelet_worn", "surface_wear", .277f, .275f, .075f, .07f);
            Bounds("gold_bracelet_worn", "surface_seam", .736f, .293f, .075f, .07f);
            Bounds("gold_bracelet_worn", "clasp_break", .499f, .184f, .12f, .07f);
            Bounds("gold_bar_suspect", "bar_seam", .660f, .574f, .13f, .09f);
            Bounds("gold_bar_suspect", "bar_stamp", .490f, .434f, .045f, .045f);
            Bounds("silver_cup_worn", "cup_dent", .560f, .540f, .13f, .14f);
            Bounds("silver_bracelet_worn", "silver_clasp", .500f, .222f, .115f, .045f);
            Bounds("silver_bar_suspect", "silver_bar_seam", .685f, .540f, .17f, .13f);
            Bounds("console_damaged", "console_screw", .171f, .487f, .08f, .11f);
            Bounds("console_damaged", "console_port", .708f, .689f, .08f, .11f);
            Bounds("disc_scratched", "disc_scratch", .671f, .338f, .13f, .15f);
            Bounds("jade_rough_cracked", "rough_crack", .592f, .490f, .17f, .20f);
            Bounds("jade_rough_cracked", "window_crack", .511f, .557f, .24f, .22f);
            Bounds("jade_pendant_dyed", "dye_pool", .565f, .527f, .09f, .13f);
            Bounds("book_reprint", "reprint_mark", .694f, .682f, .15f, .08f);
            Bounds("antique_vase_repaired", "vase_restore", .583f, .594f, .112f, .19f);
            Bounds("painting_forged", "print_dots", .374f, .507f, .10f, .12f);
            Bounds("painting_forged", "seal_mismatch", .848f, .725f, .085f, .11f);
            Bounds("handheld_damaged", "handheld_port", .894f, .504f, .050f, .100f);
            Bounds("cartridge_bootleg", "label_print", .455f, .455f, .25f, .20f);
            items["gold_bracelet_worn"].RepairedTextureKey = "starpawn:gold_bracelet_clean";
            items["gold_bracelet_plated"].RepairedTextureKey = "starpawn:gold_bracelet_plated_repaired";
            items["silver_cup_worn"].RepairedTextureKey = "starpawn:silver_cup_clean";
            items["silver_bracelet_worn"].RepairedTextureKey = "starpawn:silver_bracelet_clean";
            items["console_damaged"].RepairedTextureKey = "starpawn:console_repaired";
            items["handheld_damaged"].RepairedTextureKey = "starpawn:handheld_clean";
            items["disc_scratched"].RepairedTextureKey = "starpawn:disc_clean";
            foreach (var item in items.Values)
            {
                item.NameKey = "content." + item.Id + ".name";
                item.DescriptionKey = "content." + item.Id + ".description";
                if (item.IsBook) item.ReadingKey = "content." + item.Id + ".reading";
                foreach (var hotspot in item.Hotspots) hotspot.LabelKey = "evidence." + item.Id + "." + hotspot.Id;
                ValidateDefinition(item);
            }
        }
    }
}
