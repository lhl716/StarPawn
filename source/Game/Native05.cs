using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StarPawn.Core;
using UnityEngine;

// Revision 05 native integration. Everything here is scoped to StarPawn items and is
// written defensively: a native method that cannot be found is logged and skipped, never
// guessed at. Members named in docs/NATIVE_APPRAISAL_AUDIT_05.txt are called directly;
// anything the audit does not name is reached through reflection and reported.
namespace StarPawn.Game
{
    internal static class Native05
    {
        internal static ModLog? Log;
        internal static readonly List<string> Diagnostics = new List<string>();
        internal static void Diag(string line) { if (Diagnostics.Count < 200) Diagnostics.Add(line); Log?.Log(line); }

        // ------------------------------------------------------------ native career
        // One registration per StarPawn product group, alongside (never replacing) hand,
        // ribwich and plasma_fuel. Unique "starpawn_" IDs; Unregister removes only ours.
        internal static Func<GameItem, long>? CorrectValueProvider;
        internal static Func<string, GameItem?>? CreateCommissionItem;

        internal static readonly string[] Groups =
        {
            "gold_ring", "gold_bracelet", "gold_necklace", "gold_earrings", "gold_bar", "silver_ring", "silver_bracelet", "silver_necklace", "silver_earrings", "silver_bar",
            "jade_pendant", "jade_cabochon", "jade_bangle", "jade_beads", "jade_rough", "gem", "jewelry", "collectible", "electronics", "recovery"
        };

        internal static string GroupOf(ItemDefinition d)
        {
            if (d.ShopOnly || d.CommissionRank < 0) return "";
            var m = d.ArtModel ?? "";
            if (d.Role == "gem") return "gem";
            if (d.Role == "setting") return "jewelry";
            if (d.IsJadeRough) return "jade_rough";
            if (!string.IsNullOrEmpty(d.RecoveryClass) && d.RecoveryClass != "metal_goods" && d.RecoveryClass != "device") return "recovery";
            if (BusinessLabels.Has(d, BusinessLabels.Electronics)) return "electronics";
            foreach (var g in Groups) if (m.StartsWith(g, StringComparison.Ordinal) || m.StartsWith("legacy_" + g, StringComparison.Ordinal)) return g;
            if (m.StartsWith("jade_carving", StringComparison.Ordinal)) return "jade_pendant";
            return "collectible";
        }

        internal static int RegisterCareer(string modId)
        {
            var registered = 0;
            foreach (var group in Groups)
            {
                var members = Catalog.Definitions.Where(d => GroupOf(d) == group).OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
                if (members.Count == 0) continue;
                var rank = members.Min(d => d.CommissionRank);
                var guide = Appraisal.HandbookFor(members[0]);
                var ids = new HashSet<string>(members.Select(d => "starpawn." + d.Id), StringComparer.Ordinal);
                var key = "sp5.commission." + group;
                try
                {
                    ModHelper.AddAppraisalItem(modId, "starpawn_" + group, "starpawn." + members[0].Id, rank,
                        () => CreateCommissionItem?.Invoke(group)!,
                        item => (int)Math.Max(0, Math.Min(int.MaxValue, CorrectValueProvider?.Invoke(item) ?? 0)),
                        item => item != null && ids.Contains(item.identifier ?? ""),
                        "starpawn." + guide.Id,
                        () => TextResolver.Get(key, group));
                    registered++;
                }
                catch (Exception e) { Diag("SP5_CAREER_REGISTER_FAILED: " + group + " " + e.Message); }
            }
            Diag("SP5_CAREER: registered " + registered + " StarPawn commission types next to the native hand/ribwich/plasma_fuel types");
            return registered;
        }

        internal static void UnregisterCareer(string modId)
        {
            try { Diag("SP5_CAREER: removed " + ModHelper.RemoveAppraisalItems(modId) + " StarPawn commission types"); }
            catch (Exception e) { Diag("SP5_CAREER_REMOVE_FAILED: " + e.Message); }
        }

