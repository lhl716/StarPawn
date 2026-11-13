using System;
using System.Collections.Generic;

namespace StarPawn.Core
{
    // DTOs use public fields for the game's Newtonsoft.Json serializer.
    // This state is per save. It contains no wallet balance or financial obligations.
    public sealed class InspectionState
    {
        public int SchemaVersion = 1;
        public int Experience;
        public List<AssetInspectionState> Assets = new List<AssetInspectionState>();
    }

    public sealed class AssetInspectionState
    {
        public string AssetId = "";
        public string VariantId = "";
        public List<string> DiscoveredAnomalyIds = new List<string>();
    }

    public sealed class AnomalyDefinition
    {
        public string AnomalyId { get; }
        public string EconomicCauseId { get; }
        public string ViewId { get; }
        public string Label { get; }
        public int PreviewDeduction { get; }

        internal AnomalyDefinition(string id, string cause, string label, int deduction)
        {
            AnomalyId = id;
            EconomicCauseId = cause;
            ViewId = "front";
            Label = label;
            PreviewDeduction = deduction;
        }
    }

    public sealed class QuotePreview
    {
        public string AssetId = "";
        public int ClaimedIntactValue;
        public int EvidenceDeduction;
        public int FinalValue;
        public List<string> AppliedCauseIds = new List<string>();
        // This object never executes or authorizes a game transaction.
        public bool IsPreviewOnly = true;
    }

    public static class InspectionService
    {
        public const int SchemaVersion = 1;
        public const int ExperiencePerDiscovery = 10;
        public const int MasteryExperience = 100;
        public const string WornAssetId = "bracelet-worn-001";
        public const string CleanAssetId = "bracelet-clean-001";
        public const int ClaimedIntactValue = 1000;

        private static readonly AnomalyDefinition[] WornAnomalies = {
            new AnomalyDefinition("plating_edge", "base_material_mismatch", "镀层破损", 300),
            new AnomalyDefinition("plating_seam", "base_material_mismatch", "接缝露底", 300),
            new AnomalyDefinition("broken_clasp", "clasp_damage", "搭扣断裂", 100)
        };
        private static readonly AnomalyDefinition[] NoAnomalies = new AnomalyDefinition[0];

        public static InspectionState CreateNewState()
        {
            var state = new InspectionState();
            GetAsset(state, WornAssetId, "worn");
            GetAsset(state, CleanAssetId, "clean");
            return state;
        }

        public static bool IsMastered(InspectionState state)
        {
            Validate(state);
            return state.Experience >= MasteryExperience;
        }

        public static int GetLevel(InspectionState state)
        {
            Validate(state);
            return Math.Min(5, state.Experience / 20);
        }

        public static AssetInspectionState GetAsset(InspectionState state, string assetId)
        {
            Validate(state);
            foreach (var asset in state.Assets)
                if (asset.AssetId == assetId) return asset;
            throw new KeyNotFoundException("Unknown asset: " + assetId);
        }

        // Explicit creation only; reopening an asset never regenerates its identity or discoveries.
        public static AssetInspectionState GetAsset(InspectionState state, string assetId, string variantId)
        {
            Validate(state);
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            ValidateVariant(variantId);
            foreach (var asset in state.Assets)
            {
                if (asset.AssetId != assetId) continue;
                if (asset.VariantId != variantId) throw new InvalidOperationException("An existing asset cannot change variant.");
                return asset;
            }
            var created = new AssetInspectionState { AssetId = assetId, VariantId = variantId };
            state.Assets.Add(created);
            return created;
        }

        public static IReadOnlyList<AnomalyDefinition> GetAnomalies(AssetInspectionState asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            ValidateVariant(asset.VariantId);
            return Array.AsReadOnly(asset.VariantId == "worn" ? WornAnomalies : NoAnomalies);
        }

        // Returns true only when a valid, visible anomaly is discovered for the first time.
        // UI hit testing is performed by the game adapter; this method validates the identity/view.
        public static bool Discover(InspectionState state, string assetId, string anomalyId, string visibleView = "front")
        {
            var asset = GetAsset(state, assetId);
            var definition = FindAnomaly(asset, anomalyId);
            if (definition == null || definition.ViewId != visibleView || asset.DiscoveredAnomalyIds.Contains(anomalyId))
                return false;
            var updatedExperience = checked(state.Experience + ExperiencePerDiscovery);
            asset.DiscoveredAnomalyIds.Add(anomalyId);
            state.Experience = updatedExperience;
            return true;
        }

        public static int AutoDiscoverVisible(InspectionState state, string assetId, string visibleView = "front")
        {
            var asset = GetAsset(state, assetId);
            if (!IsMastered(state)) return 0;
            var count = 0;
            foreach (var anomaly in GetAnomalies(asset))
                if (Discover(state, assetId, anomaly.AnomalyId, visibleView)) count++;
            return count;
        }

        public static QuotePreview PreviewQuote(InspectionState state, string assetId)
        {
            var asset = GetAsset(state, assetId);
            var preview = new QuotePreview { AssetId = assetId, ClaimedIntactValue = ClaimedIntactValue };
            var causes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var anomalyId in asset.DiscoveredAnomalyIds)
            {
                var anomaly = FindAnomaly(asset, anomalyId);
                if (anomaly != null && causes.Add(anomaly.EconomicCauseId))
                {
                    preview.AppliedCauseIds.Add(anomaly.EconomicCauseId);
                    preview.EvidenceDeduction += anomaly.PreviewDeduction;
                }
            }
            preview.FinalValue = Math.Max(0, preview.ClaimedIntactValue - preview.EvidenceDeduction);
            return preview;
        }

        // Reject unfamiliar/corrupt state. The adapter must retain the original save payload
        // and show an error, rather than replacing it with a new empty state.
        public static void Validate(InspectionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.SchemaVersion != SchemaVersion) throw new InvalidOperationException("Unsupported inspection schema.");
            if (state.Experience < 0) throw new InvalidOperationException("Experience cannot be negative.");
            if (state.Assets == null) throw new InvalidOperationException("Missing asset list.");
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in state.Assets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.AssetId) || !assetIds.Add(asset.AssetId))
                    throw new InvalidOperationException("Missing or duplicate asset identity.");
                ValidateVariant(asset.VariantId);
                if (asset.DiscoveredAnomalyIds == null) throw new InvalidOperationException("Missing discovery list.");
                var discoveries = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in asset.DiscoveredAnomalyIds)
                    if (FindAnomaly(asset, id) == null || !discoveries.Add(id))
                        throw new InvalidOperationException("Invalid or duplicate saved discovery.");
            }
        }

        private static AnomalyDefinition? FindAnomaly(AssetInspectionState asset, string anomalyId)
        {
            foreach (var anomaly in GetAnomalies(asset))
                if (anomaly.AnomalyId == anomalyId) return anomaly;
            return null;
        }

        private static void ValidateVariant(string variantId)
        {
            if (variantId != "worn" && variantId != "clean")
                throw new InvalidOperationException("Unknown bracelet variant: " + variantId);
        }
    }
}
