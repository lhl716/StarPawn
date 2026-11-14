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
        // True exactly once per evidence: play the native exposure sound and show the price change.
        public bool PlayFeedback;
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
        // Revision 05 component breakdown (metal is never reduced by cosmetic findings).
        public long CraftValue;
        public long StoneValue;
        public long PremiumValue;
        public long WeightMilligrams;
        public int PurityBasisPoints;
        public bool PurityConfirmed;
        public List<QuoteLine> Lines = new List<QuoteLine>();
    }

    public static class UnifiedConfig
    {
        public static UnifiedRules Rules { get; private set; } = new UnifiedRules();
        public static void Configure(UnifiedRules rules) { Operations05.ValidateRules(rules); Rules = rules; }
    }

    public static class InspectionV2
    {
        public static InspectionRules Rules { get; private set; } = new InspectionRules();
        public static int ConsecutiveMissLimit => Rules.ConsecutiveMissLimit;

        // Revision 05: appraisal progress belongs to the native career. The old click-XP
        // mastery route is retired; CatalogState.Experience is kept only as legacy data.
        [Obsolete("Legacy 0.3 value; never changes in Revision 05.")] public static int MasteryExperience => Rules.LevelThresholds.Last();
        [Obsolete("Legacy 0.3 value; discoveries no longer award experience.")] public static int ExperiencePerDiscovery => 0;
        [Obsolete("Legacy 0.3 value.")] public static int MaximumLevel => Rules.LevelThresholds.Count - 1;
        [Obsolete("Mastery auto-reveal was removed in Revision 05.")] public static bool IsMastered(CatalogState state) => false;
        [Obsolete("Use the native appraiser rank.")] public static int GetLevel(CatalogState state) => Rules.LevelThresholds.Count(threshold => state.Experience >= threshold) - 1;

        public static void ConfigureRules(InspectionRules rules)
        {
            ContentPacks.ValidateInspectionRules(rules);
            Rules = rules;
            UnifiedConfig.Rules.ResalePriceBasisPoints = rules.ResalePriceBasisPoints;
        }

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
                }
            }
            encounter.ViewId = view;
            if (!state.Encounters.Contains(encounter)) state.Encounters.Add(encounter);
            return encounter;
        }

        public static InspectionEncounter GetEncounter(CatalogState state, string encounterId)
            => state.Encounters.FirstOrDefault(e => e.EncounterId == encounterId) ?? throw new InvalidOperationException("Unknown inspection encounter.");

        // Personal study of the player's own stock: there is no customer to annoy.
        public static bool IsStudy(InspectionEncounter encounter) => encounter.CounterpartyId == "player" || encounter.CounterpartyId.StartsWith("study:", StringComparison.Ordinal);

        public static InspectionOutcome ClickAt(CatalogState state, string encounterId, float u, float v)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", T("sp5.inspect.locked", "The customer has taken the item back. No more claims this visit."));
            if (float.IsNaN(u) || float.IsNaN(v) || u < 0 || u > 1 || v < 0 || v > 1) throw new ArgumentOutOfRangeException("Image coordinates must be normalized.");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var hit = Catalog.ActiveHotspots(asset, encounter.ViewId).FirstOrDefault(h => Math.Abs(u - h.U) <= h.Width / 2 + Rules.HitToleranceUv && Math.Abs(v - h.V) <= h.Height / 2 + Rules.HitToleranceUv);
            return hit == null ? Miss(state, encounterId) : Discover(state, encounterId, hit.Id);
        }

        public static InspectionOutcome Discover(CatalogState state, string encounterId, string evidenceId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", T("sp5.inspect.locked", "The customer has taken the item back. No more claims this visit."));
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            var evidence = Catalog.ActiveHotspots(asset, encounter.ViewId).FirstOrDefault(h => h.Id == evidenceId);
            if (evidence == null) return Miss(state, encounterId);
            // A repeated clue is neither a reward nor a miss, and never restores patience.
            if (asset.DiscoveredEvidenceIds.Contains(evidenceId)) return Result(encounter, "repeated", T("sp5.inspect.repeated", "Already recorded."));
            var revision = checked(encounter.Revision + 1);
            var version = checked(asset.StateVersion + 1);
            asset.DiscoveredEvidenceIds.Add(evidence.Id);
            // RewardedEvidenceIds now only means "native discovery feedback already played".
            var firstFeedback = !asset.RewardedEvidenceIds.Contains(evidence.Id);
            if (firstFeedback) asset.RewardedEvidenceIds.Add(evidence.Id);
            encounter.ConsecutiveMisses = 0;
            encounter.Revision = revision;
            asset.StateVersion = version;
            var result = Result(encounter, "discovered", "");
            result.NewEvidenceIds.Add(evidence.Id);
            result.PlayFeedback = firstFeedback;
            return result;
        }

        public static InspectionOutcome Miss(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            var encounter = GetEncounter(state, encounterId);
            if (encounter.Locked || encounter.Completed) return Result(encounter, "locked", T("sp5.inspect.locked", "The customer has taken the item back. No more claims this visit."));
            if (!Catalog.CanObserve(Catalog.GetAsset(state, encounter.AssetId), encounter.ViewId)) throw new InvalidOperationException("The inspected asset or view changed.");
            if (IsStudy(encounter)) return Result(encounter, "study_miss", T("sp5.inspect.study_miss", "Nothing unusual there."));
            var revision = checked(encounter.Revision + 1);
            encounter.ConsecutiveMisses++;
            encounter.Locked = encounter.ConsecutiveMisses >= ConsecutiveMissLimit;
            encounter.Revision = revision;
            return Result(encounter, encounter.Locked ? "locked" : "miss", encounter.Locked
                ? T("sp5.inspect.walkout", "\"There is nothing wrong there. I am done with this.\" The customer takes the item back.")
                : T("sp5.inspect.miss", "\"There is nothing wrong there, is there? Look properly.\""));
        }

        [Obsolete("Mastery auto-reveal was removed in Revision 05; this never discovers anything.")]
        public static InspectionOutcome AutoDiscoverVisible(CatalogState state, string encounterId)
        {
            Catalog.Validate(state);
            return Result(GetEncounter(state, encounterId), "automatic", "");
        }

        // Findings are applied automatically. The old optional evidence toggles could
        // restore value, so they are inert and kept only for binary/save compatibility.
        [Obsolete("Evidence selection was removed in Revision 05; all confirmed findings apply.")]
        public static void SelectEvidence(CatalogState state, string encounterId, string evidenceId, bool selected) { Catalog.Validate(state); GetEncounter(state, encounterId); }
        [Obsolete("Evidence selection was removed in Revision 05; all confirmed findings apply.")]
        public static void SelectAll(CatalogState state, string encounterId) { Catalog.Validate(state); GetEncounter(state, encounterId); }

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
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.EncounterId) || !ids.Add(encounter.EncounterId) || string.IsNullOrWhiteSpace(encounter.CounterpartyId) || encounter.Revision < 1 || encounter.ConsecutiveMisses < 0 || encounter.ConsecutiveMisses > ConsecutiveMissLimit || encounter.Locked != (encounter.ConsecutiveMisses >= ConsecutiveMissLimit) || encounter.SelectedEvidenceIds == null)
                    throw new InvalidOperationException("Invalid persisted inspection encounter.");
                var asset = Catalog.GetAsset(state, encounter.AssetId);
                if (encounter.ViewId != "front" && encounter.ViewId != "window") throw new InvalidOperationException("Invalid inspection view.");
                if (!encounter.Completed && !activePairs.Add(encounter.AssetId + "\n" + encounter.CounterpartyId)) throw new InvalidOperationException("Duplicate active customer inspection.");
                // Legacy selections are tolerated but must still refer to real, recorded evidence.
                Catalog.ValidateUnique(encounter.SelectedEvidenceIds, asset.DiscoveredEvidenceIds);
            }
        }

        private static string T(string key, string english) => TextResolver.Get(key, english);

        private static InspectionOutcome Result(InspectionEncounter encounter, string kind, string message)
            => new InspectionOutcome { Kind = kind, Message = message, Locked = encounter.Locked, ConsecutiveMisses = encounter.ConsecutiveMisses };
    }

    public static class ValuationV2
    {
        // Routine shop estimate. direction "buy" = Buy from customer/supplier, "sell" = Material buyback / sale to a dealer.
        // Every confirmed, unresolved finding applies automatically to its own component.
        public static AssetQuote Quote(CatalogState state, string encounterId, int gameDay, long marketVersion, long goldSell, long goldBuy, long silverSell, long silverBuy, string direction = "buy")
        {
            Catalog.Validate(state);
            if (gameDay < 0 || marketVersion < 0 || goldBuy < 0 || goldSell < goldBuy || silverBuy < 0 || silverSell < silverBuy || (direction != "buy" && direction != "sell")) throw new ArgumentException("Invalid quote market snapshot or direction.");
            var encounter = InspectionV2.GetEncounter(state, encounterId);
            if (encounter.Completed) throw new InvalidOperationException("This encounter is complete.");
            var asset = Catalog.GetAsset(state, encounter.AssetId);
            if (asset.Status != "active") throw new InvalidOperationException("Consumed or escrowed assets cannot be quoted for sale.");
            var saved = MarketService.Find(state, gameDay);
            var market = new MarketDay { Day = gameDay, Version = marketVersion, GoldSell = goldSell, GoldBuy = goldBuy, SilverSell = silverSell, SilverBuy = silverBuy,
                CopperSell = saved?.CopperSell ?? UnifiedConfig.Rules.CopperSellPer100g, CopperBuy = saved?.CopperBuy ?? UnifiedConfig.Rules.CopperBuyPer100g,
                Channels = saved?.Channels ?? new List<ChannelValue>(), EventIds = saved?.EventIds ?? new List<string>() };
            var rules = UnifiedConfig.Rules;
            var q = ComponentValuation.Quote(state, asset, market, direction == "sell" ? ComponentValuation.MaterialBuyback : ComponentValuation.BuyFromCustomer, rules);
            var precious = q.Metal.Length > 0;
            var bid = q.Metal == "gold" ? goldBuy : silverBuy;
            return new AssetQuote {
                AssetId = asset.AssetId, EncounterId = encounter.EncounterId, CounterpartyId = encounter.CounterpartyId, Direction = direction,
                AssetVersion = asset.StateVersion, EncounterRevision = encounter.Revision, MarketVersion = marketVersion, GameDay = gameDay,
                BaseValue = checked(q.MetalValue + q.CraftBasis + q.StoneValue + q.PremiumValue + q.Lines.Where(l => l.Component == "metal_mass").Sum(l => l.Amount)),
                MaterialValue = q.MetalValue, CraftPremium = q.CraftBasis, CraftValue = q.CraftValue, StoneValue = q.StoneValue, PremiumValue = q.PremiumValue,
                EvidenceDeduction = checked(q.CraftDeduction + q.Lines.Where(l => l.Component == "metal_mass").Sum(l => l.Amount)), Price = q.Total,
                // Public recovery value uses established knowledge only; it never leaks hidden purity.
                RecoveryValue = precious ? MetalValue(q.EligibleMilligrams, q.PurityBasisPoints, bid) : 0,
                RecoveryValueConfirmed = !precious || q.PurityConfirmed,
                MaterialDescription = precious ? DescribeMetal(q) : "",
                AppliedCauses = q.AppliedCauses, Lines = q.Lines, WeightMilligrams = q.EligibleMilligrams, PurityBasisPoints = q.PurityBasisPoints, PurityConfirmed = q.PurityConfirmed
            };
        }

        public static string DescribeMetal(ComponentQuote q)
        {
            var pct = (q.PurityBasisPoints / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%";
            var grams = (q.EligibleMilligrams / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " g";
            return (q.PurityConfirmed
                ? string.Format(TextResolver.Get("sp5.material.assayed", "Assay: {0}"), pct)
                : string.Format(TextResolver.Get("sp5.material.marked", "Mark: {0} | Assay: not tested"), pct)) + " · " + grams;
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
