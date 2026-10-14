using UnityEngine;
using UnityEngine.UI;

// Catalog entry image presentation. Selection and hotbar transfers are separate work.
public class InventorySlotImage : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private string componentName;
    [SerializeField] private InventoryItem item;

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
