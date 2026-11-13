using HarmonyLib;
using UnityEngine;

namespace StarPawn.Game
{
    // Inventory rendering uses native texture pixels, while the inspection desk uses
    // high-resolution artwork. Fit only our image layers to their allocated grid cells.
    // This changes UI layout, never the asset texture, hitbox, ownership, or cash.
    internal static class NativeVisuals
    {
        internal static void ConfigureTextures()
        {
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID=="starpawn");
            if(mod==null)return;
            foreach(var texture in mod.Textures.Values)
            {
                texture.filterMode=FilterMode.Point;
                texture.wrapMode=TextureWrapMode.Clamp;
            }
            foreach(var sprite in mod.PaperSprites.Values)
                if(sprite!=null && sprite.texture!=null)
                {
                    sprite.texture.filterMode=FilterMode.Point;
                    sprite.texture.wrapMode=TextureWrapMode.Clamp;
                }
        }

        internal static void Fit(GameItemElement item)
        {
            if (item == null || !item.useSpriteFromMod || item.spriteAtlasPath != "starpawn" || UISettings.current == null) return;
            var card = item.identifier == "starpawn.savings_card" || item.identifier == "starpawn.credit_card";
            var widthCells = card ? 2 : 1; var heightCells = 1;
            if(!card && item.identifier.StartsWith("starpawn.",System.StringComparison.Ordinal))
            {
                if(StarPawn.Core.Catalog.TryGetDefinition(item.identifier.Substring(9),out var d)){widthCells=d.WidthCells;heightCells=d.HeightCells;}
            }
            var width = UISettings.current.gridSize * (float)widthCells;
            var height = UISettings.current.gridSize * (float)heightCells;
            FitLayer(item.background, width, height);
            FitLayer(item.fakeBackground, width, height);
        }

        private static void FitLayer(TreeNodeRender layer, float width, float height)
        {
            if (layer == null || layer.image == null || layer.image.sprite == null) return;
            layer.image.type = UnityEngine.UI.Image.Type.Simple;
            layer.image.preserveAspect = true;
            layer.rectTransform.sizeDelta = new Vector2(width, height);
        }
    }

    [HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked")]
    internal static class FitStarPawnItemVisual
    {
        private static void Postfix(GameItemElement __instance) => NativeVisuals.Fit(__instance);
    }

    [HarmonyPatch(typeof(GameItemElement), "ValidatePartialSprite")]
    internal static class FitStarPawnPartialVisual
    {
        private static void Postfix(GameItemElement __instance) => NativeVisuals.Fit(__instance);
    }
}
