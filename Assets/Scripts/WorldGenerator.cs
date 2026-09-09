using UnityEngine;

public class FlatWorldGenerator : MonoBehaviour
{
    public GameObject cubePrefab;
    public int width = 50;
    public int depth = 50;
    public float cubeSize = 1.0f;

    void Start()
    {
        GenerateWorld();
    }

    void GenerateWorld()
    {
        for (int x = (width*-1); x < width; x++)
        {
            for (int z = (width*-1); z < depth; z++)
            {
                Vector3 position = new Vector3(x * cubeSize, 0, z * cubeSize);
                Instantiate(cubePrefab, position, Quaternion.identity, transform);
            }
        }
    }
}
