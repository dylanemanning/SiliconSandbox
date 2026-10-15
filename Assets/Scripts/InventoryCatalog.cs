using System;
using System.Collections.Generic;
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
}
