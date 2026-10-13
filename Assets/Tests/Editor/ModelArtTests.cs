using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class ModelArtTests
{
    [Test]
    public void LinearArtistPaletteIsNotDarkenedByImport()
    {
        var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/SS_shell.mat");
        // Blender's linear 0.43 red channel is about 0.69 in an sRGB color field.
        Assert.That(body.GetColor("_BaseColor").r, Is.InRange(.68f,.70f));
        foreach (string name in new[]{"AND", "source-symbol-atlas"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Models/Textures/"+name+".png");
            Assert.That(importer.sRGBTexture,Is.False,"Procedural palette bytes already store linear values.");
        }
    }
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