        internal static int NativeRank()
        {
            try { return Math.Max(0, Math.Min(3, ModHelper.GetAppraiserRank())); } catch { return 0; }
        }

        // ------------------------------------------------------------ discovery feedback
        // Plays EmporiumAudioPlayer.audioExposeScam only: no native expose/negotiation
        // handler is invoked, so no native discount or reward is applied a second time.
        internal static void PlayExposeSound()
        {
            try
            {
                var type = AccessTools.TypeByName("EmporiumAudioPlayer");
                if (type == null) { Diag("SP5_SOUND: EmporiumAudioPlayer not found"); return; }
                var player = FindInstance(type);
                var clipMember = (MemberInfo?)AccessTools.Field(type, "audioExposeScam") ?? AccessTools.Property(type, "audioExposeScam");
                object? clip = clipMember is FieldInfo f ? f.GetValue(f.IsStatic ? null : player) : clipMember is PropertyInfo p ? p.GetValue(player) : null;
                if (clip == null) { Diag("SP5_SOUND: audioExposeScam unavailable"); return; }
                // Prefer the player's own one-shot method for that clip type.
                var play = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.IndexOf("Play", StringComparison.OrdinalIgnoreCase) >= 0 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(clip));
                if (play != null && (play.IsStatic || player != null)) { play.Invoke(play.IsStatic ? null : player, new[] { clip }); return; }
                var audioSource = AccessTools.TypeByName("UnityEngine.AudioSource");
                audioSource?.GetMethod("PlayClipAtPoint", new[] { clip.GetType(), typeof(Vector3) })?.Invoke(null, new object[] { clip, Vector3.zero });
            }
            catch (Exception e) { Diag("SP5_SOUND_FAILED: " + e.Message); }
        }

        private static object? FindInstance(Type type)
        {
            foreach (var name in new[] { "Instance", "instance", "Current", "current" })
            {
                var f = AccessTools.Field(type, name); if (f != null && f.IsStatic) return f.GetValue(null);
                var p = AccessTools.Property(type, name); if (p != null && p.GetGetMethod(true)?.IsStatic == true) return p.GetValue(null);
            }
            return UnityEngine.Object.FindObjectOfType(type);
        }

        // ------------------------------------------------------------ inventory hit targets
        // Root cause addressed: StarPawn art is mostly transparent (rings, chains) and the
        // image layers were fitted with preserveAspect, so an alpha-tested raycast only hit
        // the painted pixels. Fix: our layers never alpha-test, and one transparent hit
        // plate per OCCUPIED cell sits under the item's own layer, so pointer events bubble
        // to the native item handler from anywhere in the footprint and nowhere else.
        private static readonly HashSet<string> Diagnosed = new HashSet<string>(StringComparer.Ordinal);

        internal static void FitHitTargets(GameItemElement element, int widthCells, int heightCells, float gridSize)
        {
            if (element?.background == null || element.background.image == null) return;
            var id = element.identifier ?? "";
            if (Diagnosed.Add(id))
                Diag("SP5_HIT_DIAG: " + id + " cells=" + widthCells + "x" + heightCells + " grid=" + gridSize + " alphaThreshold=" + element.background.image.alphaHitTestMinimumThreshold + " raycast=" + element.background.image.raycastTarget);
            foreach (var layer in new[] { element.background, element.fakeBackground })
                if (layer?.image != null) layer.image.alphaHitTestMinimumThreshold = 0f;
            var parent = (RectTransform)element.background.image.rectTransform;
            var existing = parent.Find("sp5-hit");
            var holder = existing != null ? existing.gameObject : null;
            if (holder == null)
            {
                holder = new GameObject("sp5-hit", typeof(RectTransform));
                holder.transform.SetParent(parent, false);
                holder.transform.SetAsFirstSibling();
            }
            var rt = (RectTransform)holder.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(widthCells * gridSize, heightCells * gridSize); rt.anchoredPosition = Vector2.zero;
            // Plates are laid out in the UNROTATED item frame; the parent rotator applies orientation/flip.
            var rects = FootprintGeometry.CellRects(FootprintGeometry.Filled(widthCells, heightCells), widthCells, heightCells, 0, false, gridSize);
            for (var i = 0; i < Math.Max(rects.Count, rt.childCount); i++)
            {
                Transform plate;
                if (i < rt.childCount) plate = rt.GetChild(i);
                else
                {
                    var go = new GameObject("cell" + i, typeof(RectTransform));
                    go.transform.SetParent(rt, false);
                    var img = go.AddComponent<UnityEngine.UI.Image>();
                    img.color = new Color(0, 0, 0, 0);
                    img.raycastTarget = true;
                    plate = go.transform;
                }
                plate.gameObject.SetActive(i < rects.Count);
                if (i >= rects.Count) continue;
                var r = (RectTransform)plate;
                r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
                r.sizeDelta = new Vector2(rects[i][2], rects[i][3]);
                r.anchoredPosition = new Vector2(rects[i][0], -rects[i][1]);
            }
        }

