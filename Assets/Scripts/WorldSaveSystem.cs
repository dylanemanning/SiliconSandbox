using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WorldSaveSystem : MonoBehaviour
{
    public const int CurrentSaveVersion = 3;

    [Serializable]
    public class SavedBlock
    {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale = Vector3.one;
        public Vector3Int footprintSize = Vector3Int.one;
        public bool isGround;
        public bool hasWireEndpointMask;
        public int wireEndpointMask;
    }

    // Read-only compatibility fields. New saves serialize SavedBlock instead,
    // so hidden connection state never survives in the geometry-only format.
    [Serializable]
    public class LegacySavedBlock : SavedBlock
    {
        public bool hasWireJoinMask;
        public int wireJoinMask;
    }

    [Serializable]
    private class SavedWorld
    {
        public int version;
        public string worldName;
        public List<SavedBlock> blocks = new List<SavedBlock>();
    }

    [Serializable]
    private class LoadedWorld
    {
        public int version;
        public string worldName;
        public List<LegacySavedBlock> blocks = new List<LegacySavedBlock>();
    }

    public static WorldSaveSystem Instance { get; private set; }
    public static string PendingWorldName { get; set; }

    [Header("Register every placeable prefab, including the floor cube")]
    [SerializeField] private GameObject[] blockPrefabs = Array.Empty<GameObject>();
    [SerializeField] private Transform worldRoot;
    [SerializeField] private float autosaveDelay = 0.5f;

    public string WorldName { get; private set; }
    private bool savePending;
    private float saveAt;
    private bool isLoading;
    private bool saveBlocked;

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Worlds");

    public static WorldSaveSystem GetOrCreate(GameObject[] prefabs, Transform root)
    {
        WorldSaveSystem saveSystem = Instance;
        if (saveSystem == null)
        {
            GameObject saveObject = new GameObject("WorldSaveSystem");
            saveSystem = saveObject.AddComponent<WorldSaveSystem>();
        }

        saveSystem.RegisterPrefabs(prefabs);
        if (saveSystem.worldRoot == null) saveSystem.worldRoot = root;
        return saveSystem;
    }

    public static WorldSaveSystem GetOrCreate(Block[] blocks, Transform root)
    {
        if (blocks == null) return GetOrCreate(Array.Empty<GameObject>(), root);

        GameObject[] prefabs = new GameObject[blocks.Length];
        for (int index = 0; index < blocks.Length; index++)
        {
            prefabs[index] = blocks[index] != null ? blocks[index].gameObject : null;
        }

        return GetOrCreate(prefabs, root);
    }

    public void RegisterPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null) return;

        List<GameObject> registeredPrefabs = new List<GameObject>(blockPrefabs);
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && !registeredPrefabs.Contains(prefab)) registeredPrefabs.Add(prefab);
        }

        blockPrefabs = registeredPrefabs.ToArray();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WorldName = string.IsNullOrWhiteSpace(PendingWorldName) ? "World" : PendingWorldName;
        PendingWorldName = null;
    }

    private void Start()
    {
        string filePath = GetSavePath(WorldName);
        if (File.Exists(filePath))
        {
            isLoading = true;
            StartCoroutine(LoadAfterWorldGeneration(filePath));
        }
    }

    private void Update()
    {
        if (savePending && Time.unscaledTime >= saveAt)
        {
            SaveNow();
        }
    }

    public void MarkDirty()
    {
        savePending = true;
        saveAt = Time.unscaledTime + autosaveDelay;
    }

    public void SaveNow()
    {
        // Never replace an existing save with a partly loaded world, including
        // when the application quits while loading or after a load failure.
        if (isLoading || saveBlocked) return;
        SavedWorld save = new SavedWorld { version = CurrentSaveVersion, worldName = WorldName };
        foreach (Block block in FindObjectsByType<Block>(FindObjectsSortMode.None))
        {
            save.blocks.Add(CaptureBlock(block));
        }

        Directory.CreateDirectory(SaveDirectory);
        File.WriteAllText(GetSavePath(WorldName), JsonUtility.ToJson(save, true));
        savePending = false;
    }

    private static SavedBlock CaptureBlock(Block block)
    {
        LogicWireBlock wire = block.GetComponent<LogicWireBlock>();
        return new SavedBlock
        {
            prefabName = GetPrefabName(block.gameObject),
            position = block.transform.position,
            rotation = block.transform.rotation,
            scale = block.transform.localScale,
            footprintSize = block.GetFootprintSize(),
            isGround = block.isGround,
            hasWireEndpointMask = wire != null,
            wireEndpointMask = wire != null ? wire.EndpointMask : 0
        };
    }

    public static string[] GetSavedWorldNames()
    {
        string directory = Path.Combine(Application.persistentDataPath, "Worlds");
        if (!Directory.Exists(directory)) return Array.Empty<string>();

        string[] paths = Directory.GetFiles(directory, "*.json");
        List<string> names = new List<string>();
        foreach (string path in paths)
        {
            try
            {
                SavedWorld save = JsonUtility.FromJson<SavedWorld>(File.ReadAllText(path));
                if (save != null && !string.IsNullOrWhiteSpace(save.worldName)) names.Add(save.worldName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read world save '{path}': {exception.Message}");
            }
        }

        return names.ToArray();
    }

    public static bool DeleteSavedWorld(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return false;

        string filePath = Path.Combine(
            Application.persistentDataPath,
            "Worlds",
            SanitizeFileName(worldName) + ".json"
        );

        try
        {
            if (!File.Exists(filePath)) return false;
            File.Delete(filePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not delete world '{worldName}': {exception.Message}");
            return false;
        }
    }

    private IEnumerator LoadAfterWorldGeneration(string filePath)
    {
        yield return null;

        LoadedWorld save;
        try
        {
            save = JsonUtility.FromJson<LoadedWorld>(File.ReadAllText(filePath));
        }
        catch (Exception exception)
        {
            saveBlocked = true;
            isLoading = false;
            Debug.LogError($"Could not load world '{WorldName}': {exception.Message}");
            yield break;
        }

        if (save == null || save.blocks == null || save.version > CurrentSaveVersion)
        {
            saveBlocked = true;
            isLoading = false;
            Debug.LogError($"Could not load world '{WorldName}': invalid or unsupported save format. The original file is preserved.");
            yield break;
        }
        // Validate references before removing anything. Silently dropping an
        // unavailable prefab would destroy authored content on the next save.
        foreach (SavedBlock savedBlock in save.blocks)
        {
            if (savedBlock != null && FindPrefab(savedBlock.prefabName) != null) continue;
            saveBlocked = true;
            isLoading = false;
            Debug.LogError($"Could not load world '{WorldName}': a saved prefab is unavailable. The original file is preserved.");
            yield break;
        }

        foreach (Block block in FindObjectsByType<Block>(FindObjectsSortMode.None))
        {
            Destroy(block.gameObject);
        }

        yield return null;
        var restoredWires = new Dictionary<LogicWireBlock, LegacySavedBlock>();
        foreach (LegacySavedBlock savedBlock in save.blocks)
        {
            GameObject prefab = FindPrefab(savedBlock.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"No registered prefab named '{savedBlock.prefabName}' was found.");
                continue;
            }

            GameObject block = Instantiate(prefab, savedBlock.position, savedBlock.rotation, worldRoot);
            bool nativeArt = prefab.GetComponent<BlockModelVisual>() != null || prefab.GetComponent<WireModelVisual>() != null;
            block.transform.localScale = nativeArt ? Vector3.one
                : savedBlock.scale == Vector3.zero ? Vector3.one : savedBlock.scale;

            Block savedBlockComponent = block.GetComponent<Block>();
            if (savedBlockComponent != null)
            {
                savedBlockComponent.footprintSize = savedBlock.footprintSize != Vector3Int.zero ? savedBlock.footprintSize : Vector3Int.one;

                bool isGround = savedBlock.isGround
                    || (!nativeArt && (savedBlock.scale.x > 1.001f || savedBlock.scale.z > 1.001f));
                savedBlockComponent.isGround = isGround;
                if (isGround) savedBlockComponent.breakable = false;
            }

            LogicWireBlock savedWire = block.GetComponent<LogicWireBlock>();
            if (savedWire != null) restoredWires.Add(savedWire, savedBlock);
        }

        RestoreWireTopology(restoredWires, save.version);
        savePending = false;
        isLoading = false;
    }

    private static void RestoreWireTopology(Dictionary<LogicWireBlock, LegacySavedBlock> restoredWires, int version)
    {
        foreach (var entry in restoredWires)
        {
            LogicWireBlock wire = entry.Key;
            LegacySavedBlock saved = entry.Value;
            if (saved.hasWireEndpointMask) wire.SetEndpointMask(saved.wireEndpointMask);
        }

        if (version < 2)
        {
            WireTopology.MigrateLegacyGeometry(restoredWires.Keys);
            RestoreLegacyZeroMaskVisuals(restoredWires.Keys);
            return;
        }
        if (version >= CurrentSaveVersion) return;

        // Version 2 rendered EndpointMask | ConnectionMask. Only reciprocal
        // stored wire joins contributed extra visible arms; a unilateral stale
        // flag must not grow geometry. Pin contact already required an endpoint.
        var byCell = new Dictionary<Vector3Int, LogicWireBlock>();
        var legacyWires = new List<LogicWireBlock>();
        foreach (var entry in restoredWires)
        {
            byCell[entry.Key.GridPosition] = entry.Key;
            if (!entry.Value.hasWireJoinMask) legacyWires.Add(entry.Key);
        }
        var normalized = new Dictionary<LogicWireBlock, int>();
        foreach (var entry in restoredWires)
        {
            LogicWireBlock wire = entry.Key;
            LegacySavedBlock saved = entry.Value;
            int mask = wire.EndpointMask;
            if (saved.hasWireJoinMask)
            foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
            {
                int face = LogicWireBlock.GetDirectionBit(cell - wire.GridPosition);
                if ((saved.wireJoinMask & face) == 0 || !byCell.TryGetValue(cell, out LogicWireBlock neighbor)) continue;
                LegacySavedBlock neighborSaved = restoredWires[neighbor];
                int back = LogicWireBlock.GetDirectionBit(wire.GridPosition - cell);
                if (neighborSaved.hasWireJoinMask && (neighborSaved.wireJoinMask & back) != 0) mask |= face;
            }
            normalized.Add(wire, mask);
        }
        foreach (var entry in normalized) entry.Key.SetEndpointMask(entry.Value);
        // Version 2 also accepted records predating its join fields.
        WireTopology.MigrateLegacyGeometry(legacyWires);
        RestoreLegacyZeroMaskVisuals(restoredWires.Keys);
    }

    private static void RestoreLegacyZeroMaskVisuals(IEnumerable<LogicWireBlock> wires)
    {
        foreach (LogicWireBlock wire in wires)
        {
            if (wire.EndpointMask != 0) continue;
            WireModelVisual visual = wire.GetComponent<WireModelVisual>();
            if (visual == null || visual.straightModel == null) continue;

            // Before version 3 a zero visible mask displayed the straight
            // model at identity local rotation. Its authored axis is local Z,
            // so preserve its parent's world forward, not the wire's local Y.
            Transform parent = visual.straightModel.transform.parent;
            Vector3Int axis = Vector3Int.RoundToInt(parent != null ? parent.forward : Vector3.forward);
            wire.SetEndpointMask(LogicWireBlock.GetDirectionBit(axis) | LogicWireBlock.GetDirectionBit(-axis));
        }
    }

    private GameObject FindPrefab(string prefabName)
    {
        foreach (GameObject prefab in blockPrefabs)
        {
            if (prefab != null && prefab.name == prefabName) return prefab;
        }

        return null;
    }

    private string GetSavePath(string worldName)
    {
        return Path.Combine(SaveDirectory, SanitizeFileName(worldName) + ".json");
    }

    private static string GetPrefabName(GameObject block)
    {
        const string cloneSuffix = "(Clone)";
        return block.name.EndsWith(cloneSuffix, StringComparison.Ordinal)
            ? block.name.Substring(0, block.name.Length - cloneSuffix.Length).TrimEnd()
            : block.name;
    }

    private static string SanitizeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "World" : value.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalidCharacter.ToString(), "_");
        }

        return result;
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveNow();
    }
}
