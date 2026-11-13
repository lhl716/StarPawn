using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using StarPawn.Core;

internal static class AppraisalTests
{
    private static int count;
    private static List<ItemDefinition> definitions = null!;
    private static string Snap(object value) => JsonConvert.SerializeObject(value);
    private static T Copy<T>(T value) where T:class => JsonConvert.DeserializeObject<T>(Snap(value))!;
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch(InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
    private static void Test(string name,Action action) { Catalog.Configure(definitions);InspectionV2.ConfigureRules(new InspectionRules());action();count++;Console.WriteLine("PASS "+name); }
    private static CatalogState State() => Catalog.CreateState("appraisal-test");
    private static AssetRecord Asset(CatalogState state,string id="sp3_gold_ring_0_p4",int seed=123) => Catalog.CreateAsset(state,id,"player",seed);
    private static InspectionEncounter Open(CatalogState state,AssetRecord asset) => InspectionV2.Open(state,asset.AssetId,"encounter-"+asset.AssetId,"customer");
    private static AssetRecord Tool(CatalogState state,string kind) => Asset(state,"sp3_tool_"+kind);
    private static AssetQuote Quote(CatalogState state,InspectionEncounter e) => ValuationV2.Quote(state,e.EncounterId,1,1,60,50,4,3);
    public static int Main(string[] args)
    {
        try
        {
            var pack=JsonConvert.DeserializeObject<ContentPack>(File.ReadAllText(Path.Combine(args[0],"mod/Content/items.json")))!;
            var loc=JsonConvert.DeserializeObject<LocalizationPack>(File.ReadAllText(Path.Combine(args[0],"mod/Content/Localization/zh-CN.json")))!;
            definitions=ContentPacks.ValidateAndMerge(new[]{pack},new InspectionRules(),loc,key=>File.Exists(Path.Combine(args[0],"mod/Textures/Paper/"+key.Split(':')[1]+".png")));
            Run();Console.WriteLine("APPRAISAL TESTS PASSED: "+count+"; real packaged content + core, not Unity inventory validation");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Run()
    {
        Test("gold and silver each cover five forms with two distinct products",()=>{
            foreach(var material in new[]{"gold","silver"}) foreach(var form in new[]{"ring","bracelet","necklace","earrings","bar"})
                Check(definitions.Where(d=>d.Material==material&&d.Form==form).Select(Appraisal.Family).Distinct().Count()>=2,"Missing requested product form");
        });
        Test("all eight fixed metal template combinations are authored per product",()=>{
            foreach(var group in definitions.Where(d=>d.Id.StartsWith("sp3_gold")||d.Id.StartsWith("sp3_silver")).GroupBy(Appraisal.Family))
                Check(group.Select(d=>string.Join(",",d.Hotspots.Select(h=>h.TemplateId).OrderBy(x=>x))).Distinct().Count()==8,"Incomplete or duplicate combinations");
            Appraisal.ValidateTemplates(definitions);
        });
        Test("profile selection is independent of enumeration and repeat viewing",()=>{
            var family=definitions.Where(d=>d.FamilyId=="sp3_gold_ring_0").ToList();
            var chosen=Appraisal.ChooseProfile("run",3,family).Id;
            Check(chosen==Appraisal.ChooseProfile("run",3,family.AsEnumerable().Reverse()).Id,"Enumeration changes outcome");
            Check(Enumerable.Range(1,40).Select(day=>Appraisal.ChooseProfile("run",day,family).Id).Distinct().Count()>3,"Daily stock never varies");
            var s=State();var a=Asset(s,chosen);var e=Open(s,a);var copy=Copy(s);
            Open(copy,Catalog.GetAsset(copy,a.AssetId));Check(Catalog.GetAsset(copy,a.AssetId).DefinitionId==chosen,"Reload rerolled profile");
        });
        Test("all appraisal categories have owned physical manuals and illustrated entries",()=>{
            foreach(var category in Appraisal.ManualCategories){Check(definitions.Any(d=>d.ManualFor==category&&d.IsBook&&d.ShopOnly),"Missing manual");Check(Appraisal.Handbook(category).Count>0,"Empty handbook");}
            Check(definitions.Any(d=>d.IsOrganizer&&d.WidthCells==2&&d.HeightCells==2),"Missing organizer");
            foreach(var kind in Appraisal.InstrumentKinds)Check(definitions.Count(d=>d.InstrumentKind==kind)==1,"Instrument catalog mismatch");
        });
        Test("handbook is a generic fixed reference and does not consume patience or XP",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);InspectionV2.Miss(s,e.EncounterId);var before=Snap(s);
            var entries=Appraisal.Handbook("metals");Check(entries.All(x=>!string.IsNullOrEmpty(x.Evidence.Guidance)),"Missing guidance");Check(before==Snap(s),"Handbook changed inspection");
        });
        Test("every handbook and legacy collection index fits five pages including its introduction",()=>{
            var manuals=definitions.Where(d=>!string.IsNullOrEmpty(d.ManualFor)).ToList();
            Check(manuals.Count(d=>d.HandbookTemplates.Count>0)==15,"Missing physical short volumes");
            Check(manuals.All(d=>Appraisal.HandbookPageCount(d)<=5),"A book exceeds five pages");
            Check(manuals.Where(d=>d.HandbookTemplates.Count>0).All(d=>d.IsBook&&d.ShopOnly&&d.WidthCells==1&&d.HeightCells==1),"Volume is not a one-cell physical book");
        });
        Test("splitting books preserves every fixed example exactly once in its category",()=>{
            foreach(var category in Appraisal.ManualCategories)
            {
                var volumes=Appraisal.HandbookVolumes(category);if(volumes.Count==0)continue;
                var actual=volumes.SelectMany(v=>Appraisal.Handbook(v)).Select(e=>e.Evidence.TemplateId).ToList();
                Check(actual.Distinct().Count()==actual.Count,"Duplicate examples across volumes");
                Check(actual.OrderBy(x=>x).SequenceEqual(Appraisal.Handbook(category).Select(e=>e.Evidence.TemplateId).OrderBy(x=>x)),"Truncated examples");
            }
        });
        Test("inspection routes clean and damaged profiles to the same category-specific volume",()=>{
            foreach(var family in definitions.Where(d=>!d.ShopOnly).GroupBy(Appraisal.Family))
                Check(family.Select(d=>Appraisal.HandbookFor(d).Id).Distinct().Count()==1,"Routing disclosed hidden profile");
            Check(Appraisal.HandbookFor(Catalog.GetDefinition("sp3_gold_ring_0_p0")).Id=="sp31_manual_gold_ring","Gold ring misrouted");
            Check(Appraisal.HandbookFor(Catalog.GetDefinition("sp3_rough_yellow_p0")).Id=="sp31_manual_rough_sand","Rough jade misrouted");
        });
        Test("legacy books retain collection access while individual volumes do not unlock other books",()=>{
            var gold=Catalog.GetDefinition("sp31_manual_gold_ring");var silver=Catalog.GetDefinition("sp31_manual_silver_ring");
            var jade=Catalog.GetDefinition("sp31_manual_rough_sand");var legacy=Catalog.GetDefinition("sp3_manual_metals");
            Check(Appraisal.CanReadHandbook(legacy,gold)&&Appraisal.CanReadHandbook(legacy,silver),"Legacy access lost");
            Check(Appraisal.CanReadHandbook(gold,gold)&&!Appraisal.CanReadHandbook(gold,silver)&&!Appraisal.CanReadHandbook(legacy,jade),"Book ownership leaks access");
        });
        Test("oversized and incomplete handbook manifests fail content validation",()=>{
            var copy=Copy(definitions);var volume=copy.First(d=>d.Id=="sp31_manual_gold_ring");
            volume.HandbookTemplates=Appraisal.Handbook("metals").Take(9).Select(e=>e.Evidence.TemplateId).ToList();Reject(()=>Appraisal.ValidateTemplates(copy));
            copy=Copy(definitions);copy.First(d=>d.Id=="sp31_manual_gold_ring").HandbookTemplates.RemoveAt(0);Reject(()=>Appraisal.ValidateTemplates(copy));
        });
        Test("cross-category family mappings and duplicated handbook coverage are rejected",()=>{
            var copy=Copy(definitions);copy.First(d=>d.Id=="sp31_manual_gold_ring").HandbookFamilies.Add("sp3_rough_yellow");Reject(()=>Appraisal.ValidateTemplates(copy));
            copy=Copy(definitions);copy.First(d=>d.Id=="sp31_manual_gold_ring").HandbookTemplates.Add(copy.First(d=>d.Id=="sp31_manual_gold_bar").HandbookTemplates[0]);Reject(()=>Appraisal.ValidateTemplates(copy));
        });
        Test("assay establishes actual material and invalidates old price without certificate",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);var t=Tool(s,"assay");var q=Quote(s,e);
            Check(!Catalog.PublicFacts(a).MaterialConfirmed&&q.Price>20,"Hidden truth leaked before assay");
            Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(Catalog.PublicFacts(a).MaterialConfirmed&&Catalog.PublicFacts(a).PurityBasisPoints==0,"Assay not projected");
            Check(Quote(s,e).Price==20&&!ValuationV2.IsCurrent(s,q,1,1,"buy"),"Assay quote remains stale");
            Check(a.Certificates.Count==0,"Instrument forged a certificate");
        });
        Test("genuine assay persists its reading and never grants XP for simple measurement",()=>{
            var s=State();var a=Asset(s,"sp3_gold_ring_0_p0");var e=Open(s,a);var t=Tool(s,"assay");
            InspectionV2.Miss(s,e.EncounterId);Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(s.Experience==0&&e.ConsecutiveMisses==1,"Measurement farmed XP or reset patience");
            var copy=Copy(s);Catalog.Validate(copy);var stored=Catalog.GetAsset(copy,a.AssetId);
            Check(Catalog.PublicFacts(stored).MaterialConfirmed&&Appraisal.Reading(stored,"assay").Contains("99.90%"),"Reading lost after save");
        });
        Test("repeating measured evidence never rewards again or restores patience",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);var t=Tool(s,"assay");Appraisal.Measure(s,e.EncounterId,t.AssetId);
            var xp=s.Experience;InspectionV2.Miss(s,e.EncounterId);var before=Snap(s);var r=Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(!r.Changed&&before==Snap(s)&&xp==s.Experience,"Repeated tool use changed state");
        });
        Test("two misses forbid every instrument and survive serialized reload",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);var t=Tool(s,"assay");InspectionV2.Miss(s,e.EncounterId);InspectionV2.Miss(s,e.EncounterId);
            s=Copy(s);var before=Snap(s);Reject(()=>Appraisal.Measure(s,e.EncounterId,t.AssetId));Check(before==Snap(s),"Tool bypassed lock");
        });
        Test("foreign, escrowed and non-instrument assets cannot authorize measurements",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);var t=Tool(s,"assay");t.OwnerId="customer";Reject(()=>Appraisal.Measure(s,e.EncounterId,t.AssetId));
            t.OwnerId="player";t.Status="escrow";Reject(()=>Appraisal.Measure(s,e.EncounterId,t.AssetId));Reject(()=>Appraisal.Measure(s,e.EncounterId,a.AssetId));Check(a.Measurements.Count==0,"Rejected tool changed asset");
        });
        Test("instrument compatibility does not reveal unrelated hidden facts",()=>{
            var s=State();var a=Asset(s,"book_reprint");var e=Open(s,a);var t=Tool(s,"assay");var before=Snap(s);Reject(()=>Appraisal.Measure(s,e.EncounterId,t.AssetId));Check(before==Snap(s),"Assay touched publication");
        });
        Test("loupe discovers only its authored microscopic clues",()=>{
            var s=State();var a=Asset(s,"sp3_gold_ring_0_p7");var e=Open(s,a);var t=Tool(s,"loupe");Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(a.DiscoveredEvidenceIds.SequenceEqual(new[]{"metal_wear"}),"Loupe exposed other clues");Check(!Catalog.PublicFacts(a).MaterialConfirmed,"Loupe revealed purity");
        });
        Test("loupe readings only report discovered clues and deduplicate labels",()=>{
            var s=State();var a=Asset(s,"sp3_rough_yellow_p1");var e=Open(s,a);var t=Tool(s,"loupe");
            Check(!Appraisal.Reading(a,"loupe").StartsWith("记录："),"Reading exposed unexamined clues");
            Appraisal.Measure(s,e.EncounterId,t.AssetId);var front=Appraisal.Reading(a,"loupe");
            Catalog.OpenJadeWindow(s,a.AssetId,1000);InspectionV2.Open(s,a.AssetId,e.EncounterId,e.CounterpartyId,"window");Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(front==Appraisal.Reading(a,"loupe"),"Same authored clue repeated across views");
        });
        Test("instrument overflow fails before adding a reading, evidence or XP",()=>{
            var s=State();var a=Asset(s);var e=Open(s,a);var t=Tool(s,"assay");a.StateVersion=int.MaxValue;var before=Snap(s);
            try{Appraisal.Measure(s,e.EncounterId,t.AssetId);throw new Exception("Expected overflow");}catch(OverflowException){}Check(before==Snap(s),"Overflow partially mutated state");
        });
        Test("visible repair preserves plating, identity and fixed measurement record",()=>{
            var s=State();var a=Asset(s,"sp3_gold_ring_0_p7");var e=Open(s,a);var t=Tool(s,"assay");Appraisal.Measure(s,e.EncounterId,t.AssetId);
            InspectionV2.Discover(s,e.EncounterId,"metal_split");Catalog.Repair(s,a.AssetId,"repair-1",1000);
            Check(Catalog.TextureFor(a).EndsWith("_p4"),"Repair erased material flaw");Check(a.Measurements.Contains("assay")&&a.DefinitionId.EndsWith("_p7"),"Repair changed identity");
            Check(Catalog.ActiveHotspots(a).All(h=>h.CauseId=="metal_plating"),"Resolved damage remains active");
        });
        Test("all expanded metal profiles respect actual material recovery floor",()=>{
            foreach(var d in definitions.Where(d=>d.Id.StartsWith("sp3_gold")||d.Id.StartsWith("sp3_silver")))
            {var s=State();s.Experience=100;var a=Asset(s,d.Id);var e=Open(s,a);var q=ValuationV2.Quote(s,e.EncounterId,1,1,60,50,4,3,"sell");Check(q.Price>=ValuationV2.MetalValue(d.WeightMilligrams,d.ActualPurityBasisPoints,d.Material=="gold"?50:3),"Below metal floor: "+d.Id);}
        });
        Test("six rough families have distinct grade distributions and fixed outcomes",()=>{
            var rough=definitions.Where(d=>d.Id.StartsWith("sp3_rough")&&d.Id.EndsWith("_p0")).ToList();Check(rough.Count==6,"Missing rough types");
            Check(rough.Select(d=>$"{d.JadeLowWeight}/{d.JadeMediumWeight}/{d.JadeHighWeight}").Distinct().Count()==6,"Identical rough risks");
            foreach(var d in rough){var s=State();var a=Asset(s,d.Id);var loaded=Copy(s);Check(Catalog.GetAsset(loaded,a.AssetId).JadeGrade==a.JadeGrade,"Reload changed grade");}
        });
        Test("jade light does not change seed or reveal exact grade or yield",()=>{
            var s=State();var a=Asset(s,"sp3_rough_black_p0");var e=Open(s,a);var t=Tool(s,"jade_light");var seed=a.JadeSeed;var grade=a.JadeGrade;var yield=a.JadeYieldCount;
            Appraisal.Measure(s,e.EncounterId,t.AssetId);var reading=Appraisal.Reading(a,"jade_light");a.JadeYieldCount=a.JadeYieldCount%3+1;
            Check(Appraisal.Reading(a,"jade_light")==reading,"Lamp leaked exact yield");Check(a.JadeSeed==seed&&a.JadeGrade==grade&&!a.WindowOpened,"Lamp rerolled/opened stone");
            a.JadeYieldCount=yield;
        });
        Test("jade instruments cannot see closed-window evidence",()=>{
            var s=State();var a=Asset(s,"sp3_rough_yellow_p2");var e=Open(s,a);var t=Tool(s,"jade_light");Appraisal.Measure(s,e.EncounterId,t.AssetId);Check(a.DiscoveredEvidenceIds.Count==0,"Lamp bypassed skin");
            Catalog.OpenJadeWindow(s,a.AssetId,1000);InspectionV2.Open(s,a.AssetId,e.EncounterId,e.CounterpartyId,"window");Appraisal.Measure(s,e.EncounterId,t.AssetId);
            Check(a.DiscoveredEvidenceIds.Contains("jade_inclusion_window"),"Open-window inclusion not recordable");
        });
        Test("four cutting plans create distinct outputs with conserved parent lineage",()=>{
            foreach(var mode in new[]{"pendants","cabochon","bangle","beads"})
            {var s=State();var a=Asset(s,"sp3_rough_white_p0");a.JadeYieldCount=3;var r=Catalog.CutJade(s,a.AssetId,mode,1000);var children=r.CreatedAssetIds.Select(id=>Catalog.GetAsset(s,id)).ToList();Check(a.Status=="consumed"&&children.All(c=>c.ParentAssetId==a.AssetId),"Lost parent");Check(children.All(c=>Catalog.GetDefinition(c.DefinitionId).Form==(mode=="pendants"?"pendant":mode)),"Wrong output");Check(children.Count==(mode=="pendants"?3:1),"Wrong piece count");}
        });
        Test("insufficient bangle material fails atomically and alternatives still work",()=>{
            var s=State();var a=Asset(s,"sp3_rough_white_p0");a.JadeYieldCount=1;var before=Snap(s);Reject(()=>Catalog.CutJade(s,a.AssetId,"bangle",1000));Check(before==Snap(s),"Failed cut consumed stone");Catalog.CutJade(s,a.AssetId,"beads",1000);Check(a.Status=="consumed","Alternative refused");
        });
        Test("cut replay from same saved state produces exactly identical children",()=>{
            var s=State();var a=Asset(s,"sp3_rough_black_p1");var other=Copy(s);Catalog.CutJade(s,a.AssetId,"pendants",1000);Catalog.CutJade(other,a.AssetId,"pendants",1000);Check(Snap(s)==Snap(other),"Cut rerolled on load");
        });
        Test("legacy stones retain old output and missing measurement fields migrate empty",()=>{
            var s=State();var a=Asset(s,"jade_rough");var loaded=JsonConvert.DeserializeObject<CatalogState>(Snap(s).Replace(",\"Measurements\":[]",""))!;Catalog.Validate(loaded);Check(loaded.Assets[0].Measurements.Count==0,"Legacy field migration failed");
            var r=Catalog.CutJade(s,a.AssetId,"cabochon",1000);Check(Catalog.GetAsset(s,r.CreatedAssetIds[0]).DefinitionId=="jade_pendant","Legacy jade output changed");
        });
        Test("malformed fixed templates and missing output references fail closed",()=>{
            var copy=Copy(definitions);copy.First(d=>d.Id=="sp3_gold_ring_0_p1").Hotspots[0].Guidance="Different semantics";Reject(()=>Appraisal.ValidateTemplates(copy));
            copy=Copy(definitions);copy.First(d=>d.Id=="sp3_rough_yellow_p0").JadePendantOutput="missing";Reject(()=>Appraisal.ValidateTemplates(copy));
        });
        Test("corrupt unknown or duplicate measurement IDs cannot load",()=>{
            var s=State();var a=Asset(s);a.Measurements.Add("unknown");Reject(()=>Catalog.Validate(s));a.Measurements.Clear();a.Measurements.Add("assay");a.Measurements.Add("assay");Reject(()=>Catalog.Validate(s));
        });
    }
}
