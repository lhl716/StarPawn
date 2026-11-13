// Fault-injection host. Links the actual CommerceActions.cs and actual Core sources.
// This is NOT a Unity/game integration test. Native operations below are explicit fakes.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StarPawn.Core;

public sealed class GameItem { public object parentInventory=EmporiumEntry.Instance.invElement; public string Id="",Definition="",Custody="",Texture=""; public bool Owned=true; public int Unique; public int unitCount=1; public Tag GetTagReadonly(string key)=>new Tag(Custody); }
public sealed class Tag { readonly string value; public Tag(string v){value=v;} public string GetString()=>value; }
public sealed class Client { public bool Selling=true; public Client GetClientBlueprint()=>this; public bool IsClientSelling()=>Selling; }
public sealed class EmporiumEntry { public static EmporiumEntry Instance=new EmporiumEntry(); public object frontInvinvElement=new object(),backInvinvElement=new object(),invElement=new object(); }
public static class GeneralHelper { public static bool IsItemOwned(GameItem item)=>item.Owned; }
public sealed class PlayerStore
{
 public static PlayerStore Instance=new PlayerStore(); public Client? currentClientInstance;
 public GameItem? Current; public GameItem? GetCurrentlyNegociatedItem()=>Current;
}
public sealed class PaperUI { }
public sealed class ModLog { public void Log(string s){} public void Warning(string s){} public void Error(string s){} }
public static class ModHelper
{
 public static void SetItemOwnerShip(GameItem i,bool value){i.Owned=value;}
 public static Dictionary<string,string> Data=new Dictionary<string,string>();
 public static void SetModData(string mod,string key,string value){Data[key]=value;}
 public static string? GetModData(string mod,string key,string? fallback=null)=>Data.TryGetValue(key,out var value)?value:fallback;
 public static void RemoveModData(string mod,string key){Data.Remove(key);}
}
namespace StarPawn.Game
{
 public sealed class NativeCashResult { public bool Success; public string Error=""; }
 public static class NativeBridge
 {
  public const string CustodyTag="custody";
  public static bool FailMove;
  public static bool TryMove(GameItem item,object target,out string error){error="";if(ReferenceEquals(item.parentInventory,target))return true;if(FailMove){error="capacity";return false;}item.parentInventory=target;return true;}
  public static bool IsReady=true; public static int Cash=10000, Capacity=50, NextId=1, SpawnCalls;
  public static bool FailDiscard; public static string CashFailure="";
  public static Dictionary<string,GameItem> Items=new Dictionary<string,GameItem>();
  public static Dictionary<string,GameItem> Pending=new Dictionary<string,GameItem>();
  public static Dictionary<string,GameItem> Detached=new Dictionary<string,GameItem>();
  public static Dictionary<string,string> Wallet=new Dictionary<string,string>();
  public static bool HasPendingPhysicalChanges=>Pending.Count!=0||Detached.Count!=0;
  public static void Reset(){Items.Clear();Pending.Clear();Detached.Clear();Wallet.Clear();Cash=10000;Capacity=50;NextId=1;SpawnCalls=0;CashFailure="";FailDiscard=false;FailMove=false;IsReady=true;PlayerStore.Instance=new PlayerStore();ModHelper.Data.Clear();}
  public static GameItem? FindAsset(string id)=>Items.TryGetValue(id,out var item)?item:null;
  public static void RefreshAppearance(string id,string texture){var item=FindAsset(id);if(item!=null)item.Texture=texture;}
  public static bool IsInCustody(GameItem i)=>i.Custody.Length>0;
  public static string GetDefinitionId(GameItem i)=>i.Definition;
  public static int GetNativeUniqueId(GameItem i)=>i.Unique;
  public static bool TrySpawnOwned(string definition,string id,out GameItem? item,out string error)
  {
   SpawnCalls++;error="";if(!Pending.TryGetValue(id,out item)){item=new GameItem{Id=id,Definition=definition,Unique=NextId++};Pending[id]=item;}
   if(Items.Count>=Capacity){error="capacity";return false;}Items.Add(id,item);Pending.Remove(id);return true;
  }
  public static bool TryRemoveOwned(string id,out GameItem? item,out string error)
  {
   error="";item=FindAsset(id);if(item==null||!item.Owned||IsInCustody(item)){error="remove";return false;}
   Items.Remove(id);Detached[id]=item;return true;
  }
  public static bool RestoreDetached(GameItem item,out string error)
  {error="";if(Items.Count>=Capacity||!Detached.ContainsKey(item.Id)){error="restore";return false;}Items[item.Id]=item;Detached.Remove(item.Id);return true;}
  public static bool DiscardDetached(GameItem item){if(FailDiscard)return false;return Detached.Remove(item.Id);}
  public static bool CancelPending(string id){Pending.Remove(id);return !Detached.ContainsKey(id);}
  public static bool TrySetCustody(string id,string contract,out string error)
  {error="";var i=FindAsset(id);if(i==null||!i.Owned||(i.Custody!=""&&i.Custody!=contract)){error="custody";return false;}i.Custody=contract;return true;}
  public static bool TryReleaseCustody(string id,string contract,out string error)
  {error="";var i=FindAsset(id);if(i==null||i.Custody!=contract){error="release";return false;}i.Custody="";return true;}
  public static string GetCashOperationStatus(string id)=>Wallet.TryGetValue(id,out var value)?value:"None";
  public static NativeCashResult ApplyCashOnce(string id,int delta,int before)
  {
   if(CashFailure=="before")return new NativeCashResult{Error="injected before wallet"};
   if(before!=Cash||Cash+(long)delta<0)return new NativeCashResult{Error="cash"};
   Cash+=delta;Wallet[id]=CashFailure=="after"?"Uncertain":"Applied";
   return new NativeCashResult{Success=CashFailure!="after",Error="injected after wallet"};
  }
 }
 public sealed class RunState
 {
  public int SchemaVersion=2;public CatalogState Catalog=StarPawn.Core.Catalog.CreateState("test-run");public EconomyState Economy=EconomyService.CreateNewState();
  public long NextOperationSequence=1;public int OffersDay=-1;public List<string> OfferAssetIds=new List<string>();public List<RunReceipt> Ledger=new List<RunReceipt>();public long ArchivedReceipts;
 }
 public sealed class RunReceipt {public string OperationId="";public int Day;public string Description="";public long CashDelta;public string AssetId="";}
 public sealed partial class ModEntry
 {
  private ModLog? log=new ModLog();
  private string NativeCounterpartyId(GameItem i)=>"native:seller:"+i.Id;
  private void CloseWindow(){}
  private void CancelPreparedNativeTrade(){}
  public void Present(string id)=>PresentNativeOffer(id);
  public void AuditPhysical()=>ValidatePhysicalAssets();
  private const string ModId="starpawn";private RunState? state=new RunState();private bool blocked;private string selectedEncounter="",notice="";
  private EconomyRules rules=new EconomyRules();public bool FailPersist;public int TestDay=1;
  private bool EnsureRun()=>!blocked&&state!=null;private bool SameRun()=>EnsureRun();private int Day()=>TestDay;private int Cash()=>NativeBridge.Cash;
  private RunState Clone()=>JsonConvert.DeserializeObject<RunState>(JsonConvert.SerializeObject(state))!;
  private MarketSnapshot Market()=>EconomyService.GetMarketSnapshot(state!.Economy,Day(),rules);
  private string OperationId(RunState n)=>n.Catalog.RunId+":"+n.NextOperationSequence;
  private void Validate(RunState n){Catalog.Validate(n.Catalog);EconomyService.Validate(n.Economy,rules);foreach(var p in n.Economy.PawnContracts.Where(p=>p.Status=="Active"))if(Catalog.GetAsset(n.Catalog,p.AssetId).Status!="escrow")throw new Exception("mismatch");}
  private void Persist(){if(FailPersist)throw new Exception("injected persistence failure");Validate(state!);ModHelper.SetModData(ModId,"unified-run-v2",JsonConvert.SerializeObject(state));}
  private void Commit(RunState n,string id,string description,long delta,string assetId="")
  {
   n.NextOperationSequence++;n.Ledger.Add(new RunReceipt{OperationId=id,CashDelta=delta,Description=description,AssetId=assetId,Day=Day()});Validate(n);
   var paid=NativeBridge.ApplyCashOnce(id,(int)delta,Cash());if(!paid.Success)throw new Exception(paid.Error);
   state=n;try{Persist();}catch{blocked=true;throw;}
  }
  private AssetQuote QuoteByMarket(CatalogState c,string e,string direction,MarketSnapshot m)=>ValuationV2.Quote(c,e,Day(),m.Version,m.GoldSellPerGram,m.GoldBuyPerGram,m.SilverSellPerGram,m.SilverBuyPerGram,direction);
  private PaperUI BeginPage(string a,string b)=>new PaperUI();private static void Text(PaperUI u,float x,float y,float w,float h,string s,int f=11){}
  private void Button(PaperUI u,float x,float y,float w,float h,string s,Action a,string? id=null){}private void Footer(PaperUI u){}private void Show(PaperUI u){}private void OpenPawnLedger(){}
  private void SyncCards(EconomyAction action){} // Card delivery is verified separately from commerce compensation.
  public RunState State=>state!;public bool Blocked=>blocked;public bool Recovery=>HasCommerceRecovery();
  public AssetRecord Add(string definition="console",bool owned=false)
  {
   var a=Catalog.CreateAsset(state!.Catalog,definition,owned?"player":"offer:day1");
   if(owned){NativeBridge.TrySpawnOwned("starpawn."+definition,a.AssetId,out var i,out _);a.NativeItemUniqueId=i!.Unique;}
   else state.OfferAssetIds.Add(a.AssetId);return a;
  }
  public AssetQuote Quote(string id,string direction)
  {
   var e=InspectionV2.Open(state!.Catalog,id,direction+":"+id,"testcounterparty");selectedEncounter=e.EncounterId;return QuoteByMarket(state.Catalog,e.EncounterId,direction,Market());
  }
  public void Buy(AssetQuote q)=>PurchaseAsset(q);public void Sell(AssetQuote q)=>SellAsset(q);public void Process(string id,string action)=>ProcessAsset(id,action);
  public void Command(EconomyCommand c)=>ExecuteEconomy(c);
  public void CloseDay(bool archiveContract=false)
  {var next=Clone();var r=EconomyService.SettleDay(next.Economy,Day(),rules);if(!r.Success)throw new Exception(r.Error);if(archiveContract)next.Economy.PawnContracts.RemoveAll(p=>p.Status!="Active");CompleteNightSettlement(next,r.AssetTransitions);}
 }
}

