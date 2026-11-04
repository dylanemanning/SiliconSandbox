using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GameplayModelIntegration
{
    const string Art = "Assets/Models/Prefabs/";
    const string Materials = "Assets/Models/Materials/";
    const string GameplayProfile = "Assets/Settings/GameplayAppearance.asset";
    static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");

    [MenuItem("SiliconSandbox/Art/Integrate approved models into gameplay")]
    public static void Integrate()
    {
        foreach (string gate in new[]{"AND","OR","NOT","NAND","NOR","XOR","XNOR","BUFFER"})
            UpdatePrefab(gate+" Gate", gate+"_gate", null, gate!="NOT" && gate!="BUFFER");
        UpdatePrefab("Voltage Source","constant_signal_off","constant_signal_on",false);
        UpdatePrefab("Ground Source","constant_signal_off","constant_signal_on",false);
        UpdatePrefab("Viewer Block","led_off","led_on",false);
        UpdatePrefab("Wires","wire_straight_off",null,false);
        string clockPath="Assets/Prefabs/Clock.prefab";
        if (!File.Exists(clockPath))
        {
            var clock=new GameObject("Clock");clock.AddComponent<Block>().durabilitySeconds=.05f;
            clock.AddComponent<LogicClockSource>();
            PrefabUtility.SaveAsPrefabAsset(clock,clockPath);UnityEngine.Object.DestroyImmediate(clock);
        }
        UpdatePrefab("Clock","clock_off","clock_on",false);
        AssetDatabase.CopyAsset(Materials+"ReviewFloor.mat","Assets/Settings/GameplayFloor.mat");
        var ground=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Block.prefab");
        try
        {
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/GameplayFloor.mat");
            var atlas=ground.GetComponent<BlockTexture>();if(atlas!=null) UnityEngine.Object.DestroyImmediate(atlas);
            ground.transform.localPosition=Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(ground,"Assets/Prefabs/Block.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(ground); }
        if (!File.Exists(GameplayProfile)) AssetDatabase.CopyAsset(Materials+"ModelReviewBloom.asset",GameplayProfile);
        // Update an existing gameplay copy too, preserving its GUID.
        var target=AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameplayProfile);
        var source=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Materials+"ModelReviewBloom.asset");
        foreach(var component in target.components.ToArray())
            UnityEngine.Object.DestroyImmediate(component,true);
        target.components.Clear();
        foreach(var component in source.components)
        {
            var clone=UnityEngine.Object.Instantiate(component);
            target.components.Add(clone);AssetDatabase.AddObjectToAsset(clone,target);
        }
        EditorUtility.SetDirty(target);
        ConfigureScene();AssetDatabase.SaveAssets();
        Verify();CaptureGameplay();
        Debug.Log("GAMEPLAY_MODEL_INTEGRATION_PASS: prefabs, footprint ports, gameplay lighting and build scene verified.");
    }

    static GameObject Model(string name, Transform parent)
    {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".prefab"));
        instance.transform.SetParent(parent,false);return instance;
    }

    static void UpdatePrefab(string name,string low,string high,bool large)
    {
        string path="Assets/Prefabs/"+name+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            for(int i=root.transform.childCount-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach(var component in root.GetComponents<Component>())
                if(component is Renderer || component is MeshFilter || component is Collider || component is BlockTexture
                    || component is BlockModelVisual || component is WireModelVisual)
                    UnityEngine.Object.DestroyImmediate(component);
            root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(root,0);
            var block=root.GetComponent<Block>();block.footprint=large?new Vector3Int(2,1,2):Vector3Int.one;
            block.breakable=true;block.isGround=false;
            var collider=root.AddComponent<BoxCollider>();collider.size=block.footprint;
            collider.center=large?new Vector3(.5f,0,.5f):Vector3.zero;
            var lo=Model(low,root.transform);lo.transform.localPosition=collider.center;
            if(name=="Wires")
            {
                var visual=root.AddComponent<WireModelVisual>();visual.straightModel=lo;
                visual.elbowModel=Model("wire_elbow_off",root.transform);visual.elbowModel.SetActive(false);
                visual.branches=new GameObject[6];
                for(int i=0;i<6;i++)
                {
                    var branch=Model("wire_straight_off",root.transform);branch.name="Wire branch "+i;
                    branch.transform.localScale=new Vector3(1,1,.5f);branch.SetActive(false);visual.branches[i]=branch;
                }
                visual.lowMaterial=Material("SS_zero");visual.highMaterial=Material("SS_one");
                var wire=root.GetComponent<LogicWireBlock>();
                var serialized=new SerializedObject(wire);
                serialized.FindProperty("targetRenderer").objectReferenceValue=null;
                serialized.FindProperty("unpoweredMaterial").objectReferenceValue=visual.lowMaterial;
                serialized.FindProperty("poweredMaterial").objectReferenceValue=visual.highMaterial;
                serialized.FindProperty("directionVisuals").arraySize=0;serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var visual=root.AddComponent<BlockModelVisual>();
                if(high!=null)
                {
                    var hi=Model(high,root.transform);hi.transform.localPosition=collider.center;
                    visual.lowModel=lo;visual.highModel=hi;
                    bool startsHigh=name=="Voltage Source";lo.SetActive(!startsHigh);hi.SetActive(startsHigh);
                }
                else
                {
                    visual.outputPin=lo.GetComponentsInChildren<Renderer>(true).Single(r=>r.name.Contains("__Pin_Y"));
                    visual.lowMaterial=Material("SS_PinMetal");visual.highMaterial=Material("SS_one");
                    visual.outputPin.sharedMaterial=visual.lowMaterial;
                    var gate=root.GetComponent<GateBlock>();
                    var serialized=new SerializedObject(gate);
                    serialized.FindProperty("outputIndicator").objectReferenceValue=visual.outputPin;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureScene()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.24f,.24f,.24f));RenderSettings.ambientProbe=ambient;
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(light.gameObject);
        var key=new GameObject("Gameplay key light").AddComponent<Light>();
        key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(45,-35,0);
        key.shadows=LightShadows.Soft;key.shadowStrength=.65f;key.shadowBias=.025f;key.shadowNormalBias=.08f;
        var fill=new GameObject("Gameplay fill light").AddComponent<Light>();
        fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(30,145,0);
        foreach(var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(volume.gameObject);
        var style=new GameObject("Approved gameplay appearance");style.AddComponent<GameplayArtLighting>();
        var global=style.AddComponent<Volume>();global.isGlobal=true;
        global.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameplayProfile);
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.065f,.09f,.12f).gamma;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        }
        // The old slots all referenced the floor cube while Player used a separate
        // index into its palette. Bind those same existing choices to the slots.
        var player=UnityEngine.Object.FindFirstObjectByType<Player>();
        var hotbar=new SerializedObject(player.hotbar);
        var slots=hotbar.FindProperty("slots");
        for(int i=0;i<slots.arraySize && i<player.blockPalette.Length;i++)
        {
            var slot=new SerializedObject(slots.GetArrayElementAtIndex(i).objectReferenceValue);
            slot.FindProperty("blockPrefab").objectReferenceValue=player.blockPalette[i];
            slot.ApplyModifiedPropertiesWithoutUndo();
        }
        // Register the full prefab catalog for save/load, without changing hotbar choices.
        var save=UnityEngine.Object.FindFirstObjectByType<WorldSaveSystem>();
        if(save==null) save=new GameObject("WorldSaveSystem").AddComponent<WorldSaveSystem>();
        save.RegisterPrefabs(AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"})
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        EditorSceneManager.SaveScene(scene);
    }

    public static void Verify()
    {
        foreach(string gateName in new[]{"AND","OR","NOT","NAND","NOR","XOR","XNOR","BUFFER"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+gateName+" Gate.prefab");
            var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var gate=root.GetComponent<GateBlock>();var box=root.GetComponent<BoxCollider>();
                if(root.transform.localScale!=Vector3.one || box.size!=(Vector3)root.GetComponent<Block>().footprint)
                    throw new Exception("Invalid gameplay footprint: "+gateName);
                foreach(var port in gate.WorldPorts())
                {
                    string suffix=port.port.Role==PortRole.Output?"Y":port.port.InputIndex==0?"A":"B";
                    var pin=root.GetComponentsInChildren<Renderer>(true).Single(r=>r.name.Contains("__Pin_"+suffix));
                    Vector3 expected=port.cell+(Vector3)port.face*(.5f+.02125f);
                    if(Vector3.Distance(pin.bounds.center,expected)>.002f) throw new Exception("Model/port mismatch: "+gateName+" "+suffix);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        if(!EditorBuildSettings.scenes.Any(s=>s.enabled && s.path=="Assets/Scenes/SampleScene.unity"))
            throw new Exception("Gameplay scene is missing from build settings.");
    }

    static void CaptureGameplay()
    {
        var camera=Camera.main;
        var instances=new System.Collections.Generic.List<GameObject>();
        string[] names={"Voltage Source","Ground Source","Wires","Viewer Block","AND Gate","NOT Gate","Clock"};
        for(int i=0;i<names.Length;i++)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+names[i]+".prefab"));
            go.transform.position=new Vector3((i%4)*3.5f,1,(i/4)*4);instances.Add(go);
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=new Vector3(4,1,4);
        floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/GameplayFloor.mat");
        camera.orthographic=true;camera.orthographicSize=8;
        camera.transform.SetParent(null);camera.transform.position=new Vector3(16,15,-12);camera.transform.LookAt(new Vector3(5,0,2));
        var render=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGBHalf);camera.targetTexture=render;
        var old=RenderTexture.active;camera.Render();RenderTexture.active=render;
        var texture=new Texture2D(1600,1100,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1100),0,0);texture.Apply();
        Directory.CreateDirectory("art/gameplay-review");File.WriteAllBytes("art/gameplay-review/gameplay-models.png",texture.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(texture);
        foreach(var go in instances) UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(floor);
        // Reopen saved gameplay: the capture staging is never included in the scene.
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }

    public static void BuildMac()
    {
        var result=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            "Builds/GameplayIntegration/SiliconSandbox.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("macOS gameplay build failed: "+result.summary.result);
        Debug.Log("GAMEPLAY_MAC_BUILD_PASS");
    }
}
