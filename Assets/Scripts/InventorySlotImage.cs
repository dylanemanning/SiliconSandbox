using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Image presentation and pointer hover for inventory-to-hotbar assignment.
public class InventorySlotImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private string componentName;
    [SerializeField] private InventoryItem item;
    private Image frame;
    private Color normalFrameColor;

    private void Awake()
    {
        frame = GetComponent<Image>();
        if (frame == null) return;
        normalFrameColor = frame.color;
        frame.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GetComponentInParent<InventoryGrid>()?.SetHoveredSlot(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GetComponentInParent<InventoryGrid>()?.ClearHoveredSlot(this);
    }

    public void SetHovered(bool hovered)
    {
        if (frame != null) frame.color = hovered ? new Color(.85f,.72f,.35f,1) : normalFrameColor;
    }

    private void OnDisable()
    {
        GetComponentInParent<InventoryGrid>()?.ClearHoveredSlot(this);
        SetHovered(false);
    }

    public Image Image => iconImage;
    public Sprite Sprite => iconImage != null ? iconImage.sprite : null;
    public string ComponentName => componentName;
    public InventoryItem Item => item;

    public void SetItem(InventoryItem value)
    {
        SetImage(value != null ? value.Icon : null,value != null ? value.DisplayName : "");
        item=value;
    }

    public void SetImage(Sprite sprite, string label = "")
    {
        item=null;
        componentName = sprite != null ? label : "";
        if (iconImage == null) return;
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.enabled = sprite != null;
    }
}
