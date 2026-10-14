using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class InventoryBlockImageBuilder
{
    public const string IconFolder="Assets/UI/InventoryIcons";
    public const string CatalogPath="Assets/Inventory/DefaultInventoryCatalog.asset";
    public static readonly string[] Models={
        "BUFFER_gate","NOT_gate","AND_gate","NAND_gate","OR_gate","NOR_gate","XOR_gate","XNOR_gate",
        "constant_signal_on","clock_off","wire_straight_off","led_on","building_block"
    };
    public static readonly string[] Labels={
        "BUFFER","NOT","AND","NAND","OR","NOR","XOR","XNOR","Constant Signal","Clock","Wire","LED","Building Block"
    };
    public static readonly string[] Prefabs={
        "BUFFER Gate","NOT Gate","AND Gate","NAND Gate","OR Gate","NOR Gate","XOR Gate","XNOR Gate",
        "Voltage Source","Clock","Wires","Viewer Block","Block"
    };
    public static readonly string[] ItemIds={
        "buffer","not","and","nand","or","nor","xor","xnor","constant_signal","clock","wire","led","building_block"
    };

    [MenuItem("SiliconSandbox/UI/Generate inventory block images")]
    public static void Build()
    {
        GenerateIcons();
        BuildCatalog();
        if(!File.Exists(InventoryGridBuilder.PrefabPath)) InventoryGridBuilder.Build();
        InstallImages();
        AssetDatabase.SaveAssets();
        CapturePreview();
        Debug.Log("INVENTORY_IMAGES_BUILD_PASS: 13 catalog entries, top-down block sprites and an angled wire sprite, 23 empty slots.");
    }

    static void GenerateIcons()
    {
        Directory.CreateDirectory(IconFolder);
        var oldScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        if(pipeline==null) throw new Exception("Inventory icons require the project URP pipeline.");
        // Preserve transparent pixels through tone mapping without changing the project asset.
        var serialized=new SerializedObject(pipeline);
        bool oldAlpha=serialized.FindProperty("m_AllowPostProcessAlphaOutput").boolValue;
        serialized.FindProperty("m_AllowPostProcessAlphaOutput").boolValue=true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.24f,.24f,.24f));RenderSettings.ambientProbe=ambient;
            var key=new GameObject("Icon key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;
            key.transform.rotation=Quaternion.Euler(45,-35,0);key.shadows=LightShadows.Soft;key.shadowStrength=.65f;
            key.shadowBias=.025f;key.shadowNormalBias=.08f;
            var fill=new GameObject("Icon fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;
            fill.transform.rotation=Quaternion.Euler(30,145,0);
            var volume=new GameObject("Icon grading").AddComponent<Volume>();volume.isGlobal=true;
            volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Models/Materials/ModelReviewBloom.asset");
            var camera=new GameObject("Icon camera").AddComponent<Camera>();camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.allowHDR=true;
            camera.nearClipPlane=.05f;camera.farClipPlane=100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            foreach(string modelName in Models)
            {
                var path=modelName=="building_block"?"Assets/Prefabs/Block.prefab":"Assets/Models/Prefabs/"+modelName+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null) throw new Exception("Missing model dependency: "+modelName);
                var model=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    if(modelName=="wire_straight_off")
                    {
                        camera.transform.position=bounds.center+new Vector3(4,6,-8).normalized*8;
                        camera.transform.LookAt(bounds.center);
                    }
                    else
                    {
                        // Put the +Z output on the right so gate symbols read left to right.
                        camera.transform.position=bounds.center+Vector3.up*8;
                        camera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.left);
                    }
                    float size=0;
                    for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                    {
                        var corner=Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                        size=Mathf.Max(size,Mathf.Abs(Vector3.Dot(corner,camera.transform.right)),Mathf.Abs(Vector3.Dot(corner,camera.transform.up)));
                    }
                    camera.orthographicSize=size*1.18f;
                    Render(camera,IconFolder+"/"+modelName+".png");
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }
        finally
        {
            serialized.Update();
            serialized.FindProperty("m_AllowPostProcessAlphaOutput").boolValue=oldAlpha;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if(!string.IsNullOrEmpty(oldScene)) EditorSceneManager.OpenScene(oldScene);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string name in Models)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(IconFolder+"/"+name+".png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;
            importer.SaveAndReimport();
        }
    }

    static void Render(Camera camera,string path)
    {
        var target=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGBHalf);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;
            for(int i=0;i<4;i++) camera.Render();
            RenderTexture.active=target;
            var image=new Texture2D(256,256,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
            var pixels=image.GetPixels32();
            if(pixels.Count(p=>p.a>200 && p.r>240 && p.g<10 && p.b>240)>pixels.Length/100)
                throw new Exception("Unsupported shader rendered magenta in icon: "+path);
            if(!pixels.Any(p=>p.a==0) || !pixels.Any(p=>p.a>200)) throw new Exception("Invalid icon alpha: "+path);
            File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target); }
    }

    public static void InstallImages()
    {
        var root=PrefabUtility.LoadPrefabContents(InventoryGridBuilder.PrefabPath);
        try
        {
            var grid=root.GetComponentInChildren<GridLayoutGroup>(true);
            for(int i=0;i<grid.transform.childCount;i++)
            {
                var slot=grid.transform.GetChild(i);
                var old=slot.Find("Block image");if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var view=slot.GetComponent<InventorySlotImage>();if(view==null)view=slot.gameObject.AddComponent<InventorySlotImage>();
                var icon=new GameObject("Block image",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));icon.transform.SetParent(slot,false);
                var image=icon.GetComponent<Image>();image.raycastTarget=false;
                image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;
                image.rectTransform.offsetMin=Vector2.one*6;image.rectTransform.offsetMax=-Vector2.one*6;
                var serialized=new SerializedObject(view);serialized.FindProperty("iconImage").objectReferenceValue=image;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var inventory=root.GetComponent<InventoryGrid>();
            var binding=new SerializedObject(inventory);
            binding.FindProperty("catalog").objectReferenceValue=AssetDatabase.LoadAssetAtPath<InventoryCatalog>(CatalogPath);
            binding.ApplyModifiedPropertiesWithoutUndo();inventory.RefreshImages();
            PrefabUtility.SaveAsPrefabAsset(root,InventoryGridBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }

    static void BuildCatalog()
    {
        Directory.CreateDirectory("Assets/Inventory");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog=AssetDatabase.LoadAssetAtPath<InventoryCatalog>(CatalogPath);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<InventoryCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
        var items=new InventoryItem[Models.Length];
        for(int i=0;i<items.Length;i++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+Prefabs[i]+".prefab");
            var icon=AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder+"/"+Models[i]+".png");
            if(prefab==null || icon==null) throw new Exception("Missing inventory catalog asset: "+Labels[i]);
            items[i]=new InventoryItem(ItemIds[i],Labels[i],prefab.GetComponent<Block>(),icon);
        }
        catalog.SetItems(items);EditorUtility.SetDirty(catalog);
    }

    public static void CapturePreview() => InventoryGridBuilder.CaptureTo("art/inventory-images-review/inventory-images.png");

    public static void CaptureAndBuildMac() { CapturePreview(); BuildMac(); }

    public static void RefreshIconsAndBuildMac()
    {
        GenerateIcons();
        AssetDatabase.SaveAssets();
        CapturePreview();
        BuildMac();
    }

    public static void BuildMac()
    {
        var scenes=Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path);
        var result=BuildPipeline.BuildPlayer(scenes,"Builds/InventoryImages/SiliconSandbox.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Inventory image player build failed.");
        Debug.Log("INVENTORY_IMAGES_MAC_BUILD_PASS");
    }
}
