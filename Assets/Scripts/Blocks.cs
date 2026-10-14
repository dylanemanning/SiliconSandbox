using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour
{

    public float durabilitySeconds;
    public ParticleSystem breakingParticlesPrefab;
    public bool breakable = true;
    public float placementGridSize = 1f;
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

    private void Update() {
        
        if (breakingParticles) {

            if (Time.time > lastBreakProgress + .1f) { Destroy(breakingParticles); }

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
        WorldSaveSystem.Instance?.MarkDirty();
       
        Destroy(gameObject);

    }

}