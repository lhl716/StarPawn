using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using StarPawn.Core;

public sealed class ModManifest { public string ID = "starpawn"; }
public static class ModHelper
{
    public static string Folder = "";
    private static string Resolve(string id, string path)
    {
        if (id != "starpawn") throw new Exception("Foreign mod folder");
        var root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(root, path));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new Exception("Escaped mod folder");
        return absolute;
    }
    public static bool ModFileExists(string id, string path) => File.Exists(Resolve(id, path));
    public static string ReadModFileText(string id, string path) => File.ReadAllText(Resolve(id, path));
}
public sealed class ModLog { public readonly List<string> Messages = new List<string>(); public void Log(string value) => Messages.Add(value); public void Error(string value) => Messages.Add(value); }
namespace StarPawn.Game
{
    public sealed partial class ModEntry
    {
        private readonly EconomyRules rules = new EconomyRules();
        private readonly ModLog? log = new ModLog();
        public bool Ready => ContentReady;
        public EconomyRules Finance => rules;
        public void Load() => InitializeContent(new ModManifest());
        public IReadOnlyList<string> Logs => log!.Messages;
    }
}

internal static class ContentBootstrapTests
{
    private static int passed;
    private static string delivery = "", fixture = "", sentinel = "";
    private static readonly Dictionary<string, string> originals = new Dictionary<string, string>(StringComparer.Ordinal);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reset()
    {
        foreach (var original in originals) File.WriteAllText(Path.Combine(fixture, original.Key), original.Value);
        ModHelper.Folder = fixture;
    }
    private static void Test(string name, Action action) { Reset(); action(); Check(File.ReadAllText(sentinel) == "PLAYER SAVE MUST NOT CHANGE", "Config touched player save sentinel"); passed++; Console.WriteLine("PASS " + name); }
    private static void Modify(string file, Action<JObject> edit)
    {
        var path = Path.Combine(fixture, "Content", file); var obj = JObject.Parse(File.ReadAllText(path)); edit(obj); File.WriteAllText(path, obj.ToString());
    }
    private static void Rejected(string watchedFile)
    {
        var path = Path.Combine(fixture, "Content", watchedFile); var before = File.ReadAllText(path); var mod = new StarPawn.Game.ModEntry();
        try { mod.Load(); } catch (InvalidOperationException) { Check(!mod.Ready, "Rejected config remained ready"); Check(mod.Logs.Any(l => l.Contains("SP_CONFIG_BLOCKED")), "Rejection was not logged"); Check(File.ReadAllText(path) == before, "Rejected config overwritten"); return; }
        throw new Exception("Bad configuration accepted: " + watchedFile);
    }
    private static void Main(string[] args)
    {
        try { Run(args); } catch (Exception error) { Console.Error.WriteLine("FAIL " + error); Environment.Exit(1); }
    }
    private static void Run(string[] args)
    {
        delivery = Path.GetFullPath(args[0]); fixture = Path.Combine(Path.GetFullPath(args[1]), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixture);
        var installed = Path.Combine(delivery, "mod");
        foreach (var file in Directory.GetFiles(Path.Combine(installed, "Content"), "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(installed, file); var destination = Path.Combine(fixture, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); originals.Add(relative, File.ReadAllText(file)); File.WriteAllText(destination, originals[relative]);
        }
        foreach (var file in Directory.GetFiles(Path.Combine(installed, "Textures"), "*.png", SearchOption.AllDirectories))
        {
            // These fixtures test existence checks only and are never rendered or distributed as art.
            var destination = Path.Combine(fixture, Path.GetRelativePath(installed, file)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.WriteAllBytes(destination, new byte[] { 1 });
        }
        sentinel = Path.Combine(fixture, "player-save-sentinel.json"); File.WriteAllText(sentinel, "PLAYER SAVE MUST NOT CHANGE");
        Test("real packaged content initializes through native folder API", () => { ModHelper.Folder = installed; var mod = new StarPawn.Game.ModEntry(); mod.Load(); Check(mod.Ready && Catalog.Definitions.Count >= 30 && mod.Finance.GoldSellPerGram >= mod.Finance.GoldBuyPerGram, "Pack not ready"); Check(mod.Logs.Any(l => l.Contains("SP_CONTENT_READY")), "No content readiness log"); });
        Test("two-miss rule override fails without overwriting config or save", () => { Modify("inspection-rules.json", o => o["ConsecutiveMissLimit"] = 3); Rejected("inspection-rules.json"); });
        Test("future content schema fails closed", () => { Modify("items.json", o => o["SchemaVersion"] = 99); Rejected("items.json"); });
        Test("unknown finance key is rejected rather than silently defaulting", () => { Modify("finance-rules.json", o => o["GoldBiyPerGram"] = 1); Rejected("finance-rules.json"); });
        Test("missing texture blocks registration", () => { Modify("items.json", o => o["Items"]![0]!["TextureKey"] = "starpawn:does_not_exist"); Rejected("items.json"); });
        Test("missing localization key blocks registration", () => { Modify("items.json", o => o["Items"]![0]!["NameKey"] = "missing.key"); Rejected("items.json"); });
        Test("duplicate JSON property is rejected", () => { File.WriteAllText(Path.Combine(fixture, "Content", "settings.json"), "{\"SchemaVersion\":1,\"Language\":\"zh-CN\",\"Language\":\"en\"}"); Rejected("settings.json"); });
        Test("language path traversal is rejected", () => { Modify("settings.json", o => o["Language"] = "../../secrets"); Rejected("settings.json"); });
        Test("extension path traversal is rejected", () => { Modify("settings.json", o => o["ExtensionFiles"] = new JArray("../secrets.json")); Rejected("settings.json"); });
        Test("partial language overlay preserves base keys and finance state", () => { Modify("settings.json", o => o["Language"] = "en"); var before = File.ReadAllText(Path.Combine(fixture, "Content", "finance-rules.json")); var mod = new StarPawn.Game.ModEntry(); mod.Load(); Check(mod.Ready && TextResolver.Language == "en", "Language overlay failed"); Check(TextResolver.LocalizeInline("星港大厅") == "Starport desk", "English key did not reach UI resolver"); Check(File.ReadAllText(Path.Combine(fixture, "Content", "finance-rules.json")) == before, "Language changed finance settings"); });
        Test("example extension loads with added keys and stable independent definition", () => {
            var extensions = Path.Combine(fixture, "Content", "Extensions"); Directory.CreateDirectory(extensions); var example = Path.Combine(delivery, "docs", "example-extension.json"); File.Copy(example, Path.Combine(extensions, "example.json"));
            Modify("settings.json", o => o["ExtensionFiles"] = new JArray("example.json"));
            var additions = JObject.Parse(File.ReadAllText(Path.Combine(delivery, "docs", "example-extension-localization.json")));
            Modify("Localization/zh-CN.json", o => { foreach (var entry in additions["Entries"]!) ((JArray)o["Entries"]!).Add(entry.DeepClone()); });
            var mod = new StarPawn.Game.ModEntry(); mod.Load(); Check(mod.Ready && Catalog.GetDefinition("silver_bracelet_engraved_example").CraftValue == 155, "Extension not registered");
        });
        Console.WriteLine("CONTENT BOOTSTRAP TESTS PASSED: " + passed);
    }
}
