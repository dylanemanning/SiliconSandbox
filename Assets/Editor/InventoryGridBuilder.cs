using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public static class InventoryGridBuilder
{
    public const string PrefabPath = "Assets/Prefabs/InventoryGrid.prefab";
    public const float SlotSize = 80f;
    public const float Gap = SlotSize*.1f;
    public const int Columns=9, Rows=4;

    [MenuItem("SiliconSandbox/UI/Build empty inventory grid")]
    public static void Build()
    {
        var root=new GameObject("Inventory",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var backdrop=Image("Inventory overlay",root.transform,new Color(0,0,0,.45f));
        backdrop.rectTransform.anchorMin=Vector2.zero;backdrop.rectTransform.anchorMax=Vector2.one;
        backdrop.rectTransform.offsetMin=Vector2.zero;backdrop.rectTransform.offsetMax=Vector2.zero;
        var panel=Image("Inventory panel",backdrop.transform,new Color(.14f,.16f,.19f,1));
        panel.rectTransform.sizeDelta=new Vector2(Columns*SlotSize+(Columns-1)*Gap+48,Rows*SlotSize+(Rows-1)*Gap+124);
        var title=Label("Title","Inventory",panel.transform,30,new Color(.94f,.95f,.97f));
        title.rectTransform.anchoredPosition=new Vector2(0,panel.rectTransform.sizeDelta.y/2-32);
        var footer=Label("Close hint","E / Esc to close",panel.transform,18,new Color(.68f,.72f,.77f));
        footer.rectTransform.anchoredPosition=new Vector2(0,-panel.rectTransform.sizeDelta.y/2+28);
        var grid=new GameObject("Slots",typeof(RectTransform),typeof(GridLayoutGroup));grid.transform.SetParent(panel.transform,false);
        var rect=grid.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(Columns*SlotSize+(Columns-1)*Gap,Rows*SlotSize+(Rows-1)*Gap);
        var layout=grid.GetComponent<GridLayoutGroup>();layout.cellSize=Vector2.one*SlotSize;layout.spacing=Vector2.one*Gap;
        layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=Columns;
        for(int row=0;row<Rows;row++) for(int column=0;column<Columns;column++)
        {
            var slot=Image($"Slot {row+1},{column+1}",grid.transform,new Color(.42f,.46f,.51f,1));slot.raycastTarget=false;
            var inside=Image("Empty",slot.transform,new Color(.08f,.10f,.13f,1));inside.raycastTarget=false;
            inside.rectTransform.anchorMin=Vector2.zero;inside.rectTransform.anchorMax=Vector2.one;
            inside.rectTransform.offsetMin=Vector2.one*3;inside.rectTransform.offsetMax=-Vector2.one*3;
        }
        var inventory=root.AddComponent<InventoryGrid>();var serialized=new SerializedObject(inventory);
        serialized.FindProperty("panel").objectReferenceValue=backdrop.gameObject;serialized.ApplyModifiedPropertiesWithoutUndo();
        backdrop.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach(var old in UnityEngine.Object.FindObjectsByType<InventoryGrid>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("INVENTORY_GRID_BUILD_PASS: 36 empty square slots, 9 columns, 4 rows, 10% gaps, gameplay scene reference.");
    }

    static UnityEngine.UI.Image Image(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;return image;
    }
    static TextMeshProUGUI Label(string name,string text,Transform parent,float size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=TMP_Settings.defaultFontAsset;label.text=text;
        label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.rectTransform.sizeDelta=new Vector2(500,44);return label;
    }

    public static void BuildMac()
    {
        var scenes=Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path);
        var report=BuildPipeline.BuildPlayer(scenes,"Builds/InventoryGrid/SiliconSandbox.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Inventory build failed.");
        Debug.Log("INVENTORY_MAC_BUILD_PASS");
    }

    public static void Capture() => CaptureTo("art/inventory-review/inventory-grid.png");

    public static void CaptureTo(string path, bool showInventory = true)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var inventory=UnityEngine.Object.FindFirstObjectByType<InventoryGrid>();
        inventory.Panel.SetActive(showInventory);
        var camera=Camera.main;
        // Actual overlay UI is drawn after post-processing. Avoid grading baked
        // icon colors again when using a camera-space canvas for this capture.
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var target=new RenderTexture(1600,900,24);camera.targetTexture=target;
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay) continue;
            int order=canvas.sortingOrder;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
            canvas.planeDistance=.5f;canvas.sortingOrder=order;
        }
        Canvas.ForceUpdateCanvases();
        var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        // Capture-only canvas changes must not alter the saved gameplay scene.
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Debug.Log("INVENTORY_GRID_CAPTURE_PASS");
    }
}
