using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;

// Use the exact names his Gemini suggested so his code can read your files
[System.Serializable]
public class BlockData {
    public string blockName;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
}

[System.Serializable]
public class SaveData {
    public List<BlockData> blocks = new List<BlockData>();
}

public class ProjectSaveManager : MonoBehaviour {
    public TMP_InputField nameInput;

    public void SaveNewProject() {
        string projectName = nameInput.text;
        if (string.IsNullOrEmpty(projectName)) projectName = "NewProject";
        
        // Use .json if that's what his loading logic expects
        string directoryPath = Path.Combine(Application.persistentDataPath, "SavedProjects");
        string fullPath = Path.Combine(directoryPath, projectName + ".json");

        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        SaveData data = new SaveData();
        // Use FindObjectsByType (the newer version of FindObjectsOfType)
        Block[] allBlocks = GameObject.FindObjectsByType<Block>(FindObjectsSortMode.None);

        foreach (Block b in allBlocks) {
            data.blocks.Add(new BlockData {
                blockName = b.name.Replace("(Clone)", "").Trim(),
                posX = b.transform.position.x,
                posY = b.transform.position.y,
                posZ = b.transform.position.z,
                rotX = b.transform.rotation.x,
                rotY = b.transform.rotation.y,
                rotZ = b.transform.rotation.z,
                rotW = b.transform.rotation.w
            });
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(fullPath, json);
        Debug.Log("Saved to: " + fullPath);
    }
}