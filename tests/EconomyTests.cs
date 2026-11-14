using System;
using System.Linq;
using System.Text.Json;
using StarPawn.Core;

public static class EconomyTests
{
    private static int passed;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private sealed class Fixture
    {
        public EconomyState State = EconomyService.CreateNewState();
        public EconomyRules Rules = new EconomyRules { BankInterestBps = 100, CreditInterestBps = 100, LoanInterestBps = 100, PawnInterestBps = 100, PawnLicenseCost = 0 };
        public long Cash = 10000;
        private int id;
        public EconomyCommand Command(EconomyAction kind, int day, long amount = 0) => new EconomyCommand {
            Kind = kind, GameDay = day, Amount = amount, AvailableCash = Cash, Sequence = State.LastCommandSequence + 1, TransactionId = "test-" + (++id)
        };
        public EconomyResult Do(EconomyCommand command)
        {
            command.AvailableCash = Cash;
            var result = EconomyService.Apply(State, command, Rules);
            if (!result.Success) throw new Exception(result.Error);
            Cash += result.CashDelta;
            return result;
        }
        public EconomyResult Do(EconomyAction kind, int day, long amount = 0) => Do(Command(kind, day, amount));
        public EconomyResult Close(int day) { var r = EconomyService.SettleDay(State, day, Rules); Check(r.Success, r.Error); return r; }
    }

