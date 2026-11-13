using System;
using System.Collections.Generic;
using System.Linq;

namespace StarPawn.Core
{
    public sealed class InspectionEncounter
    {
        public string EncounterId = "";
        public string AssetId = "";
        public string CounterpartyId = "";
        public string ViewId = "front";
        public int ConsecutiveMisses;
        public bool Locked;
        public bool Completed;
        public int Revision = 1;
        public List<string> SelectedEvidenceIds = new List<string>();
    }

    public sealed class InspectionOutcome
    {
        public string Kind = ""; // discovered / repeated / miss / locked / automatic
        public string Message = "";
        public bool Locked;
        public int ConsecutiveMisses;
        public List<string> NewEvidenceIds = new List<string>();
    }

    public sealed class AssetQuote
    {
        public string AssetId = "";
        public string EncounterId = "";
        public string CounterpartyId = "";
        public string Direction = "buy"; // player buys / player sells
        public int AssetVersion;
        public int EncounterRevision;
        public long MarketVersion;
        public int GameDay;
        public long BaseValue;
        public long MaterialValue;
        public long CraftPremium;
        public long EvidenceDeduction;
        public long Price;
        public long RecoveryValue;
        public bool RecoveryValueConfirmed;
        public string MaterialDescription = "";
        public List<string> AppliedCauses = new List<string>();
    }

    public static class InspectionV2
    {
        public static InspectionRules Rules { get; private set; } = new InspectionRules();
        public static int MasteryExperience => Rules.LevelThresholds.Last();
        public static int ExperiencePerDiscovery => Rules.ExperiencePerDiscovery;
        public static int ConsecutiveMissLimit => Rules.ConsecutiveMissLimit;
        public static int MaximumLevel => Rules.LevelThresholds.Count - 1;

        public static void ConfigureRules(InspectionRules rules)
        {
            ContentPacks.ValidateInspectionRules(rules);
            Rules = rules;
        }

        public static bool IsMastered(CatalogState state) => state.Experience >= MasteryExperience;
        public static int GetLevel(CatalogState state) => Rules.LevelThresholds.Count(threshold => state.Experience >= threshold) - 1;

        public static InspectionEncounter Open(CatalogState state, string assetId, string encounterId, string counterpartyId, string view = "front")
        {
            Catalog.Validate(state);
            var asset = Catalog.GetAsset(state, assetId);
            if (!Catalog.CanObserve(asset, view)) throw new InvalidOperationException("This view is not observable.");
            if (string.IsNullOrWhiteSpace(encounterId) || string.IsNullOrWhiteSpace(counterpartyId)) throw new ArgumentException("A persistent encounter and counterparty identity are required.");
            var encounter = state.Encounters.FirstOrDefault(e => e.EncounterId == encounterId);
            if (encounter != null)
            {
                if (encounter.AssetId != assetId || encounter.CounterpartyId != counterpartyId) throw new InvalidOperationException("An encounter cannot be rebound to a different asset or customer.");
                if (encounter.Completed) throw new InvalidOperationException("This deal has already ended.");
            }
            else
            {
                // UI recreation must never reset an active customer's patience by minting an ID.
                encounter = state.Encounters.FirstOrDefault(e => !e.Completed && e.AssetId == assetId && e.CounterpartyId == counterpartyId);
                if (encounter == null)
                {
                    if (state.Encounters.Count >= Catalog.MaxEncounters) throw new InvalidOperationException("Encounter capacity reached; preserve existing lockouts.");
                    encounter = new InspectionEncounter { EncounterId = encounterId, AssetId = assetId, CounterpartyId = counterpartyId, ViewId = view };
                    encounter.SelectedEvidenceIds.AddRange(asset.DiscoveredEvidenceIds);
                }
            }
            // Preflight automatic discoveries before committing a reopened view or a new encounter.
            if (IsMastered(state) && !encounter.Locked && !encounter.Completed)
            {
                var automatic = Catalog.ActiveHotspots(asset, view).Where(h => !asset.DiscoveredEvidenceIds.Contains(h.Id)).ToList();
                if (automatic.Count > 0)
                    checked { _ = state.Experience + automatic.Count(h => !asset.RewardedEvidenceIds.Contains(h.Id)) * ExperiencePerDiscovery; _ = asset.StateVersion + 1; _ = encounter.Revision + 1; }
            }
            encounter.ViewId = view;
            if (!state.Encounters.Contains(encounter)) state.Encounters.Add(encounter);
            AutoDiscoverVisible(state, encounter.EncounterId);
            return encounter;
        }

