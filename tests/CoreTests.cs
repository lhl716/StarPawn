using System;
using System.Linq;
using System.Text.Json;
using StarPawn.Core;

public static class CoreTests
{
    private static int passed;
    public static int Main()
    {
        Test("Repeat clicks and reopen do not award experience twice", () => {
            var s = InspectionService.CreateNewState();
            Assert(InspectionService.Discover(s, InspectionService.WornAssetId, "plating_edge"));
            for (int i = 0; i < 10; i++) {
                Assert(!InspectionService.Discover(s, InspectionService.WornAssetId, "plating_edge"));
                InspectionService.GetAsset(s, InspectionService.WornAssetId, "worn");
                Assert(InspectionService.PreviewQuote(s, InspectionService.WornAssetId).FinalValue == 700);
            }
            Assert(s.Experience == 10);
        });
        Test("Three discoveries retain two independent economic causes", () => {
            var s = InspectionService.CreateNewState();
            InspectionService.Discover(s, InspectionService.WornAssetId, "plating_edge");
            InspectionService.Discover(s, InspectionService.WornAssetId, "plating_seam");
            Assert(InspectionService.PreviewQuote(s, InspectionService.WornAssetId).FinalValue == 700);
            InspectionService.Discover(s, InspectionService.WornAssetId, "broken_clasp");
            var quote = InspectionService.PreviewQuote(s, InspectionService.WornAssetId);
            Assert(quote.FinalValue == 600 && quote.AppliedCauseIds.Count == 2 && quote.IsPreviewOnly);
            Assert(InspectionService.GetAsset(s, InspectionService.WornAssetId).DiscoveredAnomalyIds.Count == 3);
            Assert(s.Experience == 30);
        });
        Test("Clean items and invisible or unknown anomalies award nothing", () => {
            var s = InspectionService.CreateNewState();
            Assert(!InspectionService.Discover(s, InspectionService.CleanAssetId, "plating_edge"));
            Assert(!InspectionService.Discover(s, InspectionService.WornAssetId, "plating_edge", "back"));
            Assert(!InspectionService.Discover(s, InspectionService.WornAssetId, "invented"));
            Assert(s.Experience == 0 && InspectionService.PreviewQuote(s, InspectionService.CleanAssetId).FinalValue == 1000);
        });
        Test("Mastery marks all visible evidence once without extra clicks", () => {
            var s = InspectionService.CreateNewState();
            Assert(InspectionService.AutoDiscoverVisible(s, InspectionService.WornAssetId) == 0);
            s.Experience = InspectionService.MasteryExperience;
            Assert(InspectionService.AutoDiscoverVisible(s, InspectionService.WornAssetId, "back") == 0);
            Assert(InspectionService.AutoDiscoverVisible(s, InspectionService.WornAssetId) == 3);
            Assert(InspectionService.AutoDiscoverVisible(s, InspectionService.WornAssetId) == 0);
            Assert(InspectionService.AutoDiscoverVisible(s, InspectionService.CleanAssetId) == 0);
            Assert(s.Experience == 130 && InspectionService.GetLevel(s) == 5);
            Assert(InspectionService.PreviewQuote(s, InspectionService.WornAssetId).FinalValue == 600);
        });
        Test("Serialization reload preserves identity discoveries and rewards", () => {
            var s = InspectionService.CreateNewState();
            InspectionService.Discover(s, InspectionService.WornAssetId, "broken_clasp");
            var options = new JsonSerializerOptions { IncludeFields = true };
            var restored = JsonSerializer.Deserialize<InspectionState>(JsonSerializer.Serialize(s, options), options)!;
            InspectionService.Validate(restored);
            Assert(!InspectionService.Discover(restored, InspectionService.WornAssetId, "broken_clasp"));
            Assert(restored.Experience == 10);
            Assert(InspectionService.PreviewQuote(restored, InspectionService.WornAssetId).FinalValue == 900);
            Assert(restored.Assets[0].AssetId == s.Assets[0].AssetId);
        });
        Test("Separate saves and separate instances cannot share discoveries", () => {
            var a = InspectionService.CreateNewState();
            var b = InspectionService.CreateNewState();
            InspectionService.Discover(a, InspectionService.WornAssetId, "plating_edge");
            InspectionService.GetAsset(a, "another-bracelet", "worn");
            Assert(InspectionService.Discover(a, "another-bracelet", "plating_edge"));
            Assert(a.Experience == 20 && b.Experience == 0);
            Assert(InspectionService.PreviewQuote(b, InspectionService.WornAssetId).FinalValue == 1000);
        });
        Test("Future schema corruption and duplicate identities fail without wiping", () => {
            var s = InspectionService.CreateNewState();
            s.SchemaVersion = 2;
            Throws(() => InspectionService.Validate(s));
            Assert(s.SchemaVersion == 2 && s.Assets.Count == 2);
            s.SchemaVersion = 1;
            s.Assets.Add(s.Assets[0]);
            Throws(() => InspectionService.Validate(s));
            Assert(s.Assets.Count == 3);
            var clean = InspectionService.CreateNewState();
            clean.Assets[1].DiscoveredAnomalyIds.Add("broken_clasp");
            Throws(() => InspectionService.Validate(clean));
            Assert(clean.Assets[1].DiscoveredAnomalyIds.Count == 1);
        });
        Test("Overflow does not partially mark a discovery", () => {
            var s = InspectionService.CreateNewState();
            s.Experience = int.MaxValue;
            Throws(() => InspectionService.Discover(s, InspectionService.WornAssetId, "plating_edge"));
            Assert(s.Assets[0].DiscoveredAnomalyIds.Count == 0 && s.Experience == int.MaxValue);
        });
        Test("All discovery orders produce the same final quote", () => {
            var ids = new [] { "plating_edge", "plating_seam", "broken_clasp" };
            foreach (var first in ids) foreach (var second in ids.Where(x => x != first)) {
                var s = InspectionService.CreateNewState();
                foreach (var id in new [] { first, second, ids.Single(x => x != first && x != second) })
                    InspectionService.Discover(s, InspectionService.WornAssetId, id);
                Assert(InspectionService.PreviewQuote(s, InspectionService.WornAssetId).FinalValue == 600);
                Assert(s.Experience == 30);
            }
        });
        Console.WriteLine("PASS: " + passed + " invariant tests. Game integration not tested by this executable.");
        return 0;
    }
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
}