    public static int Main()
    {
        Test("Unparticipating state and pure market queries have no side effects", () => {
            var f = new Fixture(); var before = Encode(f.State);
            var settled = f.Close(250);
            var a = EconomyService.GetMarketSnapshot(f.State, 1, f.Rules);
            var b = EconomyService.GetMarketSnapshot(f.State, 250, f.Rules);
            Check(Encode(f.State) == before && settled.CashDelta == 0 && settled.AssetTransitions.Count == 0);
            Check(a.GoldSellPerGram == b.GoldSellPerGram && a.Securities[0].BuyPrice == b.Securities[0].BuyPrice);
        });
        Test("Late bank opening never backfills interest and daily settlement is idempotent", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 20); f.Do(EconomyAction.Deposit, 20, 1000);
            Check(f.Cash == 9000 && f.State.Bank.Balance == 1000);
            f.Close(20); Check(f.State.Bank.Balance == 1000);
            f.Close(21); Check(f.State.Bank.Balance == 1010);
            var snapshot = Encode(f.State); var duplicate = f.Close(21);
            Check(duplicate.Duplicate && Encode(f.State) == snapshot);
            f.Close(23); Check(f.State.Bank.Balance == 1030);
        });
        Test("Later deposit becomes interest eligible the next day", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1); f.Do(EconomyAction.Deposit, 20, 1000);
            Check(f.State.Bank.Balance == 1000 && f.State.Bank.LastSettledDay == 19);
            f.Close(20); Check(f.State.Bank.Balance == 1000);
            f.Close(21); Check(f.State.Bank.Balance == 1010);
        });
        Test("Card reissue references one balance and reopening gets a fresh account cursor", () => {
            var f = new Fixture(); var old = f.Do(EconomyAction.OpenBank, 1).EntityId;
            f.Do(EconomyAction.Deposit, 1, 1000);
            Check(f.Do(EconomyAction.ReissueBankCard, 1).EntityId == old && f.State.Bank.Balance == 1000 && f.State.Bank.CardSerial == 2);
            f.Do(EconomyAction.Withdraw, 1, 1000); f.Do(EconomyAction.CloseBank, 1);
            var next = f.Do(EconomyAction.OpenBank, 20).EntityId; Check(next != old && f.State.Bank.FirstInterestDay == 21);
            f.Do(EconomyAction.Deposit, 20, 1000); f.Close(20); Check(f.State.Bank.Balance == 1000);
        });
        Test("Insufficient cash rejects every staged balance/cursor change", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1); f.Cash = 10;
            var before = Encode(f.State); var bad = EconomyService.Apply(f.State, f.Command(EconomyAction.Deposit, 12, 1000), f.Rules);
            Check(!bad.Success && bad.CashDelta == 0 && Encode(f.State) == before);
        });
        Test("Duplicate buttons and archived stale tokens cannot duplicate cash", () => {
            var f = new Fixture(); f.Rules.RecentOperationLimit = 2;
            var open = f.Command(EconomyAction.OpenBank, 1); f.Do(open);
            var deposit = f.Command(EconomyAction.Deposit, 1, 100); f.Do(deposit);
            var repeat = f.Do(deposit); Check(repeat.Duplicate && repeat.CashDelta == 0 && f.Cash == 9900);
            f.Do(EconomyAction.ReissueBankCard, 1); f.Do(EconomyAction.ReissueBankCard, 1);
            Check(f.State.RecentOperations.Count == 2);
            var before = Encode(f.State); var old = EconomyService.Apply(f.State, open, f.Rules);
            Check(!old.Success && old.CashDelta == 0 && before == Encode(f.State));
        });
        Test("Credit limit is not cash and borrowing creates matching debt", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 2, 500); Check(f.Cash == 10000);
            f.Do(EconomyAction.BorrowCredit, 2, 200); Check(f.Cash == 10200 && f.State.Credit.Principal == 200);
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.BorrowCredit, 2, 400), f.Rules).Success);
            Check(Encode(f.State) == before); f.Close(2); Check(f.State.Credit.AccruedInterest == 0);
            f.Close(3); Check(f.State.Credit.AccruedInterest == 2);
            f.Do(EconomyAction.RepayCredit, 4, 202); Check(f.State.Credit.Principal == 0 && f.State.Credit.AccruedInterest == 0 && f.Cash == 9998);
        });
        Test("Credit cards reference one debt and same-day reopening invalidates old identity", () => {
            var f = new Fixture(); var account = f.Do(EconomyAction.OpenCredit, 2, 500).EntityId;
            Check(account.Length > 0 && f.State.Credit.CardSerial == 1);
            f.Do(EconomyAction.BorrowCredit, 2, 200);
            var reissue = f.Command(EconomyAction.ReissueCreditCard, 2);
            var result = f.Do(reissue);
            Check(result.EntityId == account && result.CashDelta == 0 && f.Cash == 10200 && f.State.Credit.Principal == 200 && f.State.Credit.CardSerial == 2);
            Check(f.Do(reissue).Duplicate && f.State.Credit.CardSerial == 2);
            f.Do(EconomyAction.RepayCredit, 2, 200); f.Do(EconomyAction.CloseCredit, 2);
            Check(f.State.Credit.AccountId == "" && f.State.Credit.CardSerial == 0);
            var reopened = f.Do(EconomyAction.OpenCredit, 2, 500).EntityId;
            Check(reopened != account && f.State.Credit.CardSerial == 1);
        });
        Test("Legacy credit agreement gains card identity only on explicit reissue without altering debt", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 4, 500); f.Do(EconomyAction.BorrowCredit, 4, 200);
            var oldJson = Encode(f.State).Replace("\"AccountId\":\"" + f.State.Credit.AccountId + "\",", "").Replace("\"CardSerial\":1,", "");
            f.State = JsonSerializer.Deserialize<EconomyState>(oldJson, Json)!;
            EconomyService.Validate(f.State, f.Rules);
            Check(f.State.Credit.AccountId == "" && f.State.Credit.CardSerial == 0);
            f.Close(4); f.Close(5); Check(f.State.Credit.AccruedInterest == 2 && f.State.Credit.AccountId == "");
            var cash = f.Cash; var nextId = f.State.NextEntitySequence;
            var result = f.Do(EconomyAction.ReissueCreditCard, 6);
            Check(result.CashDelta == 0 && f.Cash == cash && f.State.Credit.Principal == 200 && f.State.Credit.AccruedInterest == 2);
            Check(f.State.Credit.AccountId == "credit-" + nextId && f.State.Credit.CardSerial == 1 && f.State.Credit.FirstInterestDay == 5);
        });
        Test("Invalid credit card identity and serial overflow cannot partially change state", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 1, 500);
            f.State.Credit.CardSerial = int.MaxValue; var before = Encode(f.State);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.ReissueCreditCard, 1), f.Rules).Success && Encode(f.State) == before);
            f.State.Credit.AccountId = ""; before = Encode(f.State);
            Check(!EconomyService.SettleDay(f.State, 2, f.Rules).Success && Encode(f.State) == before);
        });
        Test("Loan terms snapshot rate and repayment pays interest before principal", () => {
            var f = new Fixture(); var request = f.Command(EconomyAction.TakeLoan, 5, 1000); request.TermDays = 2;
            var id = f.Do(request).EntityId; f.Rules.LoanInterestBps = 500;
            f.Close(5); Check(f.State.Loans[0].AccruedInterest == 0);
            f.Close(6); Check(f.State.Loans[0].AccruedInterest == 10);
            Check(EconomyService.GetLoanPayoff(f.State, id, 7, f.Rules) == 1010);
            var repay = f.Command(EconomyAction.RepayLoan, 7, 10); repay.ContractId = id; f.Do(repay);
            Check(f.State.Loans[0].Principal == 1000 && f.State.Loans[0].AccruedInterest == 0);
            repay = f.Command(EconomyAction.RepayLoan, 7, 1000); repay.ContractId = id; f.Do(repay);
            Check(f.State.Loans[0].Status == "Paid");
        });
        Test("Stocks and crypto require opt-in; trades conserve units and use deterministic quotes", () => {
            var f = new Fixture(); var buy = f.Command(EconomyAction.BuySecurity, 1); buy.Symbol = "ORBIT"; buy.Quantity = 2;
            Check(!EconomyService.Apply(f.State, buy, f.Rules).Success);
            f.Do(EconomyAction.OpenSecurities, 1);
            buy = f.Command(EconomyAction.BuySecurity, 1); buy.Symbol = "ORBIT"; buy.Quantity = 2; f.Do(buy);
            Check(f.Cash == 9800 && f.State.Positions.Single().Quantity == 2);
            var sell = f.Command(EconomyAction.SellSecurity, 1); sell.Symbol = "ORBIT"; sell.Quantity = 1; f.Do(sell);
            Check(f.Cash == 9897 && f.State.Positions.Single().Quantity == 1 && f.State.Positions.Single().TotalCost == 100);
            buy = f.Command(EconomyAction.BuySecurity, 1); buy.Symbol = "NEBULA"; buy.Quantity = 2; f.Do(buy);
            Check(f.State.Positions.Count == 2 && f.Cash == 9837);
            var before = Encode(f.State); var q = Encode(EconomyService.GetMarketSnapshot(f.State, 7, f.Rules));
            Check(q == Encode(EconomyService.GetMarketSnapshot(f.State.DeepClone(), 7, f.Rules)) && before == Encode(f.State));
            sell = f.Command(EconomyAction.SellSecurity, 1); sell.Symbol = "ORBIT"; sell.Quantity = 2;
            Check(!EconomyService.Apply(f.State, sell, f.Rules).Success);
        });
        Test("Stale displayed market rejects both buys and sells without cash position or settlement changes", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenSecurities, 1);
            var market = EconomyService.GetMarketSnapshot(f.State, 1, f.Rules);
            var buy = f.Command(EconomyAction.BuySecurity, 1); buy.Symbol = "ORBIT"; buy.Quantity = 2; buy.ExpectedMarketVersion = market.Version;
            f.Do(buy);
            var before = Encode(f.State); var cash = f.Cash;
            foreach (var action in new[] { EconomyAction.BuySecurity, EconomyAction.SellSecurity }) {
                var stale = f.Command(action, 2); stale.Symbol = "ORBIT"; stale.Quantity = 1; stale.ExpectedMarketVersion = market.Version;
                var result = EconomyService.Apply(f.State, stale, f.Rules);
                Check(!result.Success && result.CashDelta == 0 && result.AssetTransitions.Count == 0 && Encode(f.State) == before && f.Cash == cash);
            }
            var freshMarket = EconomyService.GetMarketSnapshot(f.State, 2, f.Rules);
            var fresh = f.Command(EconomyAction.SellSecurity, 2); fresh.Symbol = "ORBIT"; fresh.Quantity = 1; fresh.ExpectedMarketVersion = freshMarket.Version;
            var accepted = f.Do(fresh);
            Check(accepted.CashDelta == freshMarket.Securities.Single(x => x.Symbol == "ORBIT").SellPrice && f.State.Positions.Single().Quantity == 1);
        });
        Test("Same-day market revision also invalidates a displayed trade quote", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenSecurities, 1); f.Do(EconomyAction.OptIntoMarketEvents, 1);
            var displayed = EconomyService.GetMarketSnapshot(f.State, 1, f.Rules);
            var report = f.Command(EconomyAction.ActivateMarketEvent, 1); report.EventKind = "jade_discovery"; f.Do(report);
            var before = Encode(f.State); var order = f.Command(EconomyAction.BuySecurity, 1);
            order.Symbol = "JADE"; order.Quantity = 1; order.ExpectedMarketVersion = displayed.Version;
            var result = EconomyService.Apply(f.State, order, f.Rules);
            Check(!result.Success && result.CashDelta == 0 && Encode(f.State) == before);
        });
        Test("Pawn custody rejects double pledges and returns the same borrower asset on due day", () => {
            var f = new Fixture(); f.Do(EconomyAction.AcquirePawnLicense, 1);
            var create = f.Command(EconomyAction.CreatePawn, 1, 600); create.AssetId = "ring-7"; create.CollateralValue = 1000; create.TermDays = 2; create.PlayerIsLender = false; create.LoanToValueBps = 6000;
            var result = f.Do(create); var id = result.EntityId;
            Check(result.CashDelta == 600 && result.AssetTransitions.Single().Kind == "EscrowFromPlayer");
            var duplicate = f.Command(EconomyAction.CreatePawn, 1, 100); duplicate.AssetId = "ring-7"; duplicate.CollateralValue = 1000;
            Check(!EconomyService.Apply(f.State, duplicate, f.Rules).Success);
            Check(EconomyService.GetPawnRedemptionQuote(f.State, id, 3, f.Rules) == 606);
            var redeem = f.Command(EconomyAction.RedeemPawn, 3); redeem.ContractId = id; var returned = f.Do(redeem);
            Check(returned.CashDelta == -606 && returned.AssetTransitions.Single().AssetId == "ring-7" && returned.AssetTransitions.Single().Kind == "ReturnToPlayer");
            Check(f.State.PawnContracts.Single().Status == "Redeemed" && !EconomyService.IsAssetInEscrow(f.State, "ring-7"));
        });
        Test("Pawn lender pays once, renewal retains identity, and customer redemption pays once", () => {
            var f = new Fixture(); f.Do(EconomyAction.AcquirePawnLicense, 1);
            var create = f.Command(EconomyAction.CreatePawn, 1, 500); create.AssetId = "customer-ring"; create.CollateralValue = 1000; create.TermDays = 2;
            var id = f.Do(create).EntityId; Check(f.Cash == 9500);
            var renew = f.Command(EconomyAction.RenewPawn, 3); renew.ContractId = id; renew.TermDays = 3;
            Check(f.Do(renew).CashDelta == 10 && f.State.PawnContracts.Single().DueDay == 6);
            Check(f.State.PawnContracts.Single().Principal == 500 && f.State.PawnContracts.Single().Id == id);
            var secondRenew = f.Command(EconomyAction.RenewPawn, 3); secondRenew.ContractId = id;
            var beforeRepeat = Encode(f.State);
            Check(!EconomyService.Apply(f.State, secondRenew, f.Rules).Success && Encode(f.State) == beforeRepeat);
            var redeem = f.Command(EconomyAction.RedeemPawn, 3); redeem.ContractId = id;
            var returned = f.Do(redeem); Check(returned.CashDelta == 500 && returned.AssetTransitions.Single().Kind == "ReturnToCustomer");
            Check(f.Do(redeem).Duplicate && f.Cash == 10010);
        });
        Test("Due-day closing forfeits once and blocks later redemption", () => {
            var f = new Fixture(); f.Do(EconomyAction.AcquirePawnLicense, 1);
            var create = f.Command(EconomyAction.CreatePawn, 1, 500); create.AssetId = "escrow-a"; create.CollateralValue = 1000; create.TermDays = 2;
            var id = f.Do(create).EntityId; var settled = f.Close(3);
            Check(settled.AssetTransitions.Single().Kind == "ForfeitToPlayer" && settled.CashDelta == 0);
            Check(f.Close(3).AssetTransitions.Count == 0);
            var redeem = f.Command(EconomyAction.RedeemPawn, 4); redeem.ContractId = id;
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, redeem, f.Rules).Success && before == Encode(f.State));
        });
        Test("Multi-entity daily failure cannot partially credit earlier accounts", () => {
            var f = new Fixture(); f.Rules.MaximumLedgerBalance = 1000000;
            f.Do(EconomyAction.OpenBank, 1); f.Do(EconomyAction.Deposit, 1, 1000); f.Do(EconomyAction.TakeLoan, 1, 1000);
            f.State.Loans[0].AccruedInterest = 1000000; var before = Encode(f.State);
            var failure = EconomyService.SettleDay(f.State, 2, f.Rules);
            Check(!failure.Success && failure.AssetTransitions.Count == 0 && Encode(f.State) == before);
        });
        Test("Local events require separate consent, start tomorrow, expire, and cannot be retriggered", () => {
            var f = new Fixture(); var activate = f.Command(EconomyAction.ActivateMarketEvent, 1); activate.EventKind = "gold_supply";
            Check(!EconomyService.Apply(f.State, activate, f.Rules).Success);
            f.Do(EconomyAction.OpenBank, 1); Check(!f.State.GoldAgreementActive && !f.State.MarketEventsOptedIn && !f.State.SecuritiesOpened);
            f.Do(EconomyAction.OptIntoMarketEvents, 1); f.Do(EconomyAction.ActivateGoldAgreement, 1);
            var noEvent = f.State.DeepClone(); activate = f.Command(EconomyAction.ActivateMarketEvent, 1); activate.EventKind = "gold_supply"; f.Do(activate);
            Check(EconomyService.GetMarketSnapshot(f.State, 1, f.Rules).GoldSellPerGram == 80);
            Check(EconomyService.GetMarketSnapshot(f.State, 2, f.Rules).GoldSellPerGram < EconomyService.GetMarketSnapshot(noEvent, 2, f.Rules).GoldSellPerGram);
            Check(EconomyService.GetMarketSnapshot(f.State, 5, f.Rules).GoldSellPerGram == EconomyService.GetMarketSnapshot(noEvent, 5, f.Rules).GoldSellPerGram);
            Check(f.State.Events.Single().Scope == "starpawn");
            activate = f.Command(EconomyAction.ActivateMarketEvent, 5); activate.EventKind = "gold_supply";
            Check(!EconomyService.Apply(f.State, activate, f.Rules).Success);
        });
        Test("Bank closure cannot erase loans and normal products do not enable macro story", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1); f.Do(EconomyAction.TakeLoan, 1, 100);
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.CloseBank, 1), f.Rules).Success);
            Check(Encode(f.State) == before && !f.State.GoldAgreementActive && !f.State.MarketEventsOptedIn);
        });
        Test("Serialized state preserves idempotency and future schemas are rejected intact", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1); var deposit = f.Command(EconomyAction.Deposit, 1, 100); f.Do(deposit);
            var saved = JsonSerializer.Deserialize<EconomyState>(Encode(f.State), Json)!;
            var repeat = EconomyService.Apply(saved, deposit, f.Rules); Check(repeat.Success && repeat.Duplicate && repeat.CashDelta == 0 && saved.Bank.Balance == 100);
            saved.SchemaVersion = 99; var before = Encode(saved); var rejected = EconomyService.SettleDay(saved, 2, f.Rules);
            Check(!rejected.Success && Encode(saved) == before);
        });
        Test("Product closure cannot erase debt or positions; settled products can exit", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 1, 500); f.Do(EconomyAction.BorrowCredit, 1, 100);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.CloseCredit, 1), f.Rules).Success);
            f.Do(EconomyAction.RepayCredit, 1, 100); f.Do(EconomyAction.CloseCredit, 1); Check(!f.State.Credit.Signed);
            f.Do(EconomyAction.OpenSecurities, 1);
            var buy = f.Command(EconomyAction.BuySecurity, 1); buy.Symbol = "ORBIT"; buy.Quantity = 1; f.Do(buy);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.CloseSecurities, 1), f.Rules).Success);
            var sell = f.Command(EconomyAction.SellSecurity, 1); sell.Symbol = "ORBIT"; sell.Quantity = 1; f.Do(sell);
            f.Do(EconomyAction.CloseSecurities, 1); Check(!f.State.SecuritiesOpened);
            var before = Encode(f.State); f.Close(50); Check(Encode(f.State) == before);
        });
        Test("Terminal contract histories are bounded while active obligations survive", () => {
            var f = new Fixture(); f.Rules.ClosedContractLimit = 1;
            for (var i = 0; i < 3; i++) {
                var id = f.Do(EconomyAction.TakeLoan, 1, 100).EntityId;
                var repay = f.Command(EconomyAction.RepayLoan, 1, 100); repay.ContractId = id; f.Do(repay);
            }
            f.Do(EconomyAction.TakeLoan, 1, 200);
            Check(f.State.Loans.Count == 2 && f.State.Loans.Count(x => x.Status == "Active") == 1 && f.State.ArchivedLoanCount == 2);
        });
        Test("Archived pawn effects retain immutable asset identity while previous state resolves contracts", () => {
            var f = new Fixture(); f.Rules.ClosedContractLimit = 0; f.Do(EconomyAction.AcquirePawnLicense, 1);
            foreach (var lender in new[] { true, false }) {
                var create = f.Command(EconomyAction.CreatePawn, 1, 100); create.AssetId = lender ? "customer-archive" : "player-archive";
                create.CollateralValue = 1000; create.TermDays = 1; create.PlayerIsLender = lender; f.Do(create);
            }
            var previous = f.State.DeepClone(); var result = f.Close(2);
            Check(f.State.PawnContracts.Count == 0 && f.State.ArchivedPawnCount == 2 && result.AssetTransitions.Count == 2);
            foreach (var effect in result.AssetTransitions) {
                var contract = previous.PawnContracts.Single(x => x.Id == effect.ContractId && x.AssetId == effect.AssetId);
                Check(effect.Kind == (contract.PlayerIsLender ? "ForfeitToPlayer" : "ForfeitToCustomer"));
            }
            Check(f.Close(2).AssetTransitions.Count == 0);
        });
        Test("Negative amounts wallet overflow and closed-day actions fail without mutation", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1);
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Deposit, 1, -1), f.Rules).Success);
            Check(before == Encode(f.State)); f.Cash = int.MaxValue;
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.TakeLoan, 1, 1), f.Rules).Success);
            Check(before == Encode(f.State)); f.Close(1);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Deposit, 1, 1), f.Rules).Success);
        });
        Test("Daily limits aggregate successful amounts and failures do not consume them", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenBank, 1);
            var deposit = f.Command(EconomyAction.Deposit, 1, 3000); f.Do(deposit); f.Do(deposit);
            f.Do(EconomyAction.Deposit, 1, 2000);
            Check(f.State.DepositedToday == 5000);
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Deposit, 1, 1), f.Rules).Success);
            Check(Encode(f.State) == before);
            f.Do(EconomyAction.Withdraw, 1, 1500); f.Do(EconomyAction.Withdraw, 1, 500);
            Check(EconomyService.GetDailyRemaining(f.State, EconomyAction.Withdraw, 1, f.Rules) == 0);
            before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Withdraw, 1, 1), f.Rules).Success);
            Check(before == Encode(f.State));
        });
        Test("Reissue reopening and serialized reload cannot refresh daily usage", () => {
            var f = new Fixture(); f.Rules.DailyDepositLimit = 500; f.Do(EconomyAction.OpenBank, 1);
            f.Do(EconomyAction.Deposit, 1, 500); f.Do(EconomyAction.Withdraw, 1, 500);
            f.Do(EconomyAction.ReissueBankCard, 1); f.Do(EconomyAction.CloseBank, 1); f.Do(EconomyAction.OpenBank, 1);
            f.State = JsonSerializer.Deserialize<EconomyState>(Encode(f.State), Json)!;
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Deposit, 1, 1), f.Rules).Success);
            var before = Encode(f.State); Check(EconomyService.GetDailyRemaining(f.State, EconomyAction.Deposit, 2, f.Rules) == 500 && Encode(f.State) == before);
            f.Do(EconomyAction.Deposit, 2, 500); Check(f.State.DailyUsageDay == 2 && f.State.DepositedToday == 500);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.Withdraw, 1, 1), f.Rules).Success);
        });
        Test("Credit repayments remain available without replenishing daily borrowing", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 1, 5000); f.Do(EconomyAction.BorrowCredit, 1, 1000);
            f.Do(EconomyAction.RepayCredit, 1, 1000); f.Do(EconomyAction.ReissueCreditCard, 1);
            f.Do(EconomyAction.CloseCredit, 1); f.Do(EconomyAction.OpenCredit, 1, 5000);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.BorrowCredit, 1, 1), f.Rules).Success);
            f.Do(EconomyAction.BorrowCredit, 2, 1000); Check(f.State.CreditBorrowedToday == 1000);
        });
        Test("Loan contracts share daily and total outstanding principal caps", () => {
            var f = new Fixture(); f.Rules.MaximumTotalLoanPrincipal = 4000;
            f.Do(EconomyAction.TakeLoan, 1, 2000); f.Do(EconomyAction.TakeLoan, 1, 1000);
            Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.TakeLoan, 1, 1), f.Rules).Success);
            f.Do(EconomyAction.TakeLoan, 2, 1000);
            var before = Encode(f.State); Check(!EconomyService.Apply(f.State, f.Command(EconomyAction.TakeLoan, 2, 1), f.Rules).Success && before == Encode(f.State));
        });
        Test("Legacy states start without quota debt and signed rates stay immutable", () => {
            var f = new Fixture(); f.Do(EconomyAction.OpenCredit, 1, 5000); var original = f.State.Credit.DailyInterestBps;
            f.Rules.CreditInterestBps = 600; f.Do(EconomyAction.BorrowCredit, 2, 100); Check(f.State.Credit.DailyInterestBps == original);
            var legacy = JsonSerializer.Deserialize<EconomyState>("{}", Json)!;
            EconomyService.Validate(legacy, new EconomyRules()); Check(legacy.DailyUsageDay == -1 && legacy.Bank.NewDepositDay == -1);
        });
        Test("Depositing borrowed cash cannot earn during the loan interest grace night", () => {
            var f = new Fixture(); f.Rules = new EconomyRules(); f.Do(EconomyAction.OpenBank, 1); f.Close(1);
            f.Cash = 0; var loan = f.Do(EconomyAction.TakeLoan, 2, 1000).EntityId; f.Do(EconomyAction.Deposit, 2, 1000);
            f.Close(2); Check(f.State.Bank.Balance == 1000);
            f.Do(EconomyAction.Withdraw, 3, 1000); var repay = f.Command(EconomyAction.RepayLoan, 3, 1000); repay.ContractId = loan; f.Do(repay);
            Check(f.Cash == 0 && f.State.Bank.Balance == 0 && f.State.Loans.Single().Status == "Paid");
        });
        Test("Default 7 14 and 30 day rates preserve savings below borrowing cost", () => {
            foreach (var days in new[] { 7, 14, 30 }) {
                var f = new Fixture(); f.Rules = new EconomyRules(); f.Do(EconomyAction.OpenBank, 1); f.Do(EconomyAction.Deposit, 1, 1000);
                f.Do(EconomyAction.OpenCredit, 1, 5000); f.Do(EconomyAction.BorrowCredit, 1, 1000); f.Do(EconomyAction.TakeLoan, 1, 1000);
                f.Close(days + 1);
                var savings = f.State.Bank.Balance - 1000;
                Check(savings > 0 && savings < f.State.Credit.AccruedInterest && savings < f.State.Loans.Single().AccruedInterest);
                Console.WriteLine("BALANCE days=" + days + " savings=" + savings + " credit=" + f.State.Credit.AccruedInterest + " loan=" + f.State.Loans.Single().AccruedInterest);
            }
        });
        Console.WriteLine("PASS: " + passed + " economy invariant tests. Native cash/assets/save integration is not covered here.");
        return 0;
    }
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
}
