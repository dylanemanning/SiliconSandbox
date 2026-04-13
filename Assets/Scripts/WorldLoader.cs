using UnityEngine;
using System.IO;

public class WorldLoader : MonoBehaviour {
    // Drag your 'Block' prefab into this slot in the Unity Inspector
    public GameObject blockPrefab; 

    void Start() {
        if (PlayerPrefs.HasKey("CurrentProjectToLoad")) {
            LoadWorld(PlayerPrefs.GetString("CurrentProjectToLoad"));
            PlayerPrefs.DeleteKey("CurrentProjectToLoad");
        }
    }

    public void LoadWorld(string fileName) {
        string path = Path.Combine(Application.persistentDataPath, "SavedProjects", fileName + ".json");
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        foreach (var b in data.blocks) {
            Vector3 pos = new Vector3(b.posX, b.posY, b.posZ);
            Quaternion rot = new Quaternion(b.rotX, b.rotY, b.rotZ, b.rotW);
            
            // For now, since you only have one 'Block' prefab:
            Instantiate(blockPrefab, pos, rot);
        }
    }
}