using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace StarPawn.Core
{
    public enum EconomyAction
    {
        OpenBank, CloseBank, Deposit, Withdraw, ReissueBankCard,
        OpenCredit, BorrowCredit, RepayCredit, TakeLoan, RepayLoan,
        OpenSecurities, BuySecurity, SellSecurity, AcquirePawnLicense,
        CreatePawn, RedeemPawn, RenewPawn, OptIntoMarketEvents,
        ActivateMarketEvent, ActivateGoldAgreement, CloseCredit, CloseSecurities,
        ReissueCreditCard
    }

    public sealed class EconomyCommand
    {
        public EconomyAction Kind;
        public string TransactionId = "";
        public long Sequence;
        public int GameDay;
        public long AvailableCash;
        public long Amount;
        public string Symbol = "";
        public long Quantity;
        // Optional for non-UI integrations; game trade buttons must carry the
        // snapshot version shown to the player so an old page cannot reprice.
        public long? ExpectedMarketVersion;
        public string ContractId = "";
        public string AssetId = "";
        public long CollateralValue;
        public int TermDays;
        public bool PlayerIsLender = true;
        public string EventKind = "";
    }

    public sealed class EconomyResult
    {
        public bool Success;
        public bool Duplicate;
        public string Error = "";
        public long CashDelta;
        public string EntityId = "";
        public List<AssetTransition> AssetTransitions = new List<AssetTransition>();
    }

    public sealed class AssetTransition
    {
        public string AssetId = "";
        // EscrowFromPlayer, EscrowFromCustomer, ReturnToPlayer, ReturnToCustomer,
        // ForfeitToPlayer, ForfeitToCustomer. The game validates real ownership.
        public string Kind = "";
        public string ContractId = "";
    }

    public sealed class EconomyRules
    {
        public int RulesVersion = 1;
        public int BankInterestBps = 10;
        public int CreditInterestBps = 35;
        public int LoanInterestBps = 25;
        public int PawnInterestBps = 40;
        public int PawnRenewalFeeBps = 100;
        public int PawnLoanToValueBps = 6000;
        public long MaximumCreditLimit = 5000;
        public long MaximumLoanPrincipal = 10000;
        public long MaximumPawnPrincipal = 100000;
        public long PawnLicenseCost = 250;
        public int DefaultLoanDays = 14;
        public int DefaultPawnDays = 7;
        public int MaximumTermDays = 365;
        public int MaximumCatchupDays = 3660;
        public int MaximumActiveContracts = 128;
        public int MaximumPositions = 32;
        public int RecentOperationLimit = 256;
        public int ClosedContractLimit = 128;
        public long MaximumLedgerBalance = 1000000000000L;
        public long MaximumCashBalance = int.MaxValue;
        public long MaximumSecurityQuantity = 1000000;
        public long GoldSellPerGram = 80;
        public long GoldBuyPerGram = 65;
        public long SilverSellPerGram = 8;
        public long SilverBuyPerGram = 6;
        public int GoldDailyVariationBps = 400;
        public int SecuritySpreadBps = 300;
        public int MarketEventDays = 3;
        public int GoldSupplyEventBps = 8500;
        public int JadeDiscoveryEventBps = 8000;
        public int ElectronicsShortageEventBps = 12000;
        public List<SecurityDefinition> Securities = new List<SecurityDefinition>
        {
            new SecurityDefinition { Symbol = "ORBIT", Kind = "Stock", BasePrice = 100, DailyVariationBps = 700 },
            new SecurityDefinition { Symbol = "JADE", Kind = "Stock", BasePrice = 80, DailyVariationBps = 900 },
            new SecurityDefinition { Symbol = "BYTE", Kind = "Stock", BasePrice = 120, DailyVariationBps = 800 },
            new SecurityDefinition { Symbol = "NEBULA", Kind = "Crypto", BasePrice = 30, DailyVariationBps = 1800 }
        };
    }

    public sealed class SecurityDefinition
    {
        public string Symbol = "";
        public string Kind = "Stock";
        public long BasePrice;
        public int DailyVariationBps;
    }

    public sealed class MarketSnapshot
    {
        public int GameDay;
        public long Version;
        public long GoldSellPerGram;
        public long GoldBuyPerGram;
        public long SilverSellPerGram;
        public long SilverBuyPerGram;
        public int JadeValueBps = 10000;
        public int EntertainmentValueBps = 10000;
        public string Scope = "starpawn";
        public List<SecurityQuote> Securities = new List<SecurityQuote>();
    }

    public sealed class SecurityQuote
    {
        public string Symbol = "";
        public string Kind = "";
        // BuyPrice is the player's purchase price; SellPrice is proceeds per unit.
        public long BuyPrice;
        public long SellPrice;
    }

    public sealed class BankAccountState
    {
        public string Status = "None";
        public string AccountId = "";
        public long Balance;
        public int OpenedDay;
        public int FirstInterestDay;
        public int LastSettledDay = -1;
        public int DailyInterestBps;
        public long InterestRemainder;
        public int CardSerial;
        public BankAccountState Clone() => (BankAccountState)MemberwiseClone();
    }

    public sealed class CreditAccountState
    {
        public bool Signed;
        // Additive schema-1 identity: legacy agreements may have neither field.
        // The first explicit card reissue upgrades identity without changing debt.
        public string AccountId = "";
        public int CardSerial;
        public long Limit;
        public long Principal;
        public long AccruedInterest;
        public long InterestRemainder;
        public int DailyInterestBps;
        public int SignedDay;
        public int FirstInterestDay;
        public int LastSettledDay = -1;
        public CreditAccountState Clone() => (CreditAccountState)MemberwiseClone();
    }

    public sealed class LoanContract
    {
        public string Id = "";
        public string Status = "Active";
        public long OriginalPrincipal;
        public long Principal;
        public long AccruedInterest;
        public long InterestRemainder;
        public int DailyInterestBps;
        public int StartDay;
        public int FirstInterestDay;
        public int DueDay;
        public int LastSettledDay = -1;
        public bool Overdue;
        public int RulesVersion;
        public LoanContract Clone() => (LoanContract)MemberwiseClone();
    }

    public sealed class SecurityPosition
    {
        public string Symbol = "";
        public long Quantity;
        public long TotalCost;
        public SecurityPosition Clone() => (SecurityPosition)MemberwiseClone();
    }

    public sealed class PawnContract
    {
        public string Id = "";
        public string AssetId = "";
        public string Status = "Active";
        public bool PlayerIsLender = true;
        public long Principal;
        public long CollateralValue;
        public long AccruedFees;
        public long InterestRemainder;
        public int DailyInterestBps;
        public int RenewalFeeBps;
        public int StartDay;
        public int FirstInterestDay;
        public int DueDay;
        public int LastSettledDay = -1;
        public int RenewalCount;
        public int LastRenewalDay = -1;
        public int RulesVersion;
        public PawnContract Clone() => (PawnContract)MemberwiseClone();
    }

    public sealed class LocalMarketEvent
    {
        public string Id = "";
        public string Kind = "";
        public string Scope = "starpawn";
        public int StartDay;
        public int EndDay;
        public int MultiplierBps;
        public bool Expired;
        public LocalMarketEvent Clone() => (LocalMarketEvent)MemberwiseClone();
    }

    public sealed class EconomyReceipt
    {
        public string TransactionId = "";
        public long Sequence;
        public string Action = "";
        public string EntityId = "";
        public int GameDay;
        public long CashDelta;
        public EconomyReceipt Clone() => (EconomyReceipt)MemberwiseClone();
    }

    public sealed class EconomyState
    {
        public int SchemaVersion = 1;
        public long LastCommandSequence;
        public long NextEntitySequence = 1;
        public int LastClosedDay = -1;
        public int MarketSeed = 1771;
        public long MarketRevision;
        public BankAccountState Bank = new BankAccountState();
        public CreditAccountState Credit = new CreditAccountState();
        public List<LoanContract> Loans = new List<LoanContract>();
        public bool SecuritiesOpened;
        public int SecuritiesOpenedDay;
        public List<SecurityPosition> Positions = new List<SecurityPosition>();
        public bool PawnLicensed;
        public List<PawnContract> PawnContracts = new List<PawnContract>();
        public bool MarketEventsOptedIn;
        public bool GoldAgreementActive;
        public int GoldAgreementStartDay;
        public List<LocalMarketEvent> Events = new List<LocalMarketEvent>();
        public List<string> TriggeredEventKinds = new List<string>();
        public List<EconomyReceipt> RecentOperations = new List<EconomyReceipt>();
        public int ArchivedLoanCount;
        public int ArchivedPawnCount;

        public EconomyState DeepClone()
        {
            var copy = (EconomyState)MemberwiseClone();
            copy.Bank = Bank.Clone(); copy.Credit = Credit.Clone();
            copy.Loans = Loans.Select(x => x.Clone()).ToList();
            copy.Positions = Positions.Select(x => x.Clone()).ToList();
            copy.PawnContracts = PawnContracts.Select(x => x.Clone()).ToList();
            copy.Events = Events.Select(x => x.Clone()).ToList();
            copy.TriggeredEventKinds = new List<string>(TriggeredEventKinds);
            copy.RecentOperations = RecentOperations.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    public static class EconomyService
    {
        public const int SchemaVersion = 1;
        public static EconomyState CreateNewState(int marketSeed = 1771) => new EconomyState { MarketSeed = marketSeed };

        // Mutations are staged on a deep copy. The caller must additionally stage its
        // whole run state before committing the returned cash/asset effects to the game.
        public static EconomyResult Apply(EconomyState state, EconomyCommand command, EconomyRules rules)
        {
            try
            {
                Validate(state, rules);
                Require(command != null, "Missing command.");
                Require(!string.IsNullOrWhiteSpace(command!.TransactionId) && command.TransactionId.Length <= 128, "Invalid transaction ID.");
                var old = state.RecentOperations.FirstOrDefault(x => x.TransactionId == command.TransactionId);
                if (old != null)
                {
                    Require(old.Sequence == command.Sequence, "Transaction ID reused with a different sequence.");
                    return new EconomyResult { Success = true, Duplicate = true, EntityId = old.EntityId };
                }
                Require(command.Sequence == checked(state.LastCommandSequence + 1), "Stale or skipped operation sequence; no action was replayed.");
                Require(command.GameDay >= 0 && command.GameDay < int.MaxValue - 3661, "Invalid game day.");
                Require(command.AvailableCash >= 0 && command.AvailableCash <= rules.MaximumCashBalance, "Invalid wallet snapshot.");
                Require(command.GameDay > state.LastClosedDay, "This business day has already closed.");
                var next = state.DeepClone();
                var result = new EconomyResult();
                SettleInternal(next, command.GameDay - 1, rules, result);
                Trim(next, rules);
                if (!HasParticipation(next)) next.LastClosedDay = command.GameDay - 1;
                ApplyInternal(next, command, rules, result);
                var walletAfter = checked(command.AvailableCash + result.CashDelta);
                Require(walletAfter >= 0 && walletAfter <= rules.MaximumCashBalance, "Insufficient cash or wallet overflow.");
                next.LastCommandSequence = command.Sequence;
                next.RecentOperations.Add(new EconomyReceipt {
                    TransactionId = command.TransactionId, Sequence = command.Sequence, Action = command.Kind.ToString(),
                    EntityId = result.EntityId, GameDay = command.GameDay, CashDelta = result.CashDelta
                });
                Trim(next, rules);
                Validate(next, rules);
                Commit(state, next);
                result.Success = true;
                return result;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is OverflowException || ex is ArgumentException)
            { return new EconomyResult { Error = ex.Message }; }
        }

        public static EconomyResult SettleDay(EconomyState state, int gameDay, EconomyRules rules)
        {
            try
            {
                Validate(state, rules);
                Require(gameDay >= 0 && gameDay < int.MaxValue - 3661, "Invalid settlement day.");
                if (!HasParticipation(state)) return new EconomyResult { Success = true };
                if (gameDay <= state.LastClosedDay) return new EconomyResult { Success = true, Duplicate = true };
                var next = state.DeepClone();
                var result = new EconomyResult();
                SettleInternal(next, gameDay, rules, result);
                Trim(next, rules);
                Validate(next, rules);
                Commit(state, next);
                result.Success = true;
                return result;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is OverflowException || ex is ArgumentException)
            { return new EconomyResult { Error = ex.Message }; }
        }

        // Pure queries never advance RNG, create participation, or modify the state.
        public static MarketSnapshot GetMarketSnapshot(EconomyState state, int gameDay, EconomyRules rules)
        {
            Validate(state, rules);
            Require(gameDay >= 0, "Invalid quote day.");
            var snapshot = new MarketSnapshot { GameDay = gameDay, Version = checked((long)gameDay * 100000 + state.MarketRevision) };
            var goldBps = 10000;
            if (state.GoldAgreementActive && gameDay > state.GoldAgreementStartDay)
                goldBps += Variation(state.MarketSeed, gameDay, "GOLD", rules.GoldDailyVariationBps);
            foreach (var marketEvent in state.Events.Where(x => x.StartDay <= gameDay && x.EndDay >= gameDay))
            {
                if (marketEvent.Kind == "gold_supply") goldBps = (int)MulDiv(goldBps, marketEvent.MultiplierBps, 10000);
                if (marketEvent.Kind == "jade_discovery") snapshot.JadeValueBps = marketEvent.MultiplierBps;
                if (marketEvent.Kind == "electronics_shortage") snapshot.EntertainmentValueBps = marketEvent.MultiplierBps;
            }
            snapshot.GoldSellPerGram = Math.Max(1, MulDiv(rules.GoldSellPerGram, goldBps, 10000));
            snapshot.GoldBuyPerGram = Math.Max(1, MulDiv(rules.GoldBuyPerGram, goldBps, 10000));
            snapshot.SilverSellPerGram = rules.SilverSellPerGram;
            snapshot.SilverBuyPerGram = rules.SilverBuyPerGram;
            foreach (var definition in rules.Securities)
            {
                var bps = 10000;
                if (state.SecuritiesOpened && gameDay > state.SecuritiesOpenedDay)
                    bps += Variation(state.MarketSeed, gameDay, definition.Symbol, definition.DailyVariationBps);
                foreach (var marketEvent in state.Events.Where(x => x.StartDay <= gameDay && x.EndDay >= gameDay))
                {
                    if (definition.Symbol == "JADE" && marketEvent.Kind == "jade_discovery") bps = (int)MulDiv(bps, 11500, 10000);
                    if (definition.Symbol == "BYTE" && marketEvent.Kind == "electronics_shortage") bps = (int)MulDiv(bps, 11000, 10000);
                    if (definition.Symbol == "ORBIT" && marketEvent.Kind == "gold_supply") bps = (int)MulDiv(bps, 10500, 10000);
                }
                var ask = Math.Max(1, MulDiv(definition.BasePrice, bps, 10000));
                var bid = Math.Max(1, MulDiv(ask, 10000 - rules.SecuritySpreadBps, 10000));
                snapshot.Securities.Add(new SecurityQuote { Symbol = definition.Symbol, Kind = definition.Kind, BuyPrice = ask, SellPrice = bid });
            }
            return snapshot;
        }

        public static long GetLoanPayoff(EconomyState state, string contractId, int gameDay, EconomyRules rules)
        {
            var projected = ProjectBeforeBusiness(state, gameDay, rules);
            var loan = FindLoan(projected, contractId);
            return checked(loan.Principal + loan.AccruedInterest);
        }

        public static long GetCreditPayoff(EconomyState state, int gameDay, EconomyRules rules)
        {
            var projected = ProjectBeforeBusiness(state, gameDay, rules);
            return checked(projected.Credit.Principal + projected.Credit.AccruedInterest);
        }

        public static long GetPawnRedemptionQuote(EconomyState state, string contractId, int gameDay, EconomyRules rules)
        {
            var projected = ProjectBeforeBusiness(state, gameDay, rules);
            var pawn = FindPawn(projected, contractId);
            Require(pawn.Status == "Active" && gameDay <= pawn.DueDay, "Collateral is not redeemable.");
            return checked(pawn.Principal + pawn.AccruedFees);
        }

        public static bool IsAssetInEscrow(EconomyState state, string assetId) =>
            state.PawnContracts.Any(x => x.AssetId == assetId && x.Status == "Active");

        public static string GetSavingsCardAccountId(EconomyState state)
        {
            Require(state.Bank.Status == "Open", "No open savings account.");
            return state.Bank.AccountId;
        }

        private static void ApplyInternal(EconomyState s, EconomyCommand c, EconomyRules r, EconomyResult result)
        {
            switch (c.Kind)
            {
                case EconomyAction.OpenBank:
                    Require(s.Bank.Status != "Open", "Bank account is already open.");
                    s.Bank = new BankAccountState { Status = "Open", AccountId = NewId(s, "bank"), OpenedDay = c.GameDay,
                        FirstInterestDay = c.GameDay + 1, LastSettledDay = c.GameDay, DailyInterestBps = r.BankInterestBps, CardSerial = 1 };
                    result.EntityId = s.Bank.AccountId; break;
                case EconomyAction.CloseBank:
                    BankOpen(s); Require(s.Bank.Balance == 0, "Withdraw the bank balance before closing.");
                    Require(s.Credit.Principal == 0 && s.Credit.AccruedInterest == 0 && !s.Loans.Any(x => x.Status == "Active"), "Settle outstanding credit and loans first.");
                    s.Bank.Status = "Closed"; result.EntityId = s.Bank.AccountId; break;
                case EconomyAction.Deposit:
                    BankOpen(s); Positive(c.Amount); s.Bank.Balance = checked(s.Bank.Balance + c.Amount); result.CashDelta = -c.Amount; break;
                case EconomyAction.Withdraw:
                    BankOpen(s); Positive(c.Amount); Require(c.Amount <= s.Bank.Balance, "Insufficient bank balance.");
                    s.Bank.Balance -= c.Amount; result.CashDelta = c.Amount; break;
                case EconomyAction.ReissueBankCard:
                    BankOpen(s); s.Bank.CardSerial = checked(s.Bank.CardSerial + 1); result.EntityId = s.Bank.AccountId; break;
                case EconomyAction.OpenCredit:
                    Require(!s.Credit.Signed, "Credit agreement already exists.");
                    Positive(c.Amount); Require(c.Amount <= r.MaximumCreditLimit, "Credit limit exceeds the product maximum.");
                    s.Credit = new CreditAccountState { Signed = true, AccountId = NewId(s, "credit"), CardSerial = 1,
                        Limit = c.Amount, SignedDay = c.GameDay, FirstInterestDay = c.GameDay + 1,
                        LastSettledDay = c.GameDay, DailyInterestBps = r.CreditInterestBps };
                    result.EntityId = s.Credit.AccountId; break;
                case EconomyAction.ReissueCreditCard:
                    Require(s.Credit.Signed, "No credit agreement.");
                    if (s.Credit.AccountId.Length == 0) { s.Credit.AccountId = NewId(s, "credit"); s.Credit.CardSerial = 1; }
                    else s.Credit.CardSerial = checked(s.Credit.CardSerial + 1);
                    result.EntityId = s.Credit.AccountId; break;
                case EconomyAction.CloseCredit:
                    Require(s.Credit.Signed, "No credit agreement.");
                    Require(s.Credit.Principal == 0 && s.Credit.AccruedInterest == 0, "Repay all credit obligations before ending the agreement.");
                    s.Credit = new CreditAccountState(); break;
                case EconomyAction.BorrowCredit:
                    Require(s.Credit.Signed, "Sign a credit agreement first."); Positive(c.Amount);
                    Require(checked(s.Credit.Principal + c.Amount) <= s.Credit.Limit, "Credit limit exceeded.");
                    if (s.Credit.Principal == 0 && s.Credit.AccruedInterest == 0) { s.Credit.FirstInterestDay = c.GameDay + 1; s.Credit.LastSettledDay = c.GameDay; s.Credit.InterestRemainder = 0; }
                    s.Credit.Principal = checked(s.Credit.Principal + c.Amount); result.CashDelta = c.Amount; break;
                case EconomyAction.RepayCredit:
                    Require(s.Credit.Signed, "No credit agreement."); Positive(c.Amount);
                    Require(c.Amount <= checked(s.Credit.Principal + s.Credit.AccruedInterest), "Repayment exceeds the debt.");
                    PayDebt(c.Amount, ref s.Credit.Principal, ref s.Credit.AccruedInterest);
                    if (s.Credit.Principal == 0 && s.Credit.AccruedInterest == 0) s.Credit.InterestRemainder = 0;
                    result.CashDelta = -c.Amount; break;
                case EconomyAction.TakeLoan:
                    Positive(c.Amount); Require(c.Amount <= r.MaximumLoanPrincipal, "Loan principal exceeds the product maximum.");
                    Require(s.Loans.Count(x => x.Status == "Active") < r.MaximumActiveContracts, "Too many open loans.");
                    var term = Term(c.TermDays, r.DefaultLoanDays, r);
                    var loan = new LoanContract { Id = NewId(s, "loan"), OriginalPrincipal = c.Amount, Principal = c.Amount,
                        DailyInterestBps = r.LoanInterestBps, StartDay = c.GameDay, FirstInterestDay = c.GameDay + 1,
                        DueDay = c.GameDay + term, LastSettledDay = c.GameDay, RulesVersion = r.RulesVersion };
                    s.Loans.Add(loan); result.CashDelta = c.Amount; result.EntityId = loan.Id; break;
                case EconomyAction.RepayLoan:
                    var repayLoan = FindLoan(s, c.ContractId); Require(repayLoan.Status == "Active", "Loan is already settled.");
                    Positive(c.Amount); Require(c.Amount <= checked(repayLoan.Principal + repayLoan.AccruedInterest), "Repayment exceeds the debt.");
                    PayDebt(c.Amount, ref repayLoan.Principal, ref repayLoan.AccruedInterest);
                    if (repayLoan.Principal == 0 && repayLoan.AccruedInterest == 0) { repayLoan.Status = "Paid"; repayLoan.InterestRemainder = 0; }
                    result.CashDelta = -c.Amount; result.EntityId = repayLoan.Id; break;
                case EconomyAction.OpenSecurities:
                    Require(!s.SecuritiesOpened, "Securities account is already open.");
                    s.SecuritiesOpened = true; s.SecuritiesOpenedDay = c.GameDay; break;
                case EconomyAction.CloseSecurities:
                    Require(s.SecuritiesOpened, "No securities account.");
                    Require(s.Positions.Count == 0, "Sell all positions before closing the securities account.");
                    s.SecuritiesOpened = false; break;
                case EconomyAction.BuySecurity:
                case EconomyAction.SellSecurity:
                    Require(s.SecuritiesOpened, "Open a securities account first."); Positive(c.Quantity);
                    Require(c.Quantity <= r.MaximumSecurityQuantity, "Order quantity exceeds the maximum.");
                    var tradeMarket = GetMarketSnapshot(s, c.GameDay, r);
                    Require(!c.ExpectedMarketVersion.HasValue || c.ExpectedMarketVersion.Value == tradeMarket.Version,
                        "行情已更新，请刷新行情后确认新报价再交易。");
                    var quote = tradeMarket.Securities.FirstOrDefault(x => x.Symbol == c.Symbol);
                    Require(quote != null, "Unknown security symbol.");
                    var position = s.Positions.FirstOrDefault(x => x.Symbol == c.Symbol);
                    if (c.Kind == EconomyAction.BuySecurity)
                    {
                        var cost = checked(quote!.BuyPrice * c.Quantity);
                        if (position == null) { position = new SecurityPosition { Symbol = c.Symbol }; s.Positions.Add(position); }
                        position.Quantity = checked(position.Quantity + c.Quantity); position.TotalCost = checked(position.TotalCost + cost);
                        Require(position.Quantity <= r.MaximumSecurityQuantity, "Position limit exceeded."); result.CashDelta = -cost;
                    }
                    else
                    {
                        Require(position != null && position.Quantity >= c.Quantity, "Insufficient position; short selling is unavailable.");
                        var basisRemoved = MulDiv(position!.TotalCost, c.Quantity, position.Quantity);
                        position.Quantity -= c.Quantity; position.TotalCost -= basisRemoved;
                        result.CashDelta = checked(quote!.SellPrice * c.Quantity);
                        if (position.Quantity == 0) s.Positions.Remove(position);
                    }
                    result.EntityId = c.Symbol; break;
                case EconomyAction.AcquirePawnLicense:
                    Require(!s.PawnLicensed, "Pawn business is already licensed."); s.PawnLicensed = true; result.CashDelta = -r.PawnLicenseCost; break;
                case EconomyAction.CreatePawn:
                    Require(s.PawnLicensed, "Acquire the pawn business license first.");
                    Require(!string.IsNullOrWhiteSpace(c.AssetId) && c.AssetId.Length <= 128, "Collateral asset ID is required.");
                    Require(!IsAssetInEscrow(s, c.AssetId), "This asset is already pledged.");
                    Require(s.PawnContracts.Count(x => x.Status == "Active") < r.MaximumActiveContracts, "Too many active pawn contracts.");
                    Positive(c.Amount); Positive(c.CollateralValue);
                    Require(c.Amount <= r.MaximumPawnPrincipal && c.Amount <= MulDiv(c.CollateralValue, r.PawnLoanToValueBps, 10000), "Principal exceeds collateral lending value.");
                    var pawn = new PawnContract { Id = NewId(s, "pawn"), AssetId = c.AssetId, PlayerIsLender = c.PlayerIsLender,
                        Principal = c.Amount, CollateralValue = c.CollateralValue, DailyInterestBps = r.PawnInterestBps,
                        RenewalFeeBps = r.PawnRenewalFeeBps, StartDay = c.GameDay, FirstInterestDay = c.GameDay + 1,
                        DueDay = c.GameDay + Term(c.TermDays, r.DefaultPawnDays, r), LastSettledDay = c.GameDay, RulesVersion = r.RulesVersion };
                    s.PawnContracts.Add(pawn); result.EntityId = pawn.Id;
                    result.CashDelta = c.PlayerIsLender ? -c.Amount : c.Amount;
                    AddAsset(result, pawn, c.PlayerIsLender ? "EscrowFromCustomer" : "EscrowFromPlayer"); break;
                case EconomyAction.RedeemPawn:
                    var redeem = FindPawn(s, c.ContractId); Require(redeem.Status == "Active" && c.GameDay <= redeem.DueDay, "Collateral is not redeemable.");
                    var redemption = checked(redeem.Principal + redeem.AccruedFees);
                    result.CashDelta = redeem.PlayerIsLender ? redemption : -redemption;
                    redeem.Status = "Redeemed"; result.EntityId = redeem.Id;
                    AddAsset(result, redeem, redeem.PlayerIsLender ? "ReturnToCustomer" : "ReturnToPlayer"); break;
                case EconomyAction.RenewPawn:
                    var renew = FindPawn(s, c.ContractId); Require(renew.Status == "Active" && c.GameDay <= renew.DueDay, "Collateral cannot be renewed.");
                    Require(c.GameDay > renew.LastRenewalDay, "This contract has already been renewed today.");
                    var fee = checked(renew.AccruedFees + MulDiv(renew.Principal, renew.RenewalFeeBps, 10000));
                    result.CashDelta = renew.PlayerIsLender ? fee : -fee;
                    renew.AccruedFees = 0; renew.InterestRemainder = 0; renew.FirstInterestDay = c.GameDay + 1; renew.LastSettledDay = c.GameDay;
                    renew.DueDay = checked(Math.Max(c.GameDay, renew.DueDay) + Term(c.TermDays, r.DefaultPawnDays, r));
                    renew.RenewalCount = checked(renew.RenewalCount + 1); renew.LastRenewalDay = c.GameDay;
                    result.EntityId = renew.Id; break;
                case EconomyAction.OptIntoMarketEvents:
                    Require(!s.MarketEventsOptedIn, "Local market events are already enabled by agreement."); s.MarketEventsOptedIn = true; break;
                case EconomyAction.ActivateGoldAgreement:
                    Require(!s.GoldAgreementActive, "Gold agreement is already active.");
                    s.GoldAgreementActive = true; s.GoldAgreementStartDay = c.GameDay; s.MarketRevision = checked(s.MarketRevision + 1); break;
                case EconomyAction.ActivateMarketEvent:
                    Require(s.MarketEventsOptedIn, "Opt into local market events first.");
                    Require(!s.TriggeredEventKinds.Contains(c.EventKind), "This story event has already occurred.");
                    int multiplier;
                    if (c.EventKind == "gold_supply") { Require(s.GoldAgreementActive, "Gold market agreement is required."); multiplier = r.GoldSupplyEventBps; }
                    else if (c.EventKind == "jade_discovery") multiplier = r.JadeDiscoveryEventBps;
                    else if (c.EventKind == "electronics_shortage") multiplier = r.ElectronicsShortageEventBps;
                    else throw new InvalidOperationException("Unknown local market event.");
                    var marketEvent = new LocalMarketEvent { Id = NewId(s, "event"), Kind = c.EventKind, StartDay = c.GameDay + 1,
                        EndDay = c.GameDay + r.MarketEventDays, MultiplierBps = multiplier };
                    s.Events.Add(marketEvent); s.TriggeredEventKinds.Add(c.EventKind); s.MarketRevision = checked(s.MarketRevision + 1);
                    result.EntityId = marketEvent.Id; break;
                default: throw new InvalidOperationException("Unknown economy action.");
            }
        }

        private static void SettleInternal(EconomyState s, int gameDay, EconomyRules r, EconomyResult result)
        {
            if (!HasParticipation(s) || gameDay <= s.LastClosedDay) return;
            Require((long)gameDay - s.LastClosedDay <= r.MaximumCatchupDays, "Settlement gap exceeds the safe catch-up limit; preserve state for recovery.");
            for (var day = s.LastClosedDay + 1; day <= gameDay; day++)
            {
                if (s.Bank.Status == "Open" && day > s.Bank.LastSettledDay)
                {
                    if (day >= s.Bank.FirstInterestDay)
                        s.Bank.Balance = checked(s.Bank.Balance + Interest(s.Bank.Balance, s.Bank.DailyInterestBps, ref s.Bank.InterestRemainder));
                    s.Bank.LastSettledDay = day;
                }
                if (s.Credit.Signed && day > s.Credit.LastSettledDay)
                {
                    if (day >= s.Credit.FirstInterestDay)
                        s.Credit.AccruedInterest = checked(s.Credit.AccruedInterest + Interest(s.Credit.Principal, s.Credit.DailyInterestBps, ref s.Credit.InterestRemainder));
                    s.Credit.LastSettledDay = day;
                }
                foreach (var loan in s.Loans.Where(x => x.Status == "Active"))
                {
                    if (day > loan.LastSettledDay)
                    {
                        if (day >= loan.FirstInterestDay) loan.AccruedInterest = checked(loan.AccruedInterest + Interest(loan.Principal, loan.DailyInterestBps, ref loan.InterestRemainder));
                        loan.LastSettledDay = day;
                    }
                    if (day >= loan.DueDay) loan.Overdue = true;
                }
                foreach (var pawn in s.PawnContracts.Where(x => x.Status == "Active"))
                {
                    if (day > pawn.LastSettledDay)
                    {
                        if (day >= pawn.FirstInterestDay) pawn.AccruedFees = checked(pawn.AccruedFees + Interest(pawn.Principal, pawn.DailyInterestBps, ref pawn.InterestRemainder));
                        pawn.LastSettledDay = day;
                    }
                    // Due day remains redeemable during business; closing that day forfeits.
                    if (day >= pawn.DueDay)
                    {
                        pawn.Status = "Forfeited";
                        AddAsset(result, pawn, pawn.PlayerIsLender ? "ForfeitToPlayer" : "ForfeitToCustomer");
                    }
                }
                foreach (var marketEvent in s.Events) if (day >= marketEvent.EndDay) marketEvent.Expired = true;
                s.LastClosedDay = day;
                ValidateBalances(s, r);
            }
        }

        private static EconomyState ProjectBeforeBusiness(EconomyState s, int day, EconomyRules r)
        {
            Validate(s, r); Require(day >= 0 && day >= s.LastClosedDay, "Invalid or stale quote day.");
            var copy = s.DeepClone(); SettleInternal(copy, day - 1, r, new EconomyResult()); return copy;
        }

        public static void Validate(EconomyState s, EconomyRules r)
        {
            Require(s != null && r != null, "Missing economy state or rules.");
            ValidateRules(r!);
            Require(s!.SchemaVersion == SchemaVersion, "Unsupported economy schema; original state must be retained.");
            Require(s.Bank != null && s.Credit != null && s.Loans != null && s.Positions != null && s.PawnContracts != null && s.Events != null && s.TriggeredEventKinds != null && s.RecentOperations != null, "Missing economy collection.");
            Require(s.Loans.All(x => x != null) && s.PawnContracts.All(x => x != null) && s.Positions.All(x => x != null) && s.Events.All(x => x != null) && s.RecentOperations.All(x => x != null), "Null economy record.");
            Require(s.LastCommandSequence >= 0 && s.NextEntitySequence > 0 && s.LastClosedDay >= -1 && s.MarketRevision >= 0, "Invalid economy cursors.");
            Require(new[] { "None", "Open", "Closed" }.Contains(s.Bank.Status), "Invalid bank state.");
            if (s.Bank.Status != "None") Require(!string.IsNullOrWhiteSpace(s.Bank.AccountId) && s.Bank.CardSerial > 0, "Invalid bank identity.");
            if (s.Bank.Status != "None") Require(s.Bank.OpenedDay >= 0 && s.Bank.FirstInterestDay > s.Bank.OpenedDay && s.Bank.LastSettledDay >= s.Bank.OpenedDay, "Invalid bank accrual dates.");
            if (s.Bank.Status != "Open") Require(s.Bank.Balance == 0, "Closed account holds money.");
            ValidateRate(s.Bank.DailyInterestBps); ValidateRate(s.Credit.DailyInterestBps);
            Require(s.Credit.AccountId != null && s.Credit.AccountId.Length <= 128 && s.Credit.CardSerial >= 0, "Invalid credit card identity.");
            Require((s.Credit.AccountId.Length == 0 && s.Credit.CardSerial == 0) ||
                (!string.IsNullOrWhiteSpace(s.Credit.AccountId) && s.Credit.CardSerial > 0), "Incomplete credit card identity.");
            if (!s.Credit.Signed) Require(s.Credit.Limit == 0 && s.Credit.Principal == 0 && s.Credit.AccruedInterest == 0 &&
                s.Credit.AccountId.Length == 0 && s.Credit.CardSerial == 0, "Unsigned credit has obligations or an active card identity.");
            if (s.Credit.Signed) Require(s.Credit.Limit > 0 && s.Credit.SignedDay >= 0 && s.Credit.FirstInterestDay > s.Credit.SignedDay && s.Credit.LastSettledDay >= s.Credit.SignedDay, "Invalid credit agreement dates or limit.");
            Require(s.Credit.Principal <= s.Credit.Limit, "Principal exceeds agreed credit limit.");
            Require(s.Bank.InterestRemainder >= 0 && s.Bank.InterestRemainder < 10000 && s.Credit.InterestRemainder >= 0 && s.Credit.InterestRemainder < 10000, "Invalid interest remainder.");
            Unique(s.Loans.Select(x => x.Id)); Unique(s.PawnContracts.Select(x => x.Id)); Unique(s.Positions.Select(x => x.Symbol));
            Unique(s.Events.Select(x => x.Id)); Unique(s.TriggeredEventKinds);
            Unique(s.PawnContracts.Where(x => x.Status == "Active").Select(x => x.AssetId));
            Require(s.Loans.Count(x => x.Status == "Active") <= r.MaximumActiveContracts && s.PawnContracts.Count(x => x.Status == "Active") <= r.MaximumActiveContracts, "Too many open contracts.");
            Require(s.Loans.Count(x => x.Status != "Active") <= r.ClosedContractLimit && s.PawnContracts.Count(x => x.Status != "Active") <= r.ClosedContractLimit, "Archived contract history exceeds its bound.");
            Require(s.ArchivedLoanCount >= 0 && s.ArchivedPawnCount >= 0, "Invalid archive counters.");
            Require(s.Positions.Count <= r.MaximumPositions && s.RecentOperations.Count <= r.RecentOperationLimit, "Record limit exceeded.");
            Require(s.Events.Count <= 3 && s.TriggeredEventKinds.Count <= 3, "Unknown market history size.");
            foreach (var loan in s.Loans)
            {
                Require(loan != null && (loan.Status == "Active" || loan.Status == "Paid"), "Invalid loan status.");
                ValidateRate(loan.DailyInterestBps); ValidateDates(loan.StartDay, loan.FirstInterestDay, loan.DueDay, loan.LastSettledDay);
                Require(loan.Principal <= loan.OriginalPrincipal && loan.OriginalPrincipal > 0, "Invalid loan principal.");
                Require(loan.InterestRemainder >= 0 && loan.InterestRemainder < 10000, "Invalid loan remainder.");
                if (loan.Status == "Paid") Require(loan.Principal == 0 && loan.AccruedInterest == 0, "Paid loan still has debt.");
            }
            foreach (var pawn in s.PawnContracts)
            {
                Require(pawn != null && new[] { "Active", "Redeemed", "Forfeited" }.Contains(pawn.Status), "Invalid pawn status.");
                Require(!string.IsNullOrWhiteSpace(pawn.AssetId) && pawn.Principal > 0 && pawn.CollateralValue >= pawn.Principal, "Invalid collateral identity or value.");
                ValidateRate(pawn.DailyInterestBps); ValidateRate(pawn.RenewalFeeBps); ValidateDates(pawn.StartDay, pawn.FirstInterestDay, pawn.DueDay, pawn.LastSettledDay);
                Require(pawn.InterestRemainder >= 0 && pawn.InterestRemainder < 10000 && pawn.RenewalCount >= 0 && pawn.LastRenewalDay >= -1, "Invalid pawn fee state.");
            }
            foreach (var position in s.Positions)
                Require(position != null && position.Quantity > 0 && position.Quantity <= r.MaximumSecurityQuantity && r.Securities.Any(x => x.Symbol == position.Symbol), "Invalid security position.");
            if (!s.SecuritiesOpened) Require(s.Positions.Count == 0, "Unopened securities account has positions.");
            if (s.SecuritiesOpened) Require(s.SecuritiesOpenedDay >= 0, "Invalid securities opening day.");
            if (s.GoldAgreementActive) Require(s.GoldAgreementStartDay >= 0, "Invalid gold agreement day.");
            if (!s.PawnLicensed) Require(s.PawnContracts.Count == 0, "Unlicensed pawn contracts.");
            if (!s.MarketEventsOptedIn) Require(s.Events.Count == 0 && s.TriggeredEventKinds.Count == 0, "Events without agreement.");
            Unique(s.Events.Select(x => x.Kind));
            Require(s.TriggeredEventKinds.All(x => new[] { "gold_supply", "jade_discovery", "electronics_shortage" }.Contains(x)), "Unknown market event history.");
            foreach (var marketEvent in s.Events)
            {
                Require(marketEvent.Scope == "starpawn" && new[] { "gold_supply", "jade_discovery", "electronics_shortage" }.Contains(marketEvent.Kind)
                    && marketEvent.StartDay >= 0 && marketEvent.EndDay >= marketEvent.StartDay && marketEvent.MultiplierBps > 0 && marketEvent.MultiplierBps <= 20000, "Invalid event scope or terms.");
                Require(s.TriggeredEventKinds.Contains(marketEvent.Kind) && (marketEvent.Kind != "gold_supply" || s.GoldAgreementActive), "Market effect lacks its participation record.");
            }
            Unique(s.RecentOperations.Select(x => x.TransactionId));
            var previous = 0L;
            foreach (var receipt in s.RecentOperations)
            {
                Require(receipt.Sequence > previous && receipt.Sequence <= s.LastCommandSequence, "Invalid transaction ordering.");
                previous = receipt.Sequence;
            }
            ValidateBalances(s, r);
        }

        private static void ValidateBalances(EconomyState s, EconomyRules r)
        {
            Balance(s.Bank.Balance, r); Balance(s.Credit.Limit, r); Balance(s.Credit.Principal, r); Balance(s.Credit.AccruedInterest, r);
            foreach (var x in s.Loans) { Balance(x.OriginalPrincipal, r); Balance(x.Principal, r); Balance(x.AccruedInterest, r); }
            foreach (var x in s.PawnContracts) { Balance(x.Principal, r); Balance(x.CollateralValue, r); Balance(x.AccruedFees, r); }
            foreach (var x in s.Positions) Balance(x.TotalCost, r);
        }

        private static void ValidateRules(EconomyRules r)
        {
            ValidateRate(r.BankInterestBps); ValidateRate(r.CreditInterestBps); ValidateRate(r.LoanInterestBps); ValidateRate(r.PawnInterestBps); ValidateRate(r.PawnRenewalFeeBps);
            Require(r.RulesVersion > 0 && r.PawnLoanToValueBps > 0 && r.PawnLoanToValueBps <= 10000, "Invalid rules version or lending ratio.");
            Require(r.MaximumLedgerBalance > 0 && r.MaximumLedgerBalance <= 1000000000000L && r.MaximumCashBalance > 0 && r.MaximumCashBalance <= int.MaxValue, "Invalid monetary bound.");
            Require(r.MaximumCreditLimit > 0 && r.MaximumLoanPrincipal > 0 && r.MaximumPawnPrincipal > 0 && r.PawnLicenseCost >= 0, "Invalid finance limits.");
            Require(r.MaximumActiveContracts > 0 && r.MaximumPositions > 0 && r.RecentOperationLimit >= 1 && r.ClosedContractLimit >= 0, "Invalid record limits.");
            Require(r.DefaultLoanDays > 0 && r.DefaultPawnDays > 0 && r.MaximumTermDays > 0 && r.DefaultLoanDays <= r.MaximumTermDays && r.DefaultPawnDays <= r.MaximumTermDays && r.MaximumTermDays <= 3650, "Invalid term lengths.");
            Require(r.MaximumCatchupDays >= 1 && r.MaximumCatchupDays <= 36600 && r.MaximumSecurityQuantity > 0, "Invalid settlement/position bound.");
            Require(r.GoldBuyPerGram > 0 && r.GoldSellPerGram >= r.GoldBuyPerGram && r.SilverBuyPerGram > 0 && r.SilverSellPerGram >= r.SilverBuyPerGram, "Invalid precious metal spread.");
            Require(r.GoldDailyVariationBps >= 0 && r.GoldDailyVariationBps < 10000 && r.SecuritySpreadBps >= 0 && r.SecuritySpreadBps < 10000, "Invalid market variation or spread.");
            Require(r.MarketEventDays > 0 && r.MarketEventDays <= 3650 && r.GoldSupplyEventBps > 0 && r.GoldSupplyEventBps <= 20000 && r.JadeDiscoveryEventBps > 0 && r.JadeDiscoveryEventBps <= 20000 && r.ElectronicsShortageEventBps > 0 && r.ElectronicsShortageEventBps <= 20000, "Invalid event terms.");
            Require(r.Securities != null && r.Securities.Count > 0 && r.Securities.Count <= r.MaximumPositions, "Invalid securities catalog.");
            Require(r.Securities.All(x => x != null), "Null security definition.");
            Unique(r.Securities.Select(x => x.Symbol));
            foreach (var x in r.Securities)
                Require(x.BasePrice > 0 && x.BasePrice <= r.MaximumLedgerBalance && (x.Kind == "Stock" || x.Kind == "Crypto") && x.DailyVariationBps >= 0 && x.DailyVariationBps < 10000, "Invalid security definition.");
        }

        private static bool HasParticipation(EconomyState s) => s.Bank.Status == "Open" || s.Credit.Signed || s.Loans.Any(x => x.Status == "Active") || s.SecuritiesOpened || s.PawnLicensed || s.MarketEventsOptedIn || s.GoldAgreementActive;
        private static void BankOpen(EconomyState s) => Require(s.Bank.Status == "Open", "Open a savings account first.");
        private static void Positive(long amount) => Require(amount > 0, "Amount must be positive.");
        private static void Balance(long value, EconomyRules r) => Require(value >= 0 && value <= r.MaximumLedgerBalance, "Balance is negative or exceeds its bound.");
        private static void ValidateRate(int rate) => Require(rate >= 0 && rate <= 10000, "Invalid basis-point interest rate.");
        private static void ValidateDates(int start, int first, int due, int last) => Require(start >= 0 && first > start && due >= first && last >= start, "Invalid contract dates.");
        private static void Unique(IEnumerable<string> ids)
        { var set = new HashSet<string>(StringComparer.Ordinal); foreach (var id in ids) Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 128 && set.Add(id), "Missing or duplicate record ID."); }
        private static void Require([DoesNotReturnIf(false)] bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        private static int Term(int requested, int fallback, EconomyRules r) { var term = requested == 0 ? fallback : requested; Require(term > 0 && term <= r.MaximumTermDays, "Invalid contract term."); return term; }
        private static string NewId(EconomyState s, string prefix) { var id = prefix + "-" + s.NextEntitySequence; s.NextEntitySequence = checked(s.NextEntitySequence + 1); return id; }
        private static LoanContract FindLoan(EconomyState s, string id) => s.Loans.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Unknown loan contract.");
        private static PawnContract FindPawn(EconomyState s, string id) => s.PawnContracts.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Unknown pawn contract.");
        private static long Interest(long principal, int rate, ref long remainder)
        { var numerator = checked(principal * rate + remainder); var accrued = numerator / 10000; remainder = numerator % 10000; return accrued; }
        private static long MulDiv(long a, long b, long divisor) => checked((long)((decimal)a * b / divisor));
        private static void PayDebt(long amount, ref long principal, ref long interest)
        { var paidInterest = Math.Min(amount, interest); interest -= paidInterest; principal -= amount - paidInterest; }
        private static int Variation(int seed, int day, string key, int maximum)
        {
            unchecked { uint hash = (uint)seed ^ 2166136261u; foreach (var c in key) hash = (hash ^ c) * 16777619u;
                hash = (hash ^ (uint)day) * 16777619u; hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                return maximum == 0 ? 0 : (int)(hash % (uint)(maximum * 2 + 1)) - maximum; }
        }
        private static void AddAsset(EconomyResult result, PawnContract pawn, string kind) =>
            result.AssetTransitions.Add(new AssetTransition { AssetId = pawn.AssetId, ContractId = pawn.Id, Kind = kind });
        private static void Trim(EconomyState s, EconomyRules r)
        {
            while (s.RecentOperations.Count > r.RecentOperationLimit) s.RecentOperations.RemoveAt(0);
            while (s.Loans.Count(x => x.Status != "Active") > r.ClosedContractLimit)
            { s.Loans.Remove(s.Loans.First(x => x.Status != "Active")); s.ArchivedLoanCount = checked(s.ArchivedLoanCount + 1); }
            while (s.PawnContracts.Count(x => x.Status != "Active") > r.ClosedContractLimit)
            { s.PawnContracts.Remove(s.PawnContracts.First(x => x.Status != "Active")); s.ArchivedPawnCount = checked(s.ArchivedPawnCount + 1); }
        }
        private static void Commit(EconomyState s, EconomyState n)
        {
            s.LastCommandSequence = n.LastCommandSequence; s.NextEntitySequence = n.NextEntitySequence; s.LastClosedDay = n.LastClosedDay;
            s.MarketSeed = n.MarketSeed; s.MarketRevision = n.MarketRevision; s.Bank = n.Bank; s.Credit = n.Credit; s.Loans = n.Loans;
            s.SecuritiesOpened = n.SecuritiesOpened; s.SecuritiesOpenedDay = n.SecuritiesOpenedDay; s.Positions = n.Positions;
            s.PawnLicensed = n.PawnLicensed; s.PawnContracts = n.PawnContracts; s.MarketEventsOptedIn = n.MarketEventsOptedIn;
            s.GoldAgreementActive = n.GoldAgreementActive; s.GoldAgreementStartDay = n.GoldAgreementStartDay;
            s.Events = n.Events; s.TriggeredEventKinds = n.TriggeredEventKinds; s.RecentOperations = n.RecentOperations;
            s.ArchivedLoanCount = n.ArchivedLoanCount; s.ArchivedPawnCount = n.ArchivedPawnCount;
        }
    }
}
