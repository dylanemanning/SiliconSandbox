using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class InventoryCatalogDisplayTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale=1;WorldSaveSystem.PendingWorldName=null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");yield return null;
    }

    [UnityTest]
    public IEnumerator GameplayDisplaysCatalogImagesAndLeavesRemainingSlotsEmpty()
    {
        WorldSaveSystem.PendingWorldName="TC_InventoryImages";
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
        var inventory=Object.FindFirstObjectByType<InventoryGrid>();
        Assert.That(inventory.Catalog,Is.Not.Null);
        inventory.SetOpen(true);Canvas.ForceUpdateCanvases();
        var slots=inventory.GetComponentsInChildren<InventorySlotImage>(true);
        Assert.That(slots.Length,Is.EqualTo(36));
        Assert.That(slots.Count(s=>s.Sprite!=null),Is.EqualTo(14));
        Assert.That(slots.Count(s=>s.Item==null),Is.EqualTo(22));
        for(int i=0;i<inventory.Catalog.Items.Count;i++)
        {
            Assert.That(slots[i].Sprite,Is.EqualTo(inventory.Catalog.Items[i].Icon));
            Assert.That(slots[i].Item.Prefab,Is.EqualTo(inventory.Catalog.Items[i].Prefab));
            Assert.That(slots[i].Image.enabled,Is.True);Assert.That(slots[i].Image.preserveAspect,Is.True);
            Assert.That(slots[i].Image.raycastTarget,Is.False);
        }
        var grid=inventory.GetComponentInChildren<GridLayoutGroup>();
        // Cells scale with the 80%-of-screen panel but stay square with 10% gaps.
        Assert.That(grid.cellSize.x,Is.EqualTo(grid.cellSize.y));Assert.That(grid.cellSize.x,Is.GreaterThanOrEqualTo(80));
        Assert.That(grid.spacing.x,Is.EqualTo(grid.cellSize.x*.1f).Within(.001f));
        Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,Is.EqualTo(1),"Icons must not instantiate preview cameras in the game.");
        slots[0].SetItem(null);inventory.RefreshImages();
        Assert.That(slots[0].Item.Id,Is.EqualTo(inventory.Catalog.Items[0].Id));
        inventory.SetOpen(false);inventory.SetOpen(true);
        Assert.That(slots.Count(s=>s.Sprite!=null),Is.EqualTo(14));
        LogAssert.NoUnexpectedReceived();
    }
}
