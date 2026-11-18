using UnityEngine;

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
    private PackagingPanel panel;
    private Material outlineMaterial;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    public bool HasFirstCorner => hasFirstCorner;
    public bool HasSecondCorner => hasSecondCorner;
    public bool IsAreaConfirmed => confirmedArea;
    public Vector3Int FirstCorner => firstCorner;
    public Vector3Int SecondCorner => secondCorner;
    public PackagingPanel Panel => panel;
    public PackageDraft LastDraft { get; private set; }
    public PackageDefinition LastCreated { get; private set; }

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

    /// <summary>Creates the package from the open window (same as its Create package button).</summary>
    public void ConfirmSelection()
    {
        if (IsConfirmationOpen && panel != null) panel.Create();
    }

    /// <summary>Closes the window and keeps the first corner (same as its Back button).</summary>
    public void RejectSelection()
    {
        if (IsConfirmationOpen && panel != null) panel.Back();
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

    // Opens the package configuration window for the selected box.
    private void ShowConfirmation()
    {
        LastDraft = PackageCompiler.Analyze(firstCorner, secondCorner);
        panel = PackagingPanel.Open(LastDraft, "Package " + (PackageLibrary.All.Count + 1), OnPackageCreated, OnPanelBack);

        previousTimeScale = Time.timeScale;
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsConfirmationOpen = true;
    }

    private void OnPackageCreated(PackageDefinition definition)
    {
        panel = null;
        CloseConfirmation();
        PackageLibrary.Add(definition);
        LastCreated = definition;
        confirmedArea = true;
        RemoveOutline();
        var player = GetComponent<Player>();
        if (player != null)
            player.ShowPlayerStatus($"Created package \"{definition.name}\". Find it in the inventory (E).");
    }

    private void OnPanelBack()
    {
        panel = null;
        CloseConfirmation();
        hasSecondCorner = false;
        UpdateOutline(firstCorner);
    }

    private void CloseConfirmation()
    {
        IsConfirmationOpen = false;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        if (panel != null) Destroy(panel.gameObject);
        panel = null;
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
