using System;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private void AuditInstruments()
        {
            if (!EnsureRun() || state == null) return;
            foreach (var a in state.Catalog.Assets.Where(a => a.OwnerId == "player" && a.Status == "active"))
            {
                var d = Catalog.GetDefinition(a.DefinitionId);
                if (string.IsNullOrEmpty(d.InstrumentKind)) continue;
                var tool = NativeBridge.FindAsset(a.AssetId);
                if (tool == null) continue;
                foreach (var targetAsset in state.Catalog.Assets.Where(t => t.OwnerId == "player" && t.Status == "active" && Appraisal.CanMeasure(Catalog.GetDefinition(t.DefinitionId), d.InstrumentKind)).Take(3))
                {
                    var target = NativeBridge.FindAsset(targetAsset.AssetId);
                    if (target != null) log?.Log("SP_INSTRUMENT_AUDIT: " + d.Id + " -> " + targetAsset.DefinitionId + ", may=" + tool.MayTarget(target) + ", can=" + tool.CanTarget(target) + ", callback=" + (tool.onThisTargetItemFunc != null));
                }
            }
        }

        private void OpenEquipmentShop(int page)
        {
            if (!EnsureRun() || state == null) return;
            var goods = Catalog.Definitions.Where(d => d.ShopOnly).OrderBy(d => d.Id).ToList();
            page = Math.Max(0, Math.Min(page, Math.Max(0, (goods.Count - 1) / 5)));
            var ui = BeginPage("星港鉴定用品", "仪器、分类图鉴与八格手册收纳；购买后交付到店。");
            var row = 0;
            foreach (var d in goods.Skip(page * 5).Take(5))
            {
                var y = 74 + row++ * 44;
                var count = state.Catalog.Assets.Count(a => a.DefinitionId == d.Id && a.OwnerId == "player" && a.Status == "active");
                Text(ui,24,y,375,39,d.Name + " · " + d.BaseValue + " Cr" + (count > 0 ? " · 持有 " + count : "") + "\n" + d.Description,10);
                Button(ui,420,y+5,154,27,"购买",()=>{BuyEquipment(d.Id);OpenEquipmentShop(page);});
            }
            if (page > 0) Button(ui,24,306,110,24,"上一页",()=>OpenEquipmentShop(page-1));
            if ((page+1)*5 < goods.Count) Button(ui,146,306,110,24,"下一页",()=>OpenEquipmentShop(page+1));
            Text(ui,278,307,260,22,"第 "+(page+1)+" / "+Math.Max(1,(goods.Count+4)/5)+" 页",11);
            Footer(ui); Show(ui);
        }

        private void BuyEquipment(string definitionId)
        {
            RequireCommerceReady();
            var d = Catalog.GetDefinition(definitionId);
            if (!d.ShopOnly || d.BaseValue <= 0) throw new InvalidOperationException("此商品不在用品商店销售。");
            var next = Clone(); var asset = Catalog.CreateAsset(next.Catalog,d.Id);
            RunCommerce(next,OperationId(next),"购入用品 · "+d.Name,-d.BaseValue,asset.AssetId,changes=>StageSpawn(next,asset,changes));
            notice = d.Name+"已放入库存。";
        }

        private void OpenEquipment(string assetId)
        {
            var asset = Catalog.GetAsset(state!.Catalog,assetId); var d = Catalog.GetDefinition(asset.DefinitionId);
            if (d.IsOrganizer) { OpenOrganizer(assetId,0); return; }
            if (!string.IsNullOrEmpty(d.ManualFor)) { OpenHandbook(d.ManualFor,0); return; }
            var ui=BeginPage(d.Name,"检测仪器 · 右键实物选择“使用”，再点击待检物品。");
            ui.AddImage(24,80,245,163,d.TextureKey,"stretch","equipment"); ui.Last.raycast=false;
            Text(ui,290,88,275,125,d.Description+"\n\n进入待检物品的验货台，选择“检测仪器”。重复测量不重复获得经验，也不刷新顾客耐心。",13);
            Button(ui,24,292,174,27,"查看我的货物",OpenOwned);
            Button(ui,212,292,174,27,"来客验货",OpenOffers);
            Footer(ui);Show(ui);
        }

        private void OpenInstruments()
        {
            var asset=Catalog.GetAsset(state!.Catalog,selectedAsset);var d=Catalog.GetDefinition(asset.DefinitionId);
            var ui=BeginPage("检测台 · "+d.Name,"读数保留在实物档案；仪器不能恢复已耗尽的顾客耐心。");
            var tools=state.Catalog.Assets.Where(a=>a.OwnerId=="player"&&a.Status=="active"&&!string.IsNullOrEmpty(Catalog.GetDefinition(a.DefinitionId).InstrumentKind)).GroupBy(a=>Catalog.GetDefinition(a.DefinitionId).InstrumentKind).Select(g=>g.First()).ToList();
            var row=0;
            foreach(var kind in Appraisal.InstrumentKinds)
            {
                var tool=tools.FirstOrDefault(a=>Catalog.GetDefinition(a.DefinitionId).InstrumentKind==kind);
                var y=75+row++*43;
                Text(ui,24,y,370,39,Appraisal.InstrumentName(kind)+"\n"+(asset.Measurements.Contains(kind)?Appraisal.Reading(asset,kind):Appraisal.CanMeasure(d,kind)?"尚未检测":"不适用于这件物品"),10);
                if(tool!=null&&Appraisal.CanMeasure(d,kind))Button(ui,420,y+5,154,27,"检测",()=>UseInstrument(tool.AssetId));
                else if(tool==null)Button(ui,420,y+5,154,27,"购买仪器",()=>OpenEquipmentShop(0));
            }
            Button(ui,24,306,174,24,"回到验货台",DrawInspection);Footer(ui);Show(ui);
        }

        private void UseInstrument(string toolAssetId)
        {
            RequireCommerceReady();
            RequireNativeOwned(Catalog.GetAsset(state!.Catalog,toolAssetId));
            var next=Clone();var result=Appraisal.Measure(next.Catalog,selectedEncounter,toolAssetId);
            if(result.Changed) { state=next;Persist(); }
            notice=result.Message;OpenInstruments();
        }

        private void OpenHandbook(string category,int page)
        {
            var manual=state!.Catalog.Assets.FirstOrDefault(a=>a.OwnerId=="player"&&a.Status=="active"&&Catalog.GetDefinition(a.DefinitionId).ManualFor==category);
            if(manual==null)
            {
                var ui=BeginPage("分类鉴定图鉴","手册是可购买和收纳的实体书。");
                Text(ui,30,100,530,110,"书架上还没有这一类鉴定手册。\n星港用品店提供对应图鉴，记录固定缺陷的外观、检测方法和议价依据。",16);
                Button(ui,30,250,240,30,"前往鉴定用品商店",()=>OpenEquipmentShop(0));Footer(ui);Show(ui);return;
            }
            RequireNativeOwned(manual);
            var d=Catalog.GetDefinition(manual.DefinitionId);
            var entries=Appraisal.Handbook(category);
            var lastPage=(entries.Count+1)/2;
            page=Math.Max(0,Math.Min(page,lastPage));
            var book=BeginHandbook(d.Name);
            if(page==0)
            {
                Text(book,28,88,384,152,d.ReadingText,13);
                Text(book,28,256,384,28,"查验图例",15);book.Last.color="#486071";
                Text(book,28,295,384,124,"左栏为参考外观，右栏为同一部位的异常实例。\n\n先比较结构与表面，再使用合适的仪器。示例只说明一种特征，不代表正在交易的实物已经完成鉴定。",13);
                Text(book,28,446,384,76,"图鉴收录 "+entries.Count+" 项固定特征。\n每页最多两项，翻页查看对照。",13);
            }
            else
                foreach(var pair in entries.Skip((page-1)*2).Take(2).Select((entry,index)=>new {entry,index}))
                    HandbookSection(book,pair.entry,140+pair.index*220);
            if(page>0)HandbookLink(book,28,580,80,20,"上一页",()=>OpenHandbook(category,page-1));
            if(page<lastPage)HandbookLink(book,332,580,80,20,"下一页",()=>OpenHandbook(category,page+1));
            Text(book,156,580,128,20,(page+1)+" / "+(lastPage+1),11);book.Last.align="center";
            ClearButton(book,410,8,20,20,CloseWindow,"handbook-close");
            if(!string.IsNullOrEmpty(selectedEncounter)&&state.Catalog.Encounters.Any(e=>e.EncounterId==selectedEncounter&&!e.Completed))
                HandbookLink(book,276,page==0?548:108,136,20,"返回当前验货",DrawInspection);
            Show(book);
        }

        private void OpenOrganizer(string assetId,int page)
        {
            var asset=Catalog.GetAsset(state!.Catalog,assetId);
            var item=RequireNativeOwned(asset);
            var inventory=NativeEquipment.Inventory(item);
            var manuals=state.Catalog.Assets.Where(a=>a.OwnerId=="player"&&a.Status=="active"&&(!string.IsNullOrEmpty(Catalog.GetDefinition(a.DefinitionId).ManualFor)||a.DefinitionId=="inspection_guide")).ToList();
            page=Math.Max(0,Math.Min(page,Math.Max(0,(manuals.Count-1)/5)));
            var ui=BeginPage("鉴定手册收纳 · "+inventory.childItems.Count+" / 8","八格书匣，收好常用图鉴。搬动或出售前先取出书册。");
            if(manuals.Count==0)Text(ui,24,90,550,90,"还没有可收纳的鉴定手册。先去用品店购买分类图鉴。",15);
            var row=0;
            foreach(var manual in manuals.Skip(page*5).Take(5))
            {
                var native=RequireNativeOwned(manual);var stored=ReferenceEquals(native.parentInventory,inventory);var y=74+row++*44;
                var d=Catalog.GetDefinition(manual.DefinitionId);
                Text(ui,24,y,315,38,d.Name+"\n"+(stored?"已在此收纳中":"在其他库存位置"),11);
                Button(ui,346,y+5,105,27,"翻阅",()=>{if(string.IsNullOrEmpty(d.ManualFor))OpenReading(manual.AssetId);else OpenHandbook(d.ManualFor,0);});
                Button(ui,463,y+5,111,27,stored?"取出":"放入",()=>{
                    RequireCommerceReady();
                    if(PlayerStore.Instance.currentClientInstance!=null&&ReferenceEquals(PlayerStore.Instance.GetCurrentlyNegociatedItem(),native))throw new InvalidOperationException("先从交易台收回这本手册。");
                    var target=stored?EmporiumEntry.Instance.invElement:inventory;
                    if(!NativeBridge.TryMove(native,target,out var error))throw new InvalidOperationException(error);
                    ValidatePhysicalAssets();Persist();notice=stored?"原手册已取回库存。":"原手册已放入收纳。";OpenOrganizer(assetId,page);
                });
            }
            if(page>0)Button(ui,24,306,110,24,"上一页",()=>OpenOrganizer(assetId,page-1));
            if((page+1)*5<manuals.Count)Button(ui,146,306,110,24,"下一页",()=>OpenOrganizer(assetId,page+1));
            Button(ui,408,306,166,24,"打开书匣",()=>{CloseWindow();item.contentWindow.Show().Center();});Footer(ui);Show(ui);
        }
    }
}
