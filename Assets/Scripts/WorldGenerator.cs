using UnityEngine;

public class FlatWorldGenerator : MonoBehaviour
{
    public GameObject cubePrefab;
    public int width = 50;
    public int depth = 50;
    public float cubeSize = 1.0f;

    void Start()
    {
        WorldSaveSystem.GetOrCreate(new[] { cubePrefab }, transform);
        GenerateWorld();
    }

    void GenerateWorld()
    {
        GameObject ground = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity, transform);

        Vector3 scale = ground.transform.localScale;
        scale.x *= width * 2 * cubeSize;
        scale.z *= depth * 2 * cubeSize;
        ground.transform.localScale = scale;

        Block groundBlock = ground.GetComponent<Block>();
        if (groundBlock != null)
        {
            groundBlock.isGround = true;
            groundBlock.breakable = false;
            groundBlock.placementGridSize = cubeSize;
        }
    }
}
