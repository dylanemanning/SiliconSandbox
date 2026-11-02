using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WireTopologyTests
{
    private static readonly Vector3Int Origin = new Vector3Int(20000, 20000, 20000);
    private readonly List<LogicSignalBlock> placed = new List<LogicSignalBlock>();

    private static int Bit(Vector3Int face) => LogicWireBlock.GetDirectionBit(face);

    private T Place<T>(Vector3Int offset, Quaternion? rotation = null) where T : LogicSignalBlock
    {
        var go = new GameObject("Topology test " + typeof(T).Name);
        go.transform.SetPositionAndRotation(Origin + offset, rotation ?? Quaternion.identity);
        go.AddComponent<Block>();
        T block = go.AddComponent<T>();
        placed.Add(block);
        typeof(LogicSignalBlock).GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(block, null);
        return block;
    }

    private LogicWireBlock Wire(Vector3Int offset, int? endpoints = 0, Quaternion? rotation = null)
    {
        LogicWireBlock wire = Place<LogicWireBlock>(offset, rotation);
        if (endpoints.HasValue) wire.SetEndpointMask(endpoints.Value);
        return wire;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (LogicSignalBlock block in placed)
        {
            // A deleted EditMode object may not receive OnDisable. The managed
            // unregister routine still releases only this fixture's cells.
            typeof(LogicSignalBlock).GetMethod("Unregister", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(block, null);
            if (block != null) Object.DestroyImmediate(block.gameObject);
        }
        placed.Clear();
    }

    [TestCase(1, 0, 0)] [TestCase(-1, 0, 0)]
    [TestCase(0, 1, 0)] [TestCase(0, -1, 0)]
    [TestCase(0, 0, 1)] [TestCase(0, 0, -1)]
    public void TouchingArmsConnectWithoutASeparateJoinInEveryDirection(int x, int y, int z)
    {
        var face = new Vector3Int(x, y, z);
        var first = Wire(Vector3Int.zero, Bit(face));
        var second = Wire(face, Bit(-face));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.True);
        Assert.That(second.ConnectsTo(first, first.GridPosition), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(1));
        second.SetEndpointMask(0);
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.False);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(2));
    }

    [Test]
    public void NewAndReplacementComponentsConnectWhenTheirPinsTouchTheWire()
    {
        var wire = Wire(Vector3Int.zero, Bit(Vector3Int.left));
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False);
        Assert.That(wire.ConnectsTo(source, source.GridPosition), Is.True);
        typeof(LogicSignalBlock).GetMethod("Unregister", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(source, null);
        Object.DestroyImmediate(source.gameObject);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.True);
        Assert.That(wire.EndpointMask, Is.EqualTo(Bit(Vector3Int.left)), "Removing a component leaves its physical wire arm.");

        var replacement = Place<LogicSignalSource>(Vector3Int.left, Quaternion.identity);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.True, "A blank component face does not connect.");
        replacement.transform.rotation = Quaternion.Euler(0, 90, 0);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False, "The facing pin connects from geometry alone.");
    }

    [Test]
    public void DeletingARunDoesNotTrimANeighborArmThatNeverTouchedIt()
    {
        var start = Wire(Vector3Int.zero, Bit(Vector3Int.forward));
        var end = Wire(Vector3Int.forward, Bit(Vector3Int.back));
        var unrelated = Wire(Vector3Int.left, Bit(Vector3Int.right));
        Assert.That(start.ConnectsTo(unrelated, unrelated.GridPosition), Is.False);
        Assert.That(WireTopology.DeleteRun(start), Is.EqualTo(2));
        Assert.That(unrelated != null, Is.True);
        Assert.That(unrelated.EndpointMask, Is.EqualTo(Bit(Vector3Int.right)));
    }

    [Test]
    public void DeletingTheCellBesideAFreeEndRemovesItsNowEmptyNeighbor()
    {
        var middle = Wire(Vector3Int.zero, Bit(Vector3Int.left) | Bit(Vector3Int.right));
        var freeEnd = Wire(Vector3Int.right, Bit(Vector3Int.left));
        freeEnd.GetComponent<Block>().RegisterCells();
        Assert.That(Block.IsCellOccupied(Origin + Vector3Int.right), Is.True);
        WireTopology.Detach(middle);
        Assert.That(freeEnd == null, Is.True, "No invisible zero-arm block should remain.");
        Assert.That(Block.IsCellOccupied(Origin + Vector3Int.right), Is.False);
    }

    [Test]
    public void DeletingBentRunRetainsJunctionOtherBranchesAndAttachedPin()
    {
        var junction = Wire(Vector3Int.zero);
        var left = Wire(Vector3Int.left);
        var forward = Wire(Vector3Int.forward);
        var start = Wire(Vector3Int.right);
        var bend = Wire(Vector3Int.right * 2);
        var end = Wire(Vector3Int.right * 2 + Vector3Int.forward);
        var unrelated = Wire(Vector3Int.right + Vector3Int.forward, Bit(Vector3Int.forward));
        var pin = Place<LogicSignalSource>(Vector3Int.right * 3 + Vector3Int.forward, Quaternion.Euler(0, 90, 0));
        WireTopology.Join(junction, left);
        WireTopology.Join(junction, forward);
        WireTopology.Join(junction, start);
        WireTopology.Join(start, bend);
        WireTopology.Join(bend, end);
        WireTopology.JoinPin(end, pin, pin.GridPosition, Vector3Int.left);
        CollectionAssert.AreEquivalent(new[] { start, bend, end }, WireTopology.GetRun(bend));
        Assert.That(WireTopology.GetRun(junction), Is.Empty, "A junction click cannot select a branch safely.");

        Assert.That(WireTopology.DeleteRun(bend), Is.EqualTo(3));
        Assert.That(junction != null && left != null && forward != null && pin != null && unrelated != null, Is.True);
        Assert.That(junction.EndpointMask, Is.EqualTo(Bit(Vector3Int.left) | Bit(Vector3Int.forward)));
        Assert.That(junction.ConnectsTo(left, left.GridPosition), Is.True);
        Assert.That(junction.ConnectsTo(forward, forward.GridPosition), Is.True);
        Assert.That(unrelated.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward)));
    }

    [Test]
    public void ClosedLoopDeletesCompletely()
    {
        var loop = new[]
        {
            Wire(Vector3Int.zero), Wire(Vector3Int.right),
            Wire(Vector3Int.right + Vector3Int.forward), Wire(Vector3Int.forward)
        };
        for (int i = 0; i < loop.Length; i++) WireTopology.Join(loop[i], loop[(i + 1) % loop.Length]);
        CollectionAssert.AreEquivalent(loop, WireTopology.GetRun(loop[1]));
        Assert.That(WireTopology.DeleteRun(loop[2]), Is.EqualTo(4));
        Assert.That(loop.All(w => w == null), Is.True);
    }

    [Test]
    public void JunctionWithAnOpenArmIsPreservedAndRejectsRunDeletion()
    {
        var center = Wire(Vector3Int.zero, Bit(Vector3Int.up));
        var left = Wire(Vector3Int.left);
        var right = Wire(Vector3Int.right);
        WireTopology.Join(center, left);
        WireTopology.Join(center, right);
        Assert.That(WireTopology.DeleteRun(center), Is.Zero);
        CollectionAssert.AreEquivalent(new[] { right }, WireTopology.GetRun(right));
        Assert.That(center != null && left != null && right != null, Is.True);
    }

    [Test]
    public void VersionOneRestorePreservesRotatedDefaultsAndMaterializesTheOldVisibleBend()
    {
        var rotation = Quaternion.Euler(0, 0, 90);
        var first = Wire(Vector3Int.zero, null, rotation);
        var bend = Wire(Vector3Int.right);
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        var saved = new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = JsonUtility.FromJson<WorldSaveSystem.LegacySavedBlock>("{\"prefabName\":\"Wires\"}"),
            [bend] = Saved(Bit(Vector3Int.forward))
        };
        Restore(saved, 1);
        Assert.That(first.EndpointMask, Is.EqualTo(Bit(Vector3Int.left) | Bit(Vector3Int.right)));
        Assert.That(bend.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward) | Bit(Vector3Int.left)), "The old OR connection's rendered arm becomes authored geometry.");
        Assert.That(Quaternion.Angle(first.transform.rotation, rotation), Is.LessThan(.01f));
        Assert.That(first.GridPosition, Is.EqualTo(Origin));
        Assert.That(bend.GridPosition, Is.EqualTo(Origin + Vector3Int.right));
        Assert.That(first.ConnectsTo(bend, bend.GridPosition), Is.True);
        Assert.That(first.ConnectsTo(source, source.GridPosition), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False);
    }

    [Test]
    public void VersionOneMigrationUsesOriginalMasksAndPreservesEveryOldVisibleEdge()
    {
        var wires = new List<LogicWireBlock>();
        for (int x = 0; x < 3; x++)
        for (int z = 0; z < 3; z++)
            wires.Add(Wire(new Vector3Int(x, 0, z), (x * 23 + z * 17) & 0x3f));
        var original = wires.ToDictionary(w => w, w => w.EndpointMask);
        WireTopology.MigrateLegacyGeometry(wires);
        foreach (LogicWireBlock first in wires)
        {
            int expectedVisible = original[first];
            foreach (LogicWireBlock second in wires)
            {
                int face = Bit(second.GridPosition - first.GridPosition);
                int back = Bit(first.GridPosition - second.GridPosition);
                bool oldEdge = face != 0 && ((original[first] & face) != 0 || (original[second] & back) != 0);
                if (oldEdge) expectedVisible |= face;
                Assert.That(first.ConnectsTo(second, second.GridPosition), Is.EqualTo(oldEdge),
                    first.GridPosition + " -> " + second.GridPosition);
            }
            Assert.That(first.EndpointMask, Is.EqualTo(expectedVisible));
        }
    }

    [Test]
    public void VersionTwoZeroJoinFlagsCannotInsulateTouchingArmsOrPins()
    {
        var first = Wire(Vector3Int.zero);
        var second = Wire(Vector3Int.right);
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = Saved(Bit(Vector3Int.left) | Bit(Vector3Int.right), 0),
            [second] = Saved(Bit(Vector3Int.left), 0)
        }, 2);
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.True);
        Assert.That(first.ConnectsTo(source, source.GridPosition), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False);
    }

    [Test]
    public void VersionTwoReciprocalHiddenEdgesPreservePreviouslyRenderedArms()
    {
        var first = Wire(Vector3Int.zero);
        var second = Wire(Vector3Int.right);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = Saved(Bit(Vector3Int.up) | Bit(Vector3Int.down), Bit(Vector3Int.right)),
            [second] = Saved(Bit(Vector3Int.forward) | Bit(Vector3Int.back), Bit(Vector3Int.left))
        }, 2);
        Assert.That(first.EndpointMask, Is.EqualTo(Bit(Vector3Int.up) | Bit(Vector3Int.down) | Bit(Vector3Int.right)));
        Assert.That(second.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward) | Bit(Vector3Int.back) | Bit(Vector3Int.left)));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.True);
    }

    [Test]
    public void VersionTwoUnilateralAndPinFlagsDoNotGrowPreviouslyInvisibleArms()
    {
        var first = Wire(Vector3Int.zero);
        AddLegacyWireVisual(first);
        var second = Wire(Vector3Int.right);
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = Saved(Bit(Vector3Int.up), Bit(Vector3Int.right) | Bit(Vector3Int.left)),
            [second] = Saved(Bit(Vector3Int.forward), 0)
        }, 2);
        Assert.That(first.EndpointMask, Is.EqualTo(Bit(Vector3Int.up)));
        Assert.That(second.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward)));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.False);
        Assert.That(first.ConnectsTo(source, source.GridPosition), Is.False);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.All(n => n.HasNoDriver), Is.True);
    }

    [TestCase(1)] [TestCase(2)]
    public void LegacyZeroMaskModelPreservesItsRotatedStraightAppearance(int version)
    {
        var rotation = Quaternion.Euler(0, 90, 0);
        var wire = Wire(Vector3Int.right, 0, rotation);
        AddLegacyWireVisual(wire);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [wire] = Saved(0, version == 2 ? (int?)0 : null)
        }, version);
        Assert.That(wire.EndpointMask, Is.EqualTo(Bit(Vector3Int.right) | Bit(Vector3Int.left)),
            "The old fallback straight followed local Z, not the default wire endpoint axis Y.");
        Assert.That(wire.GridPosition, Is.EqualTo(Origin + Vector3Int.right));
        Assert.That(Quaternion.Angle(wire.transform.rotation, rotation), Is.LessThan(.01f));
    }

    [TestCase(1)] [TestCase(2)]
    public void LegacyZeroMaskUsesTheStraightModelsParentAxis(int version)
    {
        var wire = Wire(Vector3Int.zero, 0, Quaternion.Euler(0, 90, 0));
        AddLegacyWireVisual(wire, Quaternion.Euler(90, 0, 0));
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [wire] = Saved(0, version == 2 ? (int?)0 : null)
        }, version);
        Assert.That(wire.EndpointMask, Is.EqualTo(Bit(Vector3Int.up) | Bit(Vector3Int.down)));
        Assert.That(wire.GridPosition, Is.EqualTo(Origin));
        Assert.That(Quaternion.Angle(wire.transform.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.01f));
    }

    [TestCase(1)] [TestCase(2)]
    public void LegacyZeroMaskWithoutANativeWireModelStaysUnchanged(int version)
    {
        var wire = Wire(Vector3Int.zero);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [wire] = Saved(0, version == 2 ? (int?)0 : null)
        }, version);
        Assert.That(wire.EndpointMask, Is.Zero);
    }

    [Test]
    public void VersionThreeZeroMaskDoesNotRestoreTheLegacyStraightFallback()
    {
        var wire = Wire(Vector3Int.zero, 0, Quaternion.Euler(0, 90, 0));
        AddLegacyWireVisual(wire);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [wire] = Saved(0)
        }, 3);
        Assert.That(wire.EndpointMask, Is.Zero);
        Assert.That(wire.GridPosition, Is.EqualTo(Origin));
        Assert.That(Quaternion.Angle(wire.transform.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.01f));
    }

    [Test]
    public void VersionTwoWorldJsonReadsInheritedGeometryAndOldFlagsBeforeNormalization()
    {
        const string json = "{\"version\":2,\"worldName\":\"Existing\",\"blocks\":[" +
            "{\"prefabName\":\"Wires\",\"position\":{\"x\":20000,\"y\":20000,\"z\":20000}," +
            "\"hasWireEndpointMask\":true,\"wireEndpointMask\":4,\"hasWireJoinMask\":true,\"wireJoinMask\":1}," +
            "{\"prefabName\":\"Wires\",\"hasWireEndpointMask\":true,\"wireEndpointMask\":16," +
            "\"hasWireJoinMask\":true,\"wireJoinMask\":2}]}";
        List<WorldSaveSystem.LegacySavedBlock> loaded = ReadWorldJson(json, out int version);
        Assert.That(version, Is.EqualTo(2));
        Assert.That(loaded[0].prefabName, Is.EqualTo("Wires"));
        Assert.That(loaded[0].position, Is.EqualTo((Vector3)Origin));
        Assert.That(loaded[0].hasWireEndpointMask && loaded[0].hasWireJoinMask, Is.True);
        Assert.That(loaded[0].wireEndpointMask, Is.EqualTo(4));
        Assert.That(loaded[0].wireJoinMask, Is.EqualTo(1));
        var first = Wire(Vector3Int.zero);
        var second = Wire(Vector3Int.right);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = loaded[0], [second] = loaded[1]
        }, version);
        Assert.That(first.EndpointMask, Is.EqualTo(5));
        Assert.That(second.EndpointMask, Is.EqualTo(18));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.True);
    }

    [Test]
    public void VersionThreeRoundTripStoresGeometryOnlyAndDoesNotReapplyLegacyOr()
    {
        var first = Wire(Vector3Int.zero, Bit(Vector3Int.right));
        var second = Wire(Vector3Int.right, Bit(Vector3Int.forward));
        var firstSaved = Capture(first);
        var secondSaved = Capture(second);
        System.Type worldType = typeof(WorldSaveSystem).GetNestedType("SavedWorld", BindingFlags.NonPublic);
        object world = System.Activator.CreateInstance(worldType);
        worldType.GetField("version").SetValue(world, WorldSaveSystem.CurrentSaveVersion);
        worldType.GetField("worldName").SetValue(world, "Round trip");
        worldType.GetField("blocks").SetValue(world, new List<WorldSaveSystem.SavedBlock> { firstSaved, secondSaved });
        string json = JsonUtility.ToJson(world);
        Assert.That(WorldSaveSystem.CurrentSaveVersion, Is.EqualTo(3));
        StringAssert.DoesNotContain("JoinMask", json);
        StringAssert.DoesNotContain("joinMask", json);
        Assert.That(firstSaved.position, Is.EqualTo((Vector3)first.GridPosition));
        Assert.That(firstSaved.rotation, Is.EqualTo(first.transform.rotation));
        List<WorldSaveSystem.LegacySavedBlock> loaded = ReadWorldJson(json, out int version);
        Assert.That(version, Is.EqualTo(3));
        Assert.That(loaded[0].position, Is.EqualTo(firstSaved.position));
        Assert.That(loaded[0].rotation, Is.EqualTo(firstSaved.rotation));
        Assert.That(loaded[0].hasWireEndpointMask, Is.True);
        Assert.That(loaded[0].hasWireJoinMask, Is.False);
        WireTopology.Join(first, second);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock>
        {
            [first] = loaded[0], [second] = loaded[1]
        }, version);
        Assert.That(first.EndpointMask, Is.EqualTo(Bit(Vector3Int.right)));
        Assert.That(second.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward)));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.False);
    }

    private static WorldSaveSystem.LegacySavedBlock Saved(int endpointMask, int? legacyJoinMask = null)
        => new WorldSaveSystem.LegacySavedBlock
        {
            hasWireEndpointMask = true, wireEndpointMask = endpointMask,
            hasWireJoinMask = legacyJoinMask.HasValue, wireJoinMask = legacyJoinMask ?? 0
        };

    private static void AddLegacyWireVisual(LogicWireBlock wire, Quaternion? parentRotation = null)
    {
        var visual = wire.gameObject.AddComponent<WireModelVisual>();
        Transform parent = wire.transform;
        if (parentRotation.HasValue)
        {
            parent = new GameObject("Model parent").transform;
            parent.SetParent(wire.transform, false);
            parent.localRotation = parentRotation.Value;
        }
        visual.straightModel = new GameObject("Straight model");
        visual.straightModel.transform.SetParent(parent, false);
    }

    private static WorldSaveSystem.SavedBlock Capture(LogicWireBlock wire)
        => (WorldSaveSystem.SavedBlock)typeof(WorldSaveSystem)
            .GetMethod("CaptureBlock", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { wire.GetComponent<Block>() });

    private static List<WorldSaveSystem.LegacySavedBlock> ReadWorldJson(string json, out int version)
    {
        System.Type type = typeof(WorldSaveSystem).GetNestedType("LoadedWorld", BindingFlags.NonPublic);
        object world = JsonUtility.FromJson(json, type);
        version = (int)type.GetField("version").GetValue(world);
        return (List<WorldSaveSystem.LegacySavedBlock>)type.GetField("blocks").GetValue(world);
    }

    private static void Restore(Dictionary<LogicWireBlock, WorldSaveSystem.LegacySavedBlock> saved, int version)
    {
        typeof(WorldSaveSystem).GetMethod("RestoreWireTopology", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { saved, version });
    }
}