static class CommerceHarness
{
 static int passed;static void Check(bool ok,string text){if(!ok)throw new Exception("ASSERT: "+text);}
 static void Throws(Action a){try{a();}catch{return;}throw new Exception("Expected failure");}
 static StarPawn.Game.ModEntry New(){StarPawn.Game.NativeBridge.Reset();return new StarPawn.Game.ModEntry();}
 static void Run(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
 static void Main()
 {
  Run("purchase and sale conserve inventory and spread",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");h.Buy(q);Check(StarPawn.Game.NativeBridge.Cash==10000-q.Price,"purchase cash");var s=h.Quote(a.AssetId,"sell");h.Sell(s);Check(StarPawn.Game.NativeBridge.Items.Count==0,"sale removed actual item");Check(StarPawn.Game.NativeBridge.Cash==10000-q.Price+s.Price,"one wallet transfer");Check(h.State.Ledger.Count==2&&!h.Recovery,"ledger and clear");});
  Run("capacity failure cancels unplaced spawn",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");StarPawn.Game.NativeBridge.Capacity=0;Throws(()=>h.Buy(q));Check(StarPawn.Game.NativeBridge.Cash==10000&&!StarPawn.Game.NativeBridge.HasPendingPhysicalChanges&&!h.Recovery,"capacity rollback");Check(h.State.Catalog.Assets[0].OwnerId!="player","domain not changed");});
  Run("wallet pre-mutation failure compensates spawned item",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");StarPawn.Game.NativeBridge.CashFailure="before";Throws(()=>h.Buy(q));Check(StarPawn.Game.NativeBridge.Items.Count==0&&StarPawn.Game.NativeBridge.Cash==10000&&!h.Recovery&&!h.Blocked,"before wallet rollback");});
  Run("wallet post-mutation uncertainty preserves proposal and item",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");StarPawn.Game.NativeBridge.CashFailure="after";Throws(()=>h.Buy(q));Check(h.Blocked&&h.Recovery&&StarPawn.Game.NativeBridge.Items.Count==1,"uncertain held");Check(StarPawn.Game.NativeBridge.Cash==10000-q.Price,"never blindly refunds");});
  Run("state persistence failure blocks without refund",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");h.FailPersist=true;Throws(()=>h.Buy(q));Check(h.Blocked&&h.Recovery&&StarPawn.Game.NativeBridge.Items.Count==1,"persist recovery");Check(StarPawn.Game.NativeBridge.Cash==10000-q.Price,"single cash mutation");});
  Run("failed compensation retains blocking recovery",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");StarPawn.Game.NativeBridge.CashFailure="before";StarPawn.Game.NativeBridge.FailDiscard=true;Throws(()=>h.Buy(q));Check(h.Blocked&&h.Recovery&&StarPawn.Game.NativeBridge.HasPendingPhysicalChanges,"failed undo retained");});
  Run("stale asset version cannot charge",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");a.StateVersion++;Throws(()=>h.Buy(q));Check(StarPawn.Game.NativeBridge.SpawnCalls==0&&StarPawn.Game.NativeBridge.Cash==10000,"stale rejected before physical");});
  Run("forged quote amount cannot charge",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"buy");q.Price++;Throws(()=>h.Buy(q));Check(StarPawn.Game.NativeBridge.SpawnCalls==0,"forged rejected");});
  Run("wrong direction cannot purchase",()=>{var h=New();var a=h.Add();var q=h.Quote(a.AssetId,"sell");Throws(()=>h.Buy(q));Check(StarPawn.Game.NativeBridge.SpawnCalls==0,"direction rejected");});
  Run("bank-only participation does not spawn or remove items",()=>{var h=New();h.Command(new EconomyCommand{Kind=EconomyAction.OpenBank});h.Command(new EconomyCommand{Kind=EconomyAction.Deposit,Amount=200});h.Command(new EconomyCommand{Kind=EconomyAction.Withdraw,Amount=50});Check(StarPawn.Game.NativeBridge.Cash==9850&&h.State.Economy.Bank.Balance==150,"bank conservation");Check(StarPawn.Game.NativeBridge.SpawnCalls==0&&StarPawn.Game.NativeBridge.Items.Count==0,"bank inventory unaffected");});
  Run("borrower pawn redemption releases same physical item",()=>{var h=New();var a=h.Add(owned:true);h.State.Economy.PawnLicensed=true;var uid=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Unique;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=false,Amount=100});var p=h.State.Economy.PawnContracts.Single();Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Custody==p.Id,"custody real");h.Command(new EconomyCommand{Kind=EconomyAction.RedeemPawn,ContractId=p.Id});Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Unique==uid&&StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Custody=="","same item released");Check(StarPawn.Game.NativeBridge.Cash==10000,"principal roundtrip");});
  Run("borrower default removes real collateral",()=>{var h=New();var a=h.Add(owned:true);h.State.Economy.PawnLicensed=true;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=false,Amount=100});h.TestDay=h.State.Economy.PawnContracts.Single().DueDay;h.CloseDay();Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)==null,"forfeited physical removed");Check(Catalog.GetAsset(h.State.Catalog,a.AssetId).OwnerId!="player"&&!h.Recovery,"catalog and native agree");});
  Run("lender customer redemption returns collateral outside inventory",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100});var p=h.State.Economy.PawnContracts.Single();Check(Catalog.GetAsset(h.State.Catalog,a.AssetId).OwnerId!="player"&&StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Owned,"custody not domain ownership");h.Command(new EconomyCommand{Kind=EconomyAction.RedeemPawn,ContractId=p.Id});Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)==null&&StarPawn.Game.NativeBridge.Cash==10000,"customer redemption");});
  Run("lender default transfers same collateral to player",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100});var uid=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Unique;h.TestDay=h.State.Economy.PawnContracts.Single().DueDay;h.CloseDay();Check(Catalog.GetAsset(h.State.Catalog,a.AssetId).OwnerId=="player","forfeit ownership");Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Unique==uid&&StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!.Custody=="","same item unlocked");});
  Run("archived contract transition uses original role safely",()=>{var h=New();var a=h.Add(owned:true);h.State.Economy.PawnLicensed=true;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=false,Amount=100});h.TestDay=h.State.Economy.PawnContracts.Single().DueDay;h.CloseDay(true);Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)==null&&!h.Recovery,"archival fallback");});
  Run("partial jade output capacity rollback keeps original",()=>{var h=New();var a=h.Add("jade_rough",true);a.JadeYieldCount=3;StarPawn.Game.NativeBridge.Capacity=2;Throws(()=>h.Process(a.AssetId,"cut_pendants"));Check(StarPawn.Game.NativeBridge.Items.Count==1&&StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!=null,"original retained");Check(Catalog.GetAsset(h.State.Catalog,a.AssetId).Status=="active"&&h.State.Catalog.Assets.Count==1&&StarPawn.Game.NativeBridge.Cash==10000&&!h.Recovery,"no child sequence leakage");});
  Run("successful jade cutting exchanges original for stable children",()=>{var h=New();var a=h.Add("jade_rough",true);a.JadeYieldCount=3;h.Process(a.AssetId,"cut_pendants");Check(StarPawn.Game.NativeBridge.FindAsset(a.AssetId)==null&&StarPawn.Game.NativeBridge.Items.Count==3,"native consumed and output");Check(h.State.Catalog.Assets.Count(x=>x.ParentAssetId==a.AssetId)==3&&Catalog.GetAsset(h.State.Catalog,a.AssetId).Status=="consumed","stable ancestry");Check(StarPawn.Game.NativeBridge.Cash==9920&&!h.Recovery,"cut fee once");});
  Run("current native negotiation item cannot be sold through starport",()=>{var h=New();var a=h.Add(owned:true);var q=h.Quote(a.AssetId,"sell");PlayerStore.Instance.currentClientInstance=new Client();PlayerStore.Instance.Current=StarPawn.Game.NativeBridge.FindAsset(a.AssetId);Throws(()=>h.Sell(q));Check(StarPawn.Game.NativeBridge.Items.Count==1&&StarPawn.Game.NativeBridge.Cash==10000,"native customer preserved");});
  Run("explicit seller presentation has no cash and no duplicate offer",()=>{var h=New();var a=h.Add();PlayerStore.Instance.currentClientInstance=new Client();h.Present(a.AssetId);var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;Check(!item.Owned&&StarPawn.Game.NativeBridge.Cash==10000,"seller owns physical");Check(!h.State.OfferAssetIds.Contains(a.AssetId)&&Catalog.GetAsset(h.State.Catalog,a.AssetId).NativeItemUniqueId==item.Unique,"one physical identity");Throws(()=>h.Present(a.AssetId));Check(StarPawn.Game.NativeBridge.Items.Count==1,"no duplicate");});
  Run("seller presentation requires present seller",()=>{var h=New();var a=h.Add();Throws(()=>h.Present(a.AssetId));Check(StarPawn.Game.NativeBridge.SpawnCalls==0,"absent seller");PlayerStore.Instance.currentClientInstance=new Client{Selling=false};Throws(()=>h.Present(a.AssetId));Check(StarPawn.Game.NativeBridge.SpawnCalls==0,"buyer cannot source");});
  Run("seller tray failure restores domain and removes uncharged item",()=>{var h=New();var a=h.Add();PlayerStore.Instance.currentClientInstance=new Client();StarPawn.Game.NativeBridge.FailMove=true;Throws(()=>h.Present(a.AssetId));Check(StarPawn.Game.NativeBridge.Items.Count==0&&!h.Recovery&&!h.Blocked,"physical rollback");Check(h.State.OfferAssetIds.Contains(a.AssetId)&&StarPawn.Game.NativeBridge.Cash==10000,"offer and cash unchanged");});
  Run("physical audit rejects missing replacement and custody mismatch",()=>{var h=New();var a=h.Add(owned:true);h.AuditPhysical();var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;item.Unique++;Throws(()=>h.AuditPhysical());item.Unique--;item.Custody="wrong";Throws(()=>h.AuditPhysical());item.Custody="";StarPawn.Game.NativeBridge.Items.Clear();Throws(()=>h.AuditPhysical());});
  Run("physical audit accepts both custody roles and consumed ancestry",()=>{var h=New();h.State.Economy.PawnLicensed=true;var a=h.Add(owned:true);var b=h.Add();h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,Amount=100,PlayerIsLender=false});h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=b.AssetId,Amount=100,PlayerIsLender=true});h.AuditPhysical();var j=h.Add("jade_rough",true);h.Process(j.AssetId,"cut_pendants");h.AuditPhysical();});
  Run("native seller pawn takes same displayed instance into saved custody",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;PlayerStore.Instance.currentClientInstance=new Client();h.Present(a.AssetId);var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;var spawns=StarPawn.Game.NativeBridge.SpawnCalls;h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100});Check(ReferenceEquals(item,StarPawn.Game.NativeBridge.FindAsset(a.AssetId))&&item.Owned&&item.Custody!=""&&ReferenceEquals(item.parentInventory,EmporiumEntry.Instance.invElement),"same item saved custody");Check(StarPawn.Game.NativeBridge.SpawnCalls==spawns&&StarPawn.Game.NativeBridge.Cash==9900,"no duplicate physical or cash");h.AuditPhysical();});
  Run("native collateral capacity failure restores seller ownership",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;PlayerStore.Instance.currentClientInstance=new Client();h.Present(a.AssetId);var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;StarPawn.Game.NativeBridge.FailMove=true;Throws(()=>h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100}));Check(!item.Owned&&item.Custody==""&&ReferenceEquals(item.parentInventory,EmporiumEntry.Instance.frontInvinvElement),"seller item restored");Check(StarPawn.Game.NativeBridge.Cash==10000&&h.State.Economy.PawnContracts.Count==0&&!h.Recovery&&!h.Blocked,"no partial pawn");});
  Run("native collateral wallet failure restores exact tray item",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;PlayerStore.Instance.currentClientInstance=new Client();h.Present(a.AssetId);var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;StarPawn.Game.NativeBridge.CashFailure="before";Throws(()=>h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100}));Check(!item.Owned&&item.Custody==""&&ReferenceEquals(item.parentInventory,EmporiumEntry.Instance.frontInvinvElement)&&ReferenceEquals(item,StarPawn.Game.NativeBridge.FindAsset(a.AssetId)),"rollback same item");Check(StarPawn.Game.NativeBridge.Cash==10000&&!h.Recovery&&!h.Blocked,"no cash or unresolved journal");});
  Run("native collateral rejects absent seller and substituted instance",()=>{var h=New();var a=h.Add();h.State.Economy.PawnLicensed=true;PlayerStore.Instance.currentClientInstance=new Client();h.Present(a.AssetId);var item=StarPawn.Game.NativeBridge.FindAsset(a.AssetId)!;PlayerStore.Instance.currentClientInstance=null;Throws(()=>h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100}));PlayerStore.Instance.currentClientInstance=new Client();item.Unique++;Throws(()=>h.Command(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=a.AssetId,PlayerIsLender=true,Amount=100}));Check(!item.Owned&&StarPawn.Game.NativeBridge.Cash==10000,"identity checks before payment");});
  Console.WriteLine("COMMERCE_HARNESS_PASSED="+passed+"; actual CommerceActions/NativeSourcing/Core source + fake native boundary; NOT an in-game test");
 }
}
