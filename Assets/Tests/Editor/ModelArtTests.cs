using System.IO;
using NUnit.Framework;
using UnityEngine;

public class ModelArtTests
{
    [Test]
    public void ImportedModelsPreserveApprovedDimensionsPinsAndUrpMaterials()
    {
        ModelArtBuilder.VerifyAll();
    }

    [Test]
    public void UnityAssetsDoNotRequireBlenderOrContainInventoryImageModels()
    {
        Assert.That(Directory.GetFiles(Application.dataPath,"*.blend",SearchOption.AllDirectories),Is.Empty);
        Assert.That(Directory.GetFiles(Application.dataPath+"/Models","*inventory_image*",SearchOption.AllDirectories),Is.Empty);
    }
}
