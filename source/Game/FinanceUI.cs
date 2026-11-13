using System;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private int financeLoanPage;
        private int financePawnPage;

        private void OpenBank()
        {
            if (state == null) return;
            var bank = state.Economy.Bank;
            var ui = BeginPage("星港银行 · 储蓄与卡片", "仅主动签约后生效。随身现金 " + Cash() + " Cr");
            if (bank.Status != "Open")
            {
                Text(ui, 24, 74, 550, 36, bank.Status == "Closed" ? "原账户已关闭，可主动重新开户。" : "尚未开户：不会产生存款、利息、债务或自动费用。");
                Text(ui, 24, 116, 550, 45, "储蓄日息 " + FinanceRate(rules.BankInterestBps) + "；新存款次日起息。\n每日存入上限 " + rules.DailyDepositLimit + "，取现上限 " + rules.DailyWithdrawalLimit + " Cr。", 10);
                Button(ui, 24, 169, 150, 24, "开立储蓄账户", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.OpenBank }, OpenBank), "bank-open");
            }
            else
            {
                Text(ui, 24, 73, 550, 27, "账户余额 " + bank.Balance + " Cr    日息 " + FinanceRate(bank.DailyInterestBps), 14);
                Text(ui, 24, 105, 550, 39, "账户 " + bank.AccountId + " · 储蓄卡版本 " + bank.CardSerial + "\n今日存入 " + DailyUsage(EconomyAction.Deposit) + " · 取现 " + DailyUsage(EconomyAction.Withdraw), 10);
                Button(ui, 24, 152, 115, 24, "存入 100", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.Deposit, Amount = 100 }, OpenBank), "bank-deposit-100");
                Button(ui, 149, 152, 115, 24, "存入 500", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.Deposit, Amount = 500 }, OpenBank), "bank-deposit-500");
                Button(ui, 274, 152, 115, 24, "取出 100", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.Withdraw, Amount = 100 }, OpenBank), "bank-withdraw-100");
                var withdrawable = Math.Min(bank.Balance, EconomyService.GetDailyRemaining(state.Economy, EconomyAction.Withdraw, Day(), rules));
                if (withdrawable > 0)
                    Button(ui, 399, 152, 175, 24, "取出可用 " + withdrawable, () => FinanceDo(new EconomyCommand { Kind = EconomyAction.Withdraw, Amount = withdrawable }, OpenBank), "bank-withdraw-all");
                else Text(ui, 399, 154, 175, 22, bank.Balance == 0 ? "暂无可取余额" : "今日取现额度已用完", 10);
                Button(ui, 24, 188, 150, 24, "补发储蓄卡", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.ReissueBankCard }, OpenBank), "bank-card");
                Button(ui, 184, 188, 150, 24, "关闭零余额账户", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.CloseBank }, OpenBank), "bank-close");
                Button(ui, 344, 188, 230, 24, "实体银行卡 · 领取与补办", OpenCards, "bank-cards");
                Text(ui, 24, 226, 550, 45, "当前可计息本金 " + EconomyService.GetSavingsInterestPrincipal(bank, Day()) + " Cr；新存款次日起息。\n已结算至第 " + bank.LastSettledDay + " 日；补卡或重新开户不刷新今日额度。", 10);
            }
            Button(ui, 24, 283, 170, 24, "信用与普通贷款", OpenCreditLoans, "bank-credit");
            Button(ui, 204, 283, 170, 24, "证券与本地行情", OpenMarkets, "bank-markets");
            Button(ui, 384, 283, 190, 24, "返回经营台", OpenHome, "bank-home");
            Footer(ui); Show(ui);
        }

        private void OpenCreditLoans()
        {
            if (state == null) return;
            var credit = state.Economy.Credit;
            var ui = BeginPage("信用与贷款", "额度不是现金。借款增加现金与负债；还款从随身现金中扣除。");
            if (!credit.Signed)
            {
                Text(ui, 24, 73, 550, 44, "信用产品：额度 " + rules.MaximumCreditLimit + " Cr，日息 " + FinanceRate(rules.CreditInterestBps) + "。\n仅对已借本金计息，首笔借款次日开始，不因查看自动签约。", 10);
                Button(ui, 24, 124, 210, 24, "签约信用额度 " + rules.MaximumCreditLimit, () => FinanceDo(new EconomyCommand { Kind = EconomyAction.OpenCredit, Amount = rules.MaximumCreditLimit }, OpenCreditLoans), "credit-open");
            }
            else
            {
                var debt = EconomyService.GetCreditPayoff(state.Economy, Day(), rules);
                Text(ui, 24, 73, 550, 40, "授信 " + credit.Limit + " · 已借本金 " + credit.Principal + " · 应还合计 " + debt + " Cr\n日息 " + FinanceRate(credit.DailyInterestBps) + "；还款先偿利息，再减少本金。", 10);
                Button(ui, 24, 124, 125, 24, "借出 100", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.BorrowCredit, Amount = 100 }, OpenCreditLoans), "credit-borrow-100");
                Button(ui, 159, 124, 125, 24, "借出 500", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.BorrowCredit, Amount = 500 }, OpenCreditLoans), "credit-borrow-500");
                Button(ui, 294, 124, 125, 24, "还款 " + Math.Min(100, debt), () => FinanceDo(new EconomyCommand { Kind = EconomyAction.RepayCredit, Amount = Math.Min(100, debt) }, OpenCreditLoans), "credit-repay-100");
                Button(ui, 429, 124, 145, 24, "还清 " + debt, () => FinanceDo(new EconomyCommand { Kind = EconomyAction.RepayCredit, Amount = debt }, OpenCreditLoans), "credit-repay-all");
            }
            Text(ui, 24, 151, 550, 17, "今日信用借款 " + DailyUsage(EconomyAction.BorrowCredit) + " Cr；还款不限额。", 9);
            Text(ui, 24, 174, 550, 39, "普通贷款：期限 " + rules.DefaultLoanDays + " 个游戏日，日息 " + FinanceRate(rules.LoanInterestBps) + "。\n签约次日起计息；旧合同保持签约利率。", 10);
            Button(ui, 24, 221, 160, 24, "借款 500", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.TakeLoan, Amount = 500, TermDays = rules.DefaultLoanDays }, OpenCreditLoans), "loan-500");
            Button(ui, 194, 221, 160, 24, "借款 1000", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.TakeLoan, Amount = 1000, TermDays = rules.DefaultLoanDays }, OpenCreditLoans), "loan-1000");
            Button(ui, 364, 221, 210, 24, "查看贷款合同", () => { financeLoanPage = 0; OpenLoanLedger(); }, "loan-ledger");
            Text(ui, 24, 251, 550, 22, "今日贷款 " + DailyUsage(EconomyAction.TakeLoan) + "；未还本金 " + state.Economy.Loans.Where(x => x.Status == "Active").Sum(x => x.Principal) + " / " + rules.MaximumTotalLoanPrincipal, 9);
            Button(ui, 24, 283, 170, 24, "返回储蓄账户", OpenBank, "credit-back");
            Button(ui, 204, 283, 170, 24, "返回经营台", OpenHome, "credit-home");
            if (credit.Signed)
                Button(ui, 384, 283, 190, 24, "结束零欠款信用协议", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.CloseCredit }, OpenCreditLoans), "credit-close");
            Footer(ui); Show(ui);
        }

        private void OpenLoanLedger()
        {
            if (state == null) return;
            var all = state.Economy.Loans.OrderBy(x => x.Status == "Active" ? 0 : 1).ThenBy(x => x.DueDay).ToList();
            const int pageSize = 3;
            financeLoanPage = Math.Max(0, Math.Min(financeLoanPage, Math.Max(0, (all.Count - 1) / pageSize)));
            var ui = BeginPage("贷款合同", "现金 " + Cash() + " Cr · 第 " + Day() + " 个游戏日 · 合同 " + all.Count + " 份");
            if (all.Count == 0) Text(ui, 24, 87, 550, 70, "暂无贷款。信用卡欠款在信用页单独显示。");
            var index = 0;
            foreach (var loan in all.Skip(financeLoanPage * pageSize).Take(pageSize))
            {
                var y = 72 + index++ * 70;
                var payoff = loan.Status == "Active" ? EconomyService.GetLoanPayoff(state.Economy, loan.Id, Day(), rules) : 0;
                Text(ui, 24, y, 390, 49, loan.Id + " · " + (loan.Status == "Paid" ? "已清偿" : loan.Overdue ? "已逾期" : "履约中") + " · 到期第 " + loan.DueDay + " 日\n本金 " + loan.Principal + " / 应还 " + payoff + " Cr / 日息 " + FinanceRate(loan.DailyInterestBps), 10);
                if (loan.Status == "Active")
                    Button(ui, 427, y + 10, 147, 24, "偿清 " + payoff, () => FinanceDo(new EconomyCommand { Kind = EconomyAction.RepayLoan, ContractId = loan.Id, Amount = payoff }, OpenLoanLedger), "loan-pay-" + loan.Id);
            }
            Button(ui, 24, 294, 105, 24, "上一页", () => { financeLoanPage--; OpenLoanLedger(); }, "loans-prev");
            Button(ui, 139, 294, 105, 24, "下一页", () => { financeLoanPage++; OpenLoanLedger(); }, "loans-next");
            Button(ui, 254, 294, 155, 24, "信用与贷款", OpenCreditLoans, "loans-back");
            Button(ui, 419, 294, 155, 24, "经营台", OpenHome, "loans-home");
            Footer(ui); Show(ui);
        }

        private void OpenMarkets()
        {
            if (state == null) return;
            var market = Market();
            var ui = BeginPage("星港交易所 · 今日行情", "第 " + Day() + " 日 · 报价单位 Cr · 价格可能涨跌，买卖存在价差。");
            Text(ui, 24, 71, 550, 22, "黄金每克：买入 " + market.GoldSellPerGram + " / 回收 " + market.GoldBuyPerGram +
                "；白银：买入 " + market.SilverSellPerGram + " / 回收 " + market.SilverBuyPerGram, 10);
            var news = state.Economy.Events.Where(x => x.EndDay >= Day()).OrderBy(x => x.StartDay).ToList();
            var headlines = news.Count == 0 ? "前线消息：目前没有生效或待生效的商会简报。" :
                string.Join("\n", news.Select(x => (x.StartDay > Day() ? "预告 · " : "生效 · ") + FinanceEventName(x.Kind) +
                    "  第 " + x.StartDay + "–" + x.EndDay + " 日"));
            Text(ui, 24, 96, 550, 40, headlines, 10);
            if (!state.Economy.SecuritiesOpened)
            {
                Text(ui, 24, 145, 550, 66, "证券账户尚未开通，没有持仓与证券业务。\n开户可交易股票及星云币，商会黄金协议与简报订阅须另行参加。", 11);
                Button(ui, 24, 220, 185, 24, "主动开通证券账户", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.OpenSecurities }, OpenMarkets), "securities-open");
            }
            else
            {
                var i = 0;
                foreach (var security in market.Securities.Take(4))
                {
                    var y = 140 + i++ * 32;
                    var position = state.Economy.Positions.FirstOrDefault(x => x.Symbol == security.Symbol);
                    Text(ui, 24, y, 340, 30, FinanceSecurityName(security.Symbol) + " " + security.Symbol + " · 持有 " + (position?.Quantity ?? 0) +
                        "\n每单位买入 " + security.BuyPrice + " / 卖出 " + security.SellPrice, 10);
                    Button(ui, 372, y + 3, 96, 24, "买 1 单位", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.BuySecurity, Symbol = security.Symbol, Quantity = 1, ExpectedMarketVersion = market.Version }, OpenMarkets), "buy-" + security.Symbol);
                    Button(ui, 478, y + 3, 96, 24, "卖 1 单位", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.SellSecurity, Symbol = security.Symbol, Quantity = 1, ExpectedMarketVersion = market.Version }, OpenMarkets), "sell-" + security.Symbol);
                }
                Button(ui, 429, 271, 145, 20, "关闭空仓账户", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.CloseSecurities }, OpenMarkets), "securities-close");
            }
            Button(ui, 24, 271, 150, 20, "刷新今日行情", OpenMarkets, "market-refresh");
            Button(ui, 24, 300, 180, 24, "商会协议与前线简报", OpenMarketAgreements, "market-events");
            Button(ui, 214, 300, 170, 24, "库存与投资金条", OpenOwned, "market-owned");
            Button(ui, 394, 300, 180, 24, "返回经营台", OpenHome, "market-home");
            Footer(ui); Show(ui);
        }

        private void OpenMarketAgreements()
        {
            if (state == null) return;
            var economy = state.Economy;
            var ui = BeginPage("星港商会 · 协议与前线简报", "简报影响本商会收录商品与相关证券的报价，其他商家仍按各自规则营业。");
            Text(ui, 24, 73, 550, 40, economy.GoldAgreementActive ? "黄金协议已签署：次日起金价每日涨跌，金饰与投资金条共享行情。\n协议不会代你买卖，盈亏取决于成交价格。" : "黄金协议尚未签署：银行开户或购买金饰均不会自动参加。\n主动签署后，次日起接受商会每日金价，金饰与金条共享行情。", 10);
            if (!economy.GoldAgreementActive)
                Button(ui, 24, 119, 220, 24, "签署商会黄金协议", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.ActivateGoldAgreement }, OpenMarketAgreements), "gold-agreement");
            Text(ui, 24, 156, 550, 39, economy.MarketEventsOptedIn ? "前线简报已订阅：接收一次报告，便推进对应的商会剧情。\n每份报告每局仅一次，相关行情次日起生效。" : "前线简报尚未订阅，相关商会剧情不会推进。\n主动订阅后，再接收报告参与；相关行情次日起生效。", 10);
            if (!economy.MarketEventsOptedIn)
                Button(ui, 24, 204, 220, 24, "订阅简报并参与商会剧情", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.OptIntoMarketEvents }, OpenMarketAgreements), "events-opt-in");
            else
            {
                AddMarketEventButton(ui, 24, "gold_supply", "接收矿业简报", "gold-event");
                AddMarketEventButton(ui, 214, "jade_discovery", "接收青岚勘探报告", "jade-event");
                AddMarketEventButton(ui, 404, "electronics_shortage", "接收航运通告", "electronics-event");
                Text(ui, 24, 250, 550, 36, "报告影响持续 " + rules.MarketEventDays + " 个游戏日；矿业简报须先签署黄金协议。\n消息可能改变商品与证券价格；收益不保证，成交仍有买卖价差。", 10);
            }
            Button(ui, 24, 292, 170, 24, "返回行情", OpenMarkets, "events-back");
            Button(ui, 204, 292, 170, 24, "返回经营台", OpenHome, "events-home");
            Footer(ui); Show(ui);
        }

        private void AddMarketEventButton(PaperUI ui, float x, string kind, string label, string id)
        {
            if (state == null) return;
            var known = state.Economy.TriggeredEventKinds.Contains(kind);
            if (known)
            {
                var report = state.Economy.Events.FirstOrDefault(e => e.Kind == kind);
                Text(ui, x, 203, 170, 44, FinanceEventName(kind) + " · 已收阅" +
                    (report == null ? "" : "\n影响：第 " + report.StartDay + "–" + report.EndDay + " 日"), 9);
            }
            else Button(ui, x, 204, 170, 24, label, () => FinanceDo(new EconomyCommand { Kind = EconomyAction.ActivateMarketEvent, EventKind = kind }, OpenMarketAgreements), id);
        }

        private void OpenPawnLedger()
        {
            if (state == null) return;
            var economy = state.Economy;
            var ui = BeginPage("典当合同台账", "抵押原件交由保管。期限当天营业期间可赎当，收盘后才绝当。");
            if (!economy.PawnLicensed)
            {
                Text(ui, 24, 78, 550, 86, "典当经营尚未取得许可。\n许可费用 " + rules.PawnLicenseCost + " Cr；默认期限 " + rules.DefaultPawnDays + " 个游戏日。\n最高放款为估值的 " + FinanceRate(rules.PawnLoanToValueBps) + "，日费用 " + FinanceRate(rules.PawnInterestBps) + "。", 11);
                Button(ui, 24, 178, 240, 24, "支付许可费用并开业", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.AcquirePawnLicense }, OpenPawnLedger), "pawn-license");
            }
            else
            {
                var all = economy.PawnContracts.OrderBy(x => x.Status == "Active" ? 0 : 1).ThenBy(x => x.DueDay).ToList();
                const int pageSize = 3;
                financePawnPage = Math.Max(0, Math.Min(financePawnPage, Math.Max(0, (all.Count - 1) / pageSize)));
                if (all.Count == 0) Text(ui, 24, 84, 550, 80, "暂无合同。请从资产或顾客流程发起典当。\n托管物保留原实例并被锁定，仍占用原库存空间。", 11);
                var i = 0;
                foreach (var pawn in all.Skip(financePawnPage * pageSize).Take(pageSize))
                {
                    var y = 72 + i++ * 67;
                    var active = pawn.Status == "Active" && Day() <= pawn.DueDay;
                    var due = active ? EconomyService.GetPawnRedemptionQuote(economy, pawn.Id, Day(), rules) : 0;
                    var status = pawn.Status == "Redeemed" ? "已赎回" : pawn.Status == "Forfeited" ? "已绝当" : active ? "托管中" : "待收盘结算";
                    Text(ui, 24, y, 335, 53, pawn.Id + " · " + (pawn.PlayerIsLender ? "店主放款" : "玩家借款") + " · " + status +
                        "\n资产 " + pawn.AssetId + "\n本金 " + pawn.Principal + " / 赎当 " + due + " / 截止第 " + pawn.DueDay + " 日", 9);
                    if (active)
                    {
                        Button(ui, 368, y + 5, 100, 24, pawn.PlayerIsLender ? "顾客赎当" : "支付赎当", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.RedeemPawn, ContractId = pawn.Id }, OpenPawnLedger), "pawn-redeem-" + pawn.Id);
                        Button(ui, 478, y + 5, 96, 24, "续当", () => FinanceDo(new EconomyCommand { Kind = EconomyAction.RenewPawn, ContractId = pawn.Id, TermDays = rules.DefaultPawnDays }, OpenPawnLedger), "pawn-renew-" + pawn.Id);
                        Text(ui, 368, y + 34, 206, 20, "续当需付已计费用 + " + FinanceRate(pawn.RenewalFeeBps), 9);
                    }
                }
            }
            Button(ui, 24, 291, 95, 24, "上一页", () => { financePawnPage--; OpenPawnLedger(); }, "pawn-prev");
            Button(ui, 129, 291, 95, 24, "下一页", () => { financePawnPage++; OpenPawnLedger(); }, "pawn-next");
            Button(ui, 234, 291, 165, 24, "我的资产", OpenOwned, "pawn-assets");
            Button(ui, 409, 291, 165, 24, "返回经营台", OpenHome, "pawn-home");
            Footer(ui); Show(ui);
        }

        private void FinanceDo(EconomyCommand command, Action refresh)
        {
            try { ExecuteEconomy(command); }
            catch (InvalidOperationException error) when (!blocked)
            { notice = error.Message; log?.Warning("SP_FINANCE_DECLINED: " + error.Message); }
            refresh();
        }

        private static string FinanceRate(int basisPoints) => (basisPoints / 100m).ToString("0.##") + "%";
        private string DailyUsage(EconomyAction kind) => state == null ? "" : EconomyService.GetDailyUsed(state.Economy, kind, Day()) + " / " + EconomyService.GetDailyLimit(kind, rules);

        private static string FinanceSecurityName(string symbol)
        {
            switch (symbol)
            {
                case "ORBIT": return "星环矿业";
                case "JADE": return "青岚开发";
                case "BYTE": return "轨道电子";
                case "NEBULA": return "星云币";
                default: return symbol;
            }
        }

        private static string FinanceEventName(string kind)
        {
            switch (kind)
            {
                case "gold_supply": return "星环金矿增供";
                case "jade_discovery": return "青岚发现翡翠矿";
                case "electronics_shortage": return "航运受阻、电子供给短缺";
                default: return "商会简报";
            }
        }
    }
}
