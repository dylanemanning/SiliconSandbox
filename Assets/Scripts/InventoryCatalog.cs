using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class InventoryItem
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Block prefab;
    [SerializeField] private Sprite icon;

    public string Id => id;
    public string DisplayName => displayName;
    public Block Prefab => prefab;
    public Sprite Icon => icon;

    public InventoryItem(string id,string name,Block prefab,Sprite icon)
    {
        this.id=id;displayName=name;this.prefab=prefab;this.icon=icon;
    }
}

[CreateAssetMenu(menuName="SiliconSandbox/Inventory catalog")]
public class InventoryCatalog : ScriptableObject
{
    [SerializeField] private InventoryItem[] items=Array.Empty<InventoryItem>();
    public IReadOnlyList<InventoryItem> Items => items;
    public void SetItems(InventoryItem[] value) => items=value ?? Array.Empty<InventoryItem>();

    public InventoryItem FindForPrefab(Block prefab)
    {
        if (prefab == null) return null;
        var exact = items.FirstOrDefault(item => item != null && item.Prefab == prefab);
        if (exact != null) return exact;
        // Variants share a thumbnail, but keep their original placement prefab.
        var gate = prefab.GetComponent<GateBlock>();
        if (gate != null)
            return items.FirstOrDefault(item => item?.Prefab != null &&
                item.Prefab.GetComponent<GateBlock>()?.GateType == gate.GateType);
        if (prefab.GetComponent<LogicClockSource>() != null)
            return items.FirstOrDefault(item => item?.Prefab != null && item.Prefab.GetComponent<LogicClockSource>() != null);
        if (prefab.GetComponent<LogicSignalSource>() != null)
            return items.FirstOrDefault(item => item?.Prefab != null && item.Prefab.GetComponent<LogicSignalSource>() != null &&
                item.Prefab.GetComponent<LogicClockSource>() == null);
        if (prefab.GetComponent<LogicWireBlock>() != null)
            return items.FirstOrDefault(item => item?.Prefab != null && item.Prefab.GetComponent<LogicWireBlock>() != null);
        if (prefab.GetComponent<StateViewerBlock>() != null)
            return items.FirstOrDefault(item => item?.Prefab != null && item.Prefab.GetComponent<StateViewerBlock>() != null);
        return null;
    }
}
