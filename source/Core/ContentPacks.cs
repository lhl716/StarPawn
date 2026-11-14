using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StarPawn.Core
{
    public sealed class InspectionRules
    {
        public int SchemaVersion = 1;
        public List<int> LevelThresholds = new List<int> { 0, 20, 40, 60, 80, 100 };
        public int ExperiencePerDiscovery = 10;
        public int ConsecutiveMissLimit = 2;
        public float HitToleranceUv;
        public float MarkerScale = 1;
        public int MaximumHotspotsPerDefinition = 32;
        public int ResalePriceBasisPoints = 8500;
        public long CertificateFee = 25;
    }

    public sealed class ContentPack
    {
        public int SchemaVersion = 1;
        public string PackId = "starpawn.base";
        public List<ItemDefinition> Items = new List<ItemDefinition>();
    }

    public sealed class ContentSettings
    {
        public int SchemaVersion = 1;
        public string Language = "zh-CN";
        public List<string> ExtensionFiles = new List<string>();
    }

    public sealed class LocalizedText
    {
        public string Key = "";
        public string Source = "";
        public string Text = "";
    }

    public sealed class LocalizationPack
    {
        public int SchemaVersion = 1;
        public string Language = "zh-CN";
        public List<LocalizedText> Entries = new List<LocalizedText>();
    }

    public static class ContentPacks
    {
        public static void ValidateInspectionRules(InspectionRules rules)
        {
            if (rules == null || rules.SchemaVersion != 1 || rules.LevelThresholds == null || rules.LevelThresholds.Count < 2 || rules.LevelThresholds.Count > 20 || rules.LevelThresholds[0] != 0 || rules.LevelThresholds.Last() > 1000000 || rules.ExperiencePerDiscovery < 0 || rules.ExperiencePerDiscovery > 10000 || rules.ConsecutiveMissLimit != 2 || float.IsNaN(rules.HitToleranceUv) || rules.HitToleranceUv < 0 || rules.HitToleranceUv > .02f || float.IsNaN(rules.MarkerScale) || rules.MarkerScale < .5f || rules.MarkerScale > 1.5f || rules.MaximumHotspotsPerDefinition < 1 || rules.MaximumHotspotsPerDefinition > 128 || rules.ResalePriceBasisPoints < 1 || rules.ResalePriceBasisPoints > 10000 || rules.CertificateFee < 0 || rules.CertificateFee > 1000000)
                throw new InvalidOperationException("Invalid inspection rules. The two consecutive miss rule is mandatory.");
            for (var i = 1; i < rules.LevelThresholds.Count; i++) if (rules.LevelThresholds[i] <= rules.LevelThresholds[i - 1]) throw new InvalidOperationException("Level thresholds must strictly increase.");
        }

        public static List<ItemDefinition> ValidateAndMerge(IEnumerable<ContentPack> packs, InspectionRules rules, LocalizationPack localization, Func<string, bool>? textureExists = null)
        {
            ValidateInspectionRules(rules);
            ValidateLocalization(localization);
            var keys = new HashSet<string>(localization.Entries.Select(e => e.Key), StringComparer.Ordinal);
            var packIds = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<ItemDefinition>();
            foreach (var pack in packs)
            {
                if (pack == null || pack.SchemaVersion != 1 || string.IsNullOrWhiteSpace(pack.PackId) || !packIds.Add(pack.PackId) || pack.Items == null || pack.Items.Count == 0) throw new InvalidOperationException("Invalid, empty or duplicate content pack.");
                foreach (var item in pack.Items)
                {
                    Catalog.ValidateDefinition(item);
                    if (!itemIds.Add(item.Id) || item.Hotspots.Count > rules.MaximumHotspotsPerDefinition) throw new InvalidOperationException("Duplicate definition or hotspot limit exceeded.");
                    RequireKey(keys, item.NameKey); RequireKey(keys, item.DescriptionKey);
                    if (item.IsBook) RequireKey(keys, item.ReadingKey);
                    foreach (var hotspot in item.Hotspots) { RequireKey(keys, hotspot.LabelKey); if (!string.IsNullOrEmpty(hotspot.TemplateId)) RequireKey(keys, hotspot.GuidanceKey); }
                    foreach (var texture in new[] { item.TextureKey, item.WindowTextureKey, item.RepairedTextureKey }.Where(k => !string.IsNullOrEmpty(k)))
                    {
                        if (!Regex.IsMatch(texture, "^starpawn:[a-z0-9_]+$", RegexOptions.CultureInvariant) || (textureExists != null && !textureExists(texture))) throw new InvalidOperationException("Missing or invalid local texture: " + texture);
                    }
                    result.Add(item);
                }
            }
            if (result.Count == 0 || result.Count > 1024 || !itemIds.Contains("jade_pendant")) throw new InvalidOperationException("Content catalog is empty, oversized, or missing jade processing output.");
            Appraisal.ValidateTemplates(result);
            return result;
        }

        public static void ValidateLocalization(LocalizationPack pack)
        {
            if (pack == null || pack.SchemaVersion != 1 || string.IsNullOrWhiteSpace(pack.Language) || pack.Entries == null || pack.Entries.Count > 8192) throw new InvalidOperationException("Invalid localization pack.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in pack.Entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 180 || !keys.Add(entry.Key) || entry.Text == null || entry.Text.Length > 16000 || entry.Source == null || entry.Source.Length > 16000) throw new InvalidOperationException("Missing, oversized or duplicate localization key.");
        }

        public static LocalizationPack CreateDefaultLocalization(IEnumerable<ItemDefinition> definitions)
        {
            var pack = new LocalizationPack();
            foreach (var item in definitions)
            {
                pack.Entries.Add(new LocalizedText { Key = item.NameKey, Source = item.Name, Text = item.Name });
                pack.Entries.Add(new LocalizedText { Key = item.DescriptionKey, Source = item.Description, Text = item.Description });
                if (item.IsBook) pack.Entries.Add(new LocalizedText { Key = item.ReadingKey, Source = item.ReadingText, Text = item.ReadingText });
                foreach (var hotspot in item.Hotspots) pack.Entries.Add(new LocalizedText { Key = hotspot.LabelKey, Source = hotspot.Label, Text = hotspot.Label });
            }
            return pack;
        }

        private static void RequireKey(HashSet<string> keys, string key)
        { if (string.IsNullOrWhiteSpace(key) || !keys.Contains(key)) throw new InvalidOperationException("Missing content localization key: " + key); }
    }

    public static class TextResolver
    {
        private static Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Regex? inlinePattern;
        public static string Language { get; private set; } = "zh-CN";

        public static void Configure(LocalizationPack pack)
        {
            ContentPacks.ValidateLocalization(pack);
            var nextValues = pack.Entries.ToDictionary(e => e.Key, e => e.Text, StringComparer.Ordinal);
            var nextReplacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in pack.Entries.Where(e => e.Source.Length > 0 && e.Source != e.Text))
            {
                if (nextReplacements.TryGetValue(entry.Source, out var existing) && existing != entry.Text) throw new InvalidOperationException("Conflicting translations for one inline source.");
                nextReplacements[entry.Source] = entry.Text;
            }
            var nextPattern = nextReplacements.Count == 0 ? null : new Regex(string.Join("|", nextReplacements.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)), RegexOptions.CultureInvariant);
            values = nextValues; replacements = nextReplacements; inlinePattern = nextPattern; Language = pack.Language;
        }

        public static string Get(string key, string fallback = "") => values.TryGetValue(key, out var translated) ? translated : fallback;
        public static string LocalizeInline(string value) => inlinePattern == null || string.IsNullOrEmpty(value) ? value : inlinePattern.Replace(value, match => replacements[match.Value]);
    }
}
