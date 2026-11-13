using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarPawn.Core;

namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private bool ContentReady { get; set; }

        // Startup-only static configuration. Never reads, creates or overwrites a player's save.
        private void InitializeContent(ModManifest manifest)
        {
            ContentReady = false;
            try
            {
                var modId = manifest.ID;
                var settings = ReadConfig<ContentSettings>(modId, "Content/settings.json", "SchemaVersion", 1);
                if (!Regex.IsMatch(settings.Language ?? "", "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8})?$")) throw new InvalidOperationException("Invalid localization language code.");
                if (settings.ExtensionFiles == null || settings.ExtensionFiles.Count > 32 || settings.ExtensionFiles.Distinct(StringComparer.Ordinal).Count() != settings.ExtensionFiles.Count || settings.ExtensionFiles.Any(name => name == null || !Regex.IsMatch(name, "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,80}\\.json$"))) throw new InvalidOperationException("Invalid explicit extension filename list.");
                var inspection = ReadConfig<InspectionRules>(modId, "Content/inspection-rules.json", "SchemaVersion", 1);
                ContentPacks.ValidateInspectionRules(inspection);
                var finance = ReadConfig<EconomyRules>(modId, "Content/finance-rules.json", "RulesVersion", 1);
                EconomyService.Validate(EconomyService.CreateNewState(), finance);
                var localization = ReadConfig<LocalizationPack>(modId, "Content/Localization/zh-CN.json", "SchemaVersion", 1);
                ContentPacks.ValidateLocalization(localization);
                if (localization.Language != "zh-CN") throw new InvalidOperationException("The base localization must declare zh-CN.");
                if (!string.Equals(settings.Language, "zh-CN", StringComparison.Ordinal))
                {
                    var overlay = ReadConfig<LocalizationPack>(modId, "Content/Localization/" + settings.Language + ".json", "SchemaVersion", 1);
                    ContentPacks.ValidateLocalization(overlay);
                    if (overlay.Language != settings.Language) throw new InvalidOperationException("Localization file language does not match settings.");
                    var entries = localization.Entries.ToDictionary(e => e.Key, e => e, StringComparer.Ordinal);
                    foreach (var replacement in overlay.Entries)
                    {
                        if (!entries.TryGetValue(replacement.Key, out var original)) throw new InvalidOperationException("Unknown translation override key: " + replacement.Key);
                        entries[replacement.Key] = new LocalizedText { Key = replacement.Key, Source = original.Source, Text = replacement.Text };
                    }
                    localization = new LocalizationPack { Language = settings.Language, Entries = entries.Values.ToList() };
                }
                var packs = new List<ContentPack> { ReadConfig<ContentPack>(modId, "Content/items.json", "SchemaVersion", 1) };
                foreach (var file in settings.ExtensionFiles.OrderBy(p => p, StringComparer.Ordinal)) packs.Add(ReadConfig<ContentPack>(modId, "Content/Extensions/" + file, "SchemaVersion", 1));
                bool HasTexture(string key)
                {
                    var filename = key.Substring(key.IndexOf(':') + 1) + ".png";
                    return ModHelper.ModFileExists(modId, "Textures/Paper/" + filename) && ModHelper.ModFileExists(modId, "Textures/Items/" + filename);
                }
                var definitions = ContentPacks.ValidateAndMerge(packs, inspection, localization, HasTexture);
                TextResolver.Configure(localization);
                foreach (var definition in definitions)
                {
                    definition.Name = TextResolver.Get(definition.NameKey, definition.Name);
                    definition.Description = TextResolver.Get(definition.DescriptionKey, definition.Description);
                    if (definition.IsBook) definition.ReadingText = TextResolver.Get(definition.ReadingKey, definition.ReadingText);
                    foreach (var hotspot in definition.Hotspots) { hotspot.Label = TextResolver.Get(hotspot.LabelKey, hotspot.Label); hotspot.Guidance = TextResolver.Get(hotspot.GuidanceKey,hotspot.Guidance); }
                }
                Catalog.Configure(definitions);
                InspectionV2.ConfigureRules(inspection);
                foreach (var field in typeof(EconomyRules).GetFields(BindingFlags.Public | BindingFlags.Instance)) field.SetValue(rules, field.GetValue(finance));
                ContentReady = true;
                log?.Log("SP_CONTENT_READY: definitions=" + definitions.Count + ", language=" + TextResolver.Language + ", mastery=" + InspectionV2.MasteryExperience);
            }
            catch (Exception error)
            {
                log?.Error("SP_CONFIG_BLOCKED: static configuration rejected; existing saves untouched. " + error);
                throw new InvalidOperationException("星港内容配置需要检查，现有存档保持原样。", error);
            }
        }

        private static T ReadConfig<T>(string modId, string path, string schemaField, int schema) where T : class
        {
                if (!ModHelper.ModFileExists(modId, path)) throw new InvalidOperationException("Missing configuration: " + path);
                var text = ModHelper.ReadModFileText(modId, path);
                if (string.IsNullOrEmpty(text) || text.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Empty or oversized configuration: " + path);
                var obj = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (obj[schemaField]?.Type != JTokenType.Integer || obj[schemaField]!.Value<int>() != schema) throw new InvalidOperationException("Unsupported configuration schema: " + path);
                var serializer = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, ObjectCreationHandling = ObjectCreationHandling.Replace });
                return obj.ToObject<T>(serializer) ?? throw new InvalidOperationException("Null configuration: " + path);
        }
    }
}
