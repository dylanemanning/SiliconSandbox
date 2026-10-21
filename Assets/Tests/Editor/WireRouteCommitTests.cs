using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class WireRouteCommitTests
{
    private static readonly Vector3Int Origin = new Vector3Int(24000, 24000, 24000);
    private const string Prefix = "Wire commit regression ";
    private readonly List<GameObject> owned = new List<GameObject>();
    private Block prefab;

    [SetUp]
    public void SetUp()
    {
        var template = Own(new GameObject(Prefix + "prefab"));
        template.SetActive(false);
        prefab = template.AddComponent<Block>();
        template.AddComponent<LogicWireBlock>();
    }

    [TearDown]
    public void TearDown()
    {
        // Commits create independent root objects; own those by the test prefab's prefix.
        foreach (var block in Object.FindObjectsByType<Block>(FindObjectsSortMode.None))
            if (block != null && block.name.StartsWith(Prefix, StringComparison.Ordinal))
                Object.DestroyImmediate(block.gameObject);
        foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
        owned.Clear();
    }

    [Test]
    public void RouteAtTheExactStepLimitCommitsAllItsCells()
    {
        var start = new WireEndpoint(Origin);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2);
        var snapshot = Capture(start, end);
        snapshot.Request.MaxPathLength = 2;
        var result = WireRouteSolver.Find(snapshot.Request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Path, Has.Count.EqualTo(3));
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out var error), Is.True, error);
        Assert.That(ActiveWires(), Has.Count.EqualTo(3));
        var middle = ActiveWireAt(Origin + Vector3Int.right);
        Assert.That(middle, Is.Not.Null);
        Assert.That(middle.ConnectsTo(ActiveWireAt(start.Cell), start.Cell), Is.True);
        Assert.That(middle.ConnectsTo(ActiveWireAt(end.Cell), end.Cell), Is.True);
    }

    [TestCase("wrong-start")] [TestCase("wrong-end")] [TestCase("gap")]
    [TestCase("repeat")] [TestCase("outside")] [TestCase("too-long")]
    public void MalformedPathNeverChangesAnExistingEndpointOrPublishesAPartialWire(string defect)
    {
        var wire = Wire(Origin, Mask(Vector3Int.forward, Vector3Int.back));
        var start = new WireEndpoint(Origin, wire);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2);
        var snapshot = Capture(start, end);
        var path = new List<Vector3Int> { start.Cell, start.Cell + Vector3Int.right, end.Cell };
        if (defect == "wrong-start") path[0] += Vector3Int.up;
        if (defect == "wrong-end") path[path.Count - 1] += Vector3Int.up;
        if (defect == "gap") path.RemoveAt(1);
        if (defect == "repeat") path.InsertRange(2, new[] { start.Cell, start.Cell + Vector3Int.right });
        if (defect == "outside") path[1] = snapshot.Request.Max + Vector3Int.up;
        if (defect == "too-long") snapshot.Request.MaxPathLength = 1;
        int oldMask = wire.EndpointMask;
        Assert.That(WireRouteCommit.TryCommit(snapshot, path, prefab, start, end, out _), Is.False);
        Assert.That(ActiveWires(), Has.Count.EqualTo(1));
        Assert.That(wire.EndpointMask, Is.EqualTo(oldMask));
        Assert.That(wire.JoinMask, Is.Zero);
    }

    [Test]
    public void ManualObstructionRejectsTheEntireRowAndKeepsItsStartingWire()
    {
        var wire = Wire(Origin, Mask(Vector3Int.forward, Vector3Int.back));
        Surface(Origin + Vector3Int.right * 2);
        var start = new WireEndpoint(Origin, wire);
        var end = new WireEndpoint(Origin + Vector3Int.right * 3);
        var snapshot = Capture(start, end);
        var row = Enumerable.Range(0, 4).Select(x => Origin + Vector3Int.right * x).ToList();
        Assert.That(WireRouteCommit.TryCommit(snapshot, row, prefab, start, end, out var error, Vector3Int.right), Is.False);
        Assert.That(error, Does.Contain("obstructed"));
        Assert.That(ActiveWires(), Has.Count.EqualTo(1));
        Assert.That(wire.EndpointMask, Is.EqualTo(Mask(Vector3Int.forward, Vector3Int.back)));
        Assert.That(wire.JoinMask, Is.Zero);
    }

    [Test]
    public void UnselectedPinApproachCannotBeUsedAsAnOpenEndpoint()
    {
        Source(Origin, 90);
        var openAtPin = new WireEndpoint(Origin + Vector3Int.right);
        var other = new WireEndpoint(Origin + Vector3Int.right * 4);
        Assert.That(WireWorldSnapshot.TryCapture(openAtPin, other, out var snapshot, out _), Is.False);
        Assert.That(snapshot, Is.Null);
        Assert.That(ActiveWires(), Is.Empty);
    }

    [Test]
    public void ExplicitPinSelectionAllowsItsOwnApproachAndCreatesOnlyTheChosenPinJoin()
    {
        var source = Source(Origin, 90);
        var start = new WireEndpoint(source, Origin, Vector3Int.right, false);
        var end = new WireEndpoint(Origin + Vector3Int.right * 3);
        var snapshot = Capture(start, end);
        Assert.That(snapshot.Request.Blocked, Does.Contain(start.Cell), "The pin cell remains reserved in the general obstacle snapshot.");
        Assert.That(snapshot.Request.Blocked, Does.Contain(Origin + Vector3Int.left), "The other terminal remains protected.");
        var result = WireRouteSolver.Find(snapshot.Request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out var error), Is.True, error);
        var attached = ActiveWireAt(start.Cell);
        Assert.That(attached, Is.Not.Null);
        Assert.That(attached.ConnectsTo(source, Origin), Is.True);
        Assert.That(attached.HasJoinTowards(Origin), Is.True);
        Assert.That(ActiveWireAt(Origin + Vector3Int.left), Is.Null);
    }

    [Test]
    public void AutomaticRouteAvoidsUnselectedPinApproachInsteadOfSilentlyConnectingIt()
    {
        Source(Origin + Vector3Int.back, 0);
        var start = new WireEndpoint(Origin + Vector3Int.left * 2);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2);
        var snapshot = Capture(start, end);
        Assert.That(snapshot.Request.Blocked, Does.Contain(Origin));
        var result = WireRouteSolver.Find(snapshot.Request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        CollectionAssert.DoesNotContain(result.Path, Origin);
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out var error), Is.True, error);
        Assert.That(ActiveWireAt(Origin), Is.Null);
    }

    [Test]
    public void ManualRowCannotCrossAnUnselectedPinApproach()
    {
        Source(Origin + Vector3Int.back, 0);
        var start = new WireEndpoint(Origin + Vector3Int.left * 2);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2);
        var snapshot = Capture(start, end);
        var row = Enumerable.Range(-2, 5).Select(x => Origin + Vector3Int.right * x).ToList();
        Assert.That(WireRouteCommit.TryCommit(snapshot, row, prefab, start, end, out _, Vector3Int.right), Is.False);
        Assert.That(ActiveWires(), Is.Empty);
    }

    [Test]
    public void ExplicitExistingWireEndpointKeepsItsPinJoinWhenAnotherRowJoinsIt()
    {
        Vector3Int targetCell = Origin + Vector3Int.right * 3;
        var source = Source(targetCell + Vector3Int.right, 90);
        var existing = Wire(targetCell, Mask(Vector3Int.right));
        Assert.That(WireTopology.JoinPin(existing, source, source.GridPosition, Vector3Int.left), Is.True);
        var start = new WireEndpoint(Origin);
        var end = new WireEndpoint(targetCell, existing);
        var snapshot = Capture(start, end);
        var row = Enumerable.Range(0, 4).Select(x => Origin + Vector3Int.right * x).ToList();
        Assert.That(WireRouteCommit.TryCommit(snapshot, row, prefab, start, end, out var error, Vector3Int.right), Is.True, error);
        Assert.That(ActiveWireAt(targetCell), Is.SameAs(existing));
        Assert.That(existing.ConnectsTo(source, source.GridPosition), Is.True);
        Assert.That(existing.ConnectsTo(ActiveWireAt(targetCell + Vector3Int.left), targetCell + Vector3Int.left), Is.True);
        Assert.That(ActiveWires(), Has.Count.EqualTo(4));
    }

    [Test]
    public void SignalValueChangesKeepSnapshotCurrentButGeometryChangesRejectCommit()
    {
        var source = Source(Origin + Vector3Int.forward * 3, 0);
        var start = new WireEndpoint(Origin);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2);
        var snapshot = Capture(start, end);
        source.SetState(1);
        Assert.That(snapshot.IsCurrent(), Is.True, "Routing depends on topology and geometry, not simulated signal values.");
        source.transform.position += Vector3.up;
        Assert.That(snapshot.IsCurrent(), Is.False);
        var row = Enumerable.Range(0, 3).Select(x => Origin + Vector3Int.right * x).ToList();
        Assert.That(WireRouteCommit.TryCommit(snapshot, row, prefab, start, end, out var error), Is.False);
        Assert.That(error, Does.Contain("World changed"));
        Assert.That(ActiveWires(), Is.Empty);
    }

    [Test]
    public void SelectedPinAxisCannotBeEnteredSidewaysEvenWithAnOtherwiseValidPath()
    {
        var source = Source(Origin, 90);
        var start = new WireEndpoint(source, Origin, Vector3Int.right, false);
        var end = new WireEndpoint(Origin + Vector3Int.right * 2 + Vector3Int.up);
        var snapshot = Capture(start, end);
        var path = new[] { start.Cell, start.Cell + Vector3Int.up, end.Cell };
        Assert.That(WireRouteCommit.TryCommit(snapshot, path, prefab, start, end, out var error), Is.False);
        Assert.That(error, Does.Contain("axis"));
        Assert.That(ActiveWires(), Is.Empty);
    }

    private WireWorldSnapshot Capture(WireEndpoint start, WireEndpoint end)
    {
        Assert.That(WireWorldSnapshot.TryCapture(start, end, out var snapshot, out var error), Is.True, error);
        return snapshot;
    }

    private GameObject Own(GameObject value) { owned.Add(value); return value; }
    private Block Surface(Vector3Int cell)
    {
        var value = Own(new GameObject(Prefix + "surface"));
        value.transform.position = cell;
        return value.AddComponent<Block>();
    }
    private LogicSignalSource Source(Vector3Int cell, int yaw)
    {
        var block = Surface(cell);
        block.transform.rotation = Quaternion.Euler(0, yaw, 0);
        return block.gameObject.AddComponent<LogicSignalSource>();
    }
    private LogicWireBlock Wire(Vector3Int cell, int mask)
    {
        var block = Surface(cell);
        var wire = block.gameObject.AddComponent<LogicWireBlock>();
        wire.SetEndpointMask(mask); wire.SetJoinMask(0);
        return wire;
    }
    private static int Mask(params Vector3Int[] faces)
    {
        int mask = 0;
        foreach (var face in faces) mask |= LogicWireBlock.GetDirectionBit(face);
        return mask;
    }
    // EditMode does not guarantee the MonoBehaviour registration lifecycle.
    // Inspect the committed active objects directly; duplicate cells still fail SingleOrDefault.
    private static LogicWireBlock ActiveWireAt(Vector3Int cell)
        => ActiveWires().SingleOrDefault(wire => wire.GridPosition == cell);

    private static List<LogicWireBlock> ActiveWires()
        => Object.FindObjectsByType<LogicWireBlock>(FindObjectsSortMode.None)
            .Where(wire => wire.name.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
}
