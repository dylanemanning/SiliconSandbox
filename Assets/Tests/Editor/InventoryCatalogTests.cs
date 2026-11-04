using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class InventoryCatalogTests
{
    InventoryCatalog Catalog => AssetDatabase.LoadAssetAtPath<InventoryCatalog>(InventoryBlockImageBuilder.CatalogPath);

    [Test]
    public void CatalogContainsEveryComponentTypeWithUniqueIdsIconsAndPlaceablePrefabs()
    {
        Assert.That(Catalog,Is.Not.Null);
        Assert.That(Catalog.Items.Count,Is.EqualTo(13));
        Assert.That(Catalog.Items.Select(i=>i.Id).Distinct().Count(),Is.EqualTo(13));
        Assert.That(Catalog.Items.Select(i=>i.Icon).Distinct().Count(),Is.EqualTo(13));
        foreach(var item in Catalog.Items)
        {
            Assert.That(item.DisplayName,Is.Not.Empty);
            Assert.That(item.Prefab,Is.Not.Null,item.Id);
            Assert.That(item.Icon,Is.Not.Null,item.Id);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(item.Prefab),Is.True,item.Id);
        }
        var gates=Catalog.Items.Select(i=>i.Prefab.GetComponent<GateBlock>()).Where(g=>g!=null).ToArray();
        Assert.That(gates.Select(g=>g.GateType).Distinct().Count(),Is.EqualTo(8));
        Assert.That(Catalog.Items.Count(i=>i.Prefab.GetComponent<LogicWireBlock>()!=null),Is.EqualTo(1));
        Assert.That(Catalog.Items.Count(i=>i.Prefab.GetComponent<LogicClockSource>()!=null),Is.EqualTo(1));
        Assert.That(Catalog.Items.Count(i=>i.Prefab.GetComponent<StateViewerBlock>()!=null),Is.EqualTo(1));
        Assert.That(Catalog.Items.Count(i=>i.Prefab.name=="Block"),Is.EqualTo(1));
        Assert.That(Catalog.Items.Count(i=>i.Id=="constant_signal"),Is.EqualTo(1));
    }

    [Test]
    public void GeneratedSpritesHaveVisibleModelsTransparentMarginsAndNoErrorShaderPixels()
    {
        foreach(var item in Catalog.Items)
        {
            string path=AssetDatabase.GetAssetPath(item.Icon);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency,Is.True);
            Assert.That(importer.mipmapEnabled,Is.False);
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(path)),Is.True);
                Assert.That(image.width,Is.EqualTo(256));Assert.That(image.height,Is.EqualTo(256));
                var pixels=image.GetPixels32();
                Assert.That(pixels.Count(p=>p.a==0),Is.GreaterThan(1000),item.Id);
                Assert.That(pixels.Count(p=>p.a>200),Is.GreaterThan(1000),item.Id);
                Assert.That(pixels.Count(p=>p.a>200 && p.r>240 && p.g<10 && p.b>240),Is.Zero,item.Id);
                Assert.That(pixels[0].a,Is.Zero,item.Id);
                if(item.Prefab.GetComponent<GateBlock>()!=null)
                    Assert.That(pixels.Count(p=>p.a>200 && p.r>180 && p.g>180 && p.b>180),Is.GreaterThan(5),item.Id+" white symbol");
            }
            finally { Object.DestroyImmediate(image); }
        }
    }

    [Test]
    public void SlotCanReplaceAndClearItsEntryWithoutChangingLayout()
    {
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryGridBuilder.PrefabPath));
        try
        {
            var slot=root.GetComponentsInChildren<InventorySlotImage>(true)[0];
            var rect=slot.GetComponent<RectTransform>();var original=rect.sizeDelta;
            slot.SetItem(Catalog.Items[11]);
            Assert.That(slot.Sprite,Is.EqualTo(Catalog.Items[11].Icon));
            Assert.That(slot.Item.Prefab,Is.EqualTo(Catalog.Items[11].Prefab));
            Assert.That(slot.Image.enabled,Is.True);Assert.That(slot.Image.preserveAspect,Is.True);
            slot.SetItem(null);
            Assert.That(slot.Sprite,Is.Null);Assert.That(slot.Item,Is.Null);
            Assert.That(slot.Image.enabled,Is.False);Assert.That(slot.ComponentName,Is.Empty);
            Assert.That(rect.sizeDelta,Is.EqualTo(original));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
