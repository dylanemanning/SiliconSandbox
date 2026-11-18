using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Short text drawn over an inventory or hotbar icon, such as a package's marking.</summary>
public static class ItemIconLabel
{
    private const string ChildName = "Item label";

    public static void Apply(Image icon, string text)
    {
        if (icon == null) return;
        Transform existing = icon.transform.Find(ChildName);
        if (string.IsNullOrEmpty(text))
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        TMP_Text label;
        if (existing == null)
        {
            var labelObject = new GameObject(ChildName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(icon.transform, false);
            label = labelObject.GetComponent<TMP_Text>();
            var rect = label.rectTransform;
            // The chip icon's black top panel.
            rect.anchorMin = new Vector2(.2f, .28f);
            rect.anchorMax = new Vector2(.8f, .72f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 6;
            label.fontSizeMax = 28;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(.92f, .95f, .92f);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        else label = existing.GetComponent<TMP_Text>();
        label.gameObject.SetActive(true);
        label.text = text;
    }
}
