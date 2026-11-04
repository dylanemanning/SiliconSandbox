using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PackagingWandController : MonoBehaviour
{
    private const float OutlineWidth = 0.035f;
    private static readonly Color OutlineColor = new Color(0.35f, 0.9f, 1f, 1f);
    private static readonly int[,] EdgeCorners =
    {
        { 0, 1 }, { 0, 2 }, { 0, 4 }, { 1, 3 },
        { 1, 5 }, { 2, 3 }, { 2, 6 }, { 3, 7 },
        { 4, 5 }, { 4, 6 }, { 5, 7 }, { 6, 7 }
    };

    public static bool IsConfirmationOpen { get; private set; }

    private readonly LineRenderer[] outlineEdges = new LineRenderer[12];
    private readonly Vector3[] outlineCorners = new Vector3[8];
    private Vector3Int firstCorner;
    private Vector3Int secondCorner;
    private bool hasFirstCorner;
    private bool hasSecondCorner;
    private bool confirmedArea;
    private bool toolActive;
    private GameObject outlineRoot;
    private GameObject confirmationCanvas;
    private Material outlineMaterial;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    public bool HasFirstCorner => hasFirstCorner;
    public bool HasSecondCorner => hasSecondCorner;
    public bool IsAreaConfirmed => confirmedArea;
    public Vector3Int FirstCorner => firstCorner;
    public Vector3Int SecondCorner => secondCorner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsConfirmationOpen = false;
    }

    public void HandleTool(bool selected, Block targetBlock, RaycastHit hit)
    {
        if (!selected)
        {
            if (toolActive) ClearSelection();
            toolActive = false;
            return;
        }

        toolActive = true;
        if (IsConfirmationOpen || confirmedArea) return;

        Vector3Int? hoveredCorner = targetBlock != null
            ? GetCellAtHit(hit)
            : null;
        if (hasFirstCorner)
        {
            UpdateOutline(hoveredCorner ?? firstCorner);
        }

        if (hoveredCorner.HasValue &&
            KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.PlaceMouseButton))
        {
            SelectCorner(hoveredCorner.Value);
        }
    }

    public void SelectCorner(Vector3Int cell)
    {
        if (IsConfirmationOpen || confirmedArea) return;

        if (!hasFirstCorner)
        {
            firstCorner = cell;
            hasFirstCorner = true;
            UpdateOutline(cell);
            return;
        }

        secondCorner = cell;
        hasSecondCorner = true;
        UpdateOutline(cell);
        ShowConfirmation();
    }

    public void ConfirmSelection()
    {
        if (!IsConfirmationOpen) return;

        CloseConfirmation();
        confirmedArea = true;
        RemoveOutline();
    }

    public void RejectSelection()
    {
        if (!IsConfirmationOpen) return;

        CloseConfirmation();
        hasSecondCorner = false;
        UpdateOutline(firstCorner);
    }

    public void CancelSelection()
    {
        ClearSelection();
    }

    private static Vector3Int GetCellAtHit(RaycastHit hit)
    {
        Vector3Int faceNormal = Vector3Int.RoundToInt(hit.normal);
        return Vector3Int.RoundToInt(hit.point - (Vector3)faceNormal * 0.5f);
    }

    private void UpdateOutline(Vector3Int candidate)
    {
        if (!hasFirstCorner) return;

        Vector3Int min = Vector3Int.Min(firstCorner, candidate);
        Vector3Int max = Vector3Int.Max(firstCorner, candidate);
        Vector3 minPoint = (Vector3)min - Vector3.one * 0.5f;
        Vector3 maxPoint = (Vector3)max + Vector3.one * 0.5f;
        outlineCorners[0] = new Vector3(minPoint.x, minPoint.y, minPoint.z);
        outlineCorners[1] = new Vector3(maxPoint.x, minPoint.y, minPoint.z);
        outlineCorners[2] = new Vector3(minPoint.x, maxPoint.y, minPoint.z);
        outlineCorners[3] = new Vector3(maxPoint.x, maxPoint.y, minPoint.z);
        outlineCorners[4] = new Vector3(minPoint.x, minPoint.y, maxPoint.z);
        outlineCorners[5] = new Vector3(maxPoint.x, minPoint.y, maxPoint.z);
        outlineCorners[6] = new Vector3(minPoint.x, maxPoint.y, maxPoint.z);
        outlineCorners[7] = new Vector3(maxPoint.x, maxPoint.y, maxPoint.z);

        if (!EnsureOutline()) return;
        for (int i = 0; i < outlineEdges.Length; i++)
        {
            outlineEdges[i].SetPosition(0, outlineCorners[EdgeCorners[i, 0]]);
            outlineEdges[i].SetPosition(1, outlineCorners[EdgeCorners[i, 1]]);
        }
    }

    private bool EnsureOutline()
    {
        if (outlineRoot != null) return true;

        outlineRoot = new GameObject("Packaging Area Outline");
        outlineRoot.transform.SetParent(transform, false);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Could not find a shader for the packaging area outline.");
            Destroy(outlineRoot);
            outlineRoot = null;
            return false;
        }

        outlineMaterial = new Material(shader)
        {
            name = "Packaging Area Outline Material"
        };
        for (int i = 0; i < outlineEdges.Length; i++)
        {
            GameObject edgeObject = new GameObject($"Packaging Area Edge {i + 1}");
            edgeObject.transform.SetParent(outlineRoot.transform, false);
            LineRenderer line = edgeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = OutlineWidth;
            line.endWidth = OutlineWidth;
            line.startColor = OutlineColor;
            line.endColor = OutlineColor;
            line.sharedMaterial = outlineMaterial;
            outlineEdges[i] = line;
        }

        return true;
    }

    private void ShowConfirmation()
    {
        confirmationCanvas = new GameObject(
            "PackagingConfirmation",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = confirmationCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = confirmationCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        MainMenuStyler styler = FindFirstObjectByType<MainMenuStyler>(FindObjectsInactive.Include);
        if (font == null && styler != null) font = styler.fontAsset;

        GameObject overlay = CreateImage("Overlay", confirmationCanvas.transform,
            new Color(0f, 0f, 0f, 0.62f), Vector2.zero, Vector2.one);
        overlay.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        overlay.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().raycastTarget = true;

        Color panelColor = styler != null
            ? styler.pauseBackground
            : new Color(0.035f, 0.07f, 0.09f, 1f);
        GameObject dialog = CreateImage(
            "Dialog",
            overlay.transform,
            panelColor,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f));
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(560f, 260f);

        TMP_Text message = CreateText("Message", dialog.transform, font,
            "Packaging is a visual preview only.\nConfirm this cuboid?");
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0.08f, 0.4f);
        messageRect.anchorMax = new Vector2(0.92f, 0.92f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;
        message.alignment = TextAlignmentOptions.Center;
        message.verticalAlignment = VerticalAlignmentOptions.Middle;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.enableAutoSizing = true;
        message.fontSize = 28f;
        message.fontSizeMin = 18f;
        message.fontSizeMax = 28f;
        message.color = Color.white;
        message.raycastTarget = false;

        GameObject buttonRow = new GameObject(
            "Buttons",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(dialog.transform, false);
        RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0.12f, 0.08f);
        buttonRowRect.anchorMax = new Vector2(0.88f, 0.36f);
        buttonRowRect.offsetMin = Vector2.zero;
        buttonRowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateDialogButton(buttonRow.transform, "Yes", "ConfirmButton", font,
            new Color(0.35f, 0.69f, 0.63f, 1f), ConfirmSelection);
        CreateDialogButton(buttonRow.transform, "No", "RejectButton", font,
            new Color(0.72f, 0.24f, 0.22f, 1f), RejectSelection);

        previousTimeScale = Time.timeScale;
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsConfirmationOpen = true;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string value)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value;
        return text;
    }

    private static void CreateDialogButton(Transform parent, string label, string name,
        TMP_FontAsset font, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;

        TMP_Text buttonText = CreateText("Label", buttonObject.transform, font, label);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.verticalAlignment = VerticalAlignmentOptions.Middle;
        buttonText.color = Color.white;
        buttonText.raycastTarget = false;
        RectTransform textRect = buttonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void CloseConfirmation()
    {
        IsConfirmationOpen = false;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        if (confirmationCanvas != null) Destroy(confirmationCanvas);
        confirmationCanvas = null;
    }

    private void ClearSelection()
    {
        if (IsConfirmationOpen) CloseConfirmation();
        hasFirstCorner = false;
        hasSecondCorner = false;
        confirmedArea = false;
        RemoveOutline();
    }

    private void RemoveOutline()
    {
        if (outlineRoot != null)
        {
            outlineRoot.SetActive(false);
            Destroy(outlineRoot);
        }

        outlineRoot = null;
        for (int i = 0; i < outlineEdges.Length; i++)
        {
            outlineEdges[i] = null;
        }

        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }
        outlineMaterial = null;
    }

    private void OnDisable()
    {
        ClearSelection();
    }
}
