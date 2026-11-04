using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HotbarBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Hotbar.prefab";

    [MenuItem("SiliconSandbox/UI/Build inventory-style hotbar")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var oldManager = player.hotbar;
        if (oldManager == null || oldManager.SlotCount != 9) throw new Exception("Expected nine existing hotbar choices.");
        var choices = new Block[9];
        for (int i=0;i<9;i++) choices[i] = oldManager.GetSlot(i).BlockPrefab;
        var oldRow = oldManager.GetSlot(0).GetComponentInParent<HorizontalLayoutGroup>();
        var catalog = AssetDatabase.LoadAssetAtPath<InventoryCatalog>(InventoryBlockImageBuilder.CatalogPath);
        if (catalog == null) throw new Exception("Missing inventory catalog.");

        var root = new GameObject("Hotbar UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(HotbarManager));
        var canvas = root.GetComponent<Canvas>();canvas.renderMode = RenderMode.ScreenSpaceOverlay;canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);scaler.matchWidthOrHeight = .5f;
        float rowWidth = 9*InventoryGridBuilder.SlotSize+8*InventoryGridBuilder.Gap;
        var panel = Image("Hotbar panel",root.transform,new Color(.14f,.16f,.19f,1));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f,0);
        panel.rectTransform.pivot = new Vector2(.5f,0);
        panel.rectTransform.anchoredPosition = new Vector2(0,24);
        panel.rectTransform.sizeDelta = new Vector2(rowWidth+48,InventoryGridBuilder.SlotSize+48);
        var row = new GameObject("Slots",typeof(RectTransform),typeof(GridLayoutGroup));row.transform.SetParent(panel.transform,false);
        var rowRect = row.GetComponent<RectTransform>();rowRect.sizeDelta = new Vector2(rowWidth,InventoryGridBuilder.SlotSize);
        var layout = row.GetComponent<GridLayoutGroup>();layout.cellSize = Vector2.one*InventoryGridBuilder.SlotSize;
        layout.spacing = Vector2.one*InventoryGridBuilder.Gap;layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount = 9;
        var slots = new List<HotbarSlot>();
        for (int i=0;i<9;i++)
        {
            var frame = Image("Slot "+(i+1),row.transform,new Color(.42f,.46f,.51f,1));
            Stretch(Image("Interior",frame.transform,new Color(.08f,.10f,.13f,1)).rectTransform,3);
            var icon = Image("Block image",frame.transform,Color.white);Stretch(icon.rectTransform,6);
            var chip = Image("Number background",frame.transform,new Color(.08f,.10f,.13f,.95f));
            chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0,1);
            chip.rectTransform.pivot = new Vector2(0,1);chip.rectTransform.anchoredPosition = new Vector2(5,-5);
            chip.rectTransform.sizeDelta = new Vector2(18,20);
            var label = new GameObject("Slot number",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
            label.transform.SetParent(chip.transform,false);
            var text = label.GetComponent<TextMeshProUGUI>();text.font = TMP_Settings.defaultFontAsset;text.text = (i+1).ToString();
            text.fontSize = 18;text.color = new Color(.94f,.95f,.97f,1);text.alignment = TextAlignmentOptions.Center;text.raycastTarget = false;
            Stretch(text.rectTransform,0);
            var slot = frame.gameObject.AddComponent<HotbarSlot>();
            var binding = new SerializedObject(slot);
            binding.FindProperty("blockPrefab").objectReferenceValue = choices[i];
            binding.FindProperty("background").objectReferenceValue = frame;
            binding.FindProperty("blockIcon").objectReferenceValue = icon;
            binding.ApplyModifiedPropertiesWithoutUndo();
            slot.RefreshIcon(catalog);slot.SetSelected(i==0);slots.Add(slot);
        }
        var manager = root.GetComponent<HotbarManager>();
        var serialized = new SerializedObject(manager);serialized.FindProperty("catalog").objectReferenceValue = catalog;
        var entries = serialized.FindProperty("slots");entries.arraySize = slots.Count;
        for (int i=0;i<slots.Count;i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);
        if (oldRow != null) UnityEngine.Object.DestroyImmediate(oldRow.gameObject);
        if (oldManager != null) UnityEngine.Object.DestroyImmediate(oldManager.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        player.hotbar = instance.GetComponent<HotbarManager>();EditorUtility.SetDirty(player);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        CapturePreview();
        Debug.Log("HOTBAR_BUILD_PASS: nine inventory-style slots, framed panel, preserved placement choices and catalog images.");
    }

    private static Image Image(string name,Transform parent,Color color)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
        var image = go.GetComponent<Image>();image.color = color;image.raycastTarget = false;return image;
    }

    private static void Stretch(RectTransform rect,float padding)
    {
        rect.anchorMin = Vector2.zero;rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one*padding;rect.offsetMax = -Vector2.one*padding;
    }

    public static void CapturePreview()
    {
        InventoryGridBuilder.CaptureTo("art/hotbar-review/hotbar.png",false);
        InventoryGridBuilder.CaptureTo("art/hotbar-review/hotbar-and-inventory.png");
    }

    public static void BuildMac()
    {
        var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path);
        var report = BuildPipeline.BuildPlayer(scenes,"Builds/HotbarImages/SiliconSandbox.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Hotbar build failed.");
        Debug.Log("HOTBAR_MAC_BUILD_PASS");
    }
}
