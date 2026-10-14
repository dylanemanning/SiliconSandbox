using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class HotbarCatalogMappingTests
{
    [Test]
    public void PrefabVariantsResolveTheirComponentImageIncludingClockBeforeConstantSignal()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<InventoryCatalog>(InventoryBlockImageBuilder.CatalogPath);
        foreach (var item in catalog.Items)
        {
            Assert.That(catalog.FindForPrefab(item.Prefab), Is.SameAs(item));
            // A clone is not the catalog's exact asset reference, so it exercises type matching.
            if (item.Id=="building_block") continue;
            var clone = Object.Instantiate(item.Prefab.gameObject,new Vector3(150,10,150),Quaternion.identity);
            try { Assert.That(catalog.FindForPrefab(clone.GetComponent<Block>()), Is.SameAs(item),item.Id); }
            finally { Object.DestroyImmediate(clone); }
        }
        Assert.That(catalog.FindForPrefab(null), Is.Null);
        var unknown = new GameObject("Unknown component");
        try { Assert.That(catalog.FindForPrefab(unknown.AddComponent<Block>()), Is.Null); }
        finally { Object.DestroyImmediate(unknown); }
    }
}
