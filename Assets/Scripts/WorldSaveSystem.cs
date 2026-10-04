using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WorldSaveSystem : MonoBehaviour
{
    [Serializable]
    public class SavedBlock
    {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    private class SavedWorld
    {
        public string worldName;
        public List<SavedBlock> blocks = new List<SavedBlock>();
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
        SavedWorld save = new SavedWorld { worldName = WorldName };
        foreach (Block block in FindObjectsByType<Block>(FindObjectsSortMode.None))
        {
            save.blocks.Add(new SavedBlock
            {
                prefabName = GetPrefabName(block.gameObject),
                position = block.transform.position,
                rotation = block.transform.rotation
            });
        }

        Directory.CreateDirectory(SaveDirectory);
        File.WriteAllText(GetSavePath(WorldName), JsonUtility.ToJson(save, true));
        savePending = false;
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

    private IEnumerator LoadAfterWorldGeneration(string filePath)
    {
        yield return null;

        SavedWorld save;
        try
        {
            save = JsonUtility.FromJson<SavedWorld>(File.ReadAllText(filePath));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not load world '{WorldName}': {exception.Message}");
            yield break;
        }

        foreach (Block block in FindObjectsByType<Block>(FindObjectsSortMode.None))
        {
            Destroy(block.gameObject);
        }

        yield return null;
        foreach (SavedBlock savedBlock in save.blocks)
        {
            GameObject prefab = FindPrefab(savedBlock.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"No registered prefab named '{savedBlock.prefabName}' was found.");
                continue;
            }

            Instantiate(prefab, savedBlock.position, savedBlock.rotation, worldRoot);
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