using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Inventory catalog display and the ways an item moves into the hotbar:
//   1. hover + number key        -> that hotbar slot
//   2. hover + double select     -> the selected hotbar slot
//   3. hover + quick select      -> the leftmost empty slot (slot 9 when full)
//   4. press and hold select     -> drag the item onto any hotbar slot
// The scroll wheel changes the selected hotbar slot while the inventory is open.
[DefaultExecutionOrder(-100)]
public class InventoryGrid : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private InventoryCatalog catalog;
    [Tooltip("Share of the screen's width and height the inventory panel covers.")]
    [SerializeField, Range(.3f, 1f)] private float screenFraction = .8f;
    [SerializeField] private float doubleClickSeconds = .35f;
    [SerializeField] private float dragHoldSeconds = .2f;
    [SerializeField] private float dragStartPixels = 12f;

    private static InventoryGrid instance;
    private static int closedFrame = -1;
    private bool open;
    private InventorySlotImage hoveredSlot;
    private HotbarManager hotbar;
    private TMP_Text assignmentHint;
    private Canvas hotbarCanvas;
    private int hotbarSortingOrder;
    private Vector2 laidOutSize;

    // Select-button state for double-click and drag.
    private InventorySlotImage pressSlot, lastClickSlot;
    private float pressTime, lastClickTime = float.NegativeInfinity;
    private Vector2 pressPosition;
    private bool selectHeld;
    private InventoryItem draggedItem;
    private Image dragIcon;

    private const string DefaultHint =
        "Hover a block: 1-9, double-click, Shift-click, or drag to the hotbar | Scroll selects a slot | E / Esc to close";

    public static bool IsOpen => instance != null && instance.open;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    public GameObject Panel => panel;
    public InventoryCatalog Catalog => catalog;
    public InventorySlotImage HoveredSlot => hoveredSlot;
    public string AssignmentHint => assignmentHint != null ? assignmentHint.text : "";
    public bool IsDragging => draggedItem != null;
    public float ScreenFraction => screenFraction;
    public RectTransform PanelRect => FindRect("Inventory panel");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { instance = null; closedFrame = -1; }

    private void OnEnable()
    {
        instance = this;
        PackageLibrary.Changed += RefreshImages;
        open = false;
        if (panel != null) panel.SetActive(false);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.name == "Close hint") assignmentHint = label;
        SetHint(DefaultHint);
        RefreshImages();
    }

    /// <summary>Shows the catalog, then every package the player has created.</summary>
    public void RefreshImages()
    {
        ClearHoveredSlot(hoveredSlot);
        var entries = new System.Collections.Generic.List<InventoryItem>();
        if (catalog != null) entries.AddRange(catalog.Items);
        entries.AddRange(PackageLibrary.InventoryItems);
        var slots = GetComponentsInChildren<InventorySlotImage>(true);
        for (int i = 0; i < slots.Length; i++)
            slots[i].SetItem(i < entries.Count ? entries[i] : null);
    }

    private void Update()
    {
        if (Keyboard.current == null || PauseManager.IsPaused || PackagingWandController.IsConfirmationOpen) return;
        if (open && (Keyboard.current.escapeKey.wasPressedThisFrame
            || KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause)))
            SetOpen(false);
        else if (Keyboard.current.eKey.wasPressedThisFrame) SetOpen(!open);
    }

    // Resolve pointer enter/exit events before using the hovered entry. Update stays
    // early so opening/closing still blocks player and hotbar input in that frame.
    private void LateUpdate()
    {
        if (!open || PauseManager.IsPaused || PackagingWandController.IsConfirmationOpen) return;
        if (Screen.width != laidOutSize.x || Screen.height != laidOutSize.y) ApplyLayout();
        ResolveHotbar();

        float scroll = KeybindSettings.ReadHotbarScroll();
        if (scroll != 0 && hotbar != null) hotbar.ScrollSelection(scroll);

        UpdateSelectAndDrag();
        if (IsDragging) return;

        if (hoveredSlot == null || hoveredSlot.Item == null || Keyboard.current == null) return;
        for (int i = 0; i < 9; i++)
            if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
            {
                AssignHoveredItem(i);
                return;
            }
        if (KeybindSettings.InventoryQuickSelectBinding.WasPressedThisFrame())
            QuickAssignHoveredItem();
    }

    private void UpdateSelectAndDrag()
    {
        var select = KeybindSettings.InventorySelectBinding;
        Vector2 pointer = PointerPosition();
        if (select.WasPressedThisFrame())
        {
            selectHeld = true;
            pressSlot = hoveredSlot != null && hoveredSlot.Item != null ? hoveredSlot : null;
            pressTime = Time.unscaledTime;
            pressPosition = pointer;
        }

        if (!selectHeld) return;
        if (select.PrimaryIsPressed())
        {
            if (!IsDragging && pressSlot != null &&
                (Time.unscaledTime - pressTime >= dragHoldSeconds || (pointer - pressPosition).magnitude >= dragStartPixels))
                BeginDrag(pressSlot.Item);
            if (IsDragging) MoveDragIcon(pointer);
            return;
        }

        // Released.
        selectHeld = false;
        if (IsDragging)
        {
            DropDraggedItem(pointer);
            return;
        }
        if (pressSlot == null) return;
        if (lastClickSlot == pressSlot && Time.unscaledTime - lastClickTime <= doubleClickSeconds)
        {
            ResolveHotbar();
            if (hotbar != null && hoveredSlot == pressSlot) AssignHoveredItem(hotbar.SelectedSlotIndex);
            lastClickSlot = null;
            lastClickTime = float.NegativeInfinity;
        }
        else
        {
            lastClickSlot = pressSlot;
            lastClickTime = Time.unscaledTime;
        }
        pressSlot = null;
    }

    private static Vector2 PointerPosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private void BeginDrag(InventoryItem item)
    {
        draggedItem = item;
        lastClickSlot = null;
        if (dragIcon == null)
        {
            var iconObject = new GameObject("Dragged item", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(transform, false);
            dragIcon = iconObject.GetComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.preserveAspect = true;
        }
        dragIcon.transform.SetAsLastSibling();
        float size = FindGrid() != null ? FindGrid().cellSize.x : 80f;
        dragIcon.rectTransform.sizeDelta = Vector2.one * size;
        dragIcon.sprite = item.Icon;
        dragIcon.enabled = true;
        dragIcon.gameObject.SetActive(true);
        SetHint(item.DisplayName + ": drop on a hotbar slot");
    }

    private void MoveDragIcon(Vector2 screenPosition)
    {
        if (dragIcon == null) return;
        var canvas = GetComponentInParent<Canvas>();
        var parent = (RectTransform)dragIcon.transform.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition,
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, out var local))
            dragIcon.rectTransform.localPosition = local;
    }

    private void DropDraggedItem(Vector2 screenPosition)
    {
        var item = draggedItem;
        CancelDrag();
        ResolveHotbar();
        int destination = HotbarSlotAt(screenPosition);
        if (hotbar != null && destination >= 0 && hotbar.AssignItem(destination, item))
            SetHint(item.DisplayName + " assigned to slot " + (destination + 1) + " | E / Esc to close");
        else SetHint(DefaultHint);
    }

    private void CancelDrag()
    {
        draggedItem = null;
        pressSlot = null;
        if (dragIcon != null) dragIcon.gameObject.SetActive(false);
    }

    /// <summary>The hotbar slot under a screen position, or -1.</summary>
    public int HotbarSlotAt(Vector2 screenPosition)
    {
        ResolveHotbar();
        if (hotbar == null) return -1;
        for (int i = 0; i < hotbar.SlotCount; i++)
        {
            var slot = hotbar.GetSlot(i);
            if (slot == null) continue;
            var canvas = slot.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, screenPosition, camera))
                return i;
        }
        return -1;
    }

    public void SetHoveredSlot(InventorySlotImage slot)
    {
        if (!open || PauseManager.IsPaused) return;
        ClearHoveredSlot(hoveredSlot);
        if (slot == null || slot.Item == null || slot.GetComponentInParent<InventoryGrid>() != this) return;
        hoveredSlot = slot;
        slot.SetHovered(true);
        if (!IsDragging)
            SetHint(slot.ComponentName + ": 1-9, double-click, Shift-click, or drag to the hotbar | E / Esc to close");
    }

    public void ClearHoveredSlot(InventorySlotImage slot)
    {
        if (hoveredSlot != slot) return;
        if (hoveredSlot != null) hoveredSlot.SetHovered(false);
        hoveredSlot = null;
        if (!IsDragging) SetHint(DefaultHint);
    }

    public bool AssignHoveredItem(int destination)
    {
        if (!open || PauseManager.IsPaused || hoveredSlot == null || hoveredSlot.Item == null) return false;
        ResolveHotbar();
        if (hotbar == null || !hotbar.AssignItem(destination, hoveredSlot.Item)) return false;
        SetHint(hoveredSlot.ComponentName + " assigned to slot " + (destination + 1) + " | E / Esc to close");
        return true;
    }

    /// <summary>Puts the hovered item in the leftmost empty hotbar slot, or slot 9 when none is empty.</summary>
    public bool QuickAssignHoveredItem()
    {
        ResolveHotbar();
        if (hotbar == null || hotbar.SlotCount == 0) return false;
        int destination = hotbar.FirstEmptySlot();
        if (destination < 0) destination = hotbar.SlotCount - 1;
        return AssignHoveredItem(destination);
    }

    private void ResolveHotbar()
    {
        if (hotbar != null) return;
        var player = FindFirstObjectByType<Player>();
        hotbar = player != null ? player.hotbar : null;
    }

    private void SetHint(string text)
    {
        if (assignmentHint != null) assignmentHint.text = text;
    }

    public void SetOpen(bool value)
    {
        if (value == open || (value && PauseManager.IsPaused)) return;
        ClearHoveredSlot(hoveredSlot);
        CancelDrag();
        selectHeld = false;
        lastClickSlot = null;
        open = value;
        if (panel != null) panel.SetActive(value);
        if (value) ApplyLayout();
        RaiseHotbar(value);
        if (!value) closedFrame = Time.frameCount;
        Cursor.lockState = value || PauseManager.IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value || PauseManager.IsPaused;
        if (value)
        {
            var player = FindFirstObjectByType<Player>();
            if (player == null) return;
            if (player.blockHighlighter != null) player.blockHighlighter.SetActive(false);
            var body = player.GetComponent<Rigidbody>();
            if (body != null) body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
        }
    }

    // The hotbar draws above the inventory overlay while it is open, so items can be
    // dropped on it and the selected slot stays visible.
    private void RaiseHotbar(bool raise)
    {
        ResolveHotbar();
        if (hotbar == null) return;
        if (hotbarCanvas == null)
        {
            hotbarCanvas = hotbar.GetComponentInParent<Canvas>();
            if (hotbarCanvas == null) return;
            hotbarSortingOrder = hotbarCanvas.sortingOrder;
        }
        var own = GetComponentInParent<Canvas>();
        hotbarCanvas.sortingOrder = raise && own != null
            ? Mathf.Max(hotbarSortingOrder, own.sortingOrder + 1) : hotbarSortingOrder;
    }

    /// <summary>
    /// Sizes the panel to screenFraction of the screen, above the hotbar, and fits
    /// the square 9-column grid inside it with 10% gaps.
    /// </summary>
    public void ApplyLayout()
    {
        laidOutSize = new Vector2(Screen.width, Screen.height);
        var panelRect = PanelRect;
        var grid = FindGrid();
        if (panelRect == null || grid == null) return;

        // Leave the bottom of the screen for the hotbar; the panel spans the rest.
        float margin = (1f - screenFraction) * .5f;
        float bottom = Mathf.Clamp01(HotbarTopFraction() + .01f);
        float top = Mathf.Min(1f, bottom + screenFraction);
        panelRect.anchorMin = new Vector2(margin, bottom);
        panelRect.anchorMax = new Vector2(1f - margin, top);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panelRect.pivot = new Vector2(.5f, .5f);

        var canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
        Vector2 panelSize = Vector2.Scale(canvasRect.rect.size, panelRect.anchorMax - panelRect.anchorMin);
        float header = Mathf.Max(44f, panelSize.y * .1f);

        Place(FindRect("Title"), new Vector2(.5f, 1f), new Vector2(0, -header * .6f), new Vector2(panelSize.x * .9f, header));
        Place(FindRect("Close hint"), new Vector2(.5f, 0f), new Vector2(0, header * .55f), new Vector2(panelSize.x * .95f, header));

        var slotsRect = (RectTransform)grid.transform;
        slotsRect.anchorMin = slotsRect.anchorMax = new Vector2(.5f, .5f);
        slotsRect.anchoredPosition = Vector2.zero;
        int columns = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.Max(1, Mathf.CeilToInt(grid.transform.childCount / (float)columns));
        float availableWidth = panelSize.x * .94f;
        float availableHeight = panelSize.y - header * 2.4f;
        float cell = Mathf.Floor(Mathf.Min(
            availableWidth / (columns + (columns - 1) * .1f),
            availableHeight / (rows + (rows - 1) * .1f)));
        cell = Mathf.Max(16f, cell);
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(cell * .1f, cell * .1f);
        grid.childAlignment = TextAnchor.MiddleCenter;
        slotsRect.sizeDelta = new Vector2(columns * cell + (columns - 1) * cell * .1f, rows * cell + (rows - 1) * cell * .1f);

        foreach (var label in new[] { FindRect("Title"), FindRect("Close hint") })
        {
            var text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text == null) continue;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14;
            text.fontSizeMax = label.name == "Title" ? 48 : 28;
        }
    }

    // Top edge of the hotbar panel as a fraction of the screen height.
    private float HotbarTopFraction()
    {
        ResolveHotbar();
        if (hotbar == null || hotbar.SlotCount == 0 || Screen.height <= 0) return .15f;
        var row = hotbar.GetSlot(0).transform.parent;
        var hotbarPanel = row != null && row.parent != null ? row.parent as RectTransform : null;
        var canvas = hotbar.GetComponentInParent<Canvas>();
        if (hotbarPanel == null || canvas == null) return .15f;
        var corners = new Vector3[4];
        hotbarPanel.GetWorldCorners(corners);
        Vector2 top = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, corners[1]);
        return Mathf.Clamp(top.y / Screen.height, 0f, .5f);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private GridLayoutGroup FindGrid() => GetComponentInChildren<GridLayoutGroup>(true);

    private RectTransform FindRect(string objectName)
    {
        foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            if (rect.name == objectName) return rect;
        return null;
    }

    public static void Close() { if (instance != null) instance.SetOpen(false); }

    private void OnDisable()
    {
        PackageLibrary.Changed -= RefreshImages;
        if (instance != this) return;
        if (open) SetOpen(false);
        instance = null;
    }
}
