using System;
using System.Linq;
using System.Text;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        // All businesses use the same daily material snapshot, including scoped events.
        private AssetQuote QuoteByMarket(CatalogState catalog,string encounterId,string direction)
            => QuoteByMarket(catalog,encounterId,direction,Market());
        private AssetQuote QuoteByMarket(CatalogState catalog,string encounterId,string direction,MarketSnapshot m)
        {
            var q=ValuationV2.Quote(catalog,encounterId,Day(),m.Version,m.GoldSellPerGram,m.GoldBuyPerGram,m.SilverSellPerGram,m.SilverBuyPerGram,direction);
            var d=Catalog.GetDefinition(Catalog.GetAsset(catalog,q.AssetId).DefinitionId);
            var factor=d.Category.StartsWith("翡翠",StringComparison.Ordinal)?m.JadeValueBps:
                (d.Category=="主机"||d.Category=="掌机"||d.Category=="卡带"||d.Category=="光盘")?m.EntertainmentValueBps:10000;
            q.Price=checked((long)decimal.Floor(q.Price*(decimal)factor/10000));
            q.BaseValue=checked((long)decimal.Floor(q.BaseValue*(decimal)factor/10000));
            q.EvidenceDeduction=checked((long)decimal.Floor(q.EvidenceDeduction*(decimal)factor/10000));
            return q;
        }
        private void OpenNativeAsset(string assetId)
        {
            if(!EnsureRun()||state==null) return;
            notice = "";
            var item=NativeBridge.FindAsset(assetId);
            if(item==null) {notice="未找到这件实物。";OpenHome();return;}
            var a=state.Catalog.Assets.FirstOrDefault(x=>x.AssetId==assetId);
            if(a==null)
            {
                var next=Clone();
                a=Catalog.CreateAsset(next.Catalog,NativeBridge.GetVariantId(item),GeneralHelper.IsItemOwned(item)?"player":"native-customer");
                a.AssetId=assetId; a.NativeItemUniqueId=NativeBridge.GetNativeUniqueId(item);
                state=next;Persist();
            }
            OpenInspection(assetId,a.OwnerId=="player"?"workshop":NativeCounterpartyId(item));
        }
        private void OpenInspection(string assetId,string counterpartyId,string view="front")
        {
            if(!EnsureRun()||state==null) return;
            var a=Catalog.GetAsset(state.Catalog,assetId);
            if(a.Status=="consumed") {notice="这件原料已经加工消耗。";OpenOwned();return;}
            if(a.Status=="escrow") {notice="实物正在托管，先查看对应合同。";OpenPawnLedger();return;}
            var definition=Catalog.GetDefinition(a.DefinitionId);
            if(a.OwnerId=="player" && definition.ShopOnly) { OpenEquipment(assetId);return; }
            if(selectedAsset!=assetId)notice="";
            selectedAsset=assetId;selectedView=view;
            var existing=state.Catalog.Encounters.FirstOrDefault(x=>!x.Completed&&x.AssetId==assetId&&x.CounterpartyId==counterpartyId);
            var e=InspectionV2.Open(state.Catalog,assetId,existing?.EncounterId??counterpartyId+":"+assetId+":v"+a.StateVersion,counterpartyId,view);
            selectedEncounter=e.EncounterId;Persist();
            DrawInspection();
        }
        private void DrawInspection()
        {
            if(state==null) return;
            var a=Catalog.GetAsset(state.Catalog,selectedAsset);
            var e=InspectionV2.GetEncounter(state.Catalog,selectedEncounter);
            var d=Catalog.GetDefinition(a.DefinitionId);
            var facts=Catalog.PublicFacts(a);
            var q=QuoteByMarket(state.Catalog,e.EncounterId,a.OwnerId=="player"?"sell":"buy");
            var ui=BeginPage("验货台 · "+facts.Name,"鉴定等级 "+InspectionV2.GetLevel(state.Catalog)+" / "+InspectionV2.MaximumLevel+"    经验 "+state.Catalog.Experience+"    "+(InspectionV2.IsMastered(state.Catalog)?"老练的目光：可见异常自动记录":"观察实物，点击可见异常"));
            const float ix=49,iy=99,iw=298,ih=199;
            if(e.Locked)
            {
                Text(ui,45,135,305,90,a.OwnerId=="player"?"反复检查让眼睛有些疲劳。\n先交给检验所看看吧。":"顾客把物品收回身边。\n“今天不再检查了，谈谈价格吧。”",16);
            }
            else
            {
                ui.AddImage(ix,iy,iw,ih,Catalog.TextureFor(a,selectedView),"stretch","asset-image");ui.Last.raycast=false;
                ClearButton(ui,ix,iy,iw,ih,()=>{notice=InspectionNotice(a,InspectionV2.Miss(state.Catalog,selectedEncounter));Persist();DrawInspection();},"blank-hit");
                foreach(var h in Catalog.ActiveHotspots(a,selectedView))
                {
                    var x=ix+h.U*iw-h.Width*iw/2; var y=iy+h.V*ih-h.Height*ih/2;
                    var tolerance=InspectionV2.Rules.HitToleranceUv;
                    ClearButton(ui,x-tolerance*iw,y-tolerance*ih,(h.Width+2*tolerance)*iw,(h.Height+2*tolerance)*ih,()=>{notice=InspectionNotice(a,InspectionV2.Discover(state.Catalog,selectedEncounter,h.Id));Persist();DrawInspection();},"hit-"+h.Id);
                    if(a.DiscoveredEvidenceIds.Contains(h.Id))
                    {
                        var mw=(h.Width*iw+6)*InspectionV2.Rules.MarkerScale;var mh=(h.Height*ih+6)*InspectionV2.Rules.MarkerScale;
                        ui.AddImage(ix+h.U*iw-mw/2,iy+h.V*ih-mh/2,mw,mh,"starpawn:marker","stretch","marker-"+h.Id);ui.Last.raycast=false;
                    }
                }
            }
            Button(ui,22,74,174,22,"检测仪器",OpenInstruments);
            Button(ui,206,74,168,22,"查阅分类手册",()=>OpenHandbook(Appraisal.Category(d),0));
            var s=new StringBuilder();
            s.AppendLine(q.MaterialDescription);
            if(d.Material=="gold"||d.Material=="silver")
            {
                var m=Market(); s.AppendLine("卖出 / 回收 每克");
                s.AppendLine((d.Material=="gold"?m.GoldSellPerGram:m.SilverSellPerGram)+" / "+(d.Material=="gold"?m.GoldBuyPerGram:m.SilverBuyPerGram));
                s.AppendLine((q.RecoveryValueConfirmed?"核实材料回收":"标称回收估算")+" "+q.RecoveryValue);
            }
            s.AppendLine("当前报价 "+q.Price+" Cr");
            s.AppendLine("引用原因 "+q.AppliedCauses.Count+" 项");
            if(!string.IsNullOrEmpty(facts.JadeDescription)) s.AppendLine(facts.JadeDescription);
            Text(ui,388,77,190,116,s.ToString(),11);
            int index=0;
            foreach(var h in d.Hotspots.Where(h=>a.DiscoveredEvidenceIds.Contains(h.Id)).Take(4))
            {
                bool selected=e.SelectedEvidenceIds.Contains(h.Id);
                Button(ui,388,198+index++*25,190,22,(a.ResolvedCauseIds.Contains(h.CauseId)?"已修复 · ":selected?"✓ ":"○ ")+h.Label,()=>{InspectionV2.SelectEvidence(state.Catalog,selectedEncounter,h.Id,!selected);Persist();DrawInspection();},"evidence-"+h.Id);
            }
            if(index==0)Text(ui,388,201,190,49,"尚未记录异常。\n没有异常的物品可以直接议价。",10);
            Button(ui,22,308,106,23,"引用全部证据",()=>{InspectionV2.SelectAll(state.Catalog,selectedEncounter);Persist();DrawInspection();});
            if(d.IsJadeRough&&a.WindowOpened)
                Button(ui,136,308,106,23,selectedView=="front"?"查看窗口":"查看皮壳",()=>OpenInspection(a.AssetId,e.CounterpartyId,selectedView=="front"?"window":"front"));
            else if(a.OwnerId=="player") Button(ui,136,308,106,23,"加工 / 检验",()=>OpenServices(a.AssetId));
            else Button(ui,136,308,106,23,"返回来客",OpenOffers);
            Button(ui,250,308,124,23,"议价与交易",()=>OpenQuote(q));
            if (d.IsJadeRough && a.WindowOpened && a.OwnerId == "player")
                Button(ui,388,308,190,23,"加工 / 检验",()=>OpenServices(a.AssetId));
            else Button(ui,388,308,190,23,"资产档案",()=>OpenAssetRecord(a.AssetId));
            Footer(ui);Show(ui);
        }
        private static string InspectionNotice(AssetRecord asset,InspectionOutcome outcome)
        {
            if(asset.OwnerId!="player")return outcome.Message;
            if(outcome.Kind=="discovered")return "新的观察已记入这件实物的档案。";
            if(outcome.Kind=="repeated")return "这处已经记录，重新看它没有新发现。";
            return outcome.Locked?"眼睛有些疲劳，先交给检验所看看吧。":"这里没有发现异常，再看看其他细节。";
        }
        private void CertificateButton(PaperUI ui,AssetRecord asset,string scope,string label,float x,float y)
        {
            var current=asset.Certificates.Any(c=>c.Scope==scope&&c.AssetVersion==asset.StateVersion);
            Button(ui,x,y,174,27,current?"查看证书 · "+label:label+" "+InspectionV2.Rules.CertificateFee,()=>{
                if(current)OpenAssetRecord(asset.AssetId);
                else {ProcessAsset(asset.AssetId,scope);OpenServices(asset.AssetId);}
            });
        }
        private void OpenQuote(AssetQuote quote)
        {
            var a=Catalog.GetAsset(state!.Catalog,quote.AssetId);
            var ui=BeginPage("议价 · "+Catalog.PublicFacts(a).Name,"本次金额只对当前实物、顾客、证据和当日行情有效。" );
            Text(ui,24,80,550,72,(quote.Direction=="buy"?"购入这件商品":"卖给星港回收商")+"  "+quote.Price+" Cr\n材料："+quote.MaterialDescription+"\n引用 "+quote.AppliedCauses.Count+" 个独立原因",15);
            var precious=Catalog.GetDefinition(a.DefinitionId).Material=="gold"||Catalog.GetDefinition(a.DefinitionId).Material=="silver";
            Text(ui,24,168,550,50,"估值基准 "+quote.BaseValue+" · 已计证据差额 "+quote.EvidenceDeduction+(precious?"\n"+(quote.RecoveryValueConfirmed?"已核实材料回收值 ":"按标称估算回收值，未获材料检验确认 ")+quote.RecoveryValue:""),11);
            var native=NativeBridge.FindAsset(a.AssetId);
            if (native == null || a.OwnerId == "player")
                Button(ui,24,236,174,28,"确认"+(quote.Direction=="buy"?"付款 ":"出售 ")+quote.Price,()=>{if(quote.Direction=="buy")PurchaseAsset(quote);else SellAsset(quote);OpenOwned();});
            Button(ui,210,236,174,28,"继续查看证据",DrawInspection);
            if(native!=null&&PlayerStore.Instance.currentClientInstance!=null)
                Button(ui,396,236,178,28,"放到交易台并报价",()=>PlaceAndPrepareNativeTrade(a.AssetId));
            if(a.OwnerId!="player")
            {
                Button(ui,24,283,265,25,"按此估值办理顾客典当",()=>PreparePawn(a.AssetId,true));
                if(native==null) Button(ui,303,283,271,25,"请柜台卖家展示此物",()=>PresentNativeOffer(a.AssetId));
            }
            else Button(ui,24,283,265,25,"以此物抵押周转",()=>PreparePawn(a.AssetId,false));
            Footer(ui);Show(ui);
        }
        private void OpenPawnOffer(string assetId,bool lending,AssetQuote quote)
        {
            var principal=checked((long)decimal.Floor(quote.Price*(decimal)rules.PawnLoanToValueBps/10000));
            var ui=BeginPage(lending?"顾客典当 · 放款":"抵押周转 · 借款","典当使用这件实物的资产编号，托管期间占用库存位置。" );
            Text(ui,24,80,550,145,"估值 "+quote.Price+" · 本金 "+principal+" Cr\n期限 "+rules.DefaultPawnDays+" 个游戏日 · 日费率 "+(rules.PawnInterestBps/100m)+"%\n到期日仍可赎回，营业结束后未赎则转归贷款方。\n托管物不能出售、加工或重复抵押。",13);
            if(!state!.Economy.PawnLicensed)
                Button(ui,24,235,220,28,"申请典当经营许可 "+rules.PawnLicenseCost,()=>{ExecuteEconomy(new EconomyCommand{Kind=EconomyAction.AcquirePawnLicense});OpenPawnOffer(assetId,lending,quote);});
            else Button(ui,24,235,220,28,(lending?"确认放款 ":"确认借款 ")+principal,()=>{
                if(!ValuationV2.IsCurrent(state.Catalog,quote,Day(),Market().Version,quote.Direction))throw new InvalidOperationException("报价已变化，请重新议价。");
                ExecuteEconomy(new EconomyCommand{Kind=EconomyAction.CreatePawn,AssetId=assetId,Amount=principal,CollateralValue=quote.Price,TermDays=rules.DefaultPawnDays,PlayerIsLender=lending});OpenPawnLedger();});
            Button(ui,259,235,220,28,"返回议价",()=>OpenQuote(quote));Footer(ui);Show(ui);
        }
        private void OpenServices(string assetId)
        {
            var a=Catalog.GetAsset(state!.Catalog,assetId);var d=Catalog.GetDefinition(a.DefinitionId);
            var ui=BeginPage("工坊与检验所 · "+d.Name,"实物身份与历史保留；加工后需要重新报价。" );
            Text(ui,24,80,550,40,"已确认问题可申请修复；检验证书只证明列明的范围。",12);
            if(d.Hotspots.Any(h=>h.Repairable&&a.DiscoveredEvidenceIds.Contains(h.Id)&&!a.ResolvedCauseIds.Contains(h.CauseId)))
                Button(ui,24,132,174,27,"修复已确认问题 "+d.RepairCost,()=>{ProcessAsset(assetId,"repair");OpenServices(assetId);});
            else Text(ui,24,137,174,25,a.RepairHistory.Count>0?"已完成修复":"暂无需修复的问题",11);
            if(d.Material=="gold"||d.Material=="silver")CertificateButton(ui,a,"material","材质检验",210,132);
            CertificateButton(ui,a,"authenticity","真伪检验",396,132);
            if(!string.IsNullOrEmpty(d.EditionId))CertificateButton(ui,a,"edition","版本核验",24,177);
            if(d.IsBook)Button(ui,210,177,174,27,"翻开阅读",()=>{ProcessAsset(assetId,"read");OpenReading(assetId);});
            if(d.IsJadeRough)
            {
                Button(ui,24,222,174,27,a.WindowOpened?"查看开窗":"开窗 "+d.WindowCost,()=>{if(!a.WindowOpened)ProcessAsset(assetId,"window");OpenInspection(assetId,"workshop","window");});
                Button(ui,210,222,174,27,"切成挂件 "+d.CutCost,()=>{ProcessAsset(assetId,"cut_pendants");OpenOwned();});
                Button(ui,396,222,178,27,"磨制蛋面 "+d.CutCost,()=>{ProcessAsset(assetId,"cut_cabochon");OpenOwned();});
                if(!string.IsNullOrEmpty(d.JadeBangleOutput)) Button(ui,24,257,174,23,"取手镯 "+d.CutCost,()=>{ProcessAsset(assetId,"cut_bangle");OpenOwned();});
                if(!string.IsNullOrEmpty(d.JadeBeadsOutput)) Button(ui,210,257,174,23,"磨珠串 "+d.CutCost,()=>{ProcessAsset(assetId,"cut_beads");OpenOwned();});
            }
            Button(ui,24,287,174,27,"回到鉴定台",()=>OpenInspection(assetId,"workshop"));
            Button(ui,210,287,174,27,"资产档案",()=>OpenAssetRecord(assetId));Footer(ui);Show(ui);
        }
        private void OpenReading(string assetId)
        {
            var a=Catalog.GetAsset(state!.Catalog,assetId);var d=Catalog.GetDefinition(a.DefinitionId);
            if(notice==d.ReadingText)notice="";
            var ui=BeginPage(d.Name,"书页 · "+Catalog.PublicFacts(a).Edition);
            Text(ui,42,90,510,190,d.ReadingText,17);Button(ui,24,293,174,27,"合上书页",()=>OpenServices(assetId));Footer(ui);Show(ui);
        }
        private void OpenAssetRecord(string assetId)
        {
            var a=Catalog.GetAsset(state!.Catalog,assetId);var facts=Catalog.PublicFacts(a);
            var ui=BeginPage("资产档案 · "+facts.Name,"独立实物编号与作品 / 版本分开记录。" );
            var status = a.Status == "escrow" ? "托管中" : a.Status == "consumed" ? "已加工消耗" : "在册";
            var work = string.IsNullOrEmpty(facts.WorkId) ? "" : "\n作品 "+facts.WorkId+" · "+facts.Edition;
            Text(ui,24,76,550,93,"实物编号 "+a.AssetId+"\n状态 "+status+" · 修复 "+a.RepairHistory.Count+" 次 · 已发现 "+a.DiscoveredEvidenceIds.Count+" 处"+work+"\n"+facts.MaterialDescription,10);
            var y=180;
            foreach(var c in a.Certificates.AsEnumerable().Reverse().Take(3))
            {var scope=c.Scope=="material"?"材质检验":c.Scope=="authenticity"?"真伪检验":c.Scope=="edition"?"版本核验":c.Scope;
                Text(ui,24,y,550,34,c.Issuer+" · 第 "+c.IssuedDay+" 日 · "+scope+"\n"+c.Conclusion,10);y+=39;}
            if(!string.IsNullOrEmpty(a.ParentAssetId))Text(ui,24,305,550,23,"来源原石 "+a.ParentAssetId,9);
            Footer(ui);Show(ui);
        }
    }
}
