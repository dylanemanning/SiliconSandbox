using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuStyler : MonoBehaviour
{
    private sealed class GateStreamItem
    {
        public RectTransform rect;
        public float speed;
        public float laneX;
        public float phase;
    }

    public enum MenuTextRole
    {
        Title,
        Section,
        Body,
        Button,
        Input,
        ListItem
    }

    [Header("Shared menu style")]
    [Tooltip("Set this to the font asset you want all menu text to use.")]
    public TMP_FontAsset fontAsset;

    [Tooltip("Background color used for the full menu panels.")]
    public Color panelBackground = new Color(0.12f, 0.18f, 0.21f, 1f);
    public Color mainMenuBackground = new Color(0.055f, 0.12f, 0.15f, 1f);
    public Color pauseBackground = new Color(0.035f, 0.07f, 0.09f, 0.94f);
    public Color loadBackground = new Color(0.07f, 0.19f, 0.24f, 1f);
    public Color createBackground = new Color(0.12f, 0.21f, 0.17f, 1f);
    public Color settingsBackground = new Color(0.12f, 0.14f, 0.19f, 1f);
    public Color controlsBackground = new Color(0.12f, 0.18f, 0.21f, 1f);
    public Color videoBackground = new Color(0.18f, 0.17f, 0.13f, 1f);
    public Color audioBackground = new Color(0.1f, 0.2f, 0.19f, 1f);
    public Color accessibilityBackground = new Color(0.2f, 0.16f, 0.12f, 1f);

    [Tooltip("Background color used for the button blocks.")]
    public Color buttonBackground = new Color(0.82f, 0.88f, 0.83f, 1f);
    public Color buttonHighlight = new Color(0.96f, 0.75f, 0.34f, 1f);
    public Color buttonPressed = new Color(0.35f, 0.69f, 0.63f, 1f);

    [Tooltip("Text color used for titles and body text.")]
    public Color textColor = Color.white;

    [Tooltip("Text color used for input and button text where dark text is easier to read.")]
    public Color darkTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);

    [Header("Sizing")]
    public float titleFontSize = 64f;
    public float sectionFontSize = 30f;
    public float bodyFontSize = 22f;
    public float buttonFontSize = 28f;
    public float listItemFontSize = 22f;
    public float menuButtonHeight = 75f;

    [Header("Main menu backdrop")]
    public bool animateGateBackdrop = true;
    public int gatesPerSide = 7;
    public float gateFlowSpeed = 24f;

    [Header("Layout")]
    [Tooltip("Set this to true if the menu should be reset at runtime on enable.")]
    public bool applyOnStart = true;

    private Transform styleRoot;
    private RectTransform backdropRoot;
    private RectTransform backdropPanel;
    private readonly List<GateStreamItem> gateStreamItems = new List<GateStreamItem>();
    private static readonly string[] GateLabels = { "AND", "OR", "NOT", "XOR", "NAND", "NOR", "BUF" };
    private static readonly Dictionary<string, Sprite> GateSymbolSprites = new Dictionary<string, Sprite>();

    private Transform StyleRoot
    {
        get
        {
            if (styleRoot != null) return styleRoot;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

            Transform pausePanel = transform.name == "PausePanel" ? transform : null;
            if (pausePanel == null && canvas != null)
            {
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "PausePanel")
                    {
                        pausePanel = child;
                        break;
                    }
                }
            }

            styleRoot = pausePanel != null ? pausePanel : canvas == null ? transform : canvas.transform;
            return styleRoot;
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            Apply();
        }
    }

    private void OnEnable()
    {
        if (applyOnStart)
        {
            Apply();
        }
    }

    private void Update()
    {
        if (!animateGateBackdrop || backdropRoot == null || backdropPanel == null)
        {
            return;
        }

        float panelWidth = backdropPanel.rect.width;
        backdropRoot.gameObject.SetActive(panelWidth >= 1450f);
        if (panelWidth < 1450f)
        {
            return;
        }

        float halfHeight = backdropPanel.rect.height * 0.5f;
        foreach (GateStreamItem item in gateStreamItems)
        {
            Vector2 position = item.rect.anchoredPosition;
            position.y -= item.speed * Time.deltaTime;
            if (position.y < -halfHeight - 70f)
            {
                position.y = halfHeight + 70f;
            }

            position.x = item.laneX + Mathf.Sin(Time.time * 0.45f + item.phase) * 9f;
            item.rect.anchoredPosition = position;
        }
    }

    public void Apply()
    {
        ApplyPageBackgrounds();
        ApplyMainMenuBackdrop();
        ApplyButtons();
        ApplyInputFields();
        ApplyText();
        ApplySavedWorldList();
    }

    public void ApplyTo(Transform root)
    {
        if (root == null) return;

        styleRoot = root;
        Apply();
    }

    private void ApplyMainMenuBackdrop()
    {
        Transform mainMenuPanel = null;
        foreach (Transform child in StyleRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "MainMenuPanel")
            {
                mainMenuPanel = child;
                break;
            }
        }

        if (mainMenuPanel == null)
        {
            return;
        }

        backdropPanel = mainMenuPanel.GetComponent<RectTransform>();
        Transform existingBackdrop = mainMenuPanel.Find("LogicGateBackdrop");
        if (existingBackdrop != null)
        {
            backdropRoot = existingBackdrop.GetComponent<RectTransform>();
            backdropRoot.SetAsFirstSibling();
            return;
        }

        GameObject backdrop = new GameObject("LogicGateBackdrop", typeof(RectTransform));
        backdrop.transform.SetParent(mainMenuPanel, false);
        backdropRoot = backdrop.GetComponent<RectTransform>();
        backdropRoot.anchorMin = Vector2.zero;
        backdropRoot.anchorMax = Vector2.one;
        backdropRoot.offsetMin = Vector2.zero;
        backdropRoot.offsetMax = Vector2.zero;
        backdropRoot.SetAsFirstSibling();

        CreateCircuitTrace(true);
        CreateCircuitTrace(false);

        int itemCount = Mathf.Max(1, gatesPerSide);
        for (int side = 0; side < 2; side++)
        {
            bool leftSide = side == 0;
            for (int index = 0; index < itemCount; index++)
            {
                const float laneX = 185f;
                float spacing = (backdropPanel.rect.height + 140f) / itemCount;
                float y = backdropPanel.rect.height * 0.5f + 70f - spacing * index;
                CreateGateStreamItem(leftSide, index, laneX, y);
            }
        }
    }

    private void CreateCircuitTrace(bool leftSide)
    {
        RectTransform trace = CreateBackdropImage("CircuitTrace", backdropRoot, new Color(0.35f, 0.75f, 0.66f, 0.16f));
        trace.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        trace.anchorMax = trace.anchorMin;
        trace.pivot = new Vector2(0.5f, 0.5f);
        const float laneX = 185f;
        trace.anchoredPosition = new Vector2(leftSide ? laneX + 108f : -laneX - 108f, 0f);
        trace.sizeDelta = new Vector2(2f, backdropPanel.rect.height - 120f);
    }

    private void CreateGateStreamItem(bool leftSide, int index, float laneX, float y)
    {
        Color accent = index % 3 == 0
            ? new Color(0.93f, 0.68f, 0.35f, 1f)
            : new Color(0.48f, 0.82f, 0.72f, 1f);
        RectTransform badge = CreateBackdropImage(
            "Gate_" + GateLabels[index % GateLabels.Length],
            backdropRoot,
            new Color(accent.r, accent.g, accent.b, 0.68f)
        );
        Image symbolImage = badge.GetComponent<Image>();
        symbolImage.sprite = GetGateSymbolSprite(GateLabels[index % GateLabels.Length]);
        symbolImage.preserveAspect = true;
        badge.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        badge.anchorMax = badge.anchorMin;
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(150f, 94f);
        float signedLaneX = leftSide ? laneX : -laneX;
        badge.anchoredPosition = new Vector2(signedLaneX, y);
        badge.localRotation = Quaternion.Euler(0f, 0f, ((index % 3) - 1) * 1.5f);

        gateStreamItems.Add(new GateStreamItem
        {
            rect = badge,
            speed = gateFlowSpeed * (1f),
            laneX = signedLaneX,
            phase = index * 0.9f + (leftSide ? 0f : 1.7f)
        });
    }

    private RectTransform CreateBackdropImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageObject.GetComponent<RectTransform>();
    }

    private static Sprite GetGateSymbolSprite(string gateName)
    {
        if (GateSymbolSprites.TryGetValue(gateName, out Sprite sprite))
        {
            return sprite;
        }

        const int width = 256;
        const int height = 160;
        Color32[] pixels = new Color32[width * height];

        switch (gateName)
        {
            case "AND":
            case "NAND":
                DrawLine(pixels, width, height, new Vector2(52, 30), new Vector2(126, 30), 6f);
                DrawBezier(pixels, width, height, new Vector2(126, 30), new Vector2(222, 30), new Vector2(222, 130), new Vector2(126, 130), 6f);
                DrawLine(pixels, width, height, new Vector2(126, 130), new Vector2(52, 130), 6f);
                DrawLine(pixels, width, height, new Vector2(52, 130), new Vector2(52, 30), 6f);
                DrawLine(pixels, width, height, new Vector2(12, 60), new Vector2(52, 60), 6f);
                DrawLine(pixels, width, height, new Vector2(12, 100), new Vector2(52, 100), 6f);
                if (gateName == "NAND")
                {
                    DrawCircle(pixels, width, height, new Vector2(217, 80), 9f, 6f);
                    DrawLine(pixels, width, height, new Vector2(226, 80), new Vector2(248, 80), 6f);
                }
                else
                {
                    DrawLine(pixels, width, height, new Vector2(220, 80), new Vector2(248, 80), 6f);
                }
                break;

            case "OR":
            case "NOR":
            case "XOR":
                DrawBezier(pixels, width, height, new Vector2(54, 30), new Vector2(112, 30), new Vector2(146, 52), new Vector2(202, 80), 6f);
                DrawBezier(pixels, width, height, new Vector2(202, 80), new Vector2(146, 108), new Vector2(112, 130), new Vector2(54, 130), 6f);
                DrawBezier(pixels, width, height, new Vector2(54, 130), new Vector2(80, 104), new Vector2(80, 56), new Vector2(54, 30), 6f);
                DrawLine(pixels, width, height, new Vector2(12, 60), new Vector2(66, 60), 6f);
                DrawLine(pixels, width, height, new Vector2(12, 100), new Vector2(66, 100), 6f);
                if (gateName == "XOR")
                {
                    DrawBezier(pixels, width, height, new Vector2(34, 30), new Vector2(60, 56), new Vector2(60, 104), new Vector2(34, 130), 6f);
                }

                if (gateName == "NOR")
                {
                    DrawCircle(pixels, width, height, new Vector2(215, 80), 9f, 6f);
                    DrawLine(pixels, width, height, new Vector2(224, 80), new Vector2(248, 80), 6f);
                }
                else
                {
                    DrawLine(pixels, width, height, new Vector2(202, 80), new Vector2(248, 80), 6f);
                }
                break;

            case "NOT":
            case "BUF":
                DrawLine(pixels, width, height, new Vector2(56, 30), new Vector2(188, 80), 6f);
                DrawLine(pixels, width, height, new Vector2(188, 80), new Vector2(56, 130), 6f);
                DrawLine(pixels, width, height, new Vector2(56, 130), new Vector2(56, 30), 6f);
                DrawLine(pixels, width, height, new Vector2(12, 80), new Vector2(56, 80), 6f);
                if (gateName == "NOT")
                {
                    DrawCircle(pixels, width, height, new Vector2(202, 80), 9f, 6f);
                    DrawLine(pixels, width, height, new Vector2(211, 80), new Vector2(248, 80), 6f);
                }
                else
                {
                    DrawLine(pixels, width, height, new Vector2(188, 80), new Vector2(248, 80), 6f);
                }
                break;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = gateName + "LogicSymbol";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();

        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = gateName + "LogicSymbol";
        GateSymbolSprites.Add(gateName, sprite);
        return sprite;
    }

    private static void DrawLine(Color32[] pixels, int width, int height, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;
        float radius = thickness * 0.5f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - radius - 1f));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + radius + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - radius - 1f));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + radius + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float t = segmentLengthSquared == 0f
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared);
                float distance = Vector2.Distance(point, start + segment * t);
                float coverage = Mathf.Clamp01(radius + 0.75f - distance);
                if (coverage <= 0f) continue;

                int pixelIndex = y * width + x;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Max(pixels[pixelIndex].a / 255f, coverage) * 255f);
                pixels[pixelIndex] = new Color32(255, 255, 255, alpha);
            }
        }
    }

    private static void DrawBezier(Color32[] pixels, int width, int height, Vector2 start, Vector2 controlA, Vector2 controlB, Vector2 end, float thickness)
    {
        const int steps = 48;
        Vector2 previous = start;
        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            float inverseT = 1f - t;
            Vector2 point = inverseT * inverseT * inverseT * start
                + 3f * inverseT * inverseT * t * controlA
                + 3f * inverseT * t * t * controlB
                + t * t * t * end;
            DrawLine(pixels, width, height, previous, point, thickness);
            previous = point;
        }
    }

    private static void DrawCircle(Color32[] pixels, int width, int height, Vector2 center, float radius, float thickness)
    {
        float outerRadius = radius + thickness * 0.5f + 1f;
        float innerRadius = Mathf.Max(0f, radius - thickness * 0.5f - 1f);
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - outerRadius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + outerRadius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - outerRadius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + outerRadius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float coverage = Mathf.Clamp01(Mathf.Min(outerRadius - distance, distance - innerRadius));
                if (coverage <= 0f) continue;

                int pixelIndex = y * width + x;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Max(pixels[pixelIndex].a / 255f, coverage) * 255f);
                pixels[pixelIndex] = new Color32(255, 255, 255, alpha);
            }
        }
    }

    private void ApplyPageBackgrounds()
    {
        ApplyPageBackground("MainMenuPanel", mainMenuBackground);
        ApplyPageBackground("PausePanel", pauseBackground);
        ApplyPageBackground("LoadPanel", loadBackground);
        ApplyPageBackground("CreatePanel", createBackground);
        ApplyPageBackground("settingsPanel", settingsBackground);
        ApplyPageBackground("ControlsPage", controlsBackground);
        ApplyPageBackground("VideoPage", videoBackground);
        ApplyPageBackground("AudioPage", audioBackground);
        ApplyPageBackground("AccessibilityPage", accessibilityBackground);
    }

    private void ApplyPageBackground(string pageName, Color color)
    {
        foreach (var child in StyleRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(child.name, pageName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var image = child.GetComponent<Image>();
            if (image == null) image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = string.Equals(pageName, "PausePanel", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private static MenuTextRole DetermineRole(string objectName)
    {
        if (objectName.Contains("Title") || objectName.Contains("Heading") || objectName.Contains("Header"))
        {
            return MenuTextRole.Title;
        }

        if (objectName.Contains("Error") || objectName.Contains("Status") || objectName.Contains("Label"))
        {
            return MenuTextRole.Section;
        }

        if (objectName.Contains("World") || objectName.Contains("Save") || objectName.Contains("List") || objectName.Contains("Item"))
        {
            return MenuTextRole.ListItem;
        }

        return MenuTextRole.Body;
    }

    private void ApplyTextStyle(TMP_Text text, MenuTextRole role)
    {
        if (text == null)
        {
            return;
        }

        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        switch (role)
        {
            case MenuTextRole.Title:
                text.fontSize = titleFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = textColor;
                break;
            case MenuTextRole.Section:
                text.fontSize = sectionFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = textColor;
                break;
            case MenuTextRole.Button:
                text.fontSize = buttonFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = darkTextColor;
                break;
            case MenuTextRole.Input:
                text.fontSize = buttonFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = darkTextColor;
                break;
            case MenuTextRole.ListItem:
                text.fontSize = listItemFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = textColor;
                break;
            default:
                text.fontSize = bodyFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = textColor;
                break;
        }

        text.enableAutoSizing = false;

        if (role == MenuTextRole.Title)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -112f);
            rect.sizeDelta = new Vector2(900f, 100f);
        }
    }

    private void ApplyButtons()
    {
        foreach (var button in StyleRoot.GetComponentsInChildren<Button>(true))
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
                image.type = Image.Type.Sliced;
            }

            var colors = button.colors;
            colors.normalColor = buttonBackground;
            colors.highlightedColor = buttonHighlight;
            colors.selectedColor = buttonHighlight;
            colors.pressedColor = buttonPressed;
            colors.disabledColor = new Color(0.55f, 0.59f, 0.56f, 0.65f);
            button.colors = colors;

            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                ApplyTextStyle(text, MenuTextRole.Button);
            }

            var parent = button.transform.parent;
            var verticalLayout = parent == null ? null : parent.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout != null)
            {
                verticalLayout.childAlignment = TextAnchor.MiddleCenter;
                verticalLayout.spacing = 14f;

                if (parent.name == "Button Container")
                {
                    var containerRect = parent.GetComponent<RectTransform>();
                    if (containerRect != null)
                    {
                        containerRect.anchorMin = new Vector2(0.5f, containerRect.anchorMin.y);
                        containerRect.anchorMax = new Vector2(0.5f, containerRect.anchorMax.y);
                        containerRect.anchoredPosition = new Vector2(0f, containerRect.anchoredPosition.y);
                        containerRect.sizeDelta = new Vector2(520f, containerRect.sizeDelta.y);
                    }

                    verticalLayout.childControlWidth = true;
                    verticalLayout.childForceExpandWidth = true;
                    verticalLayout.childControlHeight = false;
                    verticalLayout.childForceExpandHeight = false;
                }

                var layoutElement = button.GetComponent<LayoutElement>();
                if (layoutElement == null) layoutElement = button.gameObject.AddComponent<LayoutElement>();
                layoutElement.minHeight = menuButtonHeight;
                layoutElement.preferredHeight = menuButtonHeight;
                layoutElement.flexibleHeight = 0f;
            }
        }
    }

    private void ApplyInputFields()
    {
        foreach (var input in StyleRoot.GetComponentsInChildren<TMP_InputField>(true))
        {
            // RectTransform inputRect = input.GetComponent<RectTransform>();
            // inputRect.sizeDelta = new Vector2(inputRect.sizeDelta.x, 75f);

            var inputImage = input.GetComponent<Image>();
            if (inputImage != null)
            {
                inputImage.color = new Color(1f, 1f, 1f, 0.95f);
            }

            if (fontAsset != null)
            {
                input.fontAsset = fontAsset;
            }

            if (input.textComponent != null)
            {
                ApplyTextStyle(input.textComponent, MenuTextRole.Input);
            }

            if (input.placeholder != null)
            {
                // Debug.Log("Viewport Found");
                var placeholderText = input.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    if (fontAsset != null)
                    {
                        placeholderText.font = fontAsset;
                    }

                    placeholderText.fontSize = buttonFontSize;
                    placeholderText.alignment = TextAlignmentOptions.Center;
                    placeholderText.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                }
            }

            var viewport = input.transform.Find("Text Area");
            if (viewport != null)
            {
                var textArea = viewport.GetComponent<RectTransform>();
                if (textArea != null)
                {
                    textArea.anchorMin = new Vector2(0, 0);
                    textArea.anchorMax = new Vector2(1, 1);
                    textArea.offsetMin = new Vector2(10f, 8f);
                    textArea.offsetMax = new Vector2(-10f, -8f);
                }
            }
        }
    }

    private void ApplyText()
    {
        foreach (var text in StyleRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.GetComponentInParent<Button>(true) != null
                || text.GetComponentInParent<TMP_InputField>(true) != null
                || (backdropRoot != null && text.transform.IsChildOf(backdropRoot)))
            {
                continue;
            }

            var role = DetermineRole(text.gameObject.name);
            ApplyTextStyle(text, role);
        }
    }

    private void ApplySavedWorldList()
    {
        foreach (var scrollRect in StyleRoot.GetComponentsInChildren<ScrollRect>(true))
        {
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.inertia = true;

            var content = scrollRect.content;
            if (content == null)
            {
                continue;
            }

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.spacing = 8f;
                layout.padding = new RectOffset(10, 10, 10, 10);
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

        }
    }
}
