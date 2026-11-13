using System;
using System.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        // Explicit developer-menu action for the separately cloned art-QA slot.
        // No normal game entry point calls this, and original slots are refused.
        private void PrepareArtQa()
        {
            if(!EnsureRun() || state==null || currentSlot!=6)
            {log?.Warning("SP_ART_QA_REFUSED: this fixture is restricted to isolated slot 6.");return;}
            if(state.Ledger.Any(r=>r.Description=="ART_QA_FIXTURE_04"))
            {OpenArtQa();return;}
            RequireCommerceReady();
            var next=Clone();next.Catalog.Experience=0;
            var owned=new[]{"sp3_gold_ring_0_p7","sp3_silver_necklace_0_p7","sp3_rough_yellow_p3"}
                .Select(id=>Catalog.CreateAsset(next.Catalog,id)).ToList();
            var visitor=Catalog.CreateAsset(next.Catalog,"sp3_gold_ring_1_p2","art-qa-visitor");
            next.OfferAssetIds.Add(visitor.AssetId);
            RunCommerce(next,OperationId(next),"ART_QA_FIXTURE_04",0,owned[0].AssetId,
                changes=>{foreach(var asset in owned)StageSpawn(next,asset,changes);});
            SaveDemo();OpenArtQa();
        }

        private void OpenArtQa()
        {
            if(!EnsureRun() || state==null || currentSlot!=6)return;
            var ui=BeginPage("美术与点击测试","独立演示档 · 三件实物与一位专用测试来客。");
            var ids=new[]{"sp3_gold_ring_0_p7","sp3_silver_necklace_0_p7","sp3_rough_yellow_p3"};
            for(var i=0;i<ids.Length;i++)
            {
                var asset=state.Catalog.Assets.LastOrDefault(a=>a.DefinitionId==ids[i]&&a.OwnerId=="player"&&a.Status=="active");
                if(asset!=null)Button(ui,30,85+i*48,540,34,Catalog.GetDefinition(asset.DefinitionId).Name,
                    ()=>OpenInspection(asset.AssetId,"art-qa-workshop"));
            }
            var visitor=state.Catalog.Assets.LastOrDefault(a=>a.OwnerId=="art-qa-visitor");
            if(visitor!=null)Button(ui,30,240,540,34,"来客 · 两次失误与退出后重开",()=>OpenInspection(visitor.AssetId,"art-qa-visitor"));
            Button(ui,30,288,250,28,"保存演示进度",SaveDemo);
            Footer(ui);Show(ui);
        }
    }
}
