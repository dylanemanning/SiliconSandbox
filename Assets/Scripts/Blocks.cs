using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour
{
    private static readonly Dictionary<Vector3Int, Block> occupiedCells = new Dictionary<Vector3Int, Block>();

    public float durabilitySeconds;
    public ParticleSystem breakingParticlesPrefab;
    public bool breakable = true;
    public float placementGridSize = 1f;
    public Vector3Int footprintSize = Vector3Int.one;
    public bool isGround;
    // Root is the local (0,0,0) anchor cell; visuals/collider may span more cells.
    public Vector3Int footprint = Vector3Int.one;

    public IEnumerable<Vector3Int> OccupiedCells(Vector3Int anchor, Quaternion rotation)
    {
        for (int x=0; x<Mathf.Max(1,footprint.x); x++)
        for (int y=0; y<Mathf.Max(1,footprint.y); y++)
        for (int z=0; z<Mathf.Max(1,footprint.z); z++)
            yield return anchor + Vector3Int.RoundToInt(rotation * new Vector3(x,y,z));
    }

    ParticleSystem breakingParticles;

    float lastBreakProgress;

    private void Awake()
    {
        if (isGround) breakable = false;
    }

    private void OnEnable()
    {
        RegisterCells();
    }

    private void OnDisable()
    {
        UnregisterCells();
    }

    private void Update() {
        
        if (breakingParticles) {

            if (Time.time > lastBreakProgress + .1f) { Destroy(breakingParticles); }

        }

    }

    public Vector3Int GetFootprintSize()
    {
        return new Vector3Int(
            Mathf.Max(1, Mathf.Abs(footprintSize.x)),
            Mathf.Max(1, Mathf.Abs(footprintSize.y)),
            Mathf.Max(1, Mathf.Abs(footprintSize.z)));
    }

    public Vector3Int GetCellOriginAt(Vector3 worldPosition)
    {
        Vector3Int footprint = GetFootprintSize();
        Vector3 halfExtent = new Vector3(
            (footprint.x - 1) * 0.5f,
            (footprint.y - 1) * 0.5f,
            (footprint.z - 1) * 0.5f);

        return Vector3Int.RoundToInt(worldPosition - halfExtent);
    }

    public IEnumerable<Vector3Int> GetOccupiedCellsAt(Vector3 worldPosition)
    {
        Vector3Int origin = GetCellOriginAt(worldPosition);
        Vector3Int footprint = GetFootprintSize();
        for (int x = 0; x < footprint.x; x++)
        {
            for (int y = 0; y < footprint.y; y++)
            {
                for (int z = 0; z < footprint.z; z++)
                {
                    yield return origin + new Vector3Int(x, y, z);
                }
            }
        }
    }

    public static bool CanPlaceAt(Vector3 worldPosition, Block blockPrefab)
    {
        if (blockPrefab == null) return false;

        foreach (Vector3Int occupiedCell in blockPrefab.GetOccupiedCellsAt(worldPosition))
        {
            if (occupiedCells.TryGetValue(occupiedCell, out Block existing) && existing != blockPrefab)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsCellOccupied(Vector3Int cell, Block ignoreBlock = null)
    {
        return occupiedCells.TryGetValue(cell, out Block existing) && existing != ignoreBlock;
    }

    public void RegisterCells()
    {
        foreach (Vector3Int cell in GetOccupiedCellsAt(transform.position))
        {
            occupiedCells[cell] = this;
        }
    }

    public void UnregisterCells()
    {
        foreach (Vector3Int cell in GetOccupiedCellsAt(transform.position))
        {
            if (occupiedCells.TryGetValue(cell, out Block existing) && existing == this)
            {
                occupiedCells.Remove(cell);
            }
        }
    }

    public bool TryBreak(float breakSeconds) {

        if (!breakable) return false;

        lastBreakProgress = Time.time;

        if (!breakingParticles && breakingParticlesPrefab) {

            breakingParticles = Instantiate(breakingParticlesPrefab);

            breakingParticles.transform.position = transform.position;

        }

        if (breakSeconds > durabilitySeconds) {

            Break();

            return true;

        }

        return false;

    }

    public void Break() {

        if (!breakable) return;

        if (breakingParticles) { Destroy(breakingParticles); }
        WireTopology.Detach(GetComponent<LogicWireBlock>());
        UnregisterCells();
        WorldSaveSystem.Instance?.MarkDirty();
       
        Destroy(gameObject);

    }

}
