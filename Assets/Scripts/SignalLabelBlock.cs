using TMPro;
using UnityEngine;

public class SignalLabelBlock : Block
{
    private static bool labelsVisible = true;

    [SerializeField, TextArea] private string labelText = "";
    [SerializeField] private bool hasAttachment;
    [SerializeField] private Vector3Int attachmentCell;
    [SerializeField] private Vector3Int attachmentFace;

    private TMP_Text textDisplay;
    private TMP_Text backTextDisplay;
    private LineRenderer connector;
    private Material panelMaterial;
    private Material connectorMaterial;
    private Transform visualsRoot;
    private Camera viewerCamera;

    public string LabelText => labelText;
    public bool HasAttachment => hasAttachment;
    public Vector3Int AttachmentCell => attachmentCell;
    public Vector3Int AttachmentFace => attachmentFace;
    public static bool AreVisible => labelsVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVisibility() => labelsVisible = true;

    private void Awake()
    {
        CreateVisuals();
        RefreshText();
        ApplyVisibility();
    }

    private void Update()
    {
        if (!hasAttachment || connector == null) return;

        connector.SetPosition(0, AttachmentPoint);
        connector.SetPosition(1, transform.position - transform.forward * 0.05f);
    }

    private void LateUpdate()
    {
        if (!labelsVisible || textDisplay == null || backTextDisplay == null) return;
        if (viewerCamera == null) viewerCamera = Camera.main;
        if (viewerCamera == null) return;

        bool viewingFront = Vector3.Dot(viewerCamera.transform.position - transform.position, transform.forward) >= 0f;
        if (textDisplay.gameObject.activeSelf != viewingFront)
            textDisplay.gameObject.SetActive(viewingFront);
        if (backTextDisplay.gameObject.activeSelf == viewingFront)
            backTextDisplay.gameObject.SetActive(!viewingFront);
    }

    private void OnDestroy()
    {
        if (panelMaterial != null) Destroy(panelMaterial);
        if (connectorMaterial != null) Destroy(connectorMaterial);
    }

    public void SetLabelText(string value)
    {
        SetLabelText(value, true);
    }

    public static bool ToggleVisibility()
    {
        labelsVisible = !labelsVisible;
        foreach (SignalLabelBlock label in FindObjectsByType<SignalLabelBlock>(FindObjectsSortMode.None))
            label.ApplyVisibility();
        return labelsVisible;
    }

    public void SetAttachment(Vector3Int cell, Vector3Int face)
    {
        if (!IsCardinal(face))
        {
            Debug.LogError($"Cannot attach signal label to invalid face {face}.", this);
            return;
        }

        attachmentCell = cell;
        attachmentFace = face;
        hasAttachment = true;
        if (connector != null) connector.enabled = true;
    }

    public void RestoreState(string value, bool attached, Vector3Int cell, Vector3Int face)
    {
        SetLabelText(value, false);
        if (attached) SetAttachment(cell, face);
    }

    private void SetLabelText(string value, bool markDirty)
    {
        value = value ?? "";
        if (labelText == value) return;

        labelText = value;
        RefreshText();
        if (markDirty) WorldSaveSystem.Instance?.MarkDirty();
    }

    private void RefreshText()
    {
        if (textDisplay != null)
            textDisplay.text = labelText;
        if (backTextDisplay != null)
            backTextDisplay.text = labelText;
    }

    private void ApplyVisibility()
    {
        if (visualsRoot != null)
            visualsRoot.gameObject.SetActive(labelsVisible);
    }

    private void CreateVisuals()
    {
        visualsRoot = new GameObject("Signal label visuals").transform;
        visualsRoot.SetParent(transform, false);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Signal label panel";
        panel.transform.SetParent(visualsRoot, false);
        panel.transform.localScale = new Vector3(0.9f, 0.5f, 0.06f);
        var panelCollider = panel.GetComponent<BoxCollider>();
        panelCollider.isTrigger = true;

        var panelRenderer = panel.GetComponent<MeshRenderer>();
        panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        panelRenderer.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Signal label panel requires the Sprites/Default shader.", this);
        }
        else
        {
            panelMaterial = new Material(shader)
            {
                color = new Color(0.12f, 0.2f, 0.28f, 0.72f)
            };
            panelRenderer.sharedMaterial = panelMaterial;
        }

        textDisplay = CreateText("Signal label front text", 0.04f, Quaternion.Euler(0f, 180f, 0f));
        backTextDisplay = CreateText("Signal label back text", -0.04f, Quaternion.identity);

        var connectorObject = new GameObject("Signal label connector");
        connectorObject.transform.SetParent(visualsRoot, false);
        connector = connectorObject.AddComponent<LineRenderer>();
        connector.useWorldSpace = true;
        connector.positionCount = 2;
        connector.startWidth = 0.018f;
        connector.endWidth = 0.018f;
        connector.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        connector.receiveShadows = false;
        connector.startColor = connector.endColor = new Color(0.95f, 0.72f, 0.28f, 0.85f);
        if (shader != null)
        {
            connectorMaterial = new Material(shader)
            {
                color = new Color(0.95f, 0.72f, 0.28f, 0.85f)
            };
            connector.sharedMaterial = connectorMaterial;
        }
        connector.enabled = hasAttachment;
    }

    private TMP_Text CreateText(string objectName, float depth, Quaternion rotation)
    {
        var textObject = new GameObject(objectName, typeof(TextMeshPro));
        textObject.transform.SetParent(visualsRoot, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, depth);
        textObject.transform.localRotation = rotation;
        TMP_Text text = textObject.GetComponent<TextMeshPro>();
        text.richText = false;
        text.fontSize = 2.4f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta = new Vector2(0.84f, 0.44f);
        return text;
    }

    private Vector3 AttachmentPoint => (Vector3)attachmentCell + (Vector3)attachmentFace * 0.5f;

    private static bool IsCardinal(Vector3Int face)
    {
        return Mathf.Abs(face.x) + Mathf.Abs(face.y) + Mathf.Abs(face.z) == 1;
    }
}