        // ------------------------------------------------------------ books through Read
        // Manuals become native document/book items (BookHelper.InitBookItem, audited), so the
        // native inventory Read action appears; the native read handler is routed to the
        // StarPawn reader for StarPawn books only. The magnifier is no longer the way in.
        internal static Action<GameItem, int>? OpenBook;

        internal static void InitBook(GameItem item, ItemDefinition d)
        {
            try { BookHelper.InitBookItem(item, "starpawn:" + d.Id); }
            catch (Exception e) { Diag("SP5_BOOK_INIT_FAILED: " + d.Id + " " + e.Message); }
        }

        internal static bool TryRouteRead(object?[] args)
        {
            var item = args?.OfType<GameItem>().FirstOrDefault();
            if (item == null || !NativeBridge.IsStarPawnAsset(item) || OpenBook == null) return false;
            if (!Catalog.TryGetDefinition(NativeBridge.GetVariantId(item), out var d) || !d.IsBook) return false;
            var page = args!.OfType<int>().FirstOrDefault();
            OpenBook(item, Math.Max(0, page));
            return true;
        }

        // ------------------------------------------------------------ native shape (for migration)
        internal static bool TryReadShape(GameItem item, out int width, out int height)
        {
            width = height = 0;
            try
            {
                foreach (var name in new[] { "shape", "modifiedShape", "gridShape", "Shape" })
                {
                    var member = (MemberInfo?)AccessTools.Field(item.GetType(), name) ?? AccessTools.Property(item.GetType(), name);
                    var shape = member is FieldInfo f ? f.GetValue(item) : member is PropertyInfo p ? p.GetValue(item) : null;
                    if (shape == null) continue;
                    int Read(params string[] names)
                    {
                        foreach (var n in names)
                        {
                            var m = (MemberInfo?)AccessTools.Field(shape.GetType(), n) ?? AccessTools.Property(shape.GetType(), n);
                            var v = m is FieldInfo ff ? ff.GetValue(shape) : m is PropertyInfo pp ? pp.GetValue(shape) : null;
                            if (v is int i) return i;
                        }
                        return 0;
                    }
                    width = Read("width", "Width", "sizeX"); height = Read("height", "Height", "sizeY");
                    if (width > 0 && height > 0) return true;
                }
            }
            catch (Exception e) { Diag("SP5_SHAPE_READ_FAILED: " + e.Message); }
            return false;
        }