        public static InspectionEncounter GetEncounter(CatalogState state, string encounterId)
            => state.Encounters.FirstOrDefault(e => e.EncounterId == encounterId) ?? throw new InvalidOperationException("Unknown inspection encounter.");

        public static InspectionOutcome ClickAt(CatalogState state, string encounterId, float u, float v)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", "顾客把物品收了回去：这次就看到这里吧。");
            if (float.IsNaN(u) || float.IsNaN(v) || u < 0 || u > 1 || v < 0 || v > 1) throw new ArgumentOutOfRangeException("Image coordinates must be normalized.");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var hit = Catalog.ActiveHotspots(asset, encounter.ViewId).FirstOrDefault(h => Math.Abs(u - h.U) <= h.Width / 2 + Rules.HitToleranceUv && Math.Abs(v - h.V) <= h.Height / 2 + Rules.HitToleranceUv);
            return hit == null ? Miss(state, encounterId) : Discover(state, encounterId, hit.Id);
        }

        public static InspectionOutcome Discover(CatalogState state, string encounterId, string evidenceId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", "顾客把物品收了回去：这次就看到这里吧。");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var evidence = Catalog.ActiveHotspots(asset, encounter.ViewId).FirstOrDefault(h => h.Id == evidenceId);
            if (evidence == null) return Miss(state, encounterId);
            if (asset.DiscoveredEvidenceIds.Contains(evidenceId)) return Result(encounter, "repeated", "这处已经看过了。顾客的耐心没有恢复。");
            var newlyFound = new List<HotspotDefinition> { evidence };
            var firstExperience = checked(state.Experience + (!asset.RewardedEvidenceIds.Contains(evidenceId) ? ExperiencePerDiscovery : 0));
            if (firstExperience >= MasteryExperience)
                newlyFound.AddRange(Catalog.ActiveHotspots(asset, encounter.ViewId).Where(h => h.Id != evidenceId && !asset.DiscoveredEvidenceIds.Contains(h.Id)));
            var experience = checked(state.Experience + newlyFound.Count(h => !asset.RewardedEvidenceIds.Contains(h.Id)) * ExperiencePerDiscovery);
            var revision = checked(encounter.Revision + 1);
            var version = checked(asset.StateVersion + 1);
            foreach (var found in newlyFound)
            {
                asset.DiscoveredEvidenceIds.Add(found.Id);
                if (!asset.RewardedEvidenceIds.Contains(found.Id)) asset.RewardedEvidenceIds.Add(found.Id);
                if (!encounter.SelectedEvidenceIds.Contains(found.Id)) encounter.SelectedEvidenceIds.Add(found.Id);
            }
            state.Experience = experience;
            encounter.ConsecutiveMisses = 0;
            encounter.Revision = revision;
            asset.StateVersion = version;
            var result = Result(encounter, "discovered", "你指出了" + evidence.Label + "。顾客点头，把物品重新放稳。");
            result.NewEvidenceIds.AddRange(newlyFound.Select(h => h.Id));
            return result;
        }

        public static InspectionOutcome Miss(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", "顾客把物品收了回去：这次就看到这里吧。");
            if (!Catalog.CanObserve(Catalog.GetAsset(state, encounter.AssetId), encounter.ViewId)) throw new InvalidOperationException("The inspected asset or view changed.");
            var revision = checked(encounter.Revision + 1);
            encounter.ConsecutiveMisses++;
            encounter.Locked = encounter.ConsecutiveMisses >= ConsecutiveMissLimit;
            encounter.Revision = revision;
            return Result(encounter, encounter.Locked ? "locked" : "miss", encounter.Locked ? "顾客皱起眉，把物品收了回去：“没问题的地方就别反复挑了，这次不看了。”" : "顾客敲了敲柜台：“那里没有问题吧？你再仔细看看。”");
        }

