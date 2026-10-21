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

    [Test]
    public void MatchingEndpointsAndOneSidedJoinsDoNotMergeNetworks()
    {
        var first = Wire(Vector3Int.zero, Bit(Vector3Int.right));
        var second = Wire(Vector3Int.right, Bit(Vector3Int.left));
        Assert.That(first.JoinMask, Is.Zero);
        Assert.That(second.JoinMask, Is.Zero);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(2));
        first.SetJoinMask(Bit(Vector3Int.right));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.False);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(2));
        second.SetJoinMask(Bit(Vector3Int.left));
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(1));
    }

    [Test]
    public void OpenWireDoesNotAttachToNewOrReplacementComponentByProximity()
    {
        var wire = Wire(Vector3Int.zero, Bit(Vector3Int.left));
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.True);
        Assert.That(WireTopology.JoinPin(wire, source, source.GridPosition, Vector3Int.up), Is.False);
        Assert.That(WireTopology.JoinPin(wire, source, source.GridPosition, Vector3Int.right), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False);

        WireTopology.DetachPorts(source);
        typeof(LogicSignalBlock).GetMethod("Unregister", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(source, null);
        Object.DestroyImmediate(source.gameObject);
        Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        Assert.That(wire.EndpointMask, Is.EqualTo(Bit(Vector3Int.left)), "Keep the physical open arm.");
        Assert.That(wire.JoinMask, Is.Zero);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.True);
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
        Assert.That(junction.JoinMask, Is.EqualTo(Bit(Vector3Int.left) | Bit(Vector3Int.forward)));
        Assert.That(junction.EndpointMask, Is.EqualTo(junction.JoinMask));
        Assert.That(junction.ConnectsTo(left, left.GridPosition), Is.True);
        Assert.That(junction.ConnectsTo(forward, forward.GridPosition), Is.True);
        Assert.That(unrelated.JoinMask, Is.Zero);
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
    public void LegacyRestorePreservesRotatedDefaultEndpointsOneSidedBendsAndPinConnectivity()
    {
        var first = Wire(Vector3Int.zero, null, Quaternion.Euler(0, 0, 90));
        var bend = Wire(Vector3Int.right);
        var source = Place<LogicSignalSource>(Vector3Int.left, Quaternion.Euler(0, 90, 0));
        var saved = new Dictionary<LogicWireBlock, WorldSaveSystem.SavedBlock>
        {
            [first] = JsonUtility.FromJson<WorldSaveSystem.SavedBlock>("{\"prefabName\":\"Wires\"}"),
            [bend] = JsonUtility.FromJson<WorldSaveSystem.SavedBlock>(
                "{\"prefabName\":\"Wires\",\"hasWireEndpointMask\":true,\"wireEndpointMask\":16}")
        };
        Restore(saved);
        Assert.That(first.EndpointMask, Is.EqualTo(Bit(Vector3Int.left) | Bit(Vector3Int.right)));
        Assert.That(bend.EndpointMask, Is.EqualTo(Bit(Vector3Int.forward)), "Preserve original authored endpoints.");
        Assert.That(first.ConnectsTo(bend, bend.GridPosition), Is.True, "The legacy one-sided OR edge survives.");
        Assert.That(bend.HasJoinTowards(first.GridPosition), Is.True);
        Assert.That(first.ConnectsTo(source, source.GridPosition), Is.True);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Single().HasNoDriver, Is.False);

        var roundTrip = saved.Keys.ToDictionary(w => w, Capture);
        foreach (LogicWireBlock wire in roundTrip.Keys) wire.SetJoinMask(0);
        Restore(roundTrip);
        Assert.That(first.ConnectsTo(bend, bend.GridPosition), Is.True);
        Assert.That(first.ConnectsTo(source, source.GridPosition), Is.True);
    }

    [Test]
    public void LegacyMigrationMatchesEveryOldEdgeWithoutChangingEndpointMasks()
    {
        var wires = new List<LogicWireBlock>();
        for (int x = 0; x < 3; x++)
        for (int z = 0; z < 3; z++)
            wires.Add(Wire(new Vector3Int(x, 0, z), (x * 23 + z * 17) & 0x3f));
        var original = wires.ToDictionary(w => w, w => w.EndpointMask);
        WireTopology.MigrateLegacyJoins(wires);
        foreach (LogicWireBlock first in wires)
        {
            Assert.That(first.EndpointMask, Is.EqualTo(original[first]));
            foreach (LogicWireBlock second in wires)
            {
                int face = Bit(second.GridPosition - first.GridPosition);
                int back = Bit(first.GridPosition - second.GridPosition);
                bool oldEdge = face != 0 && ((original[first] & face) != 0 || (original[second] & back) != 0);
                Assert.That(first.ConnectsTo(second, second.GridPosition), Is.EqualTo(oldEdge),
                    first.GridPosition + " -> " + second.GridPosition);
            }
        }
    }

    [Test]
    public void SavedExplicitZeroJoinMasksRemainIsolatedAfterRoundTrip()
    {
        var first = Wire(Vector3Int.zero, Bit(Vector3Int.right));
        var second = Wire(Vector3Int.right, Bit(Vector3Int.left));
        var firstSaved = Capture(first);
        var secondSaved = Capture(second);
        Assert.That(firstSaved.hasWireJoinMask && secondSaved.hasWireJoinMask, Is.True);
        Assert.That(firstSaved.wireJoinMask | secondSaved.wireJoinMask, Is.Zero);
        WireTopology.Join(first, second);
        Restore(new Dictionary<LogicWireBlock, WorldSaveSystem.SavedBlock>
        {
            [first] = firstSaved, [second] = secondSaved
        });
        Assert.That(first.ConnectsTo(second, second.GridPosition), Is.False);
        Assert.That(NetlistBuilder.FromBlocks(placed).Networks.Length, Is.EqualTo(2));
    }

    private static WorldSaveSystem.SavedBlock Capture(LogicWireBlock wire)
    {
        var saved = (WorldSaveSystem.SavedBlock)typeof(WorldSaveSystem)
            .GetMethod("CaptureBlock", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { wire.GetComponent<Block>() });
        return JsonUtility.FromJson<WorldSaveSystem.SavedBlock>(JsonUtility.ToJson(saved));
    }

    private static void Restore(Dictionary<LogicWireBlock, WorldSaveSystem.SavedBlock> saved)
    {
        typeof(WorldSaveSystem).GetMethod("RestoreWireTopology", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { saved });
    }
}
