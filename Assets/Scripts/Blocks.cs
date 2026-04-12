using UnityEngine;

public class Block : MonoBehaviour
{

    public float durabilitySeconds;
    public ParticleSystem breakingParticlesPrefab;

    ParticleSystem breakingParticles;

    float lastBreakProgress;

    private void Update() {
        
        if (breakingParticles) {

            if (Time.time > lastBreakProgress + .1f) { Destroy(breakingParticles); }

        }

    }

    public bool TryBreak(float breakSeconds) {

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

        if (breakingParticles) { Destroy(breakingParticles); }
       
        Destroy(gameObject);

    }

}