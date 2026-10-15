using UnityEngine;
using UnityEngine.UI;

public class HotbarSlot : MonoBehaviour
{
    [SerializeField] private Block blockPrefab;
    [SerializeField] private Image background;
    [SerializeField] private Image blockIcon;

    [SerializeField] private Color normalColor = new Color(.42f,.46f,.51f,1);
    [SerializeField] private Color selectedColor = new Color(.85f,.72f,.35f,1);

    public Block BlockPrefab => blockPrefab;
    public InventoryItem Item { get; private set; }
    public Image Icon => blockIcon;
    public Image Frame => background;

    public void RefreshIcon(InventoryCatalog catalog)
    {
        Item = catalog != null ? catalog.FindForPrefab(blockPrefab) : null;
        SetIcon(Item != null ? Item.Icon : null);
    }

    public void SetItem(InventoryItem item)
    {
        if (item != null && item.Prefab == null) return;
        Item = item;
        blockPrefab = item != null ? item.Prefab : null;
        SetIcon(item != null ? item.Icon : null);
    }

    public void SetSelected(bool selected)
    {
        if (selected)
        {
            if (background != null)
                background.color = selectedColor;
        }
        else
        {
            if (background != null)
                background.color = normalColor;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (blockIcon == null) return;
        blockIcon.sprite = icon;
        blockIcon.enabled = icon != null;
        blockIcon.preserveAspect = true;
    }
}
