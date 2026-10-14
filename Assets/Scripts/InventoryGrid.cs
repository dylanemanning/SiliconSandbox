using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Inventory catalog display and hover/number-key hotbar assignment.
[DefaultExecutionOrder(-100)]
public class InventoryGrid : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private InventoryCatalog catalog;
    private static InventoryGrid instance;
    private static int closedFrame = -1;
    private bool open;
    private InventorySlotImage hoveredSlot;
    private HotbarManager hotbar;
    private TMP_Text assignmentHint;
    private const string DefaultHint = "Hover a block, press 1-9 | E / Esc to close";

    public static bool IsOpen => instance != null && instance.open;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    public GameObject Panel => panel;
    public InventoryCatalog Catalog => catalog;
    public InventorySlotImage HoveredSlot => hoveredSlot;
    public string AssignmentHint => assignmentHint != null ? assignmentHint.text : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { instance = null; closedFrame = -1; }

    private void OnEnable()
    {
        instance = this;
        open = false;
        if (panel != null) panel.SetActive(false);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.name == "Close hint") assignmentHint = label;
        SetHint(DefaultHint);
        RefreshImages();
    }

    public void RefreshImages()
    {
        ClearHoveredSlot(hoveredSlot);
        var slots=GetComponentsInChildren<InventorySlotImage>(true);
        for(int i=0;i<slots.Length;i++)
            slots[i].SetItem(catalog!=null && i<catalog.Items.Count ? catalog.Items[i] : null);
    }

    private void Update()
    {
        if (Keyboard.current == null || PauseManager.IsPaused) return;
        if (open && (Keyboard.current.escapeKey.wasPressedThisFrame
            || KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause)))
            SetOpen(false);
        else if (Keyboard.current.eKey.wasPressedThisFrame) SetOpen(!open);
    }

    // Resolve pointer enter/exit events before using the hovered entry. Update stays
    // early so opening/closing still blocks player and hotbar input in that frame.
    private void LateUpdate()
    {
        if (Keyboard.current == null || PauseManager.IsPaused) return;
        if (open && hoveredSlot != null && hoveredSlot.Item != null)
        {
            for (int i = 0; i < 9; i++)
                if (Keyboard.current[(Key)((int)Key.Digit1+i)].wasPressedThisFrame)
                {
                    AssignHoveredItem(i);
                    break;
                }
        }
    }

    public void SetHoveredSlot(InventorySlotImage slot)
    {
        if (!open || PauseManager.IsPaused) return;
        ClearHoveredSlot(hoveredSlot);
        if (slot == null || slot.Item == null || slot.GetComponentInParent<InventoryGrid>() != this) return;
        hoveredSlot = slot;
        slot.SetHovered(true);
        SetHint(slot.ComponentName+": press 1-9 | E / Esc to close");
    }

    public void ClearHoveredSlot(InventorySlotImage slot)
    {
        if (hoveredSlot != slot) return;
        if (hoveredSlot != null) hoveredSlot.SetHovered(false);
        hoveredSlot = null;
        SetHint(DefaultHint);
    }

    public bool AssignHoveredItem(int destination)
    {
        if (!open || PauseManager.IsPaused || hoveredSlot == null || hoveredSlot.Item == null) return false;
        if (hotbar == null)
        {
            var player = FindFirstObjectByType<Player>();
            hotbar = player != null ? player.hotbar : null;
        }
        if (hotbar == null || !hotbar.AssignItem(destination,hoveredSlot.Item)) return false;
        SetHint(hoveredSlot.ComponentName+" assigned to slot "+(destination+1)+" | E / Esc to close");
        return true;
    }

    private void SetHint(string text)
    {
        if (assignmentHint != null) assignmentHint.text = text;
    }

    public void SetOpen(bool value)
    {
        if (value == open || (value && PauseManager.IsPaused)) return;
        ClearHoveredSlot(hoveredSlot);
        open = value;
        if (panel != null) panel.SetActive(value);
        if (!value) closedFrame = Time.frameCount;
        Cursor.lockState = value || PauseManager.IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value || PauseManager.IsPaused;
        if (value)
        {
            var player = FindFirstObjectByType<Player>();
            if (player == null) return;
            if (player.blockHighlighter != null) player.blockHighlighter.SetActive(false);
            var body = player.GetComponent<Rigidbody>();
            if (body != null) body.linearVelocity = new Vector3(0,body.linearVelocity.y,0);
        }
    }

    public static void Close() { if (instance != null) instance.SetOpen(false); }

    private void OnDisable()
    {
        if (instance != this) return;
        if (open) SetOpen(false);
        instance = null;
    }
}