        internal static void ApplyShape(GameItem item, int width, int height)
        {
            var cells = new byte[width * height];
            for (var i = 0; i < cells.Length; i++) cells[i] = 1;
            item.SetShape(new GridShapeBuilder().SetData(cells, width));
        }
    }

    // ---------------------------------------------------------------- Harmony routes (discovered, scoped)
    // Native commission settlement: StarPawn records grade/fee bookkeeping and the follow-up
    // intent AFTER the native handler ran. It never calls native settlement a second time.
    [HarmonyPatch]
    internal static class StarPawnEstimateObserver
    {
        internal static Action<GameItem, long>? Estimated;
        private static MethodBase? target;
        private static bool Prepare() { target = AccessTools.Method(AccessTools.TypeByName("AppraisalServiceHelper"), "OnEstimate"); if (target == null) Native05.Diag("SP5_ESTIMATE_HOOK: AppraisalServiceHelper.OnEstimate not found; follow-ups need the StarPawn desk"); return target != null; }
        private static MethodBase TargetMethod() => target!;
        private static void Postfix(object[] __args)
        {
            try
            {
                var item = __args.OfType<GameItem>().FirstOrDefault();
                if (item == null || !NativeBridge.IsStarPawnAsset(item)) return;
                var number = __args.Where(a => a is int || a is long || a is float).Select(Convert.ToInt64).DefaultIfEmpty(-1).First();
                Estimated?.Invoke(item, number);
            }
            catch (Exception e) { Native05.Diag("SP5_ESTIMATE_HOOK_FAILED: " + e); }
        }
    }

    // Native percentage fees were not balanced for jewelry prices. For StarPawn commissions
    // only, the paid fee is clamped to the transparent StarPawn cap after the native receipt.
    [HarmonyPatch]
    internal static class StarPawnCommissionFeeCap
    {
        internal static Func<GameItem, long>? CapFor;
        internal static Action<GameItem, int, int>? Adjust; // item, cash before, native fee paid
        private static MethodBase? target;
        private static bool Prepare() { target = AccessTools.Method(AccessTools.TypeByName("AppraisalServiceHelper"), "GiveAppraisalFee") ?? AccessTools.Method(AccessTools.TypeByName("AppraisalRankHelper"), "GiveAppraisalFee"); if (target == null) Native05.Diag("SP5_FEE_HOOK: GiveAppraisalFee not found; native fee left unchanged"); return target != null; }
        private static MethodBase TargetMethod() => target!;
        private static void Prefix(out int __state) { __state = PlayerStore.HasInstance ? PlayerStore.Instance.GetCash() : -1; }
        private static void Postfix(object[] __args, int __state)
        {
            try
            {
                var item = __args.OfType<GameItem>().FirstOrDefault();
                if (item == null || __state < 0 || !NativeBridge.IsStarPawnAsset(item)) return;
                var paid = PlayerStore.Instance.GetCash() - __state;
                Adjust?.Invoke(item, __state, paid);
            }
            catch (Exception e) { Native05.Diag("SP5_FEE_HOOK_FAILED: " + e); }
        }
    }

    // Native Read action -> StarPawn reader for StarPawn books only.
    [HarmonyPatch]
    internal static class StarPawnReadRoute
    {
        private static List<MethodBase> targets = new List<MethodBase>();
        private static bool Prepare()
        {
            targets = new List<MethodBase>();
            foreach (var typeName in new[] { "BookHelper", "InventoryActionHelper", "ItemActionHelper", "DocumentHelper", "PaperBookHelper" })
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;
                foreach (var m in AccessTools.GetDeclaredMethods(type))
                    if ((m.Name.StartsWith("Open", StringComparison.Ordinal) || m.Name.StartsWith("Read", StringComparison.Ordinal) || m.Name.StartsWith("Show", StringComparison.Ordinal))
                        && m.GetParameters().Any(p => p.ParameterType == typeof(GameItem)) && !m.IsGenericMethod && m.ReturnType == typeof(void))
                        targets.Add(m);
            }
            Native05.Diag("SP5_READ_ROUTE: " + (targets.Count == 0 ? "no native read handler found; books open from the StarPawn desk" : string.Join(", ", targets.Select(t => t.DeclaringType!.Name + "." + t.Name))));
            return targets.Count > 0;
        }
        private static IEnumerable<MethodBase> TargetMethods() => targets;
        private static bool Prefix(object[] __args) => !Native05.TryRouteRead(__args);
    }
}