        public static InspectionOutcome AutoDiscoverVisible(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            var result = Result(encounter, "automatic", "");
            if (!IsMastered(state) || encounter.Locked || encounter.Completed) return result;
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var evidence = Catalog.ActiveHotspots(asset, encounter.ViewId).Where(h => !asset.DiscoveredEvidenceIds.Contains(h.Id)).ToList();
            if (evidence.Count == 0) return result;
            var rewards = evidence.Count(h => !asset.RewardedEvidenceIds.Contains(h.Id));
            var experience = checked(state.Experience + rewards * ExperiencePerDiscovery);
            var revision = checked(encounter.Revision + 1);
            var version = checked(asset.StateVersion + 1);
            foreach (var h in evidence)
            {
                asset.DiscoveredEvidenceIds.Add(h.Id);
                if (!asset.RewardedEvidenceIds.Contains(h.Id)) asset.RewardedEvidenceIds.Add(h.Id);
                if (!encounter.SelectedEvidenceIds.Contains(h.Id)) encounter.SelectedEvidenceIds.Add(h.Id);
                result.NewEvidenceIds.Add(h.Id);
            }
            state.Experience = experience;
            encounter.Revision = revision;
            encounter.ConsecutiveMisses = 0;
            asset.StateVersion = version;
            result.ConsecutiveMisses = 0;
            result.Message = "熟练的目光已经圈出当前视图中的全部可见问题。";
            return result;
        }

