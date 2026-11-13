// Test-only boundary doubles. The production NativeTradeRoot/NativeTradeBridge and
// Core sources are compiled unchanged. No game code or live save is executed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;
using StarPawn.Core;
using StarPawn.Game;

public class ModLog { public void Log(string s){} public void Warning(string s){} public void Error(string s){} }
public static class ModHook { public static event Action? OnGameLoadedInit; public static event Action<string>? OnNewGameStarted; public static void Load()=>OnGameLoadedInit?.Invoke(); public static void New()=>OnNewGameStarted?.Invoke("test"); }
public static class ModHelper {
 public static readonly Dictionary<string,string> Data=new Dictionary<string,string>(); public static string FailKey="";
 public static void SetModData(string mod,string key,string value){if(FailKey==key)throw new Exception("injected write failure");Data[mod+":"+key]=value;}
 public static string? GetModData(string mod,string key,string? fallback)=>Data.TryGetValue(mod+":"+key,out var value)?value:fallback;
 public static void RemoveModData(string mod,string key)=>Data.Remove(mod+":"+key);
 public static GameItem SpawnItem(string id)=>new GameItem { Definition=id };
 public static void SetItemOwnerShip(GameItem item,bool value)=>item.Owned=value;
}
public class ItemTag { public string Value=""; public void SetString(string s)=>Value=s; }
public class GameItem { public int unitCount=1,uniqueId=10; public string Asset="",Definition="starpawn.test"; public bool Owned,Back=true,Custody; public ItemTag Tag=new ItemTag(); public bool IsRootedInBackInv()=>Back; public long GetNegociatedValue()=>200; public void ModifyTag(string key,Action<ItemTag> action){action(Tag);Asset=Tag.Value;} }
public static class GeneralHelper { public static bool IsItemOwned(GameItem item)=>item.Owned; }
public class StoreClient { public string identifier="same-blueprint";public bool useClientBudget;public int Budget=100000;public bool Selling=true;public bool IsClientSelling()=>Selling;public bool IsClientBuying()=>!Selling;public int GetBudget()=>Budget;public bool HasBudgetLeftToBuy(GameItem item)=>Budget>=item.GetNegociatedValue(); }
public class StoreClientInstance { public StoreClient Blueprint=new StoreClient();public StoreClient GetClientBlueprint()=>Blueprint; }
public class Inventory { public List<GameItem> childItems=new List<GameItem>(); }
public class EmporiumEntry { public static EmporiumEntry Instance=new EmporiumEntry();public Inventory backInvinvElement=new Inventory(),frontInvinvElement=new Inventory(); }
public class NegociationUIManager { public static NegociationUIManager Instance=new NegociationUIManager();public void RefreshUI(){} public int GetFinalOfferValue(GameItem item)=>(int)item.GetNegociatedValue(); }
public class PaperUI {}
public class PlayerStore {
 public static PlayerStore Instance=new PlayerStore();public static bool HasInstance=>true;public int saveSlotId=4,Cash=100000,NativeCalls;public string Failure="";public GameItem Item=new GameItem();public StoreClientInstance? currentClientInstance=new StoreClientInstance();
 public int GetCash()=>Cash;public GameItem GetCurrentlyNegociatedItem()=>Item;public void ExamineItem(GameItem item){Item=item;}
 public void BuyItemFromTable()=>BuyItem(Item);public void SellItemFromTable()=>SellItem(Item);public bool CanBuyItemFromTable()=>true;public bool CanSellThisItem(GameItem item)=>true;public void BuyAllItemFromTable(){}public void SellAllItemFromTable(){}
 public void BuyItem(GameItem item)=>Trade(item,NativeTradeDirection.PlayerBuys);public void SellItem(GameItem item)=>Trade(item,NativeTradeDirection.PlayerSells);
 private void Trade(GameItem item,NativeTradeDirection direction){
  if(!NativeTradeBridge.BeginTrade(item,direction,out var execution))return;
  Exception? error=null;
  try {if(Failure=="decline")return;NativeCalls++;var value=(int)NativeTradeBridge.ValueAtTradeSite(item);Cash+=direction==NativeTradeDirection.PlayerBuys?-value:value;if(Failure=="cash-only")throw new Exception("injected partial native failure");item.Owned=direction==NativeTradeDirection.PlayerBuys;}
  catch(Exception e){error=e;}
  finally{NativeTradeBridge.EndTrade(execution,error);}
 }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){} }
 public static class AccessTools {public static MethodInfo Method(Type type,string name)=>type.GetMethod(name)!;}
 public class CodeInstruction {public OpCode opcode;public object? operand;public bool Calls(MethodInfo m)=>Equals(operand,m);}
}
namespace StarPawn.Game {
 public static class NativeBridge {
  public const string AssetIdTag="starpawn.asset";public static bool IsReady=true;public static int GameDay=1;
  public static bool IsStarPawnAsset(GameItem? i)=>i!=null&&i.Definition.StartsWith("starpawn.")&&!string.IsNullOrEmpty(i.Asset);
  public static bool IsInCustody(GameItem i)=>i.Custody;public static string GetAssetId(GameItem i)=>i.Asset;public static string GetDefinitionId(GameItem i)=>i.Definition;public static int GetNativeUniqueId(GameItem i)=>i.uniqueId;
  public static GameItem? FindAsset(string id)=>PlayerStore.Instance.Item.Asset==id?PlayerStore.Instance.Item:null;
  public static bool FailMove;public static bool TryMove(GameItem i,Inventory target,out string error){error=FailMove?"capacity":"";if(FailMove)return false;if(!target.childItems.Contains(i))target.childItems.Add(i);i.Back=ReferenceEquals(target,EmporiumEntry.Instance.backInvinvElement);return true;}
 }
 public sealed partial class ModEntry {
  const string ModId="starpawn",SaveKey="unified-run-v2";
  ModLog? log=new ModLog();RunState? state;PlayerStore? currentStore;int currentSlot;bool blocked;string notice="",selectedAsset="",selectedEncounter="",selectedView="front";readonly EconomyRules rules=new EconomyRules();
  bool SameRun()=>!blocked&&state!=null&&ReferenceEquals(currentStore,PlayerStore.Instance)&&currentSlot==PlayerStore.Instance.saveSlotId;
  bool EnsureRun()=>SameRun();int Day()=>NativeBridge.GameDay;int Cash()=>PlayerStore.Instance.Cash;
  MarketSnapshot Market()=>EconomyService.GetMarketSnapshot(state!.Economy,Day(),rules);
  void Validate(RunState s){Catalog.Validate(s.Catalog);EconomyService.Validate(s.Economy,rules);}
  RunState Clone()=>JsonConvert.DeserializeObject<RunState>(JsonConvert.SerializeObject(state))!;
  void Persist(){if(!SameRun())throw new Exception("wrong run");Validate(state!);var raw=JsonConvert.SerializeObject(state);ModHelper.SetModData(ModId,SaveKey,raw);if(ModHelper.GetModData(ModId,SaveKey,null)!=raw)throw new Exception("readback");}
  string OperationId(RunState s)=>s.Catalog.RunId+":"+s.NextOperationSequence;
  void Record(RunState s,string operation,string text,long delta,string asset=""){s.NextOperationSequence++;s.Ledger.Add(new RunReceipt{OperationId=operation,Description=text,CashDelta=delta,AssetId=asset,Day=Day()});}
  AssetQuote QuoteByMarket(CatalogState c,string id,string dir,MarketSnapshot m)=>ValuationV2.Quote(c,id,Day(),m.Version,m.GoldSellPerGram,m.GoldBuyPerGram,m.SilverSellPerGram,m.SilverBuyPerGram,dir);
  PaperUI BeginPage(string a,string b)=>new PaperUI();static void Text(PaperUI u,float a,float b,float c,float d,string s,int font=11){}
  void Button(PaperUI u,float a,float b,float c,float d,string s,Action action,string? id=null){}void Footer(PaperUI u){}void Show(PaperUI u){}void OpenOwned(){}void OpenNativeAsset(string id){}
  internal void Setup(bool selling=false){
   ModHelper.Data.Clear();ModHelper.FailKey="";NativeBridge.IsReady=true;NativeBridge.FailMove=false;EmporiumEntry.Instance=new EmporiumEntry();NativeBridge.GameDay=1;PlayerStore.Instance=new PlayerStore();currentStore=PlayerStore.Instance;currentSlot=4;
   currentStore.currentClientInstance!.Blueprint.Selling=!selling;state=new RunState();var def=Catalog.Definitions.First(d=>d.Hotspots.Count>0);var asset=Catalog.CreateAsset(state.Catalog,def.Id,selling?"player":"native-customer");asset.NativeItemUniqueId=10;
   currentStore.Item=new GameItem{Asset=asset.AssetId,Definition="starpawn."+def.Id,Owned=selling};InitNativeTrade();ModHook.Load();
   var sites=(HashSet<string>)typeof(NativeTradeBridge).GetField("VerifiedSites",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;sites.Add("BuyItem");sites.Add("SellItem");sites.Add("CanBuyItemFromTable");
  }
  internal void End()=>StopNativeTrade();internal void Prepare()=>PrepareNativeTrade(PlayerStore.Instance.Item.Asset);internal void Submit()=>SubmitNativeTrade(nativePending!.QuoteId);
  internal void Place()=>PlaceAndPrepareNativeTrade(PlayerStore.Instance.Item.Asset);
  internal RunState State=>state!;internal NativeTradePending? Pending=>nativePending;internal bool Blocked=>blocked;internal void Reload()=>ReconcileNativeTradeOnLoad();internal string Counterparty()=>NativeCounterpartyId(PlayerStore.Instance.Item);
 }
}
public static class AuditMain {
 static int total,failed;static void Check(bool condition,string description){if(!condition)throw new Exception(description);}
 static void Run(string name,Action<ModEntry> action,bool selling=false){var app=new ModEntry();try{app.Setup(selling);action(app);Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}finally{app.End();total++;}}
 static void MustThrow(Action a){bool did=false;try{a();}catch{did=true;}Check(did,"expected rejection");}
 public static int Main(){
  Run("place_offer_preserves_cash_owner_and_identity_until_confirm",a=>{var item=PlayerStore.Instance.Item;item.Back=false;a.Place();Check(item.Back&&ReferenceEquals(item,PlayerStore.Instance.GetCurrentlyNegociatedItem())&&PlayerStore.Instance.Cash==100000&&!item.Owned&&a.Pending!=null,"placement changed business state");});
  Run("place_rejects_busy_tray_and_wrong_customer",a=>{EmporiumEntry.Instance.backInvinvElement.childItems.Add(new GameItem());MustThrow(a.Place);Check(a.Pending==null,"busy tray prepared");EmporiumEntry.Instance.backInvinvElement.childItems.Clear();PlayerStore.Instance.currentClientInstance!.Blueprint.Selling=false;MustThrow(a.Place);Check(a.Pending==null&&PlayerStore.Instance.Cash==100000,"wrong customer prepared");});
  Run("place_capacity_failure_does_not_prepare_or_charge",a=>{NativeBridge.FailMove=true;MustThrow(a.Place);Check(a.Pending==null&&PlayerStore.Instance.Cash==100000&&!PlayerStore.Instance.Item.Owned,"failed placement changed money");});
  Run("buy_once_root_publishes_without_second_wallet",a=>{var cash=PlayerStore.Instance.Cash;a.Prepare();var p=a.Pending!;Check(cash==PlayerStore.Instance.Cash,"prepare charged");a.Submit();Check(PlayerStore.Instance.Cash==cash-p.Price&&PlayerStore.Instance.NativeCalls==1,"wrong cash");Check(a.State.Ledger.Count==1&&a.State.Catalog.Assets[0].OwnerId=="player"&&a.Pending==null,"core not committed");PlayerStore.Instance.BuyItem(PlayerStore.Instance.Item);Check(PlayerStore.Instance.NativeCalls==1,"repeat charged");});
  Run("sell_once_root_publishes_without_second_wallet",a=>{var cash=PlayerStore.Instance.Cash;a.Prepare();var p=a.Pending!;a.Submit();Check(PlayerStore.Instance.Cash==cash+p.Price&&!PlayerStore.Instance.Item.Owned&&a.State.Ledger.Count==1,"wrong sale");PlayerStore.Instance.SellItem(PlayerStore.Instance.Item);Check(PlayerStore.Instance.NativeCalls==1,"repeat sale");},true);
  Run("prepared_other_business_rejects_old_snapshot",a=>{a.Prepare();a.State.NextOperationSequence++;MustThrow(a.Submit);Check(PlayerStore.Instance.NativeCalls==0&&a.Pending==null,"stale state executed");});
  Run("different_customer_same_blueprint_rejects_instance",a=>{a.Prepare();PlayerStore.Instance.currentClientInstance=new StoreClientInstance();a.Submit();Check(PlayerStore.Instance.NativeCalls==0&&a.Pending==null,"rebound customer executed");});
  Run("different_counter_item_rejects",a=>{a.Prepare();PlayerStore.Instance.Item=new GameItem {Asset="other",Definition="starpawn.test"};a.Submit();Check(PlayerStore.Instance.NativeCalls==0,"other item executed");});
  Run("asset_version_change_rejects",a=>{a.Prepare();a.State.Catalog.Assets[0].StateVersion++;MustThrow(a.Submit);Check(PlayerStore.Instance.NativeCalls==0,"stale asset");});
  Run("market_revision_change_rejects",a=>{a.Prepare();a.State.Economy.MarketRevision++;MustThrow(a.Submit);Check(PlayerStore.Instance.NativeCalls==0,"stale market");});
  Run("game_day_change_rejects",a=>{a.Prepare();NativeBridge.GameDay++;MustThrow(a.Submit);Check(PlayerStore.Instance.NativeCalls==0,"stale day");});
  Run("cash_depleted_after_prepare_does_not_charge",a=>{a.Prepare();PlayerStore.Instance.Cash=0;a.Submit();Check(PlayerStore.Instance.Cash==0&&PlayerStore.Instance.NativeCalls==0&&a.Pending==null,"insufficient charged");});
  Run("unconfirmed_direct_native_call_rejected",a=>{a.Prepare();PlayerStore.Instance.BuyItem(PlayerStore.Instance.Item);Check(PlayerStore.Instance.NativeCalls==0,"unconfirmed direct call");});
  Run("reload_prepared_cancels_without_ownership_or_cash",a=>{a.Prepare();a.Reload();Check(a.Pending==null&&PlayerStore.Instance.NativeCalls==0&&!PlayerStore.Instance.Item.Owned,"prepared reload executed");});
  Run("reload_executing_preserves_and_rejects",a=>{a.Prepare();a.Pending!.Status="Executing";ModHelper.SetModData("starpawn","native-trade-root-pending-v1",JsonConvert.SerializeObject(a.Pending));MustThrow(a.Reload);Check(ModHelper.GetModData("starpawn","native-trade-root-pending-v1",null)!.Contains("Executing"),"journal removed");});
  Run("changed_slot_rejects_live_quote",a=>{a.Prepare();PlayerStore.Instance.saveSlotId=5;MustThrow(a.Submit);Check(PlayerStore.Instance.NativeCalls==0,"cross slot executed");});
  Run("partial_native_cash_change_blocks_with_recovery",a=>{a.Prepare();PlayerStore.Instance.Failure="cash-only";a.Submit();Check(a.Blocked&&a.Pending?.Status=="Review"&&NativeTradeBridge.GetReconciliationReceipt()!=null,"partial mutation not blocked");});
  Run("root_persist_failure_after_native_blocks",a=>{a.Prepare();ModHelper.FailKey="unified-run-v2";a.Submit();Check(a.Blocked&&a.Pending?.Status=="Review"&&NativeTradeBridge.GetReconciliationReceipt()!=null,"commit failure not blocked");});
  Run("journal_failure_before_native_blocks_without_charge",a=>{a.Prepare();ModHelper.FailKey="native-trade-root-pending-v1";MustThrow(a.Submit);Check(a.Blocked&&PlayerStore.Instance.NativeCalls==0,"journal failure executed");});
  Run("native_precondition_decline_has_no_root_commit",a=>{a.Prepare();PlayerStore.Instance.Failure="decline";a.Submit();Check(!a.Blocked&&a.Pending==null&&a.State.Ledger.Count==0,"decline recorded");});
  Run("inspection_lock_survives_new_id_and_serialized_reload",a=>{var c=a.State.Catalog;var asset=c.Assets[0];var cp=a.Counterparty();var e=InspectionV2.Open(c,asset.AssetId,"one",cp);InspectionV2.Miss(c,e.EncounterId);InspectionV2.Miss(c,e.EncounterId);var reopened=InspectionV2.Open(c,asset.AssetId,"new-id",cp);Check(reopened.Locked&&reopened.EncounterId=="one","new ID reset lock");var copy=JsonConvert.DeserializeObject<CatalogState>(JsonConvert.SerializeObject(c))!;reopened=InspectionV2.Open(copy,asset.AssetId,"third",cp);var result=InspectionV2.Discover(copy,reopened.EncounterId,Catalog.GetDefinition(asset.DefinitionId).Hotspots[0].Id);Check(reopened.Locked&&result.Kind=="locked"&&copy.Experience==0,"reload reset lock");a.Prepare();Check(a.State.Catalog.Encounters.Single(x=>!x.Completed).Locked,"quote reset lock");});
  Run("ordinary_item_keeps_native_behavior",a=>{PlayerStore.Instance.Item.Definition="base.item";PlayerStore.Instance.BuyItem(PlayerStore.Instance.Item);Check(PlayerStore.Instance.Cash==99800&&PlayerStore.Instance.NativeCalls==1,"vanilla affected");});
  Console.WriteLine("RESULT "+(total-failed)+"/"+total+" passed; boundary doubles, not an in-game test.");return failed==0?0:1;
 }
}
