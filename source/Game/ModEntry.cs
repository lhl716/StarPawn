using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarPawn.Core;
using UnityEngine;

namespace StarPawn.Game
{
    public sealed class RunState
    {
        public int SchemaVersion = 2;
        public CatalogState Catalog = StarPawn.Core.Catalog.CreateState(Guid.NewGuid().ToString("N"));
        public EconomyState Economy = EconomyService.CreateNewState();
        public long NextOperationSequence = 1;
        public int OffersDay = -1;
        public List<string> OfferAssetIds = new List<string>();
        public List<RunReceipt> Ledger = new List<RunReceipt>();
        public long ArchivedReceipts;
    }
    public sealed class RunReceipt
    {
        public string OperationId = "";
        public int Day;
        public string Description = "";
        public long CashDelta;
        public string AssetId = "";
    }
    public sealed partial class ModEntry : IMod
    {
        private const string ModId = "starpawn", SaveKey = "unified-run-v2";
        private ModLog? log;
        private ModManifest? startupManifest;
        private RunState? state;
        private readonly EconomyRules rules = new EconomyRules();
        private PlayerStore? currentStore;
        private int currentSlot = -1;
        private PaperUI? window;
        private GameObject? hotkeyObject;
        private bool enabled, blocked, runReady, busy;
        private int generation;
        private string notice = "欢迎来到星港。验货、做买卖，或到银行办理业务。";
        private string selectedAsset = "", selectedEncounter = "", selectedView = "front";
        private int ownedPage, offerPage;
        public void Init(ModManifest manifest) { startupManifest=manifest; log = new ModLog(manifest); log.Log("SP_INIT: configuration waits for the native item-directory hook."); }
        public void OnEnable()
        {
            if (enabled) return;
            enabled = true;
            // Rev13 adds LoadedMod to ModLoader.Mods only AFTER Init and OnEnable.
            // Scoped file APIs therefore become usable at this later native hook.
            ModHook.OnModItemDirectoryInit += ConfigureNativeItems;
            NativeBridge.Enable(log!, OpenNativeAsset);
            InitNativeTrade();
            InitBankCards();
            NativeBridge.NightlySettlementRequested += SettleNight;
            ModHook.OnGameLoadedInit += BeginLoad; ModHook.OnGameLoadedLate += FinishLoad; ModHook.OnNewGameStarted += NewGame;
            ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "StarPawn Desk", OpenHome);
            ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "StarPawn Save Demo", SaveDemo);
            ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "StarPawn Instrument Audit", AuditInstruments);
            hotkeyObject = new GameObject("StarPawn Desk"); UnityEngine.Object.DontDestroyOnLoad(hotkeyObject);
            var hotkey = hotkeyObject.AddComponent<StarPawnEntryPoint>(); hotkey.Pressed = Toggle;
            hotkey.Available = () => runReady && enabled && ContentReady && InPlayableStore();
            log?.Log("SP_ENABLED: F7 / desk button / native magnifier.");
        }
        private void ConfigureNativeItems(ModItemDirectory directory)
        {
            if (!ContentReady) InitializeContent(startupManifest ?? throw new InvalidOperationException("Missing startup manifest."));
            NativeBridge.RegisterDefinitions(Catalog.Definitions.Select(d => new NativeItemDefinition {
                DefinitionId = "starpawn." + d.Id, DisplayName = d.Name, VariantId = d.Id,
                SpriteKey = d.TextureKey.Substring(d.TextureKey.IndexOf(':') + 1),
                WidthCells = d.WidthCells, HeightCells = d.HeightCells,
                BaseValue = d.Material == "gold" ? ValuationV2.MetalValue(d.WeightMilligrams,d.DeclaredPurityBasisPoints,rules.GoldSellPerGram)+d.CraftValue :
                    d.Material == "silver" ? ValuationV2.MetalValue(d.WeightMilligrams,d.DeclaredPurityBasisPoints,rules.SilverSellPerGram)+d.CraftValue : d.BaseValue
            }));
            NativeEquipment.MeasureRequested = (tool, target) => {
                if (EnsureRun()) RunAction(generation, () => {
                    OpenNativeAsset(target);
                    UseInstrument(tool);
                    log?.Log("SP_NATIVE_MEASURE: tool=" + tool + ", target=" + target);
                });
            };
        }
        public void OnDisable()
        {
            if (!enabled) return; enabled = false;
            NativeEquipment.MeasureRequested = null;
            ModHook.OnModItemDirectoryInit -= ConfigureNativeItems;
            NativeBridge.NightlySettlementRequested -= SettleNight; StopBankCards(); StopNativeTrade(); NativeBridge.Disable();
            ModHook.OnGameLoadedInit -= BeginLoad; ModHook.OnGameLoadedLate -= FinishLoad; ModHook.OnNewGameStarted -= NewGame;
            ModHelper.RemoveDebugFunctions(ModId); CloseWindow();
            if (hotkeyObject != null) UnityEngine.Object.Destroy(hotkeyObject);
            hotkeyObject = null; state = null; currentStore = null; runReady = false;
        }
        private void BeginLoad()
        {
            ClearNativeTradeSession();
            runReady = false; busy = false; blocked = false; CloseWindow(); state = null; currentStore = null; currentSlot = -1;
            selectedAsset = selectedEncounter = ""; selectedView = "front";
            notice=""; ownedPage=offerPage=0;
        }
        private void FinishLoad() { runReady = true; LoadRun(); }
        private void NewGame(string scenario) { BeginLoad(); FinishLoad(); }
        private bool LoadRun()
        {
            if (!runReady || !PlayerStore.HasInstance || !ContentReady) return false;
            currentStore = PlayerStore.Instance; currentSlot = currentStore.saveSlotId;
            try
            {
                var raw = ModHelper.GetModData(ModId, SaveKey, null);
                if (string.IsNullOrEmpty(raw))
                {
                    state = new RunState();
                    var old = ModHelper.GetModData(ModId, "inspection-prototype-v1", null);
                    if (!string.IsNullOrEmpty(old))
                    {
                        var oldState = JsonConvert.DeserializeObject<InspectionState>(old);
                        if (oldState == null) throw new InvalidOperationException("Invalid legacy inspection state.");
                        InspectionService.Validate(oldState); state.Catalog.Experience = oldState.Experience;
                        notice = "已继承早期鉴定经验。旧测试记录保留，真实商品从星港来客进入。";
                    }
                }
                else
                {
                    var parsed = JObject.Parse(raw);
                    if (parsed["SchemaVersion"]?.Type != JTokenType.Integer || parsed["SchemaVersion"]!.Value<int>() != 2)
                        throw new InvalidOperationException("Unsupported run schema.");
                    state = JsonConvert.DeserializeObject<RunState>(raw) ?? throw new InvalidOperationException("Empty run state.");
                }
                Validate(state); blocked = false;
                if(HasCommerceRecovery()) { blocked=true; notice="上次实物业务需要核对，原记录已保留。"; return false; }
                ReconcileNativeTradeOnLoad();
                ValidatePhysicalAssets();
                foreach (var asset in state.Catalog.Assets.Where(a => a.Status != "consumed")) SyncNativeAppearance(asset);
                log?.Log("SP_LOADED: slot=" + currentSlot + ", assets=" + state.Catalog.Assets.Count + ", xp=" + state.Catalog.Experience); return true;
            }
            catch (Exception e) { blocked = true; state = null; notice = "星港记录需要核对，原数据已保留。"; log?.Error("SP_LOAD_BLOCKED: " + e); return false; }
        }
        private bool EnsureRun()
        {
            if (!enabled || !runReady || !InPlayableStore()) return false;
            if (!ReferenceEquals(currentStore,PlayerStore.Instance) || currentSlot != PlayerStore.Instance.saveSlotId) { CloseWindow(); return LoadRun(); }
            return !blocked && (state != null || LoadRun());
        }
        private static bool InPlayableStore() => PlayerStore.HasInstance && NewGameData.Instance != null && !NewGameData.Instance.isInMainMenu;
        private bool SameRun() => enabled && runReady && !blocked && state != null && InPlayableStore() && ReferenceEquals(currentStore,PlayerStore.Instance) && currentSlot == PlayerStore.Instance.saveSlotId;
        private void Validate(RunState value)
        {
            if (value == null || value.SchemaVersion != 2 || value.NextOperationSequence < 1 || value.Ledger == null || value.Ledger.Count > 256 || value.OfferAssetIds == null)
                throw new InvalidOperationException("Invalid unified state.");
            Catalog.Validate(value.Catalog); EconomyService.Validate(value.Economy,rules);
            foreach (var id in value.OfferAssetIds) Catalog.GetAsset(value.Catalog,id);
            foreach (var contract in value.Economy.PawnContracts.Where(p => p.Status == "Active"))
                if (Catalog.GetAsset(value.Catalog,contract.AssetId).Status != "escrow") throw new InvalidOperationException("Collateral state mismatch.");
        }
        private void Persist()
        {
            if (!SameRun()) throw new InvalidOperationException("Save context changed.");
            Validate(state!); var json = JsonConvert.SerializeObject(state); ModHelper.SetModData(ModId,SaveKey,json);
            if (ModHelper.GetModData(ModId,SaveKey,null) != json) throw new InvalidOperationException("Unified state readback failed.");
        }
        private RunState Clone() => JsonConvert.DeserializeObject<RunState>(JsonConvert.SerializeObject(state)) ?? throw new InvalidOperationException("State clone failed.");
        private int Day() => NativeBridge.GetGameDay();
        private int Cash() => NativeBridge.GetCash();
        private MarketSnapshot Market() => EconomyService.GetMarketSnapshot(state!.Economy,Day(),rules);
        private string OperationId(RunState next) => next.Catalog.RunId + ":" + next.NextOperationSequence;
        private void Record(RunState next,string operationId,string description,long cashDelta,string assetId="")
        {
            next.NextOperationSequence = checked(next.NextOperationSequence + 1);
            next.Ledger.Add(new RunReceipt { OperationId=operationId,Day=Day(),Description=description,CashDelta=cashDelta,AssetId=assetId });
            while (next.Ledger.Count > 256) { next.Ledger.RemoveAt(0); next.ArchivedReceipts++; }
        }
        private void Commit(RunState next,string operationId,string description,long cashDelta,string assetId="")
        {
            Record(next,operationId,description,cashDelta,assetId); Validate(next);
            if (cashDelta < -int.MaxValue || cashDelta > int.MaxValue) throw new InvalidOperationException("金额超过原版钱包范围。");
            var paid = NativeBridge.ApplyCashOnce(operationId,(int)cashDelta,Cash());
            if (!paid.Success) throw new InvalidOperationException(paid.Error);
            state = next; try { Persist(); } catch { blocked=true; throw; }
            log?.Log("SP_COMMIT: " + operationId + " " + description + " cash=" + paid.Before + "->" + paid.After + " asset=" + assetId);
        }
        private void SaveDemo()
        {
            if (!EnsureRun() || currentStore == null) return;
            if (currentSlot < 4) { log?.Warning("SP_SAVE_DEMO: isolated slots >=4 only."); return; }
            if(!NativeBridge.CanSaveNativeState()) {log?.Warning("SP_SAVE_DEMO_BLOCKED: pending transaction.");return;}
            Persist(); currentStore.SaveGame(); notice = "演示存档已保存。";
            log?.Log("SP_SAVE_DEMO: slot=" + currentSlot + ", day=" + Day());
        }
        private void SettleNight(int endedDay)
        {
            if (!EnsureRun() || state == null) return;
            try
            {
                var next = Clone(); var result = EconomyService.SettleDay(next.Economy,endedDay,rules);
                if (!result.Success) throw new InvalidOperationException(result.Error);
                if(result.Duplicate || next.Economy.LastClosedDay==state.Economy.LastClosedDay) return;
                var bankInterest=next.Economy.Bank.Balance-state.Economy.Bank.Balance;
                var creditInterest=next.Economy.Credit.AccruedInterest-state.Economy.Credit.AccruedInterest;
                var loanInterest=next.Economy.Loans.Sum(l=>l.AccruedInterest)-state.Economy.Loans.Sum(l=>l.AccruedInterest);
                Record(next,OperationId(next),"日结 · 第 "+endedDay+" 日：存息 +"+bankInterest+"；信用利息 +"+creditInterest+"；贷款利息 +"+loanInterest+"；到期抵押物 "+result.AssetTransitions.Count+" 件",0);
                CompleteNightSettlement(next,result.AssetTransitions);
                log?.Log("SP_DAY_CLOSE: day=" + endedDay + ", bank=" + state.Economy.Bank.Balance + ", collateral=" + result.AssetTransitions.Count);
            }
            catch (Exception e) { blocked=true; log?.Error("SP_SETTLEMENT_BLOCKED: " + e); }
        }
        private void Toggle() { if (window != null && window.IsVisible) CloseWindow(); else OpenHome(); }
        private void CloseWindow() { generation++; window?.Close(); window = null; }
        private PaperUI BeginPage(string title,string subtitle)
        {
            CloseWindow(); var ui=ModHelper.CreatePaper(ModId,"unified-desk","starpawn:panel",1200,800);
            ui.Data.designWidth=600; ui.Data.designHeight=400; ui.SetChrome(false,false,false,false).AddPage(title);
            Text(ui,22,14,555,27,title,18); Text(ui,22,44,555,21,subtitle,10); return ui;
        }
        private static void Text(PaperUI ui,float x,float y,float w,float h,string text,int font=11)
        { ui.AddText(x,y,w,h,TextResolver.LocalizeInline(text),"body","text-"+x+"-"+y,"left",font); ui.Last.raycast=false; }
        private void Button(PaperUI ui,float x,float y,float w,float h,string label,Action action,string? id=null)
        {
            var token=generation; id=id ?? "button-"+x+"-"+y;
            ui.AddButton(x,y,w,h,"",()=>RunAction(token,action),id);
            ui.Last.sprite="starpawn:button"; ui.Last.hoverSprite="starpawn:button_hover"; ui.Last.raycast=true;
            ui.AddText(x,y,w,h,TextResolver.LocalizeInline(label),"button",id+"-label","center",10); ui.Last.valign="middle"; ui.Last.color="#ffffff"; ui.Last.raycast=false;
        }
        private void ClearButton(PaperUI ui,float x,float y,float w,float h,Action action,string id)
        {
            var token=generation; ui.AddButton(x,y,w,h,"",()=>RunAction(token,action),id);
            ui.Last.sprite="starpawn:transparent"; ui.Last.hoverSprite="starpawn:transparent"; ui.Last.raycast=true;
        }
        private void Footer(PaperUI ui)
        {
            Text(ui,22,335,555,29,notice,10); Button(ui,22,369,104,22,"星港大厅",OpenHome); Button(ui,136,369,104,22,"我的货物",OpenOwned);
            Text(ui,250,370,210,21,"第 "+Day()+" 天   现金 "+Cash(),10); Button(ui,480,369,98,22,"收起",CloseWindow);
        }
        private void Show(PaperUI ui) { window=ui; ui.Show(); }
        private void RunAction(int token,Action action)
        {
            if (token!=generation || !SameRun() || busy) return; busy=true;
            try { action(); }
            catch(Exception e) { notice=e.Message; log?.Error("SP_ACTION: "+e); OpenHome(); }
            finally { busy=false; }
        }
        private void OpenHome()
        {
            if (!EnsureRun()) { log?.Warning("SP_DESK_UNAVAILABLE: load a save or inspect state error."); return; }
            var ui=BeginPage("星港 · 生意与见识","一件实物，一份经历。报价、合同与账本跟随当前存档。");
            Text(ui,25,80,340,62,"鉴定等级 "+InspectionV2.GetLevel(state!.Catalog)+" / "+InspectionV2.MaximumLevel+"\n经验 "+state.Catalog.Experience+"    已登记资产 "+state.Catalog.Assets.Count,14);
            var market=Market(); Text(ui,25,154,340,90,"今日金价 / 纯金克\n金商卖出 "+market.GoldSellPerGram+"    回收 "+market.GoldBuyPerGram+"\n白银卖出 "+market.SilverSellPerGram+"    回收 "+market.SilverBuyPerGram,13);
            Text(ui,25,260,340,60,"银行："+(state.Economy.Bank.Status=="Open"?"已开户 · 余额 "+state.Economy.Bank.Balance:"尚未开户")+"\n典当："+(state.Economy.PawnLicensed?"持证经营":"可申请经营许可"),12);
            Button(ui,25,306,340,22,"鉴定用品商店 · 仪器、手册与收纳",()=>OpenEquipmentShop(0));
            Button(ui,390,80,182,34,"星港来客 · 验货交易",OpenOffers); Button(ui,390,125,182,34,"我的实物 · 鉴定与加工",OpenOwned);
            Button(ui,390,170,182,34,"银行 · 储蓄与借贷",OpenBank); Button(ui,390,215,182,34,"证券与星港新闻",OpenMarkets);
            Button(ui,390,260,182,28,"典当合同",OpenPawnLedger); Button(ui,390,298,182,28,"资金总览与日结",OpenFinancialOverview); Footer(ui); Show(ui);
        }
        private void OpenLedger(int page)
        {
            var ui=BeginPage("星港 · 共享账本","最近业务记录；更早记录已汇总 "+state!.ArchivedReceipts+" 笔。");
            var rows=state.Ledger.AsEnumerable().Reverse().Skip(page*8).Take(8).ToList();
            for(var i=0;i<rows.Count;i++) {var r=rows[i]; Text(ui,24,76+i*29,550,26,"日 "+r.Day+"  "+r.Description+"  现金 "+(r.CashDelta>=0?"+":"")+r.CashDelta,11);}
            if(page>0) Button(ui,24,310,100,22,"上一页",()=>OpenLedger(page-1));
            if((page+1)*8<state.Ledger.Count) Button(ui,134,310,100,22,"下一页",()=>OpenLedger(page+1)); Footer(ui);Show(ui);
        }
    }
    public sealed class StarPawnEntryPoint : MonoBehaviour
    {
        public Action? Pressed;
        public Func<bool>? Available;
        private void Update() { if(Input.GetKeyDown(KeyCode.F7)) Pressed?.Invoke(); }
        private void OnGUI() { if(Available?.Invoke()==true && GUI.Button(new Rect(Screen.width-168,56,150,30),"星港业务  F7")) Pressed?.Invoke(); }
    }
}
