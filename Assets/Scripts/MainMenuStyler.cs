using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuStyler : MonoBehaviour
{
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
    public Color panelBackground = new Color(0.486f, 0.494f, 0.415f, 1f);

    [Tooltip("Background color used for the button blocks.")]
    public Color buttonBackground = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Text color used for titles and body text.")]
    public Color textColor = Color.white;

    [Tooltip("Text color used for input and button text where dark text is easier to read.")]
    public Color darkTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);

    [Header("Sizing")]
    public float titleFontSize = 72f;
    public float sectionFontSize = 36f;
    public float bodyFontSize = 28f;
    public float buttonFontSize = 28f;
    public float listItemFontSize = 26f;

    [Header("Layout")]
    [Tooltip("Set this to true if the menu should be reset at runtime on enable.")]
    public bool applyOnStart = true;

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

    public void Apply()
    {
        ApplySharedPanels();
        ApplyButtons();
        ApplyInputFields();
        ApplyText();
        ApplySavedWorldList();
        ApplyAllTextRegardlessOfInspector();
    }

    private void ApplySharedPanels()
    {
        var images = GetComponentsInChildren<Image>(true);

        foreach (var image in images)
        {
            var parent = image.transform.parent;
            if (parent == null)
            {
                continue;
            }

            var parentName = parent.name;
            if (parentName.Contains("MainMenu") || parentName.Contains("Load") || parentName.Contains("Create") || parentName.Contains("Settings") || parentName.Contains("Panel"))
            {
                image.color = panelBackground;
            }
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
                text.fontSize = bodyFontSize;
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
    }

    private void ApplyButtons()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = buttonBackground;
                image.type = Image.Type.Sliced;
            }

            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                ApplyTextStyle(text, MenuTextRole.Button);
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
            }
        }
    }

    private void ApplyInputFields()
    {
        foreach (var input in GetComponentsInChildren<TMP_InputField>(true))
        {
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
                var placeholderText = input.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    if (fontAsset != null)
                    {
                        placeholderText.font = fontAsset;
                    }

                    placeholderText.fontSize = bodyFontSize;
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
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.GetComponentInParent<Button>(true) != null || text.GetComponentInParent<TMP_InputField>(true) != null)
            {
                continue;
            }

            var role = DetermineRole(text.gameObject.name);
            ApplyTextStyle(text, role);
        }
    }

    private void ApplyAllTextRegardlessOfInspector()
    {
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.enableAutoSizing = false;

            var role = DetermineRole(text.gameObject.name);
            ApplyTextStyle(text, role);
        }

        foreach (var input in GetComponentsInChildren<TMP_InputField>(true))
        {
            if (fontAsset != null)
            {
                input.fontAsset = fontAsset;
            }

            if (input.textComponent != null)
            {
                input.textComponent.font = fontAsset != null ? fontAsset : input.textComponent.font;
                input.textComponent.fontSize = bodyFontSize;
                input.textComponent.alignment = TextAlignmentOptions.Center;
                input.textComponent.color = darkTextColor;
            }

            if (input.placeholder != null)
            {
                var placeholderText = input.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    if (fontAsset != null)
                    {
                        placeholderText.font = fontAsset;
                    }

                    placeholderText.fontSize = bodyFontSize;
                    placeholderText.alignment = TextAlignmentOptions.Center;
                    placeholderText.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                }
            }
        }
    }

    private void ApplySavedWorldList()
    {
        foreach (var scrollRect in GetComponentsInChildren<ScrollRect>(true))
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
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 8f;
                layout.padding = new RectOffset(10, 10, 10, 10);
            }

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.localScale = Vector3.one;
        }
    }
}
