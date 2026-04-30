using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;

// REMOVED: Duplicate BlockData and SaveData classes that were here.

public class ProjectSaveManager : MonoBehaviour {
    public TMP_InputField nameInput;

    public void SaveNewProject() {
        string projectName = nameInput.text;
        if (string.IsNullOrEmpty(projectName)) projectName = "NewProject";
        
        string directoryPath = Path.Combine(Application.persistentDataPath, "SavedProjects");
        string fullPath = Path.Combine(directoryPath, projectName + ".json");

        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        PlayerPrefs.SetString("CurrentProjectToLoad", projectName);
        PlayerPrefs.Save();

        SaveData data = new SaveData();
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
        Debug.Log("Local File API Success: Saved to " + fullPath);
    }
}