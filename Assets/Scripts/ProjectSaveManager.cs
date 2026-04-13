using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class BlockData {
    public string blockName;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
}

[System.Serializable]
public class SaveData {
    public System.Collections.Generic.List<BlockData> blocks = new System.Collections.Generic.List<BlockData>();
}

public class ProjectSaveManager : MonoBehaviour {
    public TMP_InputField nameInput;

    public void SaveNewProject() {
        string projectName = nameInput.text;
        if (string.IsNullOrEmpty(projectName)) projectName = "NewProject";
        
        // Save to the hidden Mac Library folder we found earlier
        string directoryPath = Path.Combine(Application.persistentDataPath, "SavedProjects");
        string fullPath = Path.Combine(directoryPath, projectName + ".json");

        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        SaveData data = new SaveData();
        // Use the 'Block' class from your teammate's Blocks.cs file
        Block[] allBlocks = GameObject.FindObjectsByType<Block>(FindObjectsSortMode.None);

        foreach (Block b in allBlocks) {
            data.blocks.Add(new BlockData {
                // This cleans the name so 'Block(Clone)' becomes 'Block'
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
        Debug.Log("Local File API Success: Saved to " + fullPath);
    }
}