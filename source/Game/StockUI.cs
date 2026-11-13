using System;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private string offerFilter="all",ownedFilter="all";
        private static bool InStockFilter(ItemDefinition d,string filter)
            => filter=="all" || (filter=="metal" && (d.Material=="gold"||d.Material=="silver")) || (filter=="jade" && d.Category.StartsWith("翡翠",StringComparison.Ordinal)) || (filter=="other" && d.Material!="gold"&&d.Material!="silver"&&!d.Category.StartsWith("翡翠",StringComparison.Ordinal));
        private void StockFilters(PaperUI ui,bool owned)
        {
            var keys=new[]{"all","metal","jade","other"};var labels=new[]{"全部","金银首饰","翡翠赌石",owned?"收藏与用品":"其他收藏"};
            for(var i=0;i<keys.Length;i++)
            {
                var key=keys[i];var selected=(owned?ownedFilter:offerFilter)==key;
                Button(ui,24+i*140,73,130,22,(selected?"● ":"")+labels[i],()=>{if(owned){ownedFilter=key;ownedPage=0;OpenOwned();}else{offerFilter=key;offerPage=0;OpenOffers();}});
            }
        }
        private void OpenOffers()
        {
            if(!EnsureRun()||state==null) return;
            // Created only when the player visits the StarPawn market, never in vanilla RNG.
            // Upgrade an existing day's stock once when entirely new product families
            // are installed. Sold families remain in Catalog and do not refill on reopen.
            var newFamily=Catalog.Definitions.Where(d=>!d.ShopOnly).GroupBy(Appraisal.Family).Any(g=>!state.Catalog.Assets.Any(a=>Appraisal.Family(Catalog.GetDefinition(a.DefinitionId))==g.Key));
            if(state.OffersDay!=Day()||newFamily)
            {
                var next=Clone();
                next.OfferAssetIds.RemoveAll(id=>{var a=Catalog.GetAsset(next.Catalog,id);return a.OwnerId=="player"||a.Status!="active";});
                foreach(var family in Catalog.Definitions.Where(d=>!d.ShopOnly).GroupBy(Appraisal.Family))
                    if(!next.OfferAssetIds.Any(id=>Appraisal.Family(Catalog.GetDefinition(Catalog.GetAsset(next.Catalog,id).DefinitionId))==family.Key))
                    {
                        var d=Appraisal.ChooseProfile(next.Catalog.RunId,Day(),family);
                        var a=Catalog.CreateAsset(next.Catalog,d.Id,"visitor:"+Day()+":"+next.Catalog.NextAssetSequence);
                        next.OfferAssetIds.Add(a.AssetId);
                    }
                next.OffersDay=Day();state=next;Persist();
            }
            var available=state.OfferAssetIds.Select(id=>Catalog.GetAsset(state.Catalog,id)).Where(a=>a.OwnerId!="player"&&a.Status=="active"&&InStockFilter(Catalog.GetDefinition(a.DefinitionId),offerFilter)).ToList();
            offerPage=Math.Max(0,Math.Min(offerPage,Math.Max(0,(available.Count-1)/5)));
            var ui=BeginPage("星港来客 · 验货交易","来客带来的实物；先看货，再决定是否付款或放款。" );
            StockFilters(ui,false);
            int i=0;
            foreach(var a in available.Skip(offerPage*5).Take(5))
            {
                var d=Catalog.GetDefinition(a.DefinitionId);var y=103+i++*38;
                Text(ui,24,y,350,38,"来客 "+a.OwnerId.Split(':').Last()+" · "+d.Name+"\n"+d.Category+(d.WeightMilligrams>0?" · "+(d.WeightMilligrams/1000m)+" 克":""),11);
                Button(ui,420,y+5,154,27,"查看实物",()=>OpenInspection(a.AssetId,a.OwnerId));
            }
            Button(ui,24,304,110,24,"上一页",()=>{offerPage--;OpenOffers();});
            Button(ui,146,304,110,24,"下一页",()=>{offerPage++;OpenOffers();});
            Text(ui,278,307,260,22,"第 "+(offerPage+1)+" / "+Math.Max(1,(available.Count+4)/5)+" 页",11);Footer(ui);Show(ui);
        }
        private void OpenOwned()
        {
            if(!EnsureRun()||state==null) return;
            var assets=state.Catalog.Assets.Where(a=>a.OwnerId=="player"&&a.Status!="consumed"&&InStockFilter(Catalog.GetDefinition(a.DefinitionId),ownedFilter)).ToList();
            ownedPage=Math.Max(0,Math.Min(ownedPage,Math.Max(0,(assets.Count-1)/5)));
            var ui=BeginPage("我的实物 · 鉴定与加工","已购商品位于原版库存。也可使用原版放大镜打开鉴定。" );
            StockFilters(ui,true);
            if(assets.Count==0)Text(ui,24,90,540,85,"还没有星港商品。到来客市场看看，或接收当前客户带来的物品。",14);
            int i=0;
            foreach(var a in assets.Skip(ownedPage*5).Take(5))
            {
                var y=103+i++*38;var d=Catalog.GetDefinition(a.DefinitionId);
                Text(ui,24,y,370,38,d.Name+" · "+(a.Status=="escrow"?"托管中":"自有")+"\n"+d.Category+" · 证书 "+a.Certificates.Count+" · 已发现 "+a.DiscoveredEvidenceIds.Count,11);
                Button(ui,420,y+5,154,27,a.Status=="escrow"?"查看典当合同":d.ShopOnly?"使用 / 翻阅":"鉴定与议价",()=>{if(a.Status=="escrow")OpenPawnLedger();else OpenInspection(a.AssetId,"workshop");});
            }
            Button(ui,24,304,110,24,"上一页",()=>{ownedPage--;OpenOwned();});
            Button(ui,146,304,110,24,"下一页",()=>{ownedPage++;OpenOwned();});
            Button(ui,408,304,166,24,"来客市场",OpenOffers);Footer(ui);Show(ui);
        }
    }
}
