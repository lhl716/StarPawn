// Uses the actual BankCardsRoot/Core source with an explicit card-delivery fake.
// This validates account references and failure policy, not Unity inventory or Harmony.
using System;
using System.Collections.Generic;
using System.Linq;
using StarPawn.Core;
public sealed class GameItem { }
public sealed class ModLog {public void Log(string s){}public void Error(string s){}}
public sealed class PaperElement {public bool raycast;}
public sealed class PaperUI {public PaperElement Last=new PaperElement();public void AddImage(float x,float y,float w,float h,string sprite,string mode,string id){}}
namespace StarPawn.Game
{
 public sealed class NativeCardInfo {public string Kind="",AccountId="",RunId="";public int Serial;}
 public static class NativeCards
 {
  public static bool Full;public static int Attempts;public static List<NativeCardInfo> Issued=new List<NativeCardInfo>();
  public static void Reset(){Full=false;Attempts=0;Issued.Clear();}
  public static void Enable(ModLog l,Action<NativeCardInfo> inspect){}public static void Disable(){}
  public static bool TryIssue(string kind,string account,int serial,string run,out GameItem? item,out string error)
  {Attempts++;item=null;error="";if(Issued.Any(c=>c.Kind==kind&&c.AccountId==account&&c.Serial==serial&&c.RunId==run)){item=new GameItem();return true;}if(Full){error="inventory full";return false;}Issued.Add(new NativeCardInfo{Kind=kind,AccountId=account,Serial=serial,RunId=run});item=new GameItem();return true;}
 }
 public sealed class CardTestRun {public CatalogState Catalog=StarPawn.Core.Catalog.CreateState("card-test");public EconomyState Economy=EconomyService.CreateNewState();}
 public sealed partial class ModEntry
 {
  private ModLog? log=new ModLog();private CardTestRun? state=new CardTestRun();private string notice="";private EconomyRules rules=new EconomyRules();
  public int Cash=10000,BankOpened,CreditOpened;public bool Ready=true;
  private bool EnsureRun()=>Ready;private bool SameRun()=>Ready;
  private void OpenBank(){BankOpened++;}private void OpenCreditLoans(){CreditOpened++;}
  private PaperUI BeginPage(string a,string b)=>new PaperUI();private static void Text(PaperUI u,float x,float y,float w,float h,string s,int f=11){}
  private void Button(PaperUI u,float x,float y,float w,float h,string label,Action action,string? id=null){}private void Footer(PaperUI u){}private void Show(PaperUI u){}
  private void ExecuteEconomy(EconomyCommand command)
  {command.Sequence=state!.Economy.LastCommandSequence+1;command.TransactionId="cards:"+command.Sequence;command.GameDay=1;command.AvailableCash=Cash;var r=EconomyService.Apply(state.Economy,command,rules);if(!r.Success)throw new Exception(r.Error);Cash+=(int)r.CashDelta;SyncCards(command.Kind);}
  public CardTestRun State=>state!;public string Notice=>notice;
  public void Command(EconomyAction action,long amount=0)=>ExecuteEconomy(new EconomyCommand{Kind=action,Amount=amount});
  public void Claim(string kind)=>ClaimBankCard(kind);public void View()=>OpenCards();public bool Valid(NativeCardInfo card)=>IsCurrentBankCard(card);public void Inspect(NativeCardInfo card)=>InspectBankCard(card);
 }
}
static class BankCardsHarness
{
 static int passed;static void Check(bool value,string why){if(!value)throw new Exception(why);}static void Throws(Action a){try{a();}catch{return;}throw new Exception("Expected failure");}
 static StarPawn.Game.ModEntry New(){StarPawn.Game.NativeCards.Reset();return new StarPawn.Game.ModEntry();}
 static void Run(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
 static void Main()
 {
  Run("viewing cards never opens account or issues plastic",()=>{var h=New();h.View();Check(h.State.Economy.Bank.Status=="None"&&!h.State.Economy.Credit.Signed&&StarPawn.Game.NativeCards.Attempts==0,"view side effect");});
  Run("savings opening issues account reference and repeated claim is idempotent",()=>{var h=New();h.Command(EconomyAction.OpenBank);var c=StarPawn.Game.NativeCards.Issued.Single();h.Claim("savings");Check(h.Valid(c)&&c.AccountId==h.State.Economy.Bank.AccountId&&StarPawn.Game.NativeCards.Issued.Count==1,"card identity");});
  Run("full inventory leaves account valid and retry uses same account",()=>{var h=New();StarPawn.Game.NativeCards.Full=true;h.Command(EconomyAction.OpenBank);var id=h.State.Economy.Bank.AccountId;Check(h.State.Economy.Bank.Status=="Open"&&StarPawn.Game.NativeCards.Issued.Count==0,"account must remain");StarPawn.Game.NativeCards.Full=false;h.Claim("savings");Check(StarPawn.Game.NativeCards.Issued.Single().AccountId==id,"same account retry");});
  Run("duplicate card references do not duplicate account money",()=>{var h=New();h.Command(EconomyAction.OpenBank);h.Command(EconomyAction.Deposit,100);var c=StarPawn.Game.NativeCards.Issued.Single();var copy=new StarPawn.Game.NativeCardInfo{Kind=c.Kind,AccountId=c.AccountId,Serial=c.Serial,RunId=c.RunId};h.Inspect(c);h.Inspect(copy);Check(h.BankOpened==2&&h.State.Economy.Bank.Balance==100&&h.Cash==9900,"one authority");});
  Run("savings reissue invalidates old serial and preserves balance",()=>{var h=New();h.Command(EconomyAction.OpenBank);h.Command(EconomyAction.Deposit,100);var old=StarPawn.Game.NativeCards.Issued.Single();h.Command(EconomyAction.ReissueBankCard);Check(!h.Valid(old)&&h.Valid(StarPawn.Game.NativeCards.Issued.Last())&&h.State.Economy.Bank.Balance==100,"serial reissue");});
  Run("closed savings account invalidates retained card",()=>{var h=New();h.Command(EconomyAction.OpenBank);var c=StarPawn.Game.NativeCards.Issued.Single();h.Command(EconomyAction.CloseBank);h.Inspect(c);Check(!h.Valid(c)&&h.BankOpened==0,"closed card access");});
  Run("credit loss and reissue preserve debt",()=>{var h=New();h.Command(EconomyAction.OpenCredit,1000);h.Command(EconomyAction.BorrowCredit,200);var c=StarPawn.Game.NativeCards.Issued.Single();StarPawn.Game.NativeCards.Issued.Clear();Check(h.State.Economy.Credit.Principal==200,"loss cannot remove debt");h.Command(EconomyAction.ReissueCreditCard);Check(!h.Valid(c)&&h.Valid(StarPawn.Game.NativeCards.Issued.Single())&&h.State.Economy.Credit.Principal==200&&h.Cash==10200,"reissue preserves obligation");Throws(()=>h.Command(EconomyAction.CloseCredit));});
  Run("same-day credit reopen has a distinct account identity",()=>{var h=New();h.Command(EconomyAction.OpenCredit,1000);var old=StarPawn.Game.NativeCards.Issued.Single();h.Command(EconomyAction.CloseCredit);h.Command(EconomyAction.OpenCredit,1000);Check(!h.Valid(old)&&h.State.Economy.Credit.AccountId!=old.AccountId,"same-day identity reuse");});
  Run("foreign run and corrupt serial cannot open account",()=>{var h=New();h.Command(EconomyAction.OpenBank);var c=StarPawn.Game.NativeCards.Issued.Single();var wrong=new StarPawn.Game.NativeCardInfo{Kind=c.Kind,AccountId=c.AccountId,Serial=c.Serial,RunId="other-run"};h.Inspect(wrong);wrong.RunId=c.RunId;wrong.Serial++;h.Inspect(wrong);Check(h.BankOpened==0,"invalid reference opened account");});
  Run("legacy signed credit obtains identity without clearing principal",()=>{var h=New();h.Command(EconomyAction.OpenCredit,1000);h.Command(EconomyAction.BorrowCredit,150);h.State.Economy.Credit.AccountId="";h.State.Economy.Credit.CardSerial=0;var before=h.State.Economy.Credit.FirstInterestDay;h.Command(EconomyAction.ReissueCreditCard);Check(h.State.Economy.Credit.AccountId!=""&&h.State.Economy.Credit.CardSerial==1&&h.State.Economy.Credit.Principal==150&&h.State.Economy.Credit.FirstInterestDay==before,"legacy debt changed");});
  Console.WriteLine("BANK_CARD_POLICY_PASSED="+passed+"; actual BankCardsRoot/Core plus delivery fake; NOT Unity/Harmony validation");
 }
}
