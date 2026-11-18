using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Art fixes from user acceptance testing:
//   - The Constant Signal "1" is drawn with a flag at the top and a base bar, so
//     players read it as the digit 1 rather than a plain line or circuit symbol.
//   - The Building Block (sandstone) inventory/hotbar icon is re-rendered from the
//     current block prefab, so it matches the block in the world.
public static class PostUatArtBuilder
{
    const string AtlasPath = "Assets/Models/Textures/source-symbol-atlas.png";
    // The "1" cell of the 4x4 atlas, in Texture2D coordinates (origin bottom-left).
    static readonly RectInt OneCell = new RectInt(384, 0, 128, 128);

    [MenuItem("SiliconSandbox/Art/Apply post-UAT art fixes")]
    public static void Build()
    {
        RedrawSourceOne();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        InventoryBlockImageBuilder.GenerateIcons("constant_signal_on", "building_block");
        AssetDatabase.SaveAssets();
        Debug.Log("POST_UAT_ART_BUILD_PASS");
    }

    // Batch entry point: Unity -batchmode -executeMethod PostUatArtBuilder.BuildAndQuit
    public static void BuildAndQuit()
    {
        try { Build(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void RedrawSourceOne()
    {
        var atlas = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!atlas.LoadImage(File.ReadAllBytes(AtlasPath))) throw new Exception("Could not read " + AtlasPath);

        // Sample the existing stroke and background colours so the glyph matches the "0".
        Color stroke = atlas.GetPixel(OneCell.x + 64, OneCell.y + 64);
        Color background = atlas.GetPixel(OneCell.x + 30, OneCell.y + 30);
        if (stroke.grayscale < .5f || background.grayscale > .2f)
            throw new Exception("Unexpected atlas layout: the '1' cell was not found at " + OneCell);

        // Clear the glyph area inside the cell's boundary ring.
        for (int y = OneCell.y + 16; y < OneCell.yMax - 16; y++)
        for (int x = OneCell.x + 16; x < OneCell.xMax - 16; x++)
            atlas.SetPixel(x, y, background);

        // Stem, slanted flag and base bar of an upright "1", in cell image space
        // (origin top-left), then turned so the digit stands upright for a player
        // looking at the top face from behind a block they placed. On the model the
        // atlas +x axis runs to the block's front (+Z) and atlas up to its +X side,
        // so the glyph's up maps to image +x and its right to image -y.
        const float radius = 3.2f;
        var upright = new (Vector2 a, Vector2 b)[]
        {
            (new Vector2(64, 22), new Vector2(64, 104)),  // stem
            (new Vector2(64, 22), new Vector2(44, 40)),   // flag
            (new Vector2(44, 104), new Vector2(84, 104)), // base bar
        };
        Vector2 Turn(Vector2 p) => new Vector2(64 + (64 - p.y), 64 - (p.x - 64));
        var segments = Array.ConvertAll(upright, s => (Turn(s.a), Turn(s.b)));
        for (int y = 16; y < 112; y++)
        for (int x = 16; x < 112; x++)
        {
            var point = new Vector2(x + .5f, y + .5f);
            float distance = float.PositiveInfinity;
            foreach (var (a, b) in segments) distance = Mathf.Min(distance, DistanceToSegment(point, a, b));
            float coverage = Mathf.Clamp01(radius + .5f - distance);
            if (coverage <= 0) continue;
            atlas.SetPixel(OneCell.x + x, OneCell.yMax - 1 - y, Color.Lerp(background, stroke, coverage));
        }
        atlas.Apply();
        File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(atlas);
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(point, a + ab * t);
    }
}