        public static void SelectEvidence(CatalogState state, string encounterId, string evidenceId, bool selected)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            if (encounter.Completed || !asset.DiscoveredEvidenceIds.Contains(evidenceId)) throw new InvalidOperationException("Evidence is unavailable for this deal.");
            if (selected == encounter.SelectedEvidenceIds.Contains(evidenceId)) return;
            var revision = checked(encounter.Revision + 1);
            if (selected) encounter.SelectedEvidenceIds.Add(evidenceId); else encounter.SelectedEvidenceIds.Remove(evidenceId);
            encounter.Revision = revision;
        }

        public static void SelectAll(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            if (encounter.Completed) throw new InvalidOperationException("The deal ended.");
            if (new HashSet<string>(encounter.SelectedEvidenceIds).SetEquals(asset.DiscoveredEvidenceIds)) return;
            var revision = checked(encounter.Revision + 1);
            encounter.SelectedEvidenceIds = new List<string>(asset.DiscoveredEvidenceIds);
            encounter.Revision = revision;
        }

        // Only a real completed/cancelled trade calls this, never a window close.
        public static void EndEncounter(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Completed) return;
            var revision = checked(encounter.Revision + 1);
            encounter.Completed = true;
            encounter.Revision = revision;
        }

        internal static void ValidateEncounters(CatalogState state)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var activePairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var encounter in state.Encounters)
            {
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.EncounterId) || !ids.Add(encounter.EncounterId) || string.IsNullOrWhiteSpace(encounter.CounterpartyId) || encounter.Revision < 1 || encounter.ConsecutiveMisses < 0 || encounter.ConsecutiveMisses > ConsecutiveMissLimit || encounter.Locked != (encounter.ConsecutiveMisses == ConsecutiveMissLimit) || encounter.SelectedEvidenceIds == null)
                    throw new InvalidOperationException("Invalid persisted inspection encounter.");
                var asset = Catalog.GetAsset(state, encounter.AssetId);
                if (encounter.ViewId != "front" && encounter.ViewId != "window") throw new InvalidOperationException("Invalid inspection view.");
                if (!encounter.Completed && !activePairs.Add(encounter.AssetId + "\n" + encounter.CounterpartyId)) throw new InvalidOperationException("Duplicate active customer inspection.");
                Catalog.ValidateUnique(encounter.SelectedEvidenceIds, asset.DiscoveredEvidenceIds);
            }
        }

        private static InspectionOutcome Result(InspectionEncounter encounter, string kind, string message)
            => new InspectionOutcome { Kind = kind, Message = message, Locked = encounter.Locked, ConsecutiveMisses = encounter.ConsecutiveMisses };
    }

    public static class ValuationV2
    {
        public static AssetQuote Quote(CatalogState state, string encounterId, int gameDay, long marketVersion, long goldSell, long goldBuy, long silverSell, long silverBuy, string direction = "buy")
        {
            Catalog.Validate(state);
            if (gameDay < 0 || marketVersion < 0 || goldBuy < 0 || goldSell < goldBuy || silverBuy < 0 || silverSell < silverBuy || (direction != "buy" && direction != "sell")) throw new ArgumentException("Invalid quote market snapshot or direction.");
            var encounter = InspectionV2.GetEncounter(state, encounterId);
            if (encounter.Completed) throw new InvalidOperationException("This encounter is complete.");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            if (asset.Status != "active") throw new InvalidOperationException("Consumed or escrowed assets cannot be quoted for sale.");
            var definition = Catalog.GetDefinition(asset.DefinitionId);
            var selected = definition.Hotspots.Where(h => encounter.SelectedEvidenceIds.Contains(h.Id) && !asset.ResolvedCauseIds.Contains(h.CauseId)).ToList();
            var materialKnown = asset.Certificates.Any(c => c.Scope == "material") || selected.Any(h => h.RevealsMaterial);
            var authenticityKnown = asset.Certificates.Any(c => c.Scope == "authenticity" || c.Scope == "edition") || selected.Any(h => h.RevealsAuthenticity);
            var precious = definition.Material == "gold" || definition.Material == "silver";
            var buy = definition.Material == "gold" ? goldBuy : silverBuy;
            var sell = definition.Material == "gold" ? goldSell : silverSell;
            var visiblePurity = materialKnown ? definition.ActualPurityBasisPoints : definition.DeclaredPurityBasisPoints;
            var materialBase = precious ? MetalValue(definition.WeightMilligrams, visiblePurity, sell) : 0;
            var trueFloor = precious ? MetalValue(definition.WeightMilligrams, definition.ActualPurityBasisPoints, buy) : 0;
            var visibleFloor = precious ? MetalValue(definition.WeightMilligrams, visiblePurity, buy) : 0;
            var baseValue = precious ? checked(materialBase + (materialKnown && visiblePurity == 0 ? definition.ActualBaseValue : definition.CraftValue)) : (authenticityKnown ? definition.ActualBaseValue : definition.BaseValue);
            baseValue = Scale(baseValue, asset.QualityBasisPoints);
            var causes = selected.GroupBy(h => h.CauseId, StringComparer.Ordinal).ToList();
            long deduction = 0;
            foreach (var cause in causes) deduction = checked(deduction + cause.Max(h => h.ValueDeduction));
            var valuation = Math.Max(trueFloor, Math.Max(0, baseValue - deduction));
            // NPC's resale spread never eats genuine recoverable precious metal value.
            var price = direction == "sell" ? Math.Max(trueFloor, Scale(valuation, InspectionV2.Rules.ResalePriceBasisPoints)) : valuation;
            return new AssetQuote {
                AssetId = asset.AssetId, EncounterId = encounter.EncounterId, CounterpartyId = encounter.CounterpartyId, Direction = direction,
                AssetVersion = asset.StateVersion, EncounterRevision = encounter.Revision, MarketVersion = marketVersion, GameDay = gameDay,
                BaseValue = baseValue, MaterialValue = materialBase, CraftPremium = precious ? Math.Max(0, baseValue - materialBase) : 0, EvidenceDeduction = Math.Max(0, baseValue - valuation), Price = price, RecoveryValue = visibleFloor,
                RecoveryValueConfirmed = !precious || materialKnown,
                MaterialDescription = precious ? (materialKnown ? "已核实成色 " : "标称成色 ") + (visiblePurity / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "% · " + (definition.WeightMilligrams / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " 克" : definition.Category,
                AppliedCauses = causes.Select(c => c.Key).ToList()
            };
        }

        public static bool IsCurrent(CatalogState state, AssetQuote quote, int gameDay, long marketVersion, string direction)
        {
            var asset = Catalog.GetAsset(state, quote.AssetId);
            var encounter = InspectionV2.GetEncounter(state, quote.EncounterId);
            return !encounter.Completed && asset.Status == "active" && asset.StateVersion == quote.AssetVersion && encounter.Revision == quote.EncounterRevision && encounter.AssetId == quote.AssetId && encounter.CounterpartyId == quote.CounterpartyId && quote.GameDay == gameDay && quote.MarketVersion == marketVersion && quote.Direction == direction;
        }

        public static long MetalValue(long weightMilligrams, int purityBasisPoints, long perPureGram)
        {
            if (weightMilligrams < 0 || purityBasisPoints < 0 || purityBasisPoints > 10000 || perPureGram < 0) throw new ArgumentOutOfRangeException("Invalid metal quote.");
            // decimal is exact for these bounded integer products; truncate fractional credits once.
            return checked((long)decimal.Floor((decimal)weightMilligrams * purityBasisPoints * perPureGram / 10000000m));
        }

        private static long Scale(long amount, int basisPoints) => checked((long)decimal.Floor((decimal)amount * basisPoints / 10000m));
    }
}
