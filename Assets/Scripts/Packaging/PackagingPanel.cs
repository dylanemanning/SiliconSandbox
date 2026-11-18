// PackagingPanel.cs — the package configuration window opened by the packaging wand.
//
// Shows what the box captured, lets the player name the package, set the short
// marking printed on its top, rename / re-direct / reorder ports, previews the
// finished block from above, and lists any problem that prevents packaging.
// Built in code with the group's menu colours and font (MainMenuStyler).

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class PackagingPanel : MonoBehaviour
{
    private static readonly Color Accent = new Color(.96f, .75f, .34f, 1f);       // menu highlight
    private static readonly Color Confirm = new Color(.35f, .69f, .63f, 1f);      // menu pressed / confirm
    private static readonly Color Danger = new Color(.72f, .24f, .22f, 1f);
    private static readonly Color Field = new Color(.07f, .12f, .15f, 1f);
    private static readonly Color Card = new Color(.1f, .16f, .19f, 1f);
    private static readonly Color Muted = new Color(.72f, .8f, .82f, 1f);
    private static readonly Color InputTint = new Color(.42f, .62f, .9f, 1f);
    private static readonly Color OutputTint = new Color(.4f, .82f, .5f, 1f);

    private PackageDraft draft;
    private Action<PackageDefinition> onCreate;
    private Action onBack;
    private TMP_FontAsset font;
    private Color panelColor = new Color(.035f, .07f, .09f, .97f);
    private TMP_InputField nameField, markingField;
    private bool markingEdited;
    private RectTransform inputList, outputList, preview;
    private TMP_Text problems, summary;
    private Button createButton;

    public PackageDraft Draft => draft;

    public static PackagingPanel Open(PackageDraft draft, string suggestedName,
        Action<PackageDefinition> onCreate, Action onBack)
    {
        var root = new GameObject("PackagingPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var panel = root.AddComponent<PackagingPanel>();
        panel.draft = draft;
        panel.onCreate = onCreate;
        panel.onBack = onBack;
        panel.Build(suggestedName);
        return panel;
    }

    private void Update()
    {
        if (KeybindSettings.WasPressedThisFrame(UnityEngine.InputSystem.Key.Escape)) Back();
    }

    public void Back()
    {
        var callback = onBack;
        onBack = null; onCreate = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    public void Create()
    {
        if (!draft.IsValid || Problems().Count > 0) return;
        var definition = draft.Build(nameField.text, markingField.text);
        var callback = onCreate;
        onBack = null; onCreate = null;
        Destroy(gameObject);
        callback?.Invoke(definition);
    }

    public void SetName(string value) { nameField.text = value; }
    public void SetMarking(string value) { markingField.text = value; markingEdited = true; }

    /// <summary>Moves a port to the other direction list (keeps its internal mapping).</summary>
    public void ToggleDirection(PackagePort port)
    {
        port.isInput = !port.isInput;
        draft.Ports.Remove(port);
        draft.Ports.Add(port);
        Refresh();
    }

    public void Move(PackagePort port, int offset)
    {
        var sameSide = draft.Ports.Where(p => p.isInput == port.isInput).ToList();
        int index = sameSide.IndexOf(port), target = index + offset;
        if (index < 0 || target < 0 || target >= sameSide.Count) return;
        var other = sameSide[target];
        int a = draft.Ports.IndexOf(port), b = draft.Ports.IndexOf(other);
        draft.Ports[a] = other; draft.Ports[b] = port;
        Refresh();
    }

    public List<string> Problems()
    {
        var list = new List<string>(draft.Errors);
        if (!draft.IsValid) return list;
        if (nameField != null && string.IsNullOrWhiteSpace(nameField.text)) list.Add("Give the package a name.");
        var names = draft.Ports.Select(p => (p.name ?? "").Trim()).ToList();
        if (names.Any(string.IsNullOrEmpty)) list.Add("Every port needs a name.");
        foreach (var duplicate in names.Where(n => n.Length > 0).GroupBy(n => n).Where(g => g.Count() > 1))
            list.Add($"Two ports are named \"{duplicate.Key}\". Port names must be different.");
        return list;
    }

    // ---- Layout -----------------------------------------------------------------

    private void Build(string suggestedName)
    {
        var styler = FindFirstObjectByType<MainMenuStyler>(FindObjectsInactive.Include);
        font = styler != null && styler.fontAsset != null ? styler.fontAsset : TMP_Settings.defaultFontAsset;
        if (styler != null) panelColor = new Color(styler.pauseBackground.r, styler.pauseBackground.g, styler.pauseBackground.b, .97f);

        var overlay = Box("Overlay", transform, new Color(0, 0, 0, .6f));
        Stretch(overlay.rectTransform, Vector2.zero, Vector2.one);
        overlay.raycastTarget = true;

        var panel = Box("Packaging window", overlay.transform, panelColor);
        Anchor(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(1240, 860));
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, .55f);
        outline.effectDistance = new Vector2(2, -2);
        var accentBar = Box("Accent", panel.transform, Accent);
        Stretch(accentBar.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -6), Vector2.zero);

        var title = Text("Title", panel.transform, "Create package", 46, Color.white, TextAlignmentOptions.Left);
        Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, -92), new Vector2(-48, -26));
        summary = Text("Summary", panel.transform, "", 22, Muted, TextAlignmentOptions.Left);
        Stretch(summary.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, -132), new Vector2(-48, -92));

        // Left column: identity and preview.
        var left = new GameObject("Details", typeof(RectTransform)).GetComponent<RectTransform>();
        left.SetParent(panel.transform, false);
        Stretch(left, new Vector2(0, 0), new Vector2(0, 1), new Vector2(48, 120), new Vector2(468, -152));

        Label(left, "Name", 0);
        nameField = Input(left, "Name field", suggestedName, PackageDefinition.MaxNameLength, -34);
        Label(left, "Top marking", -110);
        markingField = Input(left, "Marking field", PackageDefinition.SuggestMarking(suggestedName), PackageDefinition.MaxMarkingLength, -144);
        nameField.onValueChanged.AddListener(value =>
        {
            if (!markingEdited) markingField.SetTextWithoutNotify(PackageDefinition.SuggestMarking(value));
            Refresh();
        });
        markingField.onValueChanged.AddListener(_ => { markingEdited = true; Refresh(); });

        Label(left, "Package block from above", -228);
        var previewCard = Box("Preview card", left, Card);
        Stretch(previewCard.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -548), new Vector2(0, -262));
        preview = previewCard.rectTransform;

        // Right side: ports.
        var ports = new GameObject("Ports", typeof(RectTransform)).GetComponent<RectTransform>();
        ports.SetParent(panel.transform, false);
        Stretch(ports, new Vector2(0, 0), Vector2.one, new Vector2(508, 220), new Vector2(-48, -152));
        inputList = PortColumn(ports, "Inputs", "Back of the block", InputTint, new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(0, 0), new Vector2(-12, 0));
        outputList = PortColumn(ports, "Outputs", "Front of the block", OutputTint, new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(12, 0), Vector2.zero);

        problems = Text("Problems", panel.transform, "", 22, new Color(1f, .62f, .55f), TextAlignmentOptions.TopLeft);
        problems.textWrappingMode = TextWrappingModes.Normal;
        Stretch(problems.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(508, 120), new Vector2(-48, 206));

        var back = ButtonWith(panel.transform, "RejectButton", "Back", new Color(.3f, .36f, .4f, 1), Back);
        Stretch((RectTransform)back.transform, Vector2.zero, Vector2.zero, new Vector2(48, 36), new Vector2(328, 100));
        createButton = ButtonWith(panel.transform, "ConfirmButton", "Create package", Confirm, Create);
        Stretch((RectTransform)createButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-408, 36), new Vector2(-48, 100));
        var hint = Text("Hint", panel.transform, "Your circuit stays in the world.", 20, Muted, TextAlignmentOptions.Center);
        Stretch(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(340, 36), new Vector2(-420, 100));

        Refresh();
    }

    private void Refresh()
    {
        if (draft == null || inputList == null) return;
        int width = Mathf.Max(1, Mathf.Max(draft.Ports.Count(p => p.isInput), draft.Ports.Count(p => !p.isInput)));
        var size = draft.Size;
        summary.text = draft.IsValid
            ? $"{draft.ComponentCount} component{(draft.ComponentCount == 1 ? "" : "s")}, {draft.WireCount} wire{(draft.WireCount == 1 ? "" : "s")}  |  box {size.x} x {size.y} x {size.z}  |  package {width} x 1 x 1"
            : $"Box {size.x} x {size.y} x {size.z}  |  this selection can't be packaged yet";
        FillPorts(inputList, true);
        FillPorts(outputList, false);
        DrawPreview(width);
        var list = Problems();
        problems.text = list.Count == 0 ? "" : string.Join("\n", list.Take(4).Select(p => "- " + p));
        createButton.interactable = list.Count == 0;
    }

    private RectTransform PortColumn(RectTransform parent, string heading, string where, Color tint,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var column = Box(heading + " column", parent, Card);
        Stretch(column.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
        var marker = Box("Tint", column.transform, tint);
        Stretch(marker.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);
        var title = Text("Heading", column.transform, heading, 28, Color.white, TextAlignmentOptions.Left);
        Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(18, -48), new Vector2(-18, -10));
        var note = Text("Where", column.transform, where, 18, Muted, TextAlignmentOptions.Left);
        Stretch(note.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(18, -76), new Vector2(-18, -48));

        var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scroll.transform.SetParent(column.transform, false);
        scroll.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var scrollRect = (RectTransform)scroll.transform;
        Stretch(scrollRect, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -86));
        var content = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(scroll.transform, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var view = scroll.GetComponent<ScrollRect>();
        view.content = content; view.horizontal = false; view.scrollSensitivity = 30; view.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private void FillPorts(RectTransform list, bool inputs)
    {
        foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var ports = draft.Ports.Where(p => p.isInput == inputs).ToList();
        if (ports.Count == 0)
        {
            var empty = Text("None", list, inputs ? "No inputs" : "No outputs", 20, Muted, TextAlignmentOptions.Center);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            return;
        }
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            var row = Box("Port " + port.name, list, Field);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            var field = Input(row.rectTransform, "Port name", port.name, 16, 0, true);
            Stretch(field.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 1), new Vector2(6, 6), new Vector2(-246, -6));
            field.onValueChanged.AddListener(value => { port.name = value; RefreshProblemsOnly(); });
            var up = ButtonWith(row.transform, "Up", "Up", new Color(.24f, .3f, .34f, 1), () => Move(port, -1));
            Stretch((RectTransform)up.transform, new Vector2(1, 0), Vector2.one, new Vector2(-238, 6), new Vector2(-188, -6));
            up.interactable = i > 0;
            var down = ButtonWith(row.transform, "Down", "Dn", new Color(.24f, .3f, .34f, 1), () => Move(port, 1));
            Stretch((RectTransform)down.transform, new Vector2(1, 0), Vector2.one, new Vector2(-184, 6), new Vector2(-134, -6));
            down.interactable = i < ports.Count - 1;
            var flip = ButtonWith(row.transform, "Direction", inputs ? "To output" : "To input",
                inputs ? new Color(.25f, .45f, .35f, 1) : new Color(.26f, .36f, .55f, 1), () => ToggleDirection(port));
            Stretch((RectTransform)flip.transform, new Vector2(1, 0), Vector2.one, new Vector2(-130, 6), new Vector2(-6, -6));
            flip.GetComponentInChildren<TMP_Text>().fontSizeMax = 18;
        }
    }

    private void RefreshProblemsOnly()
    {
        var list = Problems();
        problems.text = list.Count == 0 ? "" : string.Join("\n", list.Take(4).Select(p => "- " + p));
        createButton.interactable = list.Count == 0;
        DrawPreview(Mathf.Max(1, Mathf.Max(draft.Ports.Count(p => p.isInput), draft.Ports.Count(p => !p.isInput))));
    }

    // Top view of the finished block: outputs along the far (front) edge, inputs along the near (back) edge.
    private void DrawPreview(int width)
    {
        foreach (Transform child in preview) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        float cardWidth = 420 - 40, cardHeight = 286 - 70;
        float cell = Mathf.Min(110, cardWidth / width);
        float bodyWidth = cell * width, bodyHeight = Mathf.Min(110, cardHeight - 80);
        var body = Box("Block", preview, new Color(.62f, .63f, .66f, 1));
        Anchor(body.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(bodyWidth, bodyHeight));
        var top = Box("Top", body.transform, new Color(.03f, .035f, .04f, 1));
        Stretch(top.rectTransform, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
        var mark = Text("Marking", top.transform, markingField != null ? markingField.text : "", 26, new Color(.92f, .95f, .92f), TextAlignmentOptions.Center);
        Stretch(mark.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
        mark.enableAutoSizing = true; mark.fontSizeMin = 10; mark.fontSizeMax = 26;

        void Pins(bool inputs)
        {
            var ports = draft.Ports.Where(p => p.isInput == inputs).ToList();
            for (int i = 0; i < ports.Count; i++)
            {
                float x = -bodyWidth * .5f + cell * (i + .5f);
                float y = inputs ? -bodyHeight * .5f - 8 : bodyHeight * .5f + 8;
                var pin = Box("Pin", preview, inputs ? InputTint : OutputTint);
                Anchor(pin.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(Mathf.Min(26, cell * .35f), 16));
                pin.rectTransform.anchoredPosition = new Vector2(x, y);
                var label = Text("Pin label", preview, ports[i].name, 18, Color.white, TextAlignmentOptions.Center);
                Anchor(label.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(cell, 26));
                label.rectTransform.anchoredPosition = new Vector2(x, inputs ? y - 24 : y + 24);
                label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 18;
            }
        }
        Pins(true); Pins(false);
        var front = Text("Front", preview, "front", 16, Muted, TextAlignmentOptions.TopRight);
        Stretch(front.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(10, -28), new Vector2(-12, -6));
        var back = Text("Back", preview, "back (your side)", 16, Muted, TextAlignmentOptions.BottomRight);
        Stretch(back.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(10, 6), new Vector2(-12, 28));
    }

    // ---- Small builders ---------------------------------------------------------------

    private void Label(RectTransform parent, string value, float top)
    {
        var label = Text(value, parent, value, 22, Muted, TextAlignmentOptions.Left);
        Stretch(label.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, top - 30), new Vector2(0, top));
    }

    private TMP_InputField Input(RectTransform parent, string name, string value, int limit, float top, bool compact = false)
    {
        var field = Box(name, parent, Field);
        if (!compact) Stretch(field.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, top - 60), new Vector2(0, top));
        var underline = Box("Underline", field.transform, new Color(Accent.r, Accent.g, Accent.b, .7f));
        Stretch(underline.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));
        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        area.SetParent(field.transform, false);
        Stretch(area, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));
        var text = Text("Text", area, "", compact ? 22 : 26, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        var placeholder = Text("Placeholder", area, "Type a name", compact ? 22 : 26, new Color(1, 1, 1, .35f), TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform, Vector2.zero, Vector2.one);
        var input = field.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = limit;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = field;
        input.caretColor = Accent;
        input.customCaretColor = true;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, .35f);
        input.SetTextWithoutNotify(value);
        return input;
    }

    private Button ButtonWith(Transform parent, string name, string label, Color color, UnityAction onClick)
    {
        var image = Box(name, parent, color);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.8f, .8f, .8f, 1);
        colors.disabledColor = new Color(.5f, .5f, .5f, .6f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        var text = Text("Label", image.transform, label, 28, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 28;
        return button;
    }

    private Image Box(string name, Transform parent, Color color)
    {
        var box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        var image = box.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);
        var text = label.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 size)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
