using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AccessibilitySettings
{
    public const string ColorblindModeKey = "Accessibility.ColorblindMode";
    public const string MouseSensitivityKey = "Accessibility.MouseSensitivity";
    public const string FontSizeKey = "Accessibility.FontSize";
    public const string DyslexiaFriendlyTypefaceKey = "Accessibility.DyslexiaFriendlyTypeface";

    private sealed class TextStyle
    {
        public float fontSize;
        public float fontSizeMin;
        public float fontSizeMax;
        public bool autoSizing;
        public TMP_FontAsset font;
    }

    private static readonly Dictionary<TMP_Text, TextStyle> OriginalTextStyles =
        new Dictionary<TMP_Text, TextStyle>();
    private static readonly Dictionary<TMP_InputField, TMP_FontAsset> OriginalInputFonts =
        new Dictionary<TMP_InputField, TMP_FontAsset>();
    private static readonly Dictionary<(Material baseMaterial, int patternIndex), Material> OverlayMaterials =
        new Dictionary<(Material baseMaterial, int patternIndex), Material>();
    private static readonly Dictionary<Material, Material> OriginalMaterials =
        new Dictionary<Material, Material>();
    private static readonly Texture2D[] SignalPatterns = new Texture2D[3];

    public static bool ColorblindModeEnabled => PlayerPrefs.GetInt(ColorblindModeKey, 0) != 0;
    public static float MouseSensitivityMultiplier =>
        Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, 1f), 0.1f, 3f);

    private static float FontSizeMultiplier
    {
        get
        {
            switch (Mathf.Clamp(PlayerPrefs.GetInt(FontSizeKey, 1), 0, 2))
            {
                case 0: return 0.8f;
                case 2: return 1.2f;
                default: return 1f;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        OriginalTextStyles.Clear();
        OriginalInputFonts.Clear();
        foreach (Material overlay in OverlayMaterials.Values)
        {
            if (overlay != null) Object.Destroy(overlay);
        }

        OverlayMaterials.Clear();
        OriginalMaterials.Clear();
        SignalPatterns[0] = null;
        SignalPatterns[1] = null;
        SignalPatterns[2] = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySettings();
    }

    public static void ApplySettings()
    {
        ApplyTextSettings();

        foreach (WireModelVisual visual in Object.FindObjectsByType<WireModelVisual>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            visual.RefreshAccessibilityVisuals();
        }

        foreach (BlockModelVisual visual in Object.FindObjectsByType<BlockModelVisual>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            visual.RefreshAccessibilityVisuals();
        }

        foreach (LogicWireBlock wire in Object.FindObjectsByType<LogicWireBlock>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            wire.RefreshAccessibilityVisuals();
        }

        foreach (GateBlock gate in Object.FindObjectsByType<GateBlock>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            gate.RefreshAccessibilityVisuals();
        }

        foreach (StateViewerBlock viewer in Object.FindObjectsByType<StateViewerBlock>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            viewer.RefreshAccessibilityVisuals();
        }
    }

    public static void ApplyTextSettings(bool refreshOriginalStyles = false, Transform refreshRoot = null)
    {
        TMP_FontAsset dyslexiaFont = null;
        if (PlayerPrefs.GetInt(DyslexiaFriendlyTypefaceKey, 0) != 0)
        {
            dyslexiaFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (dyslexiaFont == null)
            {
                Debug.LogError("Could not load the Liberation Sans TMP font for accessibility settings.");
            }
        }

        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null) continue;

            if (refreshOriginalStyles &&
                (refreshRoot == null || text.transform.IsChildOf(refreshRoot)))
            {
                OriginalTextStyles.Remove(text);
            }

            if (!OriginalTextStyles.TryGetValue(text, out TextStyle original))
            {
                original = new TextStyle
                {
                    fontSize = text.fontSize,
                    fontSizeMin = text.fontSizeMin,
                    fontSizeMax = text.fontSizeMax,
                    autoSizing = text.enableAutoSizing,
                    font = text.font
                };
                OriginalTextStyles.Add(text, original);
            }

            float scale = FontSizeMultiplier;
            text.fontSize = original.fontSize * scale;
            text.fontSizeMin = original.fontSizeMin * scale;
            text.fontSizeMax = original.fontSizeMax * scale;
            text.enableAutoSizing = original.autoSizing;
            text.font = dyslexiaFont != null ? dyslexiaFont : original.font;
        }

        foreach (TMP_InputField input in Object.FindObjectsByType<TMP_InputField>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (input == null) continue;

            if (refreshOriginalStyles &&
                (refreshRoot == null || input.transform.IsChildOf(refreshRoot)))
            {
                OriginalInputFonts.Remove(input);
            }

            if (!OriginalInputFonts.TryGetValue(input, out TMP_FontAsset originalFont))
            {
                originalFont = input.fontAsset;
                OriginalInputFonts.Add(input, originalFont);
            }

            input.fontAsset = dyslexiaFont != null ? dyslexiaFont : originalFont;
        }

        if (OriginalTextStyles.Count > 0)
        {
            List<TMP_Text> destroyedTexts = null;
            foreach (TMP_Text text in OriginalTextStyles.Keys)
            {
                if (text != null) continue;
                if (destroyedTexts == null) destroyedTexts = new List<TMP_Text>();
                destroyedTexts.Add(text);
            }

            if (destroyedTexts != null)
            {
                foreach (TMP_Text text in destroyedTexts)
                {
                    OriginalTextStyles.Remove(text);
                }
            }
        }
    }

    public static void ApplySignalPattern(Renderer renderer, int state, bool isError)
    {
        if (renderer == null) return;

        Material[] materials = renderer.sharedMaterials;
        bool changed = false;
        for (int index = 0; index < materials.Length; index++)
        {
            Material current = materials[index];
            if (current == null) continue;

            if (!ColorblindModeEnabled)
            {
                if (!OriginalMaterials.TryGetValue(current, out Material original)) continue;
                materials[index] = original;
                changed = true;
                continue;
            }

            Material baseMaterial = OriginalMaterials.TryGetValue(current, out Material originalMaterial)
                ? originalMaterial
                : current;
            if (!baseMaterial.HasProperty("_DetailAlbedoMap")) continue;

            int patternIndex = isError ? 2 : state == 0 ? 0 : 1;
            Material overlay = GetOverlayMaterial(baseMaterial, patternIndex);
            if (overlay == null || overlay == current) continue;

            materials[index] = overlay;
            changed = true;
        }

        if (changed) renderer.sharedMaterials = materials;
    }

    private static Material GetOverlayMaterial(Material baseMaterial, int patternIndex)
    {
        var key = (baseMaterial, patternIndex);
        if (OverlayMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

        Texture2D pattern = GetSignalPattern(patternIndex);
        if (pattern == null) return null;

        Material overlay = new Material(baseMaterial)
        {
            name = baseMaterial.name + " Accessibility Overlay " + patternIndex,
            hideFlags = HideFlags.HideAndDontSave
        };
        overlay.SetTexture("_DetailAlbedoMap", pattern);
        overlay.SetFloat("_DetailAlbedoMapScale", 1f);
        bool supportsScaledDetail = overlay.shader.name.StartsWith(
            "Universal Render Pipeline/", System.StringComparison.Ordinal);
        overlay.DisableKeyword(supportsScaledDetail ? "_DETAIL_MULX2" : "_DETAIL_SCALED");
        overlay.EnableKeyword(supportsScaledDetail ? "_DETAIL_SCALED" : "_DETAIL_MULX2");
        OriginalMaterials[overlay] = baseMaterial;
        OverlayMaterials[key] = overlay;
        return overlay;
    }

    private static Texture2D GetSignalPattern(int patternIndex)
    {
        if (SignalPatterns[patternIndex] != null) return SignalPatterns[patternIndex];

        string patternName = patternIndex == 0 ? "Low" : patternIndex == 1 ? "High" : "Error";
        Texture2D texture = Resources.Load<Texture2D>("AccessibilityPatterns/" + patternName);
        if (texture == null)
        {
            Debug.LogError("Could not load the accessibility signal pattern texture: " + patternName);
            return null;
        }

        SignalPatterns[patternIndex] = texture;
        return texture;
    }
}
