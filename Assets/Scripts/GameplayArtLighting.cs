using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Apply the same ambient probe in the Editor and a player build.
public class GameplayArtLighting : MonoBehaviour
{
    private void Awake()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.48f,.48f,.48f);
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(new Color(.24f,.24f,.24f));
        RenderSettings.ambientProbe = ambient;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
