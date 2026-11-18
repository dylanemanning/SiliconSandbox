// PackageLibrary.cs — every package the player has created, shared by all worlds.
//
// Definitions are saved as JSON under persistentDataPath/Packages, one file per
// package, and also embedded in each world save that uses them (WorldSaveSystem),
// so a world still loads if the global file is missing. Each definition gets an
// inventory entry; its Block "prefab" is an inactive template that placement code
// recognises through its PackageBlock component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class PackageLibrary
{
    private static readonly Dictionary<string, PackageDefinition> definitions = new Dictionary<string, PackageDefinition>();
    private static readonly Dictionary<string, InventoryItem> items = new Dictionary<string, InventoryItem>();
    private static readonly Dictionary<string, Block> templates = new Dictionary<string, Block>();
    private static readonly List<string> order = new List<string>();
    private static bool loaded;
    private static GameObject templateRoot;
    private static Sprite icon;

    /// <summary>Raised after a definition is added, so the inventory can refresh.</summary>
    public static event Action Changed;

    /// <summary>Overrides the library folder (tests).</summary>
    public static string DirectoryOverride { get; set; }
    public static string LibraryDirectory => DirectoryOverride ?? Path.Combine(Application.persistentDataPath, "Packages");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        definitions.Clear(); items.Clear(); templates.Clear(); order.Clear();
        loaded = false; templateRoot = null; icon = null; Changed = null; DirectoryOverride = null;
    }

    /// <summary>Forgets loaded definitions and reloads from disk on next use (tests).</summary>
    public static void Reload()
    {
        foreach (var template in templates.Values)
        {
            if (template == null) continue;
            if (Application.isPlaying) UnityEngine.Object.Destroy(template.gameObject);
            else UnityEngine.Object.DestroyImmediate(template.gameObject);
        }
        definitions.Clear(); items.Clear(); templates.Clear(); order.Clear();
        loaded = false;
        Changed?.Invoke();
    }

    public static IReadOnlyList<PackageDefinition> All
    {
        get { EnsureLoaded(); return order.Select(id => definitions[id]).ToList(); }
    }

    public static PackageDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        definitions.TryGetValue(id, out var definition);
        return definition;
    }

    /// <summary>Inventory entries for every package, oldest first.</summary>
    public static IReadOnlyList<InventoryItem> InventoryItems
    {
        get { EnsureLoaded(); return order.Select(ItemFor).Where(i => i != null).ToList(); }
    }

    public static InventoryItem ItemFor(string id)
    {
        var definition = Get(id);
        if (definition == null) return null;
        if (items.TryGetValue(id, out var item)) return item;
        item = new InventoryItem("package:" + id, definition.name, TemplateFor(definition), Icon,
            InventoryItemType.Block, definition.marking);
        items.Add(id, item);
        return item;
    }

    /// <summary>Adds a new definition, saves it to the library folder and announces it.</summary>
    public static void Add(PackageDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.id)) throw new ArgumentException("A package needs an id.");
        EnsureLoaded();
        Register(definition);
        try
        {
            Directory.CreateDirectory(LibraryDirectory);
            File.WriteAllText(Path.Combine(LibraryDirectory, definition.id + ".json"), JsonUtility.ToJson(definition, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Package '{definition.name}' could not be saved to the library: {exception.Message}");
        }
        Changed?.Invoke();
    }

    /// <summary>Adds a definition carried by a world save if the library does not have it yet.</summary>
    public static bool Import(PackageDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.id) || Get(definition.id) != null) return false;
        Add(definition);
        return true;
    }

    /// <summary>The definition and every package nested inside it, children first.</summary>
    public static List<PackageDefinition> Closure(IEnumerable<string> ids)
    {
        var result = new List<PackageDefinition>();
        var seen = new HashSet<string>();
        void Visit(string id, int depth)
        {
            if (depth > 16 || !seen.Add(id)) return;
            var definition = Get(id);
            if (definition == null) return;
            foreach (var part in definition.parts)
                if (part.kind == PackagedPartKind.Package) Visit(part.packageId, depth + 1);
            result.Add(definition);
        }
        foreach (var id in ids) Visit(id, 0);
        return result;
    }

    /// <summary>Creates a placed package in the world.</summary>
    public static PackageBlock CreateInstance(PackageDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var instance = new GameObject("Package " + definition.name);
        instance.SetActive(false);
        if (parent != null) instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        var package = Configure(instance, definition);
        instance.SetActive(true);
        return package;
    }

    private static PackageBlock Configure(GameObject target, PackageDefinition definition)
    {
        var block = target.AddComponent<Block>();
        block.footprint = definition.Footprint;
        block.footprintSize = Vector3Int.one;
        block.durabilitySeconds = .05f;
        var collider = target.AddComponent<BoxCollider>();
        collider.center = new Vector3((definition.Width - 1) * .5f, 0, 0);
        collider.size = new Vector3(definition.Width, 1, 1);
        var package = target.AddComponent<PackageBlock>();
        package.Initialize(definition.id);
        return package;
    }

    private static Block TemplateFor(PackageDefinition definition)
    {
        if (templates.TryGetValue(definition.id, out var existing) && existing != null) return existing;
        if (templateRoot == null)
        {
            templateRoot = new GameObject("Package templates");
            templateRoot.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(templateRoot);
        }
        var template = new GameObject("Package " + definition.name + " template");
        template.transform.SetParent(templateRoot.transform, false);
        Configure(template, definition);
        var block = template.GetComponent<Block>();
        templates[definition.id] = block;
        return block;
    }

    private static void Register(PackageDefinition definition)
    {
        if (!definitions.ContainsKey(definition.id)) order.Add(definition.id);
        definitions[definition.id] = definition;
        items.Remove(definition.id);
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            if (!Directory.Exists(LibraryDirectory)) return;
            var files = Directory.GetFiles(LibraryDirectory, "*.json").OrderBy(File.GetCreationTimeUtc);
            foreach (string path in files)
            {
                try
                {
                    var definition = JsonUtility.FromJson<PackageDefinition>(File.ReadAllText(path));
                    if (definition != null && !string.IsNullOrEmpty(definition.id)) Register(definition);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not read package '{path}': {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read the package library: {exception.Message}");
        }
    }

    /// <summary>A chip-shaped inventory icon; the package's marking is drawn over it as text.</summary>
    public static Sprite Icon
    {
        get
        {
            if (icon != null) return icon;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0, 0, 0, 0);
            var body = new Color(.62f, .63f, .66f, 1);
            var top = new Color(.03f, .035f, .04f, 1);
            var ring = new Color(.38f, .42f, .45f, 1);
            var pin = new Color(.55f, .58f, .6f, 1);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color color = clear;
                bool inBody = x >= 14 && x < 114 && y >= 22 && y < 106;
                if (inBody) color = body;
                if (x >= 20 && x < 108 && y >= 28 && y < 100) color = top;
                if (x >= 26 && x < 102 && y >= 34 && y < 94 && (x < 28 || x >= 100 || y < 36 || y >= 92)) color = ring;
                bool pinColumn = (x - 24) % 26 < 10 && x >= 24 && x < 108;
                if (pinColumn && (y >= 8 && y < 22 || y >= 106 && y < 120)) color = pin;
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            icon.name = "Package icon";
            return icon;
        }
    }
}
