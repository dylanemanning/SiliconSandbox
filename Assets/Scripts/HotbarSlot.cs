using UnityEngine;
using UnityEngine.UI;

public class HotbarSlot : MonoBehaviour
{
    [SerializeField] private Block blockPrefab;
    [SerializeField] private Image background;
    [SerializeField] private Image blockIcon;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;

    public Block BlockPrefab => blockPrefab;

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
        blockIcon.sprite = icon;
    }
}