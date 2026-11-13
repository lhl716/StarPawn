using System;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private void InitBankCards() => NativeCards.Enable(log!, InspectBankCard);
        private void StopBankCards() => NativeCards.Disable();

        private bool IsCurrentBankCard(NativeCardInfo card)
        {
            if (!SameRun() || card == null || card.RunId != state!.Catalog.RunId || card.Serial < 1) return false;
            if (card.Kind == "savings")
                return state.Economy.Bank.Status == "Open" && card.AccountId == state.Economy.Bank.AccountId && card.Serial == state.Economy.Bank.CardSerial;
            if (card.Kind == "credit")
                return state.Economy.Credit.Signed && card.AccountId == state.Economy.Credit.AccountId && card.Serial == state.Economy.Credit.CardSerial;
            return false;
        }

        private void InspectBankCard(NativeCardInfo card)
        {
            if (!EnsureRun()) return;
            if (!IsCurrentBankCard(card))
            {
                var ui = BeginPage("银行凭证 · 已失效", "旧卡、其他存档的卡或已关闭账户的卡片不能作为当前账户凭证。");
                Text(ui,24,92,550,110,"这张卡片不再对应当前有效账户与卡片版本。\n账户余额与债务保存在账户中，丢卡或销毁卡片不会清除它们。\n补卡会使旧版本失效；卡片本身没有可以复制的余额。",14);
                Button(ui,24,246,260,30,"查看我的有效凭证",OpenCards);
                Footer(ui); Show(ui); return;
            }
            notice = "已验证" + (card.Kind == "savings" ? "储蓄卡" : "信用卡") + "，版本 " + card.Serial + "。";
            if (card.Kind == "savings") OpenBank(); else OpenCreditLoans();
        }

        // Issuing plastic is deliberately after the financial commit: inventory capacity
        // cannot cancel an already valid account, and a card never stores spendable cash.
        private void SyncCards(EconomyAction action)
        {
            var kind = action == EconomyAction.OpenBank || action == EconomyAction.ReissueBankCard ? "savings" :
                action == EconomyAction.OpenCredit || action == EconomyAction.ReissueCreditCard ? "credit" : "";
            if (kind.Length == 0) return;
            try
            {
                if (!TryDeliverBankCard(kind, out var error))
                    notice += " 卡片尚未入库：" + error + "；账户已生效，可稍后在凭证页领取。";
                else notice += " 有效卡片已在原版库存。";
            }
            catch (Exception exception)
            {
                notice += " 账户已生效；卡片发放需稍后重试。";
                log?.Error("SP_CARD_DELIVERY: " + exception);
            }
        }

        private bool TryDeliverBankCard(string kind, out string error)
        {
            error = "";
            if (!SameRun() || state == null) { error = "当前账户尚未就绪。"; return false; }
            string accountId;
            int serial;
            if (kind == "savings" && state.Economy.Bank.Status == "Open")
            { accountId = state.Economy.Bank.AccountId; serial = state.Economy.Bank.CardSerial; }
            else if (kind == "credit" && state.Economy.Credit.Signed)
            { accountId = state.Economy.Credit.AccountId; serial = state.Economy.Credit.CardSerial; }
            else { error = "请先主动开立对应账户。"; return false; }
            if (string.IsNullOrEmpty(accountId) || serial < 1) { error = "旧信用账户请先补发凭证；已有债务不受影响。"; return false; }
            var issued = NativeCards.TryIssue(kind, accountId, serial, state.Catalog.RunId, out _, out error);
            if (issued) log?.Log("SP_CARD_DELIVERED: kind=" + kind + ", account=" + accountId + ", serial=" + serial);
            return issued;
        }

        private void ClaimBankCard(string kind)
        {
            if (!EnsureRun()) return;
            notice = TryDeliverBankCard(kind, out var error) ? "当前有效卡片已在原版库存；余额与欠款没有改变。" : "暂未发卡：" + error;
            OpenCards();
        }

        private void OpenCards()
        {
            if (!EnsureRun() || state == null) return;
            var bank = state.Economy.Bank;
            var credit = state.Economy.Credit;
            var ui = BeginPage("银行凭证 · 实体卡片", "卡片只引用账户；复制、遗失或补办卡片都不会复制余额或消除欠款。");
            ui.AddImage(24,82,86,54,"starpawn:bank_card","stretch","savings-card-preview"); ui.Last.raycast=false;
            Text(ui,124,78,450,55,bank.Status == "Open" ? "储蓄账户 " + bank.AccountId + " · 有效卡版本 " + bank.CardSerial +
                "\n权威余额 " + bank.Balance + " Cr" : "尚无开放的储蓄账户。\n查看或领取凭证不会自动开户。",11);
            if (bank.Status == "Open")
            {
                Button(ui,124,140,215,26,"领取当前储蓄卡",()=>ClaimBankCard("savings"));
                Button(ui,349,140,225,26,"补办储蓄卡 · 旧卡失效",()=>{ExecuteEconomy(new EconomyCommand{Kind=EconomyAction.ReissueBankCard});OpenCards();});
            }
            else Button(ui,124,140,215,26,"前往银行了解开户",OpenBank);
            ui.AddImage(24,196,86,54,"starpawn:credit_card","stretch","credit-card-preview"); ui.Last.raycast=false;
            Text(ui,124,192,450,58,credit.Signed ? "信用账户 " + credit.AccountId + " · 有效卡版本 " + credit.CardSerial +
                "\n额度 " + credit.Limit + " Cr · 已借本金 " + credit.Principal + " Cr" : "尚未签署信用协议。\n信用卡不代表额外现金；借款仍需主动办理。",11);
            if (credit.Signed)
            {
                if (!string.IsNullOrEmpty(credit.AccountId) && credit.CardSerial > 0)
                    Button(ui,124,254,215,26,"领取当前信用卡",()=>ClaimBankCard("credit"));
                else Text(ui,124,254,215,30,"旧账户请先补发卡片 →",10);
                Button(ui,349,254,225,26,"补办信用卡 · 旧卡失效",()=>{ExecuteEconomy(new EconomyCommand{Kind=EconomyAction.ReissueCreditCard});OpenCards();});
            }
            else Button(ui,124,254,215,26,"了解信用产品",OpenCreditLoans);
            Text(ui,24,296,550,30,"卡片为银行凭证，不能出售、拆解或典当。旧卡可保留为失效凭证。",10);
            Footer(ui); Show(ui);
        }
    }
}
