using System;
using System.Collections.Generic;

[Serializable]
public class BlockData
{
    public string blockName;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW; 
}

[Serializable]
public class SaveData
{
    public List<BlockData> blocks = new List<BlockData>();
}