using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldSaveSystem : MonoBehaviour
{
    public const int CurrentSaveVersion = 4;
    public const string TutorialWorldName = "tutorial";

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
        public bool hasSourceState;
        public int sourceState;
    }

    // Read-only compatibility fields for save versions that stored wire joins.
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
    public static string PendingLoadError { get; private set; }
    private static string pendingImportedWorldName;

    public static string ConsumePendingLoadError()
    {
        string error = PendingLoadError;
        PendingLoadError = null;
        return error;
    }

    internal static void MarkImportedWorldPending(string worldName)
    {
        pendingImportedWorldName = worldName;
    }

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

    public void ExportWorld(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));
        }
        if (isLoading || saveBlocked)
        {
            throw new InvalidOperationException("The world cannot be exported while its save is unavailable.");
        }

        SaveNow();
        string sourcePath = GetSavePath(WorldName);
        if (!File.Exists(sourcePath))
        {
            throw new IOException($"The save file for world '{WorldName}' was not created.");
        }

        File.Copy(sourcePath, destinationPath, true);
    }

    public static bool TryImportWorld(string sourcePath, string worldName, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = "The selected world file could not be found.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(worldName))
        {
            error = "Enter a name for the imported world.";
            return false;
        }

        if (!WorldFileValidator.TryReadFile(sourcePath, out string json, out error) ||
            !WorldFileValidator.TryValidate(json, out error))
        {
            return false;
        }

        string saveDirectory = Path.Combine(Application.persistentDataPath, "Worlds");
        string destinationPath = Path.Combine(saveDirectory, SanitizeFileName(worldName) + ".json");
        if (File.Exists(destinationPath))
        {
            error = "World Name Already Exists";
            return false;
        }

        string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            LoadedWorld save = JsonUtility.FromJson<LoadedWorld>(json);
            save.worldName = worldName.Trim();
            Directory.CreateDirectory(saveDirectory);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(save, true));
            File.Move(temporaryPath, destinationPath);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not import the selected world: {exception.Message}";
            Debug.LogError(error);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Could not remove temporary world import file '{temporaryPath}': {exception.Message}");
                }
            }
        }
    }

    public static bool TryCopySavedWorld(string sourceWorldName, string copyWorldName, out string error)
    {
        error = null;
        if (!IsValidWorldName(copyWorldName, out error)) return false;

        string directory = Path.Combine(Application.persistentDataPath, "Worlds");
        string sourcePath = Path.Combine(directory, SanitizeFileName(sourceWorldName) + ".json");
        string destinationPath = Path.Combine(directory, SanitizeFileName(copyWorldName) + ".json");
        if (!File.Exists(sourcePath))
        {
            error = $"Could not find the saved world '{sourceWorldName}'.";
            return false;
        }
        if (GetSavedWorldNames().Any(name =>
                string.Equals(name, copyWorldName.Trim(), StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(destinationPath))
        {
            error = "World Name Already Exists";
            return false;
        }

        if (!WorldFileValidator.TryReadFile(sourcePath, out string json, out error) ||
            !WorldFileValidator.TryValidate(json, out error))
        {
            return false;
        }

        string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            LoadedWorld save = JsonUtility.FromJson<LoadedWorld>(json);
            if (save == null || save.blocks == null)
            {
                error = $"The saved world '{sourceWorldName}' could not be decoded.";
                return false;
            }

            save.worldName = copyWorldName.Trim();
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(save, true));
            File.Move(temporaryPath, destinationPath);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not copy world '{sourceWorldName}': {exception.Message}";
            Debug.LogError(error);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Could not remove temporary world copy '{temporaryPath}': {exception.Message}");
                }
            }
        }
    }

    public bool TryRenameWorld(string newWorldName, out string error)
    {
        if (!IsValidWorldName(newWorldName, out error)) return false;
        if (isLoading || saveBlocked)
        {
            error = "The world cannot be renamed while its save is unavailable.";
            return false;
        }

        newWorldName = newWorldName.Trim();
        if (string.Equals(WorldName, newWorldName, StringComparison.Ordinal)) return true;

        string sourcePath = GetSavePath(WorldName);
        string destinationPath = GetSavePath(newWorldName);
        bool samePath = string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase);
        if (GetSavedWorldNames().Any(name =>
                !string.Equals(name, WorldName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(name, newWorldName, StringComparison.OrdinalIgnoreCase)) ||
            (File.Exists(destinationPath) && !samePath))
        {
            error = "World Name Already Exists";
            return false;
        }

        try
        {
            SaveNow();
        }
        catch (Exception exception)
        {
            error = $"Could not save world '{WorldName}' before renaming: {exception.Message}";
            Debug.LogError(error);
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            error = $"Could not find the save file for world '{WorldName}'.";
            return false;
        }

        string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backupPath = sourcePath + "." + Guid.NewGuid().ToString("N") + ".rename-backup";
        bool sourceMoved = false;
        try
        {
            LoadedWorld save = JsonUtility.FromJson<LoadedWorld>(File.ReadAllText(sourcePath));
            if (save == null || save.blocks == null)
            {
                error = $"The save file for world '{WorldName}' could not be decoded.";
                return false;
            }

            save.worldName = newWorldName;
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(save, true));
            File.Move(sourcePath, backupPath);
            sourceMoved = true;
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                File.Move(backupPath, sourcePath);
                sourceMoved = false;
                throw;
            }

            WorldName = newWorldName;
            savePending = false;
            try
            {
                File.Delete(backupPath);
                sourceMoved = false;
            }
            catch (Exception exception)
            {
                Debug.LogError($"World '{newWorldName}' was renamed, but the old save backup could not be removed: {exception.Message}");
                sourceMoved = false;
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not rename world '{WorldName}': {exception.Message}";
            Debug.LogError(error);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Could not remove temporary world rename file '{temporaryPath}': {exception.Message}");
                }
            }

            if (sourceMoved && File.Exists(backupPath))
            {
                Debug.LogError($"The original world save remains at '{backupPath}' after an unsuccessful rename.");
            }
        }
    }

    private static SavedBlock CaptureBlock(Block block)
    {
        LogicWireBlock wire = block.GetComponent<LogicWireBlock>();
        LogicSignalSource source = block.GetComponent<LogicSignalSource>();
        return new SavedBlock
        {
            prefabName = GetPrefabName(block.gameObject),
            position = block.transform.position,
            rotation = block.transform.rotation,
            scale = block.transform.localScale,
            footprintSize = block.GetFootprintSize(),
            isGround = block.isGround,
            hasWireEndpointMask = wire != null,
            wireEndpointMask = wire != null ? wire.EndpointMask : 0,
            hasSourceState = source != null,
            sourceState = source != null ? source.SignalState : 0
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
                if (save != null && !string.IsNullOrWhiteSpace(save.worldName) &&
                    !string.Equals(save.worldName, TutorialWorldName, StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(save.worldName);
                }
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

        if (!WorldFileValidator.TryReadFile(filePath, out string json, out string error) ||
            !WorldFileValidator.TryValidate(json, out error))
        {
            AbortWorldLoad($"The world file failed validation: {error}");
            yield break;
        }

        LoadedWorld save;
        try
        {
            save = JsonUtility.FromJson<LoadedWorld>(json);
        }
        catch (Exception exception)
        {
            AbortWorldLoad($"The world file could not be decoded: {exception.Message}");
            yield break;
        }

        if (save == null || save.blocks == null)
        {
            AbortWorldLoad("The world file could not be decoded into a Silicon Sandbox save.");
            yield break;
        }

        if (!string.Equals(save.worldName, WorldName, StringComparison.OrdinalIgnoreCase))
        {
            AbortWorldLoad("The save's worldName does not match the selected world.");
            yield break;
        }

        int gateCount = 0;
        int wireCount = 0;
        var occupiedCells = new Dictionary<Vector3Int, string>();
        foreach (LegacySavedBlock savedBlock in save.blocks)
        {
            GameObject prefab = FindPrefab(savedBlock.prefabName);
            if (prefab == null)
            {
                AbortWorldLoad(
                    $"The world references an unrecognized block '{savedBlock.prefabName}'. " +
                    "Edit the JSON to remove that block entry, then import the file again."
                );
                yield break;
            }

            if (prefab.GetComponent<Block>() == null)
            {
                AbortWorldLoad(
                    $"The world references '{savedBlock.prefabName}', which is not a valid placeable block prefab."
                );
                yield break;
            }

            if (savedBlock.hasSourceState && prefab.GetComponent<LogicSignalSource>() == null)
            {
                AbortWorldLoad(
                    $"Block '{savedBlock.prefabName}' contains source state, but its prefab is not a signal source."
                );
                yield break;
            }

            LogicWireBlock savedWire = prefab.GetComponent<LogicWireBlock>();
            if ((savedBlock.hasWireEndpointMask || savedBlock.hasWireJoinMask) && savedWire == null)
            {
                AbortWorldLoad(
                    $"Block '{savedBlock.prefabName}' contains wire data, but its prefab is not a wire."
                );
                yield break;
            }

            Block savedPrefabBlock = prefab.GetComponent<Block>();
            Vector3Int anchor = Vector3Int.RoundToInt(savedBlock.position);
            foreach (Vector3Int cell in savedPrefabBlock.OccupiedCells(anchor, savedBlock.rotation))
            {
                if (!occupiedCells.TryAdd(cell, savedBlock.prefabName))
                {
                    AbortWorldLoad(
                        $"Blocks '{savedBlock.prefabName}' and '{occupiedCells[cell]}' overlap at cell {cell}."
                    );
                    yield break;
                }
            }

            GateBlock gate = prefab.GetComponent<GateBlock>();
            if (gate != null)
            {
                try
                {
                    GatePorts.For(gate.GateType);
                }
                catch (ArgumentException)
                {
                    AbortWorldLoad(
                        $"The gate prefab '{savedBlock.prefabName}' has an unsupported gate type. " +
                        "The world was not loaded."
                    );
                    yield break;
                }

                gateCount++;
            }
            if (savedWire != null) wireCount++;
        }

        if (gateCount > WorldFileValidator.MaxGates)
        {
            AbortWorldLoad(
                $"The world contains {gateCount} gates; the development limit is {WorldFileValidator.MaxGates}."
            );
            yield break;
        }
        if (wireCount > WorldFileValidator.MaxWireBlocks)
        {
            AbortWorldLoad($"The world contains {wireCount} wire blocks; the development limit is {WorldFileValidator.MaxWireBlocks}.");
            yield break;
        }

        if (!ValidateCircuitComplexity(save, out error))
        {
            AbortWorldLoad(error);
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

            RestoreSourceState(block.GetComponent<LogicSignalSource>(), savedBlock);

            LogicWireBlock savedWire = block.GetComponent<LogicWireBlock>();
            if (savedWire != null) restoredWires.Add(savedWire, savedBlock);
        }

        RestoreWireTopology(restoredWires, save.version);
        pendingImportedWorldName = null;
        savePending = false;
        isLoading = false;

        Player player = FindFirstObjectByType<Player>();
        if (player != null) player.EnsureCurrentPositionIsSafe();
    }

    private bool ValidateCircuitComplexity(LoadedWorld save, out string error)
    {
        error = null;
        GameObject stagingRoot = new GameObject("WorldValidationStaging");
        stagingRoot.SetActive(false);
        try
        {
            var logicBlocks = new List<LogicSignalBlock>();
            foreach (LegacySavedBlock savedBlock in save.blocks)
            {
                GameObject prefab = FindPrefab(savedBlock.prefabName);
                GameObject stagedBlock = Instantiate(
                    prefab,
                    savedBlock.position,
                    savedBlock.rotation,
                    stagingRoot.transform
                );
                bool nativeArt =
                    prefab.GetComponent<BlockModelVisual>() != null ||
                    prefab.GetComponent<WireModelVisual>() != null;
                stagedBlock.transform.localScale = nativeArt
                    ? Vector3.one
                    : savedBlock.scale == Vector3.zero ? Vector3.one : savedBlock.scale;

                Block block = stagedBlock.GetComponent<Block>();
                if (block != null &&
                    savedBlock.footprintSize != Vector3Int.zero)
                {
                    block.footprintSize = savedBlock.footprintSize;
                }

                LogicWireBlock wire = stagedBlock.GetComponent<LogicWireBlock>();
                if (wire != null && savedBlock.hasWireEndpointMask)
                {
                    wire.SetEndpointMask(savedBlock.wireEndpointMask);
                }

                LogicSignalBlock logicBlock = stagedBlock.GetComponent<LogicSignalBlock>();
                if (logicBlock != null) logicBlocks.Add(logicBlock);
            }

            Netlist netlist = NetlistBuilder.FromBlocks(logicBlocks);
            if (netlist.NodeCount > WorldFileValidator.MaxLogicNodes)
            {
                error = $"The world contains too many logic nodes. The development limit is {WorldFileValidator.MaxLogicNodes}.";
                return false;
            }
            if (netlist.Connections.Length > WorldFileValidator.MaxConnections)
            {
                error = $"The world contains too many logic connections. The development limit is {WorldFileValidator.MaxConnections}.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"The world circuit could not be validated: {exception.Message}";
            return false;
        }
        finally
        {
            Destroy(stagingRoot);
        }
    }

    private void AbortWorldLoad(string reason)
    {
        saveBlocked = true;
        isLoading = false;
        string cleanupMessage = string.Empty;
        if (string.Equals(pendingImportedWorldName, WorldName, StringComparison.Ordinal))
        {
            if (File.Exists(GetSavePath(WorldName)) && !DeleteSavedWorld(WorldName))
            {
                cleanupMessage = " The imported copy could not be removed; delete it before re-importing.";
            }
            pendingImportedWorldName = null;
        }

        PendingLoadError = $"Could not load world '{WorldName}': {reason}{cleanupMessage}";
        Debug.LogError(PendingLoadError);
        SceneManager.LoadScene("MainMenu");
    }

    private static void RestoreSourceState(LogicSignalSource source, LegacySavedBlock saved)
    {
        if (source != null && saved.hasSourceState) source.SetState(saved.sourceState);
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
        if (version >= 3) return;

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

    public static string SanitizeExportFileName(string worldName)
    {
        return SanitizeFileName(worldName) + ".json";
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

    private static bool IsValidWorldName(string worldName, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(worldName))
        {
            error = "Enter a name for the world.";
            return false;
        }

        worldName = worldName.Trim();
        if (worldName.Length > MenuTraversal.MaxWorldNameCharacters)
        {
            error = $"Limit: {MenuTraversal.MaxWorldNameCharacters} characters";
            return false;
        }
        if (string.Equals(worldName, TutorialWorldName, StringComparison.OrdinalIgnoreCase))
        {
            error = "This world name is reserved";
            return false;
        }
        foreach (char character in worldName)
        {
            if (char.IsControl(character) || Path.GetInvalidFileNameChars().Contains(character))
            {
                error = "World names cannot contain control characters or file-name symbols.";
                return false;
            }
        }

        return true;
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
