using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour 
{
    public Player player; 

    public void Save(string profileName) 
    {
        SaveData data = new SaveData();
        
        
        Block[] allBlocks = Object.FindObjectsByType<Block>(FindObjectsSortMode.None);

        foreach (Block b in allBlocks)
        {
            BlockData bData = new BlockData
            {
                blockName = b.name.Replace("(Clone)", "").Trim(), 
                posX = b.transform.position.x,
                posY = b.transform.position.y,
                posZ = b.transform.position.z,
                rotX = b.transform.rotation.x,
                rotY = b.transform.rotation.y,
                rotZ = b.transform.rotation.z,
                rotW = b.transform.rotation.w
            };
            data.blocks.Add(bData);
        }

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.persistentDataPath, profileName + ".json");
        File.WriteAllText(path, json);
        
        Debug.Log("Saved to: " + path);
    }

    public void Load(string profileName) 
    {
        string path = Path.Combine(Application.persistentDataPath, profileName + ".json");
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        foreach (var b in data.blocks)
        {
            Vector3 pos = new Vector3(b.posX, b.posY, b.posZ);
            Quaternion rot = new Quaternion(b.rotX, b.rotY, b.rotZ, b.rotW);
            
            foreach (var prefab in player.blockPalette)
            {
                if (prefab.name == b.blockName)
                {
                    Instantiate(prefab, pos, rot);
                    break;
                }
            }
        }
    }
}