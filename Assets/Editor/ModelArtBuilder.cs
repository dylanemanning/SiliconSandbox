using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Art-only import and verification. Does not replace the team's gameplay prefabs.
public static class ModelArtBuilder
{
    public static readonly string[] Names = {
        "BUFFER_gate", "NOT_gate", "AND_gate", "NAND_gate", "OR_gate", "NOR_gate", "XOR_gate", "XNOR_gate",
        "constant_signal_off", "constant_signal_on", "clock_off", "clock_on",
        "wire_straight_off", "wire_straight_on", "wire_elbow_off", "wire_elbow_on", "led_off", "led_on"
    };
    const string Root = "Assets/Models/";


    [MenuItem("SiliconSandbox/Art/Import and verify approved models")]
    public static void BuildAndVerify()
    {
        Directory.CreateDirectory(Root + "Materials");
        Directory.CreateDirectory(Root + "Prefabs");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[]{Root + "Textures"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp; ti.sRGBTexture = true; ti.SaveAndReimport();
        }
        foreach (string name in Names)
        {
            string path = Root + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true;
            importer.bakeAxisConversion = true; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var material in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())
            {
                var native = MakeMaterial(material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), native);
            }
            importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try { PrefabUtility.SaveAsPrefabAsset(instance, Root + "Prefabs/" + name + ".prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        VerifyAll();
        BuildGallery();
        AssetDatabase.SaveAssets();
        Debug.Log("MODEL_ART_BUILD_PASS: 18 models, native URP materials, prefabs and review scene");
    }

    static Material MakeMaterial(string name)
    {
        string path = Root + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool fresh = material == null;
        if (fresh) material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        Color color = new Color(.43f,.48f,.52f);
        switch (name)
        {
            case "SS_edge": color = new Color(.29f,.34f,.38f); break;
            case "SS_panel": color = new Color(.065f,.09f,.12f); break;
            case "SS_rim": color = new Color(.19f,.25f,.29f); break;
            case "SS_ink": color = new Color(.9f,.94f,.92f); break;
            case "SS_zero": color = new Color(.2f,.4f,.7f); break;
            case "SS_one": color = new Color(.1f,.95f,.2f); break;
            case "SS_PinMetal": color = new Color(.65f,.69f,.71f); break;
            case "SS_FrostedLED": color = new Color(.65f,.71f,.72f,.4f); break;
            case "SS_YellowLED": color = new Color(.95f,.65f,.035f); break;
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .28f);
        material.SetFloat("_Metallic", 0);
        string textureName = null;
        if (name.StartsWith("SS_ConstantSignal_")) textureName = "source-symbol-atlas";
        else if (name.EndsWith("_Panel")) textureName = name.Substring(3, name.Length - 9);
        if (textureName != null)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + textureName + ".png");
            if (texture == null) throw new Exception("Missing symbol texture: " + textureName);
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
        }
        if (name == "SS_YellowLED")
        {
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 3);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (name == "SS_FrostedLED")
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
        }
        if (fresh) AssetDatabase.CreateAsset(material, path);
        EditorUtility.SetDirty(material); return material;
    }

    [MenuItem("SiliconSandbox/Art/Verify model geometry")]
    public static void VerifyAll()
    {
        var lines = new List<string>{"Unity " + Application.unityVersion, "Local axes: +Z output, +X right, +Y up; one unit = one block."};
        foreach (string name in Names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".fbx");
            if (prefab == null) throw new Exception("Missing FBX: " + name);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                bool large = name.EndsWith("_gate") && name != "NOT_gate" && name != "BUFFER_gate";
                var body = renderers.FirstOrDefault(r=>r.name.EndsWith("__Body"));
                if (body != null) Near(body.bounds.size, large ? new Vector3(2,1,2) : Vector3.one, name + " body");
                var pins = renderers.Where(r=>r.name.Contains("__Pin_")).ToArray();
                int expected = name.StartsWith("wire_") ? 0 : name.StartsWith("led_") ? 6 : large ? 3 : 2;
                if (pins.Length != expected) throw new Exception(name + " wrong pin count " + pins.Length);
                foreach (var pin in pins)
                {
                    Vector3 center = pin.bounds.center;
                    string key = pin.name.Substring(pin.name.LastIndexOf("__Pin_",StringComparison.Ordinal)+6);
                    Vector3 point, normal;
                    if (name.EndsWith("_gate"))
                    {
                        normal = key == "Y" ? Vector3.forward : Vector3.back;
                        point = new Vector3(large ? (key == "B" ? .5f : -.5f) : 0, 0, (key == "Y" ? 1 : -1) * (large ? 1 : .5f));
                    }
                    else if (name.StartsWith("led_"))
                    {
                        normal = key == "WEST" ? Vector3.back : key == "EAST" ? Vector3.forward :
                                 key == "SOUTH" ? Vector3.left : key == "NORTH" ? Vector3.right :
                                 key == "TOP" ? Vector3.up : Vector3.down;
                        point = normal * .5f;
                    }
                    else { normal = key == "OUT_W" ? Vector3.back : Vector3.forward; point = normal * .5f; }
                    Near(center, point + normal * .02125f, name + " " + key);
                    Vector3 size = pin.bounds.size;
                    for (int axis = 0; axis < 3; axis++)
                        if (Mathf.Abs(normal[axis]) < .1f && Mathf.Abs(size[axis] - .33f) > .002f)
                            throw new Exception(name + " incorrect pin diameter " + size);
                }
                if (name.StartsWith("wire_straight")) Near(renderers.Single().bounds.size, new Vector3(.3f,.3f,1), name);
                foreach (var r in renderers)
                    if (r.sharedMaterials.Any(m=>m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                        throw new Exception(name + " material not remapped to native URP Lit");
                lines.Add("PASS " + name + ": " + expected + " pins; geometry/materials verified");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var led = AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/SS_YellowLED.mat");
        if (!led.IsKeywordEnabled("_EMISSION") || led.GetColor("_EmissionColor").maxColorComponent <= 1)
            throw new Exception("LED emission missing");
        Directory.CreateDirectory("art/review");File.WriteAllLines("art/review/unity-model-verification.txt", lines);
        Debug.Log("MODEL_GEOMETRY_PASS\n"+string.Join("\n",lines));
    }
    static void Near(Vector3 actual, Vector3 expected, string label)
    {
        if ((actual-expected).sqrMagnitude > .00001f)
            throw new Exception(label + " expected " + expected + ", got " + actual);
    }

    static void BuildGallery()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
        for (int i=0;i<Names.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/"+Names[i]+".prefab"));
            go.transform.position=new Vector3((i%4)*3.3f,.5f,(i/4)*3.6f);
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Review floor";
        floor.transform.position=new Vector3(5,-.08f,7);floor.transform.localScale=new Vector3(3,1,3);
        var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));ground.SetColor("_BaseColor",new Color(.065f,.09f,.12f));
        string groundPath=Root+"Materials/ReviewFloor.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(groundPath)==null) AssetDatabase.CreateAsset(ground,groundPath);
        else UnityEngine.Object.DestroyImmediate(ground);
        floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(groundPath);
        var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;
        light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-35,0);
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;
        fill.intensity=.75f;fill.transform.rotation=Quaternion.Euler(30,145,0);
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(18,22,-17);camera.transform.LookAt(new Vector3(5,.5f,7));
        camera.orthographic=true;camera.orthographicSize=10;camera.allowHDR=true;
        camera.backgroundColor=new Color(.045f,.055f,.065f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var volume=new GameObject("LED glow review").AddComponent<Volume>();volume.isGlobal=true;
        string profilePath=Root+"Materials/ModelReviewBloom.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath); }
        if (!profile.TryGet<Bloom>(out var bloom)) { bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile); }
        bloom.threshold.Override(1);bloom.intensity.Override(.35f);volume.sharedProfile=profile;
        EditorUtility.SetDirty(profile);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/ModelArtReview.unity");
        Render(camera,"art/review/unity-model-gallery.png",1800,1200);
        camera.transform.position=new Vector3(10,4,7.8f);camera.transform.LookAt(new Vector3(1.65f,.5f,14.4f));
        camera.orthographicSize=2.6f;Render(camera,"art/review/unity-led-materials.png",1400,900);
    }
    static void Render(Camera camera,string path,int width,int height)
    {
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf);
        camera.targetTexture=rt;
        // Warm the SRP post-processing resources before capturing the review frame.
        for (int i=0;i<4;i++) camera.Render();
        RenderTexture.active=rt;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
        RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(texture);
    }
}
