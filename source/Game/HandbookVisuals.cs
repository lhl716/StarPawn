using System;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        // Native papers use a 1080-high reference canvas. At 1440p this displays
        // as 880 x 1232, matching the observed base-game document proportions.
        private PaperUI BeginHandbook(string title,bool introduction)
        {
            CloseWindow();
            var ui=ModHelper.CreatePaper(ModId,"appraisal-handbook",introduction?"starpawn:handbook_intro":"starpawn:handbook_page",660,924);
            ui.Data.designWidth=440;ui.Data.designHeight=616;
            ui.SetChrome(false,false,false,false).AddPage(title);
            ui.SetPosition(-545,0);
            Text(ui,28,24,332,16,"星港检验所 · 实物鉴定参考",10);ui.Last.color="#486071";
            Text(ui,28,44,332,28,title,20);ui.Last.align="center";
            return ui;
        }

        private void HandbookLink(PaperUI ui,float x,float y,float w,float h,string label,Action action)
        {
            ClearButton(ui,x,y,w,h,action,"handbook-"+x+"-"+y);
            Text(ui,x,y,w,h,label,11);ui.Last.color="#486071";ui.Last.align="center";
        }

        private static void HandbookSection(PaperUI ui,HandbookEntry entry,float y)
        {
            var sample=entry.Example;var h=entry.Evidence;
            Text(ui,20,y,400,20,sample.Name+" · "+h.Label,13);ui.Last.color="#486071";
            Text(ui,28,y+24,384,46,h.Guidance,10);
            var prefix="starpawn:manual_"+sample.Id+"_"+h.Id;
            ui.AddImage(40,y+76,160,104,prefix+"_reference","stretch","reference-"+y);ui.Last.raycast=false;
            ui.AddImage(240,y+76,160,104,prefix+"_problem","stretch","problem-"+y);ui.Last.raycast=false;
            // Whole-object location cue, then the matched detail pair with concrete captions.
            ui.AddImage(352,y,60,40,prefix+"_location","stretch","location-"+y);ui.Last.raycast=false;
            Text(ui,40,y+184,160,20,TextResolver.Get(h.LabelKey.Replace(".label",".ref_caption"),TextResolver.Get("sp5.caption.intact","Intact")),10);ui.Last.color="#497257";ui.Last.align="center";
            Text(ui,240,y+184,160,20,h.Label,10);ui.Last.color="#984b45";ui.Last.align="center";
        }
    }
}
