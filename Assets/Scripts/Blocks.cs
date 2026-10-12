using UnityEngine;

public class Block : MonoBehaviour
{

    public float durabilitySeconds;
    public ParticleSystem breakingParticlesPrefab;
    public bool breakable = true;
    public float placementGridSize = 1f;
    public bool isGround;

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