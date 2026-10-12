using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class ModelArtReviewTests
{
    [UnityTest]
    public IEnumerator ReviewSceneEntersPlayModeWithImportedModelsAndSupportedMaterials()
    {
#if UNITY_EDITOR
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ModelArtReview.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        yield return null;
        Assert.That(Camera.main,Is.Not.Null);
        Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length,Is.GreaterThan(70));
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            foreach(var material in renderer.sharedMaterials)
            {
                Assert.That(material,Is.Not.Null);
                Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.shader.isSupported,Is.True);
            }
        LogAssert.NoUnexpectedReceived();
#else
        Assert.Ignore("Art review scene is an Editor review asset, excluded from the game build.");
        yield break;
#endif
    }
}
